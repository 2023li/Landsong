using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Domain execution only. GameLoopSystem owns phase/permission/pause checks and request results.
    public static class BuildingCommandHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity target, Command command, out ResultCode result)
        {
            switch (command.Kind)
            {
                case CommandKind.Build: case CommandKind.MoveBuilding: case CommandKind.BuildRoad:
                case CommandKind.ChangeBuildingSkin: case CommandKind.ClearCrop: case CommandKind.Harvest:
                case CommandKind.Demolish: case CommandKind.Repair: case CommandKind.Upgrade:
                case CommandKind.Rename: case CommandKind.RecruitWorkerAtQuotedCost: case CommandKind.Workers:
                case CommandKind.WorkforceBudget: case CommandKind.WorkforceTarget: case CommandKind.AutoHarvest:
                case CommandKind.Subsidy: case CommandKind.Offering: case CommandKind.Plant:
                    result = Execute(em, root, target, command); return true;
                default: result = ResultCode.Unavailable; return false;
            }
        }

        public static ResultCode Execute(EntityManager em, Entity root, Entity target, Command c)
        {
            switch (c.Kind)
            {
                case CommandKind.Build: return BuildingOps.Build(em, root, c);
                case CommandKind.MoveBuilding: return BuildingOps.Move(em, root, target, GridOps.Cell(em.GetComponentData<GridData>(root), c.Position), c.Argument);
                case CommandKind.BuildRoad: return BuildingRoadOps.Build(em, root, c);
                case CommandKind.ChangeBuildingSkin:
                    if (!Sim.Operational(em, target) || !em.HasBuffer<BuildingVisualSlot>(target) || c.Text.Length > 60) return ResultCode.InvalidTarget;
                    var matchingSkin = false;
                    foreach (var slot in em.GetBuffer<BuildingVisualSlot>(target)) if (slot.Purpose == BuildingVisualPurpose.Operational && slot.Skin.ToString() == c.Text.ToString()) { matchingSkin = true; break; }
                    if (!matchingSkin) return ResultCode.InvalidContent;
                    var appearance = em.GetComponentData<Building>(target); appearance.Skin = new FixedString64Bytes(c.Text.ToString()); em.SetComponentData(target, appearance); return ResultCode.Success;
                case CommandKind.ClearCrop:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var cleared = em.GetComponentData<Building>(target); if (cleared.Crop < 0) return ResultCode.Unavailable;
                    cleared.Crop = -1; cleared.CropProgress = 0; cleared.CropSeed = 0; cleared.CropFullCycle = 0; em.SetComponentData(target, cleared); BuildingOps.Changed(em, root); return ResultCode.Success;
                case CommandKind.Harvest:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var harvestState = em.GetComponentData<Building>(target); var harvestId = em.GetComponentData<Identity>(target).Definition;
                    if (harvestState.Crop >= 0) return EconomyOps.HarvestCrop(em, root, target, false);
                    var harvest = Sim.Rule(em, root, harvestId, RuleKind.Harvest, harvestState.Level);
                    if (harvest.Level < 0 || harvestState.HarvestRemaining <= 0) return ResultCode.Unavailable;
                    using (var transaction = new InventoryTransaction(em, root))
                    {
                        var success = harvest.Target >= 0 ? InventoryOps.Add(em, root, harvest.Target, harvest.B) == harvest.B : harvestState.HarvestRemaining > 1 || RewardOps.ApplyDefinition(em, root, harvestId,level:harvestState.Level);
                        if (!success) return ResultCode.NoCapacity;
                        transaction.Commit();
                    }
                    harvestState.HarvestRemaining--; em.SetComponentData(target, harvestState);
                    if (harvestState.HarvestRemaining <= 0) { GridOps.Occupy(em, root, target, true); em.DestroyEntity(target); }
                    return ResultCode.Success;
                case CommandKind.Demolish: return BuildingOps.Demolish(em, root, target);
                case CommandKind.Repair: return BuildingOps.Repair(em, root, target);
                case CommandKind.Upgrade: return BuildingOps.Upgrade(em, root, target);
                case CommandKind.Rename:
                    return BuildingOps.Rename(em, target, c.Text);
                case CommandKind.RecruitWorkerAtQuotedCost:
                    if (c.Amount <= 0 || c.Argument < 0) return ResultCode.InvalidContent;
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    if (WorkforceOps.Quote(em, root, target).RecruitCost != c.Argument) return ResultCode.Unavailable;
                    return EconomyOps.ChangeWorkers(em, root, target, c.Amount);
                case CommandKind.Workers:
                    return EconomyOps.ChangeWorkers(em, root, target, c.Amount);
                case CommandKind.WorkforceBudget: return c.Argument==1?WorkforceOps.AdjustBudget(em,root,target,c.Amount):c.Argument==0?WorkforceOps.SetBudget(em,root,target,c.Amount):ResultCode.InvalidContent;
                case CommandKind.WorkforceTarget: return WorkforceOps.SetTarget(em, root, target, c.Amount);
                case CommandKind.AutoHarvest:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var auto = em.GetComponentData<Building>(target); auto.AutoHarvest = (byte)(c.Amount > 0 ? 1 : 0); em.SetComponentData(target, auto); return ResultCode.Success;
                case CommandKind.Subsidy: case CommandKind.Offering:
                    if (!Sim.Operational(em, target)) return ResultCode.InvalidTarget;
                    var b = em.GetComponentData<Building>(target);
                    if (c.Kind == CommandKind.Subsidy) return WorkforceOps.SetTarget(em, root, target, c.Amount > 0 ? c.Argument > 0 ? c.Argument : em.GetComponentData<BuildingStats>(target).JobCapacity : 0);
                    b.Offering = (byte)(c.Amount > 0 ? 1 : 0);
                    em.SetComponentData(target, b); return ResultCode.Success;
                case CommandKind.Plant:
                    if (!Sim.Operational(em, target) || !Sim.ValidDefinition(em, root, c.Definition) || Sim.Definition(em, root, c.Definition).Kind != ContentKind.Crop) return ResultCode.InvalidContent;
                    var field = em.GetComponentData<Building>(target); if (field.Crop >= 0) return ResultCode.Busy;
                    var available = false; var site = Sim.Definition(em, root, em.GetComponentData<Identity>(target).Definition);
                    for (var i = 0; i < site.RuleCount; i++) { var r = Sim.GetRule(em, root, site.RuleStart + i); if (EconomyOps.Matches(r,RuleKind.Crop,em.GetComponentData<Building>(target).Level) && r.Target == c.Definition) available = true; }
                    if (!available || !InventoryOps.Pay(em, root, c.Definition, RuleKind.PlacementCost, 1)) return ResultCode.Unavailable;
                    field.Crop = c.Definition; field.CropProgress = 0; field.CropFullCycle = 1; field.CropSeed = Sim.NextRandom(em, root); em.SetComponentData(target, field); return ResultCode.Success;
            }
            return ResultCode.Unavailable;
        }
    }
}
