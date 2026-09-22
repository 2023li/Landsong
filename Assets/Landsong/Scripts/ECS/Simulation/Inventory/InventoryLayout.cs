using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryLayout
    {
        public static int SlotIndex(EntityManager em, Entity root, ulong provider, int index)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++)
                if (slots[i].Provider == provider && slots[i].Index == index)
                    return i;
            return -1;
        }

        public static string Fingerprint(EntityManager em, Entity root)
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(em.GetComponentData<GameClock>(root).Turn);
            var slots = em.GetBuffer<InventorySlot>(root);
            writer.Write(slots.Length);
            foreach (var s in slots)
            {
                writer.Write(s.Provider);
                writer.Write(s.Index);
                writer.Write(s.SlotType.Index);
                writer.Write(s.Item.Index);
                writer.Write(s.Count);
                writer.Write(s.LossRemainder);
                writer.Write(s.Unavailable);
            }

            var pending = em.GetBuffer<PendingItem>(root);
            writer.Write(pending.Length);
            foreach (var p in pending)
            {
                writer.Write(p.Item.Index);
                writer.Write(p.Amount);
                writer.Write(p.LossRemainder);
            }

            writer.Flush();
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(stream.ToArray()));
        }

        public static ResultCode Execute(EntityManager em, Entity root, InventoryLayoutRequest c)
        {
            var session = em.GetComponentData<Session>(root);
            PersistenceGate sessionPersistence = em.GetComponentData<PersistenceGate>(root);
            if (session.Phase != Phase.Day)
                return ResultCode.WrongPhase;
            if (sessionPersistence.CheckpointPending != 0)
                return ResultCode.Busy;
            if (!FeatureOps.Unlocked(em, root, "Inventory"))
                return ResultCode.Unavailable;
            if (c.ExpectedInventory.ToString() != InventoryLayout.Fingerprint(em, root))
                return ResultCode.Unavailable;
            if (c.Action == InventoryLayoutAction.SortInventory)
                return InventoryLayout.Sort(em, root);
            if (c.Action == InventoryLayoutAction.StorePending)
            {
                InventoryLayout.StoreAllPending(em, root);
                return ResultCode.Success;
            }

            if (c.Quantity <= 0)
                return ResultCode.InvalidTarget;
            var to = InventoryLayout.SlotIndex(em, root, c.DestinationProvider, c.DestinationSlot);
            var slots = em.GetBuffer<InventorySlot>(root);
            if (c.Action == InventoryLayoutAction.StorePendingSlot || c.Action == InventoryLayoutAction.DiscardPending)
            {
                var pool = em.GetBuffer<PendingItem>(root);
                var pIndex = -1;
                for (var i = 0; i < pool.Length; i++)
                    if (pool[i].Item == c.Item)
                    {
                        pIndex = i;
                        break;
                    }

                if (pIndex < 0 || c.Quantity > pool[pIndex].Amount)
                    return ResultCode.InsufficientResources;
                var p = pool[pIndex];
                var debt = p.LossRemainder * ((float)c.Quantity / p.Amount);
                if (c.Action == InventoryLayoutAction.StorePendingSlot)
                {
                    if (to < 0 || slots[to].Unavailable != 0)
                        return ResultCode.InvalidTarget;
                    var target = slots[to];
                    if (!InventoryStorage.Accepts(em, root, target.SlotType, p.Item) || target.Count > 0 && target.Item != p.Item)
                        return ResultCode.InvalidContent;
                    if (target.Count + (long)c.Quantity > ItemDefinitions.Get(em, root, p.Item).MaximumStack)
                        return ResultCode.NoCapacity;
                    target.Item = p.Item;
                    target.Count += c.Quantity;
                    target.LossRemainder += debt;
                    slots[to] = target;
                }

                p.Amount -= c.Quantity;
                p.LossRemainder = math.max(0, p.LossRemainder - debt);
                if (p.Amount == 0)
                    pool.RemoveAt(pIndex);
                else
                    pool[pIndex] = p;
                EconomyJournalOps.Record(em, root, c.Item, -c.Quantity, true);
                if (c.Action == InventoryLayoutAction.StorePendingSlot)
                    EconomyJournalOps.Record(em, root, c.Item, c.Quantity);
                return ResultCode.Success;
            }

            var from = InventoryLayout.SlotIndex(em, root, c.SourceProvider, c.SourceSlot);
            if (from < 0 || slots[from].Unavailable != 0 || slots[from].Count < c.Quantity || slots[from].Item != c.Item)
                return ResultCode.InvalidTarget;
            var source = slots[from];
            if (c.Action == InventoryLayoutAction.MoveInventoryToPending)
            {
                var pool = em.GetBuffer<PendingItem>(root);
                int pendingIndex = -1;
                for (int i = 0; i < pool.Length; i++)
                    if (pool[i].Item == source.Item)
                    {
                        pendingIndex = i;
                        break;
                    }

                var pending = pendingIndex < 0 ? new PendingItem
                {
                    Item = source.Item
                }

                : pool[pendingIndex];
                if ((long)pending.Amount + c.Quantity > int.MaxValue)
                    return ResultCode.NoCapacity;
                float debt = source.LossRemainder * ((float)c.Quantity / source.Count);
                if (!math.isfinite(pending.LossRemainder + debt))
                    return ResultCode.InvalidContent;
                pending.Amount += c.Quantity;
                pending.LossRemainder += debt;
                source.Count -= c.Quantity;
                source.LossRemainder = math.max(0, source.LossRemainder - debt);
                if (source.Count == 0)
                {
                    source.Item = ItemId.None;
                    source.LossRemainder = 0;
                }

                if (pendingIndex < 0)
                    pool.Add(pending);
                else
                    pool[pendingIndex] = pending;
                slots[from] = source;
                using (EconomyJournalOps.For(em, root, WorldQueries.Find(em, c.SourceProvider), EconomyReason.CapacityTransfer))
                {
                    EconomyJournalOps.Record(em, root, c.Item, -c.Quantity);
                    EconomyJournalOps.Record(em, root, c.Item, c.Quantity, true);
                }

                return ResultCode.Success;
            }

            if (c.Action == InventoryLayoutAction.DiscardSlot)
            {
                source.LossRemainder *= (float)(source.Count - c.Quantity) / source.Count;
                source.Count -= c.Quantity;
                if (source.Count == 0)
                    source.Item = ItemId.None;
                slots[from] = source;
                EconomyJournalOps.Record(em, root, c.Item, -c.Quantity);
                return ResultCode.Success;
            }

            if (c.Action != InventoryLayoutAction.MoveInventory || to < 0 || from == to || slots[to].Unavailable != 0)
                return ResultCode.InvalidTarget;
            var dest = slots[to];
            if (!InventoryStorage.Accepts(em, root, dest.SlotType, source.Item))
                return ResultCode.InvalidContent;
            if (dest.Count > 0 && dest.Item != source.Item)
            {
                if (c.Quantity != source.Count || !InventoryStorage.Accepts(em, root, source.SlotType, dest.Item))
                    return ResultCode.Unavailable;
                var item = dest.Item;
                var count = dest.Count;
                var remainder = dest.LossRemainder;
                dest.Item = source.Item;
                dest.Count = source.Count;
                dest.LossRemainder = source.LossRemainder;
                source.Item = item;
                source.Count = count;
                source.LossRemainder = remainder;
            }
            else
            {
                if (dest.Count + (long)c.Quantity > ItemDefinitions.Get(em, root, source.Item).MaximumStack)
                    return ResultCode.NoCapacity;
                var debt = source.LossRemainder * ((float)c.Quantity / source.Count);
                dest.Item = source.Item;
                dest.Count += c.Quantity;
                dest.LossRemainder += debt;
                source.Count -= c.Quantity;
                source.LossRemainder = math.max(0, source.LossRemainder - debt);
                if (source.Count == 0)
                {
                    source.Item = ItemId.None;
                    source.LossRemainder = 0;
                }
            }

            slots[from] = source;
            slots[to] = dest;
            return ResultCode.Success;
        }

        public static void StoreAllPending(EntityManager em, Entity root)
        {
            var pool = em.GetBuffer<PendingItem>(root);
            var items = new List<ItemId>();
            foreach (var p in pool)
                items.Add(p.Item);
            items.Sort((a, b) => string.CompareOrdinal(ItemDefinitions.Get(em, root, a).Metadata.Id.ToString(), ItemDefinitions.Get(em, root, b).Metadata.Id.ToString()));
            foreach (var item in items)
                for (var i = pool.Length - 1; i >= 0; i--)
                    if (pool[i].Item == item)
                    {
                        var p = pool[i];
                        if (p.Amount == 0)
                        {
                            pool.RemoveAt(i);
                            continue;
                        }

                        var stored = InventoryOps.Add(em, root, p.Item, p.Amount, false, p.LossRemainder);
                        EconomyJournalOps.Record(em, root, p.Item, -stored, true);
                        p.LossRemainder *= (float)(p.Amount - stored) / p.Amount;
                        p.Amount -= stored;
                        if (p.Amount == 0)
                            pool.RemoveAt(i);
                        else
                            pool[i] = p;
                    }
        }

        internal static ResultCode Sort(EntityManager em, Entity root)
        {
            using var transaction = new InventoryTransaction(em, root);
            var items = new HashSet<ItemId>();
            foreach (var s in em.GetBuffer<InventorySlot>(root))
                if (s.Count > 0 && s.Unavailable == 0)
                    items.Add(s.Item);
            var ordered = new List<ItemId>(items);
            ordered.Sort((a, b) => string.CompareOrdinal(ItemDefinitions.Get(em, root, a).Metadata.Id.ToString(), ItemDefinitions.Get(em, root, b).Metadata.Id.ToString()));
            foreach (var item in ordered)
            {
                var indices = InventoryStorage.StorageOrder(em, root, item);
                var slots = em.GetBuffer<InventorySlot>(root);
                var total = 0;
                var debt = 0f;
                foreach (var i in indices)
                    if (slots[i].Item == item)
                    {
                        total = checked(total + slots[i].Count);
                        debt += slots[i].LossRemainder;
                    }

                var remaining = total;
                foreach (var i in indices)
                {
                    var slot = slots[i];
                    var count = math.min(remaining, ItemDefinitions.Get(em, root, item).MaximumStack);
                    slot.Item = count == 0 ? ItemId.None : item;
                    slot.Count = count;
                    slot.LossRemainder = total == 0 ? 0 : debt * ((float)count / total);
                    slots[i] = slot;
                    remaining -= count;
                }

                if (remaining != 0)
                    return ResultCode.NoCapacity;
            }

            transaction.Commit();
            return ResultCode.Success;
        }
    }
}
