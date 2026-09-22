using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Read-only quotes are also used by commands. UI never implements its own cost arithmetic.
    public struct BuildingCost
    {
        public ItemId Item;
        public int Amount;
        public BuildingCost(ItemId item, int amount)
        {
            Item = item;
            Amount = amount;
        }
    }

    public struct BuildingRefund
    {
        public ItemId Item;
        public int Amount, Stored, Lost;
    }

    public static partial class BuildingCostOps
    {
        // Designer-facing decimal percentages must not turn 100 * 0.3f into ceil(30.000002) == 31.
        public static int ScaleAmount(int amount, float ratio) => (int)System.Math.Ceiling((decimal)amount * (decimal)math.max(0, ratio));
        public static void Add(List<BuildingCost> costs, ItemId item, int amount)
        {
            if (!item.IsValid || amount <= 0)
                return;
            for (var i = 0; i < costs.Count; i++)
                if (costs[i].Item == item)
                {
                    costs[i] = new BuildingCost(item, checked(costs[i].Amount + amount));
                    return;
                }

            costs.Add(new BuildingCost(item, amount));
        }

        public static List<BuildingCost> Placement(EntityManager em, Entity root, BuildingId definition)
        {
            var costs = new List<BuildingCost>();
            ref var rows = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Construction.PlacementCosts;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                Add(costs, row.Item, row.Quantity);
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> ConstructionStage(EntityManager em, Entity root, BuildingId definition, int level)
        {
            var costs = new List<BuildingCost>();
            ref var rows = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Construction.StageCosts;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.Stage != 0 && row.Stage != level)
                    continue;
                Add(costs, row.Item, row.Quantity);
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> Maintenance(EntityManager em, Entity root, BuildingId definition, int level)
        {
            var costs = new List<BuildingCost>();
            ref var rows = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Maintenance.Costs;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.Level != 0 && row.Level != level)
                    continue;
                Add(costs, row.Item, row.Quantity);
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> ProductionInputs(EntityManager em, Entity root, BuildingId definition, int level)
        {
            var costs = new List<BuildingCost>();
            ref var rows = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Production.Inputs;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.Level != 0 && row.Level != level)
                    continue;
                Add(costs, row.Item, row.Quantity);
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> Upgrade(EntityManager em, Entity root, BuildingId definition, int level)
        {
            var costs = new List<BuildingCost>();
            ref var rows = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Upgrade.Costs;
            for (int i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.TargetLevel != 0 && row.TargetLevel != level)
                    continue;
                Add(costs, row.Item, row.Quantity);
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> DefinitionInvestment(EntityManager em, Entity root, BuildingId definition, int level, int paidSteps, bool upgrades = true)
        {
            var costs = Placement(em, root, definition);
            ref var source = ref BuildingDefinitions.Get(em, root, definition);
            ref var construction = ref source.Capabilities.Construction.StageCosts;
            for (int i = 0; i < construction.Length; i++)
            {
                var row = construction[i];
                if (paidSteps > 0 && row.Stage <= paidSteps)
                    Add(costs, row.Item, row.Stage == 0 ? row.Quantity * math.min(paidSteps, math.max(1, source.ConstructionTurns)) : row.Quantity);
            }

            if (upgrades && level > 1)
            {
                ref var upgradeCosts = ref source.Capabilities.Upgrade.Costs;
                for (int i = 0; i < upgradeCosts.Length; i++)
                {
                    var row = upgradeCosts[i];
                    if (row.TargetLevel == 0 || row.TargetLevel > 1 && row.TargetLevel <= level)
                        Add(costs, row.Item, row.TargetLevel == 0 ? row.Quantity * (level - 1) : row.Quantity);
                }
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> Investment(EntityManager em, Entity building)
        {
            var costs = new List<BuildingCost>();
            foreach (var c in em.GetBuffer<BuildingInvestment>(building))
                Add(costs, c.Item, c.Amount);
            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static void RecordInvestment(EntityManager em, Entity building, List<BuildingCost> costs)
        {
            var buffer = em.GetBuffer<BuildingInvestment>(building);
            foreach (var c in costs)
            {
                var found = false;
                for (var i = 0; i < buffer.Length; i++)
                    if (buffer[i].Item == c.Item)
                    {
                        var entry = buffer[i];
                        entry.Amount += c.Amount;
                        buffer[i] = entry;
                        found = true;
                        break;
                    }

                if (!found)
                    buffer.Add(new BuildingInvestment { Item = c.Item, Amount = c.Amount });
            }
        }

        public static List<BuildingCost> Scale(List<BuildingCost> original, float ratio)
        {
            var result = new List<BuildingCost>();
            foreach (var c in original)
                Add(result, c.Item, ScaleAmount(c.Amount, ratio));
            return result;
        }

        public static bool CanPay(EntityManager em, Entity root, List<BuildingCost> costs, bool pending = false)
        {
            foreach (var c in costs)
                if (InventoryOps.Count(em, root, c.Item) + (pending ? InventoryOps.PendingCount(em, root, c.Item) : 0) < c.Amount)
                    return false;
            return true;
        }

        public static bool Pay(EntityManager em, Entity root, List<BuildingCost> costs, bool pending = false)
        {
            if (!CanPay(em, root, costs, pending))
                return false;
            foreach (var c in costs)
            {
                if (pending)
                    InventoryOps.RemoveWithPending(em, root, c.Item, c.Amount);
                else
                    InventoryOps.Remove(em, root, c.Item, c.Amount);
            }

            return true;
        }

        public static List<BuildingCost> RepairTotal(EntityManager em, Entity root, Entity building, out int duration)
        {
            var state = em.GetComponentData<Building>(building);
            ref var source = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(building).Definition);
            ref var maintenance = ref source.Capabilities.Maintenance;
            int selected = -1;
            for (int i = 0; i < maintenance.Repairs.Length; i++)
                if (maintenance.Repairs[i].Level == 0 || maintenance.Repairs[i].Level == state.Level)
                {
                    selected = i;
                    break;
                }

            int configuredTurns = selected < 0 ? 0 : maintenance.Repairs[selected].RepairTurns;
            duration = math.max(1, maintenance.RepairTurns > 0 ? maintenance.RepairTurns : configuredTurns > 0 ? configuredTurns : source.ConstructionTurns);
            if (selected < 0)
                return Scale(Investment(em, building), .5f);
            var costs = new List<BuildingCost>();
            int selectedLevel = maintenance.Repairs[selected].Level;
            for (int i = 0; i < maintenance.Repairs.Length; i++)
            {
                var row = maintenance.Repairs[i];
                if (row.Level == 0 || row.Level == selectedLevel)
                    Add(costs, row.Item, row.Quantity);
            }

            costs.Sort((a, b) => a.Item.Index.CompareTo(b.Item.Index));
            return costs;
        }

        public static List<BuildingCost> RepairStep(EntityManager em, Entity building)
        {
            BuildingConstructionState bConstruction = em.GetComponentData<BuildingConstructionState>(building);
            var result = new List<BuildingCost>();
            var duration = math.max(1, bConstruction.RepairDuration);
            foreach (var r in em.GetBuffer<RepairMaterial>(building))
                Add(result, r.Item, r.Amount / duration + (bConstruction.Progress < r.Amount % duration ? 1 : 0));
            return result;
        }

        public static List<BuildingCost> MoveCost(EntityManager em, Entity root, Entity building)
        {
            var definition = em.GetComponentData<BuildingDefinitionRef>(building).Definition;
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            // Original move rule: placement + base construction, not upgrades or previous repairs.
            return Scale(DefinitionInvestment(em, root, definition, 1, int.MaxValue, false), d.PlacementAndVisuals.MoveMaterialRatio);
        }

        public static List<BuildingRefund> DemolitionRefund(EntityManager em, Entity root, Entity building)
        {
            var b = em.GetComponentData<Building>(building);
            var id = em.GetComponentData<Identity>(building).Id;
            var costs = Scale(Investment(em, building), b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing ? .2f : .5f);
            var slots = new List<InventorySlot>();
            foreach (var slot in em.GetBuffer<InventorySlot>(root))
                if (slot.Provider != id && slot.Unavailable == 0)
                    slots.Add(slot);
            var result = new List<BuildingRefund>();
            foreach (var c in costs)
            {
                var remaining = c.Amount;
                var maximum = math.max(1, ItemDefinitions.Get(em, root, c.Item).MaximumStack);
                slots.Sort((a, b) => InventoryStorage.CompareStorage(em, root, a, b, c.Item));
                for (var i = 0; i < slots.Count && remaining > 0; i++)
                {
                    var slot = slots[i];
                    if (!InventoryStorage.Accepts(em, root, slot.SlotType, c.Item) || slot.Count > 0 && slot.Item != c.Item)
                        continue;
                    var add = math.min(remaining, maximum - slot.Count);
                    if (add <= 0)
                        continue;
                    slot.Item = c.Item;
                    slot.Count += add;
                    slots[i] = slot;
                    remaining -= add;
                }

                result.Add(new BuildingRefund { Item = c.Item, Amount = c.Amount, Stored = c.Amount - remaining, Lost = remaining });
            }

            return result;
        }
    }
}
