using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static partial class InventoryOps
    {
        public static bool Accepts(EntityManager em, Entity root, int type, int item)
        {
            if (!Sim.ValidDefinition(em, root, item) || Sim.Definition(em, root, item).Kind != ContentKind.Item) return false;
            if (type < 0) return true;
            if (!Sim.ValidDefinition(em, root, type) || Sim.Definition(em, root, type).Kind != ContentKind.SlotType) return false;
            var d = Sim.Definition(em, root, type); var restricted = false;
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind != RuleKind.SlotAccept) continue; restricted = true; if (MatchesGroup(em, root, item, r.Target)) return true; }
            return !restricted;
        }
        public static float LossRate(EntityManager em, Entity root, InventorySlot slot, int item)
        {
            if (!Sim.ValidDefinition(em, root, item) || Sim.Definition(em, root, item).Loss <= 0) return 0;
            var multiplier = slot.SlotType >= 0 ? Sim.Definition(em, root, slot.SlotType).Loss : 1;
            if (slot.SlotType >= 0) { var type = Sim.Definition(em, root, slot.SlotType); for (var i = 0; i < type.RuleCount; i++) { var r = Sim.GetRule(em, root, type.RuleStart + i); if (r.Kind == RuleKind.SlotLoss && MatchesGroup(em, root, item, r.Target)) multiplier *= r.Value; } }
            var provider = Sim.Find(em, slot.Provider);
            if (provider != Entity.Null && em.HasComponent<Building>(provider)) { var b = em.GetComponentData<Building>(provider); var condition = Sim.Rule(em, root, em.GetComponentData<Identity>(provider).Definition, RuleKind.StorageCondition, b.Level); if (condition.Level >= 0) { if (b.Workers < condition.Amount) multiplier *= condition.Extra; if (b.Maintained == 0) multiplier *= condition.B / 100f; } }
            return math.saturate(Sim.Definition(em, root, item).Loss * multiplier * (1 - math.saturate(Sim.Modifier(em, root, RuleKind.LossModifier, item))));
        }
        static int StableSlot(InventorySlot a, InventorySlot b) { var c = a.Provider.CompareTo(b.Provider); return c != 0 ? c : a.Index.CompareTo(b.Index); }
        public static int CompareStorage(EntityManager em, Entity root, InventorySlot a, InventorySlot b, int item)
        { var c = LossRate(em, root, a, item).CompareTo(LossRate(em, root, b, item)); if (c == 0) c = (a.Count == 0).CompareTo(b.Count == 0); return c != 0 ? c : StableSlot(a, b); }
        public static List<int> StorageOrder(EntityManager em, Entity root, int item)
        {
            var slots = em.GetBuffer<InventorySlot>(root); var result = new List<int>();
            for (var i = 0; i < slots.Length; i++) if (slots[i].Unavailable == 0 && (slots[i].Count == 0 || slots[i].Item == item) && Accepts(em, root, slots[i].SlotType, item)) result.Add(i);
            result.Sort((a, b) => CompareStorage(em, root, slots[a], slots[b], item)); return result;
        }
        static List<int> ConsumptionOrder(EntityManager em, Entity root, int item, ulong provider)
        {
            var slots = em.GetBuffer<InventorySlot>(root); var result = new List<int>();
            for (var i = 0; i < slots.Length; i++) if (slots[i].Unavailable == 0 && slots[i].Count > 0 && slots[i].Item == item && (provider == 0 || slots[i].Provider == provider)) result.Add(i);
            result.Sort((a, b) => { var c = LossRate(em, root, slots[b], item).CompareTo(LossRate(em, root, slots[a], item)); return c != 0 ? c : StableSlot(slots[a], slots[b]); }); return result;
        }
        public static int SlotIndex(EntityManager em, Entity root, ulong provider, int index)
        { var slots = em.GetBuffer<InventorySlot>(root); for (var i = 0; i < slots.Length; i++) if (slots[i].Provider == provider && slots[i].Index == index) return i; return -1; }
        // All interactive commands are bound to the displayed inventory, never a volatile buffer index.
        public static string Fingerprint(EntityManager em, Entity root)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            writer.Write(em.GetComponentData<Session>(root).Turn);
            var slots = em.GetBuffer<InventorySlot>(root); writer.Write(slots.Length);
            foreach (var s in slots) { writer.Write(s.Provider); writer.Write(s.Index); writer.Write(s.SlotType); writer.Write(s.Item); writer.Write(s.Count); writer.Write(s.LossRemainder); writer.Write(s.Unavailable); }
            var pending = em.GetBuffer<PendingItem>(root); writer.Write(pending.Length); foreach (var p in pending) { writer.Write(p.Item); writer.Write(p.Amount); writer.Write(p.LossRemainder); }
            writer.Flush(); using var hash = SHA256.Create(); return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }
        public static ResultCode LayoutCommand(EntityManager em, Entity root, Command c)
        {
            var session = em.GetComponentData<Session>(root); if (session.Phase != Phase.Day) return ResultCode.WrongPhase;
            if (session.CheckpointPending != 0) return ResultCode.Busy;
            if (!FeatureOps.Unlocked(em, root, "Inventory")) return ResultCode.Unavailable;
            if (c.Text.ToString() != Fingerprint(em, root)) return ResultCode.Unavailable;
            if (c.Kind == CommandKind.SortInventory) return Sort(em, root);
            if (c.Kind == CommandKind.StorePending) { StoreAllPending(em, root); return ResultCode.Success; }
            if (c.Amount <= 0) return ResultCode.InvalidTarget;
            var to = SlotIndex(em, root, c.Other, c.DestinationSlot);
            var slots = em.GetBuffer<InventorySlot>(root);
            if (c.Kind == CommandKind.StorePendingSlot || c.Kind == CommandKind.DiscardPending)
            {
                var pool = em.GetBuffer<PendingItem>(root); var pIndex = -1;
                for (var i = 0; i < pool.Length; i++) if (pool[i].Item == c.Definition) { pIndex = i; break; }
                if (pIndex < 0 || c.Amount > pool[pIndex].Amount) return ResultCode.InsufficientResources;
                var p = pool[pIndex]; var debt = p.LossRemainder * ((float)c.Amount / p.Amount);
                if (c.Kind == CommandKind.StorePendingSlot)
                {
                    if (to < 0 || slots[to].Unavailable != 0) return ResultCode.InvalidTarget;
                    var target = slots[to];
                    if (!Accepts(em, root, target.SlotType, p.Item) || target.Count > 0 && target.Item != p.Item) return ResultCode.InvalidContent;
                    if (target.Count + (long)c.Amount > Sim.Definition(em, root, p.Item).Capacity) return ResultCode.NoCapacity;
                    target.Item = p.Item; target.Count += c.Amount; target.LossRemainder += debt; slots[to] = target;
                }
                p.Amount -= c.Amount; p.LossRemainder = math.max(0, p.LossRemainder - debt); if (p.Amount == 0) pool.RemoveAt(pIndex); else pool[pIndex] = p;
                EconomyJournalOps.Record(em,root,c.Definition,-c.Amount,true);
                if(c.Kind==CommandKind.StorePendingSlot)EconomyJournalOps.Record(em,root,c.Definition,c.Amount);
                return ResultCode.Success;
            }
            var from = SlotIndex(em, root, c.Target, c.SourceSlot);
            if (from < 0 || slots[from].Unavailable != 0 || slots[from].Count < c.Amount || slots[from].Item != c.Definition) return ResultCode.InvalidTarget;
            var source = slots[from];
            if (c.Kind == CommandKind.DiscardSlot) { source.LossRemainder *= (float)(source.Count - c.Amount) / source.Count; source.Count -= c.Amount; if (source.Count == 0) source.Item = -1; slots[from] = source; EconomyJournalOps.Record(em,root,c.Definition,-c.Amount); return ResultCode.Success; }
            if (c.Kind != CommandKind.MoveInventory || to < 0 || from == to || slots[to].Unavailable != 0) return ResultCode.InvalidTarget;
            var dest = slots[to]; if (!Accepts(em, root, dest.SlotType, source.Item)) return ResultCode.InvalidContent;
            if (dest.Count > 0 && dest.Item != source.Item)
            {
                if (c.Amount != source.Count || !Accepts(em, root, source.SlotType, dest.Item)) return ResultCode.Unavailable;
                var item = dest.Item; var count = dest.Count; var remainder = dest.LossRemainder;
                dest.Item = source.Item; dest.Count = source.Count; dest.LossRemainder = source.LossRemainder;
                source.Item = item; source.Count = count; source.LossRemainder = remainder;
            }
            else
            {
                if (dest.Count + (long)c.Amount > Sim.Definition(em, root, source.Item).Capacity) return ResultCode.NoCapacity;
                var debt = source.LossRemainder * ((float)c.Amount / source.Count);
                dest.Item = source.Item; dest.Count += c.Amount; dest.LossRemainder += debt;
                source.Count -= c.Amount; source.LossRemainder = math.max(0, source.LossRemainder - debt); if (source.Count == 0) { source.Item = -1; source.LossRemainder = 0; }
            }
            slots[from] = source; slots[to] = dest; return ResultCode.Success;
        }
        public static void StoreAllPending(EntityManager em, Entity root)
        {
            var pool = em.GetBuffer<PendingItem>(root);
            var items = new List<int>(); foreach (var p in pool) items.Add(p.Item);
            items.Sort((a, b) => string.CompareOrdinal(Sim.Definition(em, root, a).Id.ToString(), Sim.Definition(em, root, b).Id.ToString()));
            foreach (var item in items) for (var i = pool.Length - 1; i >= 0; i--) if (pool[i].Item == item)
            { var p = pool[i]; if (p.Amount == 0) { pool.RemoveAt(i); continue; } var stored = Add(em, root, p.Item, p.Amount, false, p.LossRemainder); EconomyJournalOps.Record(em,root,p.Item,-stored,true); p.LossRemainder *= (float)(p.Amount - stored) / p.Amount; p.Amount -= stored; if (p.Amount == 0) pool.RemoveAt(i); else pool[i] = p; }
        }
        static ResultCode Sort(EntityManager em, Entity root)
        {
            using var transaction = new InventoryTransaction(em, root);
            var items = new HashSet<int>(); foreach (var s in em.GetBuffer<InventorySlot>(root)) if (s.Count > 0 && s.Unavailable == 0) items.Add(s.Item);
            var ordered = new List<int>(items); ordered.Sort((a, b) => string.CompareOrdinal(Sim.Definition(em, root, a).Id.ToString(), Sim.Definition(em, root, b).Id.ToString()));
            foreach (var item in ordered)
            {
                var indices = StorageOrder(em, root, item); var slots = em.GetBuffer<InventorySlot>(root); var total = 0; var debt = 0f;
                foreach (var i in indices) if (slots[i].Item == item) { total = checked(total + slots[i].Count); debt += slots[i].LossRemainder; }
                var remaining = total;
                foreach (var i in indices)
                {
                    var slot = slots[i]; var count = math.min(remaining, Sim.Definition(em, root, item).Capacity);
                    slot.Item = count == 0 ? -1 : item; slot.Count = count; slot.LossRemainder = total == 0 ? 0 : debt * ((float)count / total); slots[i] = slot; remaining -= count;
                }
                if (remaining != 0) return ResultCode.NoCapacity;
            }
            transaction.Commit(); return ResultCode.Success;
        }
    }
}
