using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    /// <summary>A read of authoritative buffers; no independent stock or history is stored by UI.</summary>
    public static class InventoryReadModel
    {
        public struct Resource
        {
            public long Stored, Pending;
        }

        public static SortedDictionary<int, Resource> Resources(EntityManager em, Entity root)
        {
            var result = new SortedDictionary<int, Resource>();
            foreach (var slot in em.GetBuffer<InventorySlot>(root))
            {
                if (slot.Unavailable != 0 || slot.Count <= 0 || slot.Item < 0) continue;
                result.TryGetValue(slot.Item, out var value); value.Stored += slot.Count; result[slot.Item] = value;
            }
            foreach (var item in em.GetBuffer<PendingItem>(root))
            {
                if (item.Amount <= 0) continue;
                result.TryGetValue(item.Item, out var value); value.Pending += item.Amount; result[item.Item] = value;
            }
            return result;
        }

    }
}
