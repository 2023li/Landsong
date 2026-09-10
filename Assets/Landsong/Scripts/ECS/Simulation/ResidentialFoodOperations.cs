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
            if (b.Population <= 0) return true;
            var d = Sim.Definition(em, root, em.GetComponentData<Identity>(house).Definition);
            var definitions = em.GetComponentData<ContentCatalog>(root).Value;
            var demands = new List<FoodSelection>(); var candidates = new List<List<int>>();
            for (var i = 0; i < d.RuleCount; i++)
            {
                var rule = Sim.GetRule(em, root, d.RuleStart + i);
                if (!EconomyOps.Matches(rule, RuleKind.Food, b.Level)) continue;
                var amount = checked(b.Population * math.max(1, rule.B));
                var choices = new List<int>();
                for (var item = 0; item < definitions.Value.Definitions.Length; item++)
                    if (definitions.Value.Definitions[item].Kind == ContentKind.Item && InventoryOps.MatchesGroup(em, root, item, rule.Target) && InventoryOps.Count(em, root, item) >= amount) choices.Add(item);
                choices.Sort((a, c) => { var order = InventoryOps.Count(em, root, c).CompareTo(InventoryOps.Count(em, root, a)); return order != 0 ? order : string.CompareOrdinal(Sim.Definition(em, root, a).Id.ToString(), Sim.Definition(em, root, c).Id.ToString()); });
                if (choices.Count < rule.Amount || (long)demands.Count + rule.Amount > definitions.Value.Definitions.Length) return false;
                for (var n = 0; n < rule.Amount; n++) { demands.Add(new FoodSelection { Group = rule.Target, Amount = amount }); candidates.Add(choices); }
            }
            var reserved = new HashSet<int>();
            for (var i = 0; i < demands.Count; i++)
            {
                var chosen = -1;
                foreach (var item in candidates[i])
                {
                    if (!reserved.Add(item)) continue;
                    if (CanComplete(candidates, i + 1, reserved)) { chosen = item; break; }
                    reserved.Remove(item);
                }
                if (chosen < 0) return false;
                var food = demands[i]; food.Item = chosen; demands[i] = food;
            }
            plan = demands.ToArray(); return true;
        }
        static bool CanComplete(List<List<int>> candidates, int first, HashSet<int> reserved)
        {
            var owners = new Dictionary<int, int>();
            bool Assign(int demand, HashSet<int> visited)
            {
                foreach (var item in candidates[demand])
                {
                    if (reserved.Contains(item) || !visited.Add(item)) continue;
                    if (!owners.TryGetValue(item, out var previous) || Assign(previous, visited)) { owners[item] = demand; return true; }
                }
                return false;
            }
            for (var i = first; i < candidates.Count; i++) if (!Assign(i, new HashSet<int>())) return false;
            return true;
        }
        public static bool Pay(EntityManager em, Entity root, Entity house)
        {
            em.GetBuffer<FoodSelection>(house).Clear();
            if (!Plan(em, root, house, out var plan)) return false;
            using var meal = new InventoryTransaction(em, root);
            foreach (var food in plan) if (!InventoryOps.Remove(em, root, food.Item, food.Amount)) return false;
            meal.Commit();
            foreach (var food in plan) em.GetBuffer<FoodSelection>(house).Add(food);
            return true;
        }
    }
}
