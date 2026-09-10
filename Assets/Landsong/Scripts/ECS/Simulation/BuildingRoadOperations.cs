using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public sealed class RoadPlan
    {
        public readonly List<int2> Path = new List<int2>();
        public readonly List<int2> NewCells = new List<int2>();
        public BuildingActionQuote Quote = new BuildingActionQuote();
    }
    public static class BuildingRoadOps
    {
        public static bool IsRoad(EntityManager em, Entity root, int definition) => (Sim.Definition(em, root, definition).BuildingPolicy.Category & BuildingCategory.Road) != 0;
        static bool Existing(EntityManager em, Entity root, int2 cell)
        {
            var index = GridOps.Index(em.GetComponentData<GridData>(root), cell); if (index < 0) return false;
            var entity = Sim.Find(em, em.GetBuffer<Occupancy>(root)[index].Owner);
            return Sim.Operational(em, entity) && IsRoad(em, root, em.GetComponentData<Identity>(entity).Definition);
        }
        static List<int2> Path(int2 start, int2 end, bool horizontal)
        {
            var path = new List<int2>(); var at = start; path.Add(at);
            while (math.any(at != end))
            {
                if (horizontal ? at.x != end.x : at.y == end.y) at.x += end.x > at.x ? 1 : -1;
                else at.y += end.y > at.y ? 1 : -1;
                path.Add(at);
            }
            return path;
        }
        public static RoadPlan Plan(EntityManager em, Entity root, int definition, int2 start, int2 end)
        {
            var result = new RoadPlan(); result.Quote = BuildingOps.CheckBuild(em, root, definition);
            if (!Sim.ValidDefinition(em, root, definition) || !IsRoad(em, root, definition) || math.any(Sim.Definition(em, root, definition).Size != new int2(1)))
            { result.Quote.Fail(ResultCode.InvalidContent, "道路连续铺设只支持 1×1 道路定义"); return result; }
            var grid = em.GetComponentData<GridData>(root);
            if (GridOps.Index(grid, start) < 0 || GridOps.Index(grid, end) < 0 || math.csum(math.abs(end - start)) > 512)
            { result.Quote.Fail(ResultCode.InvalidPlacement, "道路端点超出地图或单次超过 512 格"); return result; }
            var a = Path(start, end, true); var b = Path(start, end, false);
            int Invalid(List<int2> path) { var count = 0; foreach (var cell in path) if (!Existing(em, root, cell) && !GridOps.CanPlace(em, root, definition, cell, 0)) count++; return count; }
            var invalidA = Invalid(a); var invalidB = Invalid(b); result.Path.AddRange(invalidA <= invalidB ? a : b);
            foreach (var cell in result.Path) if (!Existing(em, root, cell)) result.NewCells.Add(cell);
            result.Quote.Costs = BuildingCostOps.Scale(BuildingCostOps.Rules(em, root, definition, RuleKind.PlacementCost, 1), result.NewCells.Count);
            if (math.min(invalidA, invalidB) > 0) result.Quote.Fail(ResultCode.InvalidPlacement, "道路路径存在不可建格，整段不会扣费");
            else if (result.NewCells.Count == 0) result.Quote.Fail(ResultCode.Unavailable, "整段已是道路，不重复建造或扣费");
            else if (!BuildingCostOps.CanPay(em, root, result.Quote.Costs)) result.Quote.Fail(ResultCode.InsufficientResources, "整段道路材料不足");
            return result;
        }
        public static ResultCode Build(EntityManager em, Entity root, Command command)
        {
            var grid = em.GetComponentData<GridData>(root);
            var plan = Plan(em, root, command.Definition, GridOps.Cell(grid, command.Position), GridOps.Cell(grid, command.EndPosition));
            if (!plan.Quote.Allowed) return plan.Quote.Code;
            var d = Sim.Definition(em, root, command.Definition);
            if (d.Limit > 0)
            {
                var count = 0; using var buildings = Sim.Entities<Building>(em);
                foreach (var e in buildings) { var def = em.GetComponentData<Identity>(e).Definition; if (def == command.Definition || d.Group >= 0 && Sim.Definition(em, root, def).Group == d.Group) count++; }
                if (count + plan.NewCells.Count > d.Limit) return ResultCode.NoCapacity;
            }
            // Validate the entire path and aggregate price before creating the first segment.
            if (!BuildingCostOps.Pay(em, root, plan.Quote.Costs)) return ResultCode.InsufficientResources;
            foreach (var cell in plan.NewCells) BuildingOps.Create(em, root, command.Definition, cell, 0, 1, d.Duration <= 0);
            BuildingOps.Changed(em, root); return ResultCode.Success;
        }
    }
}
