using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    public sealed class ResourceForecastReadModel
    {
        public sealed class Resource
        {
            public long Income, Expense;
            public readonly List<EconomyEntry> Incomes = new List<EconomyEntry>();
            public readonly List<EconomyEntry> Expenses = new List<EconomyEntry>();
        }

        public static SortedDictionary<ItemId, Resource> Read(EntityManager em, Entity root, int turn)
        {
            var result = new SortedDictionary<ItemId, Resource>(Comparer<ItemId>.Create((a, b) => a.Index.CompareTo(b.Index)));
            if (!em.HasBuffer<EconomyForecastEntry>(root))
                return result;
            foreach (var row in em.GetBuffer<EconomyForecastEntry>(root))
            {
                var entry = row.Value;
                if (entry.Turn != turn || !entry.Item.IsValid || entry.Reason == EconomyReason.CapacityTransfer || entry.Delta == 0)
                    continue;
                if (!result.TryGetValue(entry.Item, out var item))
                    result.Add(entry.Item, item = new Resource());
                if (entry.Delta > 0)
                {
                    item.Income += entry.Delta;
                    item.Incomes.Add(entry);
                }
                else
                {
                    item.Expense -= (long)entry.Delta;
                    item.Expenses.Add(entry);
                }
            }

            return result;
        }
    }
}
