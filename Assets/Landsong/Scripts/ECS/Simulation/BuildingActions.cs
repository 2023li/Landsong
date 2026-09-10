using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public sealed class BuildingActionQuote
    {
        public ResultCode Code = ResultCode.Success;
        public string Reason = "";
        public List<BuildingCost> Costs = new List<BuildingCost>();
        public int ExperienceLoss;
        public bool Allowed => Code == ResultCode.Success;
        public BuildingActionQuote Fail(ResultCode code, string reason) { Code = code; Reason = reason; return this; }
    }
    public static partial class BuildingOps
    {
        public static BuildingActionQuote CheckBuild(EntityManager em, Entity root, int definition, int2? cell = null, int rotation = 0)
        {
            var q = new BuildingActionQuote();
            if (!Sim.ValidDefinition(em, root, definition) || Sim.Definition(em, root, definition).Kind != ContentKind.Building) return q.Fail(ResultCode.InvalidContent, "不是建筑定义");
            var d = Sim.Definition(em, root, definition); q.Costs = BuildingCostOps.Rules(em, root, definition, RuleKind.PlacementCost, 1);
            if (em.GetComponentData<Session>(root).Phase != Phase.Day) return q.Fail(ResultCode.WrongPhase, "只能在白天建造");
            if (!FeatureOps.Unlocked(em, root, "Building")) return q.Fail(ResultCode.Unavailable, "建造许可尚未解锁，请先完成主线任务");
            if (!Sim.HasGrant(em, root, definition)) return q.Fail(ResultCode.Unavailable, "尚未获得建筑蓝图");
            var count = 0; using (var buildings = Sim.Entities<Building>(em)) foreach (var e in buildings)
            { var other = em.GetComponentData<Identity>(e).Definition; if (other == definition || d.Group >= 0 && Sim.Definition(em, root, other).Group == d.Group) count++; }
            if (d.Limit > 0 && count >= d.Limit) return q.Fail(ResultCode.NoCapacity, "已达到建造数量上限（含施工与荒废建筑）");
            if (rotation < 0 || rotation > 3 || d.BuildingPolicy.CanRotate == 0 && rotation != 0) return q.Fail(ResultCode.InvalidPlacement, "此建筑不支持该朝向");
            if (cell.HasValue && !GridOps.CanPlace(em, root, definition, cell.Value, rotation)) return q.Fail(ResultCode.InvalidPlacement, "占地被占用、地形/高度不符或超出可建范围");
            if (!BuildingCostOps.CanPay(em, root, q.Costs)) return q.Fail(ResultCode.InsufficientResources, "放置材料不足");
            return q;
        }
        public static BuildingActionQuote CheckMove(EntityManager em, Entity root, Entity e, int2? cell = null, int rotation = -1)
        {
            var q = new BuildingActionQuote();
            if (!Sim.Operational(em, e)) return q.Fail(ResultCode.InvalidTarget, "只有正常运营的建筑可以移动");
            var b = em.GetComponentData<Building>(e); var id = em.GetComponentData<Identity>(e); var d = Sim.Definition(em, root, id.Definition);
            q.Costs = BuildingCostOps.MoveCost(em, root, e); q.ExperienceLoss = BuildingCostOps.ScaleAmount(b.Experience, d.BuildingPolicy.MoveExperienceRatio);
            if (em.GetComponentData<Session>(root).Phase != Phase.Day) return q.Fail(ResultCode.WrongPhase, "只能在白天移动");
            if (d.BuildingPolicy.CanMove == 0) return q.Fail(ResultCode.Unavailable, "该建筑禁止移动");
            if (EconomyOps.WorkforceLocked(em, id.Id)) return q.Fail(ResultCode.Busy, "远征队伍在途中，不能移动驻地");
            if (rotation < 0) rotation = b.Rotation;
            if (rotation > 3 || d.BuildingPolicy.CanRotate == 0 && rotation != b.Rotation) return q.Fail(ResultCode.InvalidPlacement, "此建筑不能旋转");
            if (cell.HasValue)
            {
                if (math.all(cell.Value == b.Cell) && rotation == b.Rotation) return q.Fail(ResultCode.Unavailable, "位置和朝向没有变化，不扣费用");
                if (!GridOps.CanPlace(em, root, id.Definition, cell.Value, rotation, id.Id)) return q.Fail(ResultCode.InvalidPlacement, "目标占地、地形或高度不符合条件");
            }
            if (!BuildingCostOps.CanPay(em, root, q.Costs)) return q.Fail(ResultCode.InsufficientResources, "移动材料不足");
            return q;
        }
        public static ResultCode Move(EntityManager em, Entity root, Entity e, int2 cell, int rotation)
        {
            var q = CheckMove(em, root, e, cell, rotation); if (!q.Allowed) return q.Code;
            if (!BuildingCostOps.Pay(em, root, q.Costs)) return ResultCode.InsufficientResources;
            var b = em.GetComponentData<Building>(e); var id = em.GetComponentData<Identity>(e); var d = Sim.Definition(em, root, id.Definition); var grid = em.GetComponentData<GridData>(root);
            GridOps.Occupy(em, root, e, true);
            b.Cell = cell; b.Rotation = rotation; b.Size = (rotation & 1) == 0 ? d.Size : d.Size.yx; b.Experience = math.max(0, b.Experience - q.ExperienceLoss);
            var ground = grid.Value.Value.Cells[GridOps.Index(grid, cell)]; b.Elevation = ground.Elevation; b.Surface = ground.Surface;
            em.SetComponentData(e, b); var t = em.GetComponentData<LocalTransform>(e); t.Position = GridOps.Position(grid, cell, b.Size); t.Rotation = quaternion.RotateY(rotation * math.PI / 2); em.SetComponentData(e, t);
            GridOps.Occupy(em, root, e);
            using (var troops = Sim.Entities<Combatant>(em)) foreach (var troop in troops)
            {
                var a = em.GetComponentData<Combatant>(troop); if (a.HomeId != id.Id || a.Deployed != 0) continue;
                a.Home = t.Position; em.SetComponentData(troop, a); var position = em.GetComponentData<LocalTransform>(troop); position.Position = t.Position; em.SetComponentData(troop, position);
            }
            Changed(em, root); return ResultCode.Success;
        }
        public static BuildingActionQuote CheckUpgrade(EntityManager em, Entity root, Entity e)
        {
            var q = new BuildingActionQuote();
            if (!Sim.Operational(em, e)) return q.Fail(ResultCode.InvalidTarget, "施工、荒废和修复中不能升级");
            if (em.GetComponentData<Session>(root).Phase != Phase.Day) return q.Fail(ResultCode.WrongPhase, "只能在白天升级");
            var b = em.GetComponentData<Building>(e); var definition = em.GetComponentData<Identity>(e).Definition; var d = Sim.Definition(em, root, definition);
            q.Costs = BuildingCostOps.Rules(em, root, definition, RuleKind.UpgradeCost, b.Level + 1);
            if (b.Level >= d.Level) return q.Fail(ResultCode.Unavailable, "已达到最高等级");
            if (!Sim.HasGrant(em, root, definition, b.Level + 1)) return q.Fail(ResultCode.MissingResearch, "尚未获得下一等级蓝图");
            if (EconomyOps.WorkforceLocked(em, em.GetComponentData<Identity>(e).Id)) return q.Fail(ResultCode.Busy, "远征在途，不能升级驻地");
            var experience = Sim.Rule(em, root, definition, RuleKind.Experience, b.Level);
            if (b.Experience < experience.B) return q.Fail(ResultCode.Unavailable, "经验不足：" + b.Experience + "/" + experience.B);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Level != 0 && r.Level != b.Level + 1) continue;
                if (r.Kind == RuleKind.UpgradeWorkers && b.Workers < r.Amount) return q.Fail(ResultCode.InsufficientPopulation, "升级需要工人：" + r.Amount);
                if (r.Kind == RuleKind.UpgradePopulation && b.Population < r.Amount) return q.Fail(ResultCode.InsufficientPopulation, "升级需要居民：" + r.Amount);
                if (r.Kind == RuleKind.UpgradeMaintained && b.Maintained == 0) return q.Fail(ResultCode.Unavailable, "须完成本回合维护");
            }
            if (!BuildingCostOps.CanPay(em, root, q.Costs)) return q.Fail(ResultCode.InsufficientResources, "升级材料不足");
            return q;
        }
        public static ResultCode Rename(EntityManager em, Entity e, FixedString128Bytes name)
        {
            if (e == Entity.Null || !em.Exists(e) || !em.HasComponent<Building>(e)) return ResultCode.InvalidTarget;
            var clean = SanitizeName(name.ToString()); var id = em.GetComponentData<Identity>(e);
            id.Name = string.IsNullOrWhiteSpace(clean) ? Sim.Definition(em, Sim.Root(em), id.Definition).Name : new FixedString128Bytes(clean); em.SetComponentData(e, id); return ResultCode.Success;
        }
        public static string SanitizeName(string input)
        {
            var result = new StringBuilder(); var tag = false; var count = 0;
            foreach (var c in input ?? "")
            {
                if (c == '<') { tag = true; continue; } if (c == '>') { tag = false; continue; }
                if (tag || char.IsControl(c) || char.IsSurrogate(c)) continue;
                var bytes = Encoding.UTF8.GetByteCount(new[] { c }); if (count + bytes > 120 || result.Length >= 32) break;
                result.Append(c); count += bytes;
            }
            return result.ToString().Trim();
        }
        public static void CommitRuin(EntityManager em, Entity root, Entity e)
        {
            var b = em.GetComponentData<Building>(e); if (b.RuinPending == 0) return;
            var id = em.GetComponentData<Identity>(e);
            b.RuinPending = 0; b.Workers = 0; b.Population = 0; b.DeferredResidents = 0; em.SetComponentData(e, b);
            InventoryOps.LoseProvider(em, root, id.Id);
            ExpeditionOps.WithdrawFromSite(em, root, id.Id);
            using var troops = Sim.Entities<Soldier>(em);
            foreach (var troop in troops) { var s = em.GetComponentData<Soldier>(troop); if (s.Garrison != id.Id) continue; s.Garrison = 0; s.Slot = 0; s.PendingSince = em.GetComponentData<Session>(root).Turn; em.SetComponentData(troop, s); }
        }
        public static void DawnBuildings(EntityManager em, Entity root)
        {
            using var buildings = Sim.Entities<Building>(em);
            foreach (var e in buildings) CommitRuin(em, root, e);
            foreach (var e in buildings)
            {
                var b = em.GetComponentData<Building>(e);
                if (b.DeferredResidents > 0 && b.Stage == LifeStage.Operational) { b.Population = math.min(b.DeferredResidents, em.GetComponentData<BuildingStats>(e).MaxPopulation); b.DeferredResidents = 0; em.SetComponentData(e, b); }
                if (em.GetComponentData<BuildingStats>(e).IsCore != 0 && Sim.Alive(em, e)) { var h = em.GetComponentData<Health>(e); h.Current = h.Maximum; em.SetComponentData(e, h); }
            }
            EconomyOps.ReconcilePopulation(em, root);
        }
        public static void Changed(EntityManager em, Entity root)
        {
            // Only targets are projected again; quantities, types, timing, seeds and threat stay locked.
            var phase = em.GetComponentData<Session>(root).Phase; if (phase != Phase.Day && phase != Phase.Settlement) return;
            NightPlanOps.Reproject(em, root);
            ProgressionOps.EvaluateQuests(em, root);
        }
    }
}
