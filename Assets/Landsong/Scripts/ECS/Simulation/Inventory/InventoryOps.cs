using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryOps
    {
        public static int Count(EntityManager em, Entity root, ItemId item, ulong provider = 0)
        {
            var count = 0;
            foreach (var s in em.GetBuffer<InventorySlot>(root))
                if (s.Unavailable == 0 && s.Item == item && (provider == 0 || s.Provider == provider))
                    count += s.Count;
            return count;
        }

        public static int Add(EntityManager em, Entity root, ItemId item, int amount, bool pending = false, float lossRemainder = 0)
        {
            if (amount <= 0 || !ItemDefinitions.IsValid(em, root, item))
                return 0;
            var remaining = amount;
            var maxStack = math.max(1, ItemDefinitions.Get(em, root, item).MaximumStack);
            var slots = em.GetBuffer<InventorySlot>(root);
            foreach (var i in InventoryStorage.StorageOrder(em, root, item))
            {
                if (remaining <= 0)
                    break;
                var s = slots[i];
                var add = math.min(remaining, maxStack - s.Count);
                if (add <= 0)
                    continue;
                s.Item = item;
                s.Count += add;
                s.LossRemainder += lossRemainder * ((float)add / amount);
                slots[i] = s;
                remaining -= add;
            }

            if (pending && remaining > 0)
            {
                var pool = em.GetBuffer<PendingItem>(root);
                var merged = false;
                for (var i = 0; i < pool.Length; i++)
                    if (pool[i].Item == item)
                    {
                        var p = pool[i];
                        p.Amount = checked(p.Amount + remaining);
                        p.LossRemainder += lossRemainder * ((float)remaining / amount);
                        pool[i] = p;
                        merged = true;
                        break;
                    }

                if (!merged)
                    pool.Add(new PendingItem { Item = item, Amount = remaining, LossRemainder = lossRemainder * ((float)remaining / amount) });
            }

            EconomyJournalOps.Record(em, root, item, amount - remaining);
            if (pending && remaining > 0)
                EconomyJournalOps.Record(em, root, item, remaining, true);
            return amount - remaining;
        }

        public static bool Remove(EntityManager em, Entity root, ItemId item, int amount, ulong provider = 0)
        {
            if (amount < 0 || InventoryOps.Count(em, root, item, provider) < amount)
                return false;
            var requested = amount;
            var slots = em.GetBuffer<InventorySlot>(root);
            foreach (var i in InventoryStorage.ConsumptionOrder(em, root, item, provider))
            {
                if (amount <= 0)
                    break;
                var s = slots[i];
                if (s.Unavailable != 0 || s.Item != item || (provider != 0 && s.Provider != provider))
                    continue;
                var take = math.min(amount, s.Count);
                s.LossRemainder *= (float)(s.Count - take) / s.Count;
                s.Count -= take;
                amount -= take;
                if (s.Count == 0)
                {
                    s.Item = ItemId.None;
                    s.LossRemainder = 0;
                }

                slots[i] = s;
            }

            EconomyJournalOps.Record(em, root, item, -requested);
            return true;
        }

        public static int PendingCount(EntityManager em, Entity root, ItemId item)
        {
            var count = 0;
            foreach (var p in em.GetBuffer<PendingItem>(root))
                if (p.Item == item)
                    count += p.Amount;
            return count;
        }

        public static bool RemoveWithPending(EntityManager em, Entity root, ItemId item, int amount)
        {
            if (amount < 0 || InventoryOps.Count(em, root, item) + InventoryOps.PendingCount(em, root, item) < amount)
                return false;
            var pool = em.GetBuffer<PendingItem>(root);
            for (var i = pool.Length - 1; i >= 0 && amount > 0; i--)
            {
                var p = pool[i];
                if (p.Item != item)
                    continue;
                if (p.Amount == 0)
                {
                    pool.RemoveAt(i);
                    continue;
                }

                var take = math.min(p.Amount, amount);
                p.LossRemainder *= (float)(p.Amount - take) / p.Amount;
                p.Amount -= take;
                amount -= take;
                EconomyJournalOps.Record(em, root, item, -take, true);
                if (p.Amount == 0)
                    pool.RemoveAt(i);
                else
                    pool[i] = p;
            }

            return InventoryOps.Remove(em, root, item, amount);
        }
    }
}
