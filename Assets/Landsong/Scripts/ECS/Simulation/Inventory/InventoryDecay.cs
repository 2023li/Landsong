using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryDecay
    {
        public static void Settle(EntityManager em, Entity root)
        {
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++)
            {
                var slot = slots[i];
                if (slot.Count <= 0 || slot.Unavailable != 0)
                    continue;
                var provider = WorldQueries.Find(em, slot.Provider);
                var loss = slot.Count * InventoryStorage.LossRate(em, root, slot, slot.Item) + slot.LossRemainder;
                var count = math.min(slot.Count, (int)math.floor(loss));
                slot.Count -= count;
                slot.LossRemainder = loss - count;
                if (count > 0 || slot.LossRemainder > 0)
                    using (var scope = EconomyJournalOps.For(em, root, provider, EconomyReason.NaturalLoss))
                        EconomyJournalOps.Record(em, root, slot.Item, -count, note: new FixedString128Bytes($"格 {slot.Index + 1} · 结余小数损耗 {slot.LossRemainder:0.###}"));
                if (slot.Count == 0)
                {
                    slot.Item = ItemId.None;
                    slot.LossRemainder = 0;
                }

                slots[i] = slot;
            }
        }
    }
}
