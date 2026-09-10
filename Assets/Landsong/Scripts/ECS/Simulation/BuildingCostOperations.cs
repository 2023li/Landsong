using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Read-only quotes are also used by commands. UI never implements its own cost arithmetic.
    public struct BuildingCost { public int Item, Amount; public BuildingCost(int item, int amount) { Item = item; Amount = amount; } }
    public struct BuildingRefund { public int Item, Amount, Stored, Lost; }
    public static partial class BuildingCostOps
    {
        // Designer-facing decimal percentages must not turn 100 * 0.3f into ceil(30.000002) == 31.
        public static int ScaleAmount(int amount, float ratio) => (int)System.Math.Ceiling((decimal)amount * (decimal)math.max(0, ratio));
        public static void Add(List<BuildingCost> costs, int item, int amount)
        {
            if (item < 0 || amount <= 0) return;
            for (var i = 0; i < costs.Count; i++) if (costs[i].Item == item) { costs[i] = new BuildingCost(item, checked(costs[i].Amount + amount)); return; }
            costs.Add(new BuildingCost(item, amount));
        }
        public static List<BuildingCost> Rules(EntityManager em, Entity root, int definition, RuleKind kind, int level)
        {
            var costs = new List<BuildingCost>(); var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (EconomyOps.Matches(r, kind, level)) Add(costs, r.Target, r.Amount); }
            costs.Sort((a, b) => a.Item.CompareTo(b.Item)); return costs;
        }
        public static List<BuildingCost> DefinitionInvestment(EntityManager em, Entity root, int definition, int level, int paidSteps, bool upgrades = true)
        {
            var costs = new List<BuildingCost>(); var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (EconomyOps.Matches(r, RuleKind.PlacementCost, 1)) Add(costs, r.Target, r.Amount);
                else if (r.Kind == RuleKind.ConstructionCost && paidSteps > 0 && r.Level <= paidSteps) Add(costs, r.Target, r.Level == 0 ? r.Amount * math.min(paidSteps, math.max(1, d.Duration)) : r.Amount);
                else if (upgrades && r.Kind == RuleKind.UpgradeCost && level > 1 && (r.Level == 0 || r.Level > 1 && r.Level <= level)) Add(costs, r.Target, r.Level == 0 ? r.Amount * (level - 1) : r.Amount);
            }
            costs.Sort((a, b) => a.Item.CompareTo(b.Item)); return costs;
        }
        public static List<BuildingCost> Investment(EntityManager em, Entity building)
        {
            var costs = new List<BuildingCost>();
            foreach (var c in em.GetBuffer<BuildingInvestment>(building)) Add(costs, c.Item, c.Amount);
            costs.Sort((a, b) => a.Item.CompareTo(b.Item)); return costs;
        }
        public static void RecordInvestment(EntityManager em, Entity building, List<BuildingCost> costs)
        {
            var buffer = em.GetBuffer<BuildingInvestment>(building);
            foreach (var c in costs)
            {
                var found = false;
                for (var i = 0; i < buffer.Length; i++) if (buffer[i].Item == c.Item) { var entry = buffer[i]; entry.Amount += c.Amount; buffer[i] = entry; found = true; break; }
                if (!found) buffer.Add(new BuildingInvestment { Item = c.Item, Amount = c.Amount });
            }
        }
        public static List<BuildingCost> Scale(List<BuildingCost> original, float ratio)
        {
            var result = new List<BuildingCost>();
            foreach (var c in original) Add(result, c.Item, ScaleAmount(c.Amount, ratio));
            return result;
        }
        public static bool CanPay(EntityManager em, Entity root, List<BuildingCost> costs, bool pending = false)
        {
            foreach (var c in costs) if (InventoryOps.Count(em, root, c.Item) + (pending ? InventoryOps.PendingCount(em, root, c.Item) : 0) < c.Amount) return false;
            return true;
        }
        public static bool Pay(EntityManager em, Entity root, List<BuildingCost> costs, bool pending = false)
        {
            if (!CanPay(em, root, costs, pending)) return false;
            foreach (var c in costs)
            {
                if (pending) InventoryOps.RemoveWithPending(em, root, c.Item, c.Amount); else InventoryOps.Remove(em, root, c.Item, c.Amount);
            }
            return true;
        }
        public static List<BuildingCost> RepairTotal(EntityManager em, Entity root, Entity building, out int duration)
        {
            var b = em.GetComponentData<Building>(building); var definition = em.GetComponentData<Identity>(building).Definition; var d = Sim.Definition(em, root, definition);
            var rule = Sim.Rule(em, root, definition, RuleKind.RepairCost, b.Level);
            duration = math.max(1, d.BuildingPolicy.RepairTurns > 0 ? d.BuildingPolicy.RepairTurns : rule.C > 0 ? rule.C : d.Duration);
            return rule.Level >= 0 ? Rules(em, root, definition, RuleKind.RepairCost, rule.Level) : Scale(Investment(em, building), .5f);
        }
        public static List<BuildingCost> RepairStep(EntityManager em, Entity building)
        {
            var b = em.GetComponentData<Building>(building); var result = new List<BuildingCost>(); var duration = math.max(1, b.RepairDuration);
            foreach (var r in em.GetBuffer<RepairMaterial>(building)) Add(result, r.Item, r.Amount / duration + (b.Progress < r.Amount % duration ? 1 : 0));
            return result;
        }
        public static List<BuildingCost> MoveCost(EntityManager em, Entity root, Entity building)
        {
            var definition = em.GetComponentData<Identity>(building).Definition; var d = Sim.Definition(em, root, definition);
            // Original move rule: placement + base construction, not upgrades or previous repairs.
            return Scale(DefinitionInvestment(em, root, definition, 1, int.MaxValue, false), d.BuildingPolicy.MoveMaterialRatio);
        }
        public static List<BuildingRefund> DemolitionRefund(EntityManager em, Entity root, Entity building)
        {
            var b = em.GetComponentData<Building>(building); var id = em.GetComponentData<Identity>(building).Id;
            var costs = Scale(Investment(em, building), b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing ? .2f : .5f);
            var slots = new List<InventorySlot>(); foreach (var slot in em.GetBuffer<InventorySlot>(root)) if (slot.Provider != id && slot.Unavailable == 0) slots.Add(slot);
            var result = new List<BuildingRefund>();
            foreach (var c in costs)
            {
                var remaining = c.Amount; var maximum = math.max(1, Sim.Definition(em, root, c.Item).Capacity);
                slots.Sort((a, b) => InventoryOps.CompareStorage(em, root, a, b, c.Item));
                for (var i = 0; i < slots.Count && remaining > 0; i++)
                {
                    var slot = slots[i]; if (!InventoryOps.Accepts(em, root, slot.SlotType, c.Item) || slot.Count > 0 && slot.Item != c.Item) continue;
                    var add = math.min(remaining, maximum - slot.Count); if (add <= 0) continue;
                    slot.Item = c.Item; slot.Count += add; slots[i] = slot; remaining -= add;
                }
                result.Add(new BuildingRefund { Item = c.Item, Amount = c.Amount, Stored = c.Amount - remaining, Lost = remaining });
            }
            return result;
        }
    }
}
