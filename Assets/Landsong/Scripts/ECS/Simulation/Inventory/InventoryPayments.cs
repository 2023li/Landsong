using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class InventoryPayments
    {
        public static bool Pay(EntityManager em, Entity root, ref BlobArray<ItemAmount> requirements, float multiplier = 1f, bool commit = true, bool includePending = false)
        {
            var costs = new Dictionary<ItemId, int>();
            for (int i = 0; i < requirements.Length; i++)
            {
                var requirement = requirements[i];
                if (!requirement.Item.IsValid)
                    continue;
                costs.TryGetValue(requirement.Item, out var prior);
                costs[requirement.Item] = checked(prior + (int)math.ceil(requirement.Quantity * multiplier));
            }

            foreach (var cost in costs)
                if (InventoryOps.Count(em, root, cost.Key) + (includePending ? InventoryOps.PendingCount(em, root, cost.Key) : 0) < cost.Value)
                    return false;
            if (commit)
                foreach (var cost in costs)
                {
                    if (includePending)
                        InventoryOps.RemoveWithPending(em, root, cost.Key, cost.Value);
                    else
                        InventoryOps.Remove(em, root, cost.Key, cost.Value);
                }

            return true;
        }
    }
}
