using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BuildingPlacementCommands
    {
        public static BuildingActionQuote CheckBuild(EntityManager em, Entity root, BuildingId definition, int2? cell = null, int rotation = 0)
        {
            var q = new BuildingActionQuote();
            if (!BuildingDefinitions.IsValid(em, root, definition))
                return q.Fail(ResultCode.InvalidContent, "不是建筑定义");
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            q.Costs = BuildingCostOps.Placement(em, root, definition);
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return q.Fail(ResultCode.WrongPhase, "只能在白天建造");
            if (!FeatureOps.Unlocked(em, root, "Building"))
                return q.Fail(ResultCode.Unavailable, "建造许可尚未解锁，请先完成主线任务");
            if (!BuildingBlueprints.Has(em, root, definition))
                return q.Fail(ResultCode.Unavailable, "尚未获得建筑蓝图");
            var count = 0;
            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var e in buildings)
                {
                    var other = em.GetComponentData<BuildingDefinitionRef>(e).Definition;
                    if (other == definition || d.LimitGroup.IsValid && BuildingDefinitions.Get(em, root, other).LimitGroup == d.LimitGroup)
                        count++;
                }

            if (d.MaximumCount > 0 && count >= d.MaximumCount)
                return q.Fail(ResultCode.NoCapacity, "已达到建造数量上限（含施工与荒废建筑）");
            if (rotation < 0 || rotation > 3 || !d.PlacementAndVisuals.CanRotate && rotation != 0)
                return q.Fail(ResultCode.InvalidPlacement, "此建筑不支持该朝向");
            if (cell.HasValue && !GridOps.CanPlace(em, root, definition, cell.Value, rotation))
            {
                if (TerrainConnectionOps.TryGet(em, root, definition, out _))
                {
                    TerrainConnectionOps.CanPlace(em, root, definition, cell.Value, rotation, 0, out var reason);
                    return q.Fail(ResultCode.InvalidPlacement, reason);
                }

                return q.Fail(ResultCode.InvalidPlacement, "占地被占用、地形/高度不符或超出可建范围");
            }

            if (!BuildingCostOps.CanPay(em, root, q.Costs))
                return q.Fail(ResultCode.InsufficientResources, "放置材料不足");
            return q;
        }

        public static BuildingActionQuote CheckMove(EntityManager em, Entity root, Entity e, int2? cell = null, int rotation = -1)
        {
            var q = new BuildingActionQuote();
            if (!BuildingStatus.Operational(em, e))
                return q.Fail(ResultCode.InvalidTarget, "只有正常运营的建筑可以移动");
            BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(e);
            BuildingExperienceState bExperience = em.GetComponentData<BuildingExperienceState>(e);
            var id = em.GetComponentData<Identity>(e);
            ref var d = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition);
            q.Costs = BuildingCostOps.MoveCost(em, root, e);
            q.ExperienceLoss = BuildingCostOps.ScaleAmount(bExperience.Experience, d.PlacementAndVisuals.MoveExperienceRatio);
            if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                return q.Fail(ResultCode.WrongPhase, "只能在白天移动");
            if (!d.PlacementAndVisuals.CanMove)
                return q.Fail(ResultCode.Unavailable, "该建筑禁止移动");
            if (WorkforceSettlement.Locked(em, id.Id))
                return q.Fail(ResultCode.Busy, "远征队伍在途中，不能移动驻地");
            if (rotation < 0)
                rotation = bPlacement.Rotation;
            if (rotation > 3 || !d.PlacementAndVisuals.CanRotate && rotation != bPlacement.Rotation)
                return q.Fail(ResultCode.InvalidPlacement, "此建筑不能旋转");
            if (cell.HasValue)
            {
                if (math.all(cell.Value == bPlacement.Cell) && rotation == bPlacement.Rotation)
                    return q.Fail(ResultCode.Unavailable, "位置和朝向没有变化，不扣费用");
                if (!GridOps.CanPlace(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition, cell.Value, rotation, id.Id))
                    return q.Fail(ResultCode.InvalidPlacement, "目标占地、地形或高度不符合条件");
            }

            if (!BuildingCostOps.CanPay(em, root, q.Costs))
                return q.Fail(ResultCode.InsufficientResources, "移动材料不足");
            return q;
        }

        public static ResultCode Move(EntityManager em, Entity root, Entity e, int2 cell, int rotation)
        {
            var q = BuildingPlacementCommands.CheckMove(em, root, e, cell, rotation);
            if (!q.Allowed)
                return q.Code;
            if (!BuildingCostOps.Pay(em, root, q.Costs))
                return ResultCode.InsufficientResources;
            BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(e);
            BuildingExperienceState bExperience = em.GetComponentData<BuildingExperienceState>(e);
            var id = em.GetComponentData<Identity>(e);
            ref var d = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition);
            var grid = em.GetComponentData<GridData>(root);
            GridOps.Occupy(em, root, e, true);
            bPlacement.Cell = cell;
            bPlacement.Rotation = rotation;
            bPlacement.Size = (rotation & 1) == 0 ? d.Footprint : d.Footprint.yx;
            bExperience.Experience = math.max(0, bExperience.Experience - q.ExperienceLoss);
            bool connection = TerrainConnectionOps.TryGet(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition, out _);
            var anchor = connection ? TerrainConnectionOps.Port(cell, d.Footprint, rotation, 0, 0) : cell;
            var ground = grid.Value.Value.Cells[GridOps.Index(grid, anchor)];
            bPlacement.Elevation = ground.Elevation;
            bPlacement.Surface = ground.Surface;
            {
                em.SetComponentData(e, bPlacement);
                em.SetComponentData(e, bExperience);
            }

            var t = em.GetComponentData<LocalTransform>(e);
            t.Position = GridOps.Position(grid, cell, bPlacement.Size);
            t.Rotation = quaternion.RotateY(rotation * math.PI / 2);
            em.SetComponentData(e, t);
            if (connection)
            {
                t.Position.y = grid.Origin.y + TerrainConnectionOps.AnchorHeight(em, root, em.GetComponentData<BuildingDefinitionRef>(e).Definition, cell, rotation);
                em.SetComponentData(e, t);
            }

            GridOps.Occupy(em, root, e);
            using (var troops = WorldQueries.Entities<Combatant>(em))
                foreach (var troop in troops)
                {
                    var a = em.GetComponentData<Combatant>(troop);
                    if (a.HomeId != id.Id || a.Deployed != 0)
                        continue;
                    a.Home = t.Position;
                    em.SetComponentData(troop, a);
                    var position = em.GetComponentData<LocalTransform>(troop);
                    position.Position = t.Position;
                    em.SetComponentData(troop, position);
                }

            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }

        public static ResultCode Build(EntityManager em, Entity root, BuildRequest c)
        {
            var cell = GridOps.Cell(em.GetComponentData<GridData>(root), c.Position);
            var check = BuildingPlacementCommands.CheckBuild(em, root, c.Definition, cell, c.Rotation);
            if (!check.Allowed)
                return check.Code;
            ref var d = ref BuildingDefinitions.Get(em, root, c.Definition);
            if (!BuildingCostOps.Pay(em, root, BuildingCostOps.Placement(em, root, c.Definition)))
                return ResultCode.InsufficientResources;
            BuildingCreation.Create(em, root, c.Definition, cell, c.Rotation, 1, d.ConstructionTurns <= 0);
            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }
    }
}
