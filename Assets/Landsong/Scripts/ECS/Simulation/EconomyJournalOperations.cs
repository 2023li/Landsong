using System;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    // One authoritative last-settlement journal. Building views filter it by stable source ID.
    // Entries are written at the resource operation, never inferred from inventory differences.
    public static class EconomyJournalOps
    {
        public static void Begin(EntityManager em, Entity root, bool forecast)
        {
            Sim.Buffer<EconomyEntry>(em, root); em.GetBuffer<EconomyEntry>(root).Clear();
            Sim.Set(em, root, new EconomyJournalState { Turn = em.GetComponentData<Session>(root).Turn, Recording = 1, Forecast = (byte)(forecast ? 1 : 0) });
        }
        public static void End(EntityManager em, Entity root)
        { var state = em.GetComponentData<EconomyJournalState>(root); state.Recording = 0; state.Forecast = 0; state.Source = 0; state.SourceName = default; em.SetComponentData(root, state); }
        public static bool Forecast(EntityManager em, Entity root) => em.HasComponent<EconomyJournalState>(root) && em.GetComponentData<EconomyJournalState>(root).Forecast != 0;
        public static void DiscardPending(EntityManager em, Entity root)
        {
            var prior = em.GetComponentData<EconomyJournalState>(root);
            var state = prior; state.Recording = 1; state.Reason = EconomyReason.NightDiscard; state.Source = 0; state.SourceName = "入夜清空";
            em.SetComponentData(root, state);
            try { foreach (var item in em.GetBuffer<PendingItem>(root)) Record(em, root, item.Item, -item.Amount, true); em.GetBuffer<PendingItem>(root).Clear(); }
            finally { em.SetComponentData(root, prior); }
        }
        public static Scope For(EntityManager em, Entity root, Entity source, EconomyReason reason) => new Scope(em, root, source, reason);
        public readonly struct Scope : IDisposable
        {
            readonly EntityManager em; readonly Entity root; readonly EconomyJournalState previous; readonly bool active;
            public Scope(EntityManager manager, Entity owner, Entity source, EconomyReason reason)
            {
                em = manager; root = owner; active = em.HasComponent<EconomyJournalState>(root);
                previous = active ? em.GetComponentData<EconomyJournalState>(root) : default;
                if (!active) return;
                var state = previous; state.Reason = reason; state.Source = 0; state.SourceName = "全城";
                if (source != Entity.Null && em.Exists(source) && em.HasComponent<Identity>(source))
                { var id = em.GetComponentData<Identity>(source); state.Source = id.Id; state.SourceName = id.Name; }
                em.SetComponentData(root, state);
            }
            public void Dispose() { if (active) em.SetComponentData(root, previous); }
        }
        public static void Record(EntityManager em, Entity root, int item, int delta, bool pending = false, FixedString128Bytes note = default)
        {
            HistoryOps.Resource(em,root,item,delta,pending,note);
            if (!em.HasComponent<EconomyJournalState>(root)) return;
            var s = em.GetComponentData<EconomyJournalState>(root); if (s.Recording == 0 || delta == 0 && note.IsEmpty) return;
            em.GetBuffer<EconomyEntry>(root).Add(new EconomyEntry { Turn = s.Turn, Source = s.Source, SourceName = s.SourceName, Reason = s.Reason, Item = item, Delta = delta, Pending = (byte)(pending ? 1 : 0), Note = note });
        }
        public static void Note(EntityManager em, Entity root, FixedString128Bytes note) => Record(em, root, -1, 0, note: note);
    }

    // Resource recipe transaction: rolled-back inputs/outputs must not leave phantom journal rows.
    public sealed class InventoryTransaction : IDisposable
    {
        readonly EntityManager em; readonly Entity root;
        readonly NativeArray<InventorySlot> slots; readonly NativeArray<PendingItem> pending;
        readonly int journalCount, historyCount; bool complete, disposed;
        public InventoryTransaction(EntityManager manager, Entity owner)
        {
            em = manager; root = owner;
            slots = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp);
            pending = em.GetBuffer<PendingItem>(root).ToNativeArray(Allocator.Temp);
            journalCount = em.HasBuffer<EconomyEntry>(root) ? em.GetBuffer<EconomyEntry>(root).Length : 0;
            historyCount = em.HasBuffer<HistoryEntry>(root) ? em.GetBuffer<HistoryEntry>(root).Length : 0;
        }
        public void Commit() => complete = true;
        public void Reject(FixedString128Bytes note) { Rollback(); complete = true; EconomyJournalOps.Note(em, root, note); }
        public void Rollback()
        {
            em.GetBuffer<InventorySlot>(root).CopyFrom(slots); em.GetBuffer<PendingItem>(root).CopyFrom(pending);
            if (em.HasBuffer<EconomyEntry>(root)) em.GetBuffer<EconomyEntry>(root).ResizeUninitialized(journalCount);
            if (em.HasBuffer<HistoryEntry>(root)) em.GetBuffer<HistoryEntry>(root).ResizeUninitialized(historyCount);
        }
        public void Dispose()
        { if (disposed) return; disposed = true; if (!complete) Rollback(); slots.Dispose(); pending.Dispose(); }
    }
}
