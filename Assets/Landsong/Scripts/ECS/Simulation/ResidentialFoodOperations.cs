using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class ResidentialFoodOps
    {
        // Stock preference is subject to a complete matching: overlapping groups cannot
        // starve narrow groups. Catalog index and slot order never break an item-stock tie.
        public static bool Plan(EntityManager em, Entity root, Entity house, out FoodSelection[] plan)
        {
            plan = Array.Empty<FoodSelection>();
            var b = em.GetComponentData<Building>(house);
            BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(house);
            if (bHousing.Population <= 0)
                return true;
            ref var foodRules = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(house).Definition).Capabilities.Housing.Food;
            int itemCount = ItemDefinitions.Count(em, root);
            var demands = new List<FoodSelection>();
            var candidates = new List<List<ItemId>>();
            for (var i = 0; i < foodRules.Length; i++)
            {
                var rule = foodRules[i];
                if (rule.Level != 0 && rule.Level != b.Level)
                    continue;
                var amount = checked(bHousing.Population * math.max(1, rule.AmountPerResident));
                var choices = new List<ItemId>();
                for (int index = 0; index < itemCount; index++)
                {
                    var item = ItemId.FromIndex(index);
                    if (ItemGroups.Matches(em, root, item, rule.FoodGroup) && InventoryOps.Count(em, root, item) >= amount)
                        choices.Add(item);
                }

                choices.Sort((a, c) =>
                {
                    var order = InventoryOps.Count(em, root, c).CompareTo(InventoryOps.Count(em, root, a));
                    return order != 0 ? order : string.CompareOrdinal(ItemDefinitions.Get(em, root, a).Metadata.Id.ToString(), ItemDefinitions.Get(em, root, c).Metadata.Id.ToString());
                });
                if (choices.Count < rule.Varieties || (long)demands.Count + rule.Varieties > itemCount)
                    return false;
                for (var n = 0; n < rule.Varieties; n++)
                {
                    demands.Add(new FoodSelection { Group = rule.FoodGroup, Amount = amount });
                    candidates.Add(choices);
                }
            }

            var reserved = new HashSet<ItemId>();
            for (var i = 0; i < demands.Count; i++)
            {
                var chosen = ItemId.None;
                foreach (var item in candidates[i])
                {
                    if (!reserved.Add(item))
                        continue;
                    if (CanComplete(candidates, i + 1, reserved))
                    {
                        chosen = item;
                        break;
                    }

                    reserved.Remove(item);
                }

                if (!chosen.IsValid)
                    return false;
                var food = demands[i];
                food.Item = chosen;
                demands[i] = food;
            }

            plan = demands.ToArray();
            return true;
        }

        static bool CanComplete(List<List<ItemId>> candidates, int first, HashSet<ItemId> reserved)
        {
            var owners = new Dictionary<ItemId, int>();
            bool Assign(int demand, HashSet<ItemId> visited)
            {
                foreach (var item in candidates[demand])
                {
                    if (reserved.Contains(item) || !visited.Add(item))
                        continue;
                    if (!owners.TryGetValue(item, out var previous) || Assign(previous, visited))
                    {
                        owners[item] = demand;
                        return true;
                    }
                }

                return false;
            }

            for (var i = first; i < candidates.Count; i++)
                if (!Assign(i, new HashSet<ItemId>()))
                    return false;
            return true;
        }

        public static bool Pay(EntityManager em, Entity root, Entity house)
        {
            em.GetBuffer<FoodSelection>(house).Clear();
            if (!Plan(em, root, house, out var plan))
                return false;
            using var meal = new InventoryTransaction(em, root);
            foreach (var food in plan)
                if (!InventoryOps.Remove(em, root, food.Item, food.Amount))
                    return false;
            meal.Commit();
            foreach (var food in plan)
                em.GetBuffer<FoodSelection>(house).Add(food);
            return true;
        }
    }
}
