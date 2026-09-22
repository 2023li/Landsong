using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS
{
    // Immutable per-turn closing balances. Source 0 is the city total; other sources retain building attribution.
    [InternalBufferCapacity(0)]
    public struct EconomyBillEntry : IBufferElementData
    {
        public int Turn;
        public ItemId Item;
        public ulong Source;
        public long Income, Expense, Stored, Pending;
    }

    public static class EconomyBillOps
    {
        public static void CaptureSettlement(EntityManager em, Entity root)
        {
            var state = em.GetComponentData<EconomyJournalState>(root);
            if (state.Turn <= 0 || state.Recording != 0 || state.Forecast != 0)
                return;
            var rows = new SortedDictionary<(ulong source, int item), EconomyBillEntry>();
            EconomyBillEntry Get(ulong source, ItemId item) => rows.TryGetValue((source, item.Index), out var row) ? row : new EconomyBillEntry
            {
                Turn = state.Turn,
                Source = source,
                Item = item
            };
            // A marker also retains completed turns with no resources.
            rows.Add((0, -1), Get(0, ItemId.None));
            foreach (var entry in em.GetBuffer<EconomyEntry>(root))
            {
                if (entry.Turn != state.Turn || !entry.Item.IsValid || entry.Delta == 0 || entry.Reason == EconomyReason.CapacityTransfer)
                    continue;
                Accumulate(0, entry);
                if (entry.Source != 0)
                    Accumulate(entry.Source, entry);
            }

            void Accumulate(ulong source, EconomyEntry entry)
            {
                var row = Get(source, entry.Item);
                if (entry.Delta > 0)
                    row.Income += entry.Delta;
                else
                    row.Expense -= (long)entry.Delta;
                rows[(source, entry.Item.Index)] = row;
            }

            foreach (var slot in em.GetBuffer<InventorySlot>(root))
            {
                if (slot.Unavailable != 0 || !slot.Item.IsValid || slot.Count <= 0)
                    continue;
                var row = Get(0, slot.Item);
                row.Stored += slot.Count;
                rows[(0, slot.Item.Index)] = row;
                if (slot.Provider != 0)
                {
                    row = Get(slot.Provider, slot.Item);
                    row.Stored += slot.Count;
                    rows[(slot.Provider, slot.Item.Index)] = row;
                }
            }

            foreach (var item in em.GetBuffer<PendingItem>(root))
            {
                if (!item.Item.IsValid || item.Amount <= 0)
                    continue;
                var row = Get(0, item.Item);
                row.Pending += item.Amount;
                rows[(0, item.Item.Index)] = row;
            }

            EntityState.Buffer<EconomyBillEntry>(em, root);
            var history = em.GetBuffer<EconomyBillEntry>(root);
            for (int i = history.Length - 1; i >= 0; i--)
                if (history[i].Turn == state.Turn)
                    history.RemoveAt(i);
            foreach (var row in rows.Values)
                history.Add(row);
        }
    }
}
