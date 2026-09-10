using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static partial class BuildingOps
    {
        public static Entity Create(EntityManager em, Entity root, int definition, int2 cell, int rotation, int level, bool complete)
        {
            var d = Sim.Definition(em, root, definition);
            var size = (rotation & 1) == 0 ? d.Size : d.Size.yx;
            var grid = em.GetComponentData<GridData>(root);
            var e = Sim.Spawn(em, root, definition, GridOps.Position(grid, cell, size), true);
            var at = GridOps.Index(grid, cell);
            var ground = at < 0 ? default : grid.Value.Value.Cells[at];
            Sim.Set(em, e, new Building { Stage = complete ? LifeStage.Operational : LifeStage.Construction, Cell = cell, Size = size, Rotation = rotation, Elevation = ground.Elevation, Surface = ground.Surface, Level = math.max(1, level), Crop = -1, Maintained = 1, AutoHarvest = 1, Skin = d.DefaultSkin });
            Sim.Set(em, e, new Health { Current = math.max(1, d.Health), Maximum = math.max(1, d.Health) });
            Sim.Set(em, e, new BuildingStats());
            Sim.Buffer<FoodSelection>(em, e); Sim.Buffer<QuestOfferSlot>(em, e);
            Sim.Buffer<ExpeditionDestinationHistory>(em, e);
            Sim.Buffer<BuildingInvestment>(em, e); Sim.Buffer<RepairMaterial>(em, e);
            BuildingCostOps.RecordInvestment(em, e, BuildingCostOps.DefinitionInvestment(em, root, definition, level, complete ? int.MaxValue : 0));
            var t = em.GetComponentData<LocalTransform>(e); t.Rotation = quaternion.RotateY(rotation * math.PI / 2); em.SetComponentData(e, t);
            ApplyLevel(em, root, e, true);
            GridOps.Occupy(em, root, e);
            if (complete) InventoryOps.Provision(em, root, e);
            return e;
        }
        public static void ApplyLevel(EntityManager em, Entity root, Entity e, bool initialize)
        {
            var b = em.GetComponentData<Building>(e);
            var d = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition);
            var stats = new BuildingStats { HeroDefinition = -1, MovementCost = d.Speed, ActionPower = d.Value };
            var slots = em.GetBuffer<QuestOfferSlot>(e);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Level > 0 && r.Level != b.Level) continue;
                switch (r.Kind)
                {
                    case RuleKind.Workforce: stats.JobCapacity = r.Amount; if (initialize) { b.Workers = b.Stage == LifeStage.Operational ? math.min(r.B, math.max(0, Sim.Population(em, root) - Sim.Employed(em))) : 0; b.Subsidy = (byte)(r.C > 0 ? 1 : 0); b.SubsidyBudget=r.C>0?WorkforceOps.SubsidyCost(r.Amount,r.Value,r.Amount):0; b.StableWorkers = r.B; } break;
                    case RuleKind.Population: stats.BasePopulation += r.Amount; stats.IsCore = (byte)r.B; break;
                    case RuleKind.Residence: stats.MaxPopulation = r.Amount; if (initialize && b.Stage == LifeStage.Operational) b.Population = math.min(r.Amount, math.max(0, r.B)); break;
                    case RuleKind.Warehouse: stats.Capacity += r.Amount; break;
                    case RuleKind.Garrison: stats.Garrison = r.Amount; stats.BatchSize = math.max(1, r.B); break;
                    case RuleKind.Bell: stats.BellRadius = r.Value; break;
                    case RuleKind.Intelligence: stats.Intelligence += r.Amount; break;
                    case RuleKind.Sanctum: stats.HeroDefinition = r.Target; stats.RequiredWorkers = r.Amount; break;
                    case RuleKind.QuestCapacity: stats.QuestCapacity += r.Amount; break;
                    case RuleKind.Provider: stats.IsProvider = 1; break;
                    case RuleKind.Harvest: if (initialize) b.HarvestRemaining = r.Amount; break;
                    case RuleKind.QuestSource:
                        for (var n = 0; n < r.Amount; n++)
                        {
                            var found = false; foreach (var old in slots) if (old.Type == r.B && old.Index == n) { found = true; break; }
                            if (!found) slots.Add(new QuestOfferSlot { Type = r.B, Index = n, NextTurn = 0 });
                        }
                        break;
                }
            }
            b.SubsidyBudget=math.clamp(b.SubsidyBudget,0,stats.JobCapacity); b.Workers = math.min(b.Workers, stats.JobCapacity); b.Population = math.min(b.Population, stats.MaxPopulation);
            em.SetComponentData(e, b); em.SetComponentData(e, stats);
            if (!initialize) QuestOfferOps.Synchronize(em, root, e);
            if (b.Stage == LifeStage.Operational) InventoryOps.Provision(em, root, e);
        }
        public static ResultCode Build(EntityManager em, Entity root, Command c)
        {
            var cell = GridOps.Cell(em.GetComponentData<GridData>(root), c.Position);
            var check = CheckBuild(em, root, c.Definition, cell, c.Argument);
            if (!check.Allowed) return check.Code;
            var d = Sim.Definition(em, root, c.Definition);
            if (!InventoryOps.Pay(em, root, c.Definition, RuleKind.PlacementCost, 1)) return ResultCode.InsufficientResources;
            Create(em, root, c.Definition, cell, c.Argument, 1, d.Duration <= 0);
            Changed(em, root);
            return ResultCode.Success;
        }
        public static void Ruin(EntityManager em, Entity root, Entity e)
        {
            if (!em.Exists(e) || !em.HasComponent<Building>(e)) return;
            var b = em.GetComponentData<Building>(e);
            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing) return;
            var id = em.GetComponentData<Identity>(e);
            var stats = em.GetComponentData<BuildingStats>(e);
            var state = em.GetComponentData<Session>(root);
            if (stats.IsCore != 0)
            {
                state.Phase = Phase.GameOver; state.Paused = 0; em.SetComponentData(root, state);
                Sim.Emit(em, root, EventKind.Message, "聚落核心失守"); return;
            }
            b.Stage = LifeStage.Ruined; b.RuinPending = 1; b.Offering = 0; b.PaidSubsidy = 0; b.PaidSubsidyTurn = 0;
            em.SetComponentData(e, b); GridOps.Occupy(em, root, e);
            // Record the committed-to-be losses now so Tonight's Report can explain them before dawn.
            var report = em.GetBuffer<BattleReportEntry>(root);
            if (b.Population > 0) report.Add(new BattleReportEntry { Kind = EventKind.ResidentsLost, Id = id.Id, Definition = id.Definition, Amount = b.Population, SourceName = id.Name });
            if (b.Workers > 0) report.Add(new BattleReportEntry { Kind = EventKind.JobsLost, Id = id.Id, Definition = id.Definition, Amount = b.Workers, SourceName = id.Name });
            var slots = em.GetBuffer<InventorySlot>(root);
            for (var i = 0; i < slots.Length; i++) if (slots[i].Provider == id.Id) { var slot = slots[i]; slot.Unavailable = 1; slots[i] = slot; if (slot.Count > 0) report.Add(new BattleReportEntry { Kind = EventKind.InventoryLost, Id = id.Id, Definition = slot.Item, Amount = slot.Count, SourceName = id.Name }); }
            using (var soldiers = Sim.Entities<Soldier>(em)) foreach (var soldier in soldiers)
            {
                var s = em.GetComponentData<Soldier>(soldier); if (s.Garrison != id.Id) continue;
                if (em.HasComponent<Combatant>(soldier) && em.GetComponentData<Combatant>(soldier).Deployed == 0)
                { var health = em.GetComponentData<Health>(soldier); health.Current = math.min(health.Current, health.Maximum * .5f); em.SetComponentData(soldier, health); }
            }
            using (var heroes = Sim.Entities<Hero>(em)) foreach (var hero in heroes) if (em.GetComponentData<Hero>(hero).Sanctum == id.Id) MilitaryOps.KillHero(em, root, hero);
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Ruin, Id = id.Id, Definition = id.Definition, Amount = 1, SourceName = id.Name });
            Sim.Emit(em, root, EventKind.Ruin, id.Name, id.Id);
            if (state.Phase == Phase.Day || state.Phase == Phase.Settlement) CommitRuin(em, root, e);
        }
        public static ResultCode Demolish(EntityManager em, Entity root, Entity e)
        {
            if (e == Entity.Null || !em.HasComponent<Building>(e)) return ResultCode.InvalidTarget;
            if (em.GetComponentData<BuildingStats>(e).IsCore != 0) return ResultCode.Unavailable;
            var refunds = BuildingCostOps.DemolitionRefund(em, root, e);
            Ruin(em, root, e); CommitRuin(em, root, e); GridOps.Occupy(em, root, e, true);
            var id = em.GetComponentData<Identity>(e).Id;
            using (var expeditions = Sim.Entities<Expedition>(em)) foreach (var expedition in expeditions) if (em.GetComponentData<Expedition>(expedition).Site == id && em.GetComponentData<Expedition>(expedition).Status == ExpeditionStatus.Travelling) em.DestroyEntity(expedition);
            using (var quests = Sim.Entities<Quest>(em)) foreach (var quest in quests) { var q = em.GetComponentData<Quest>(quest); if (q.Source == id && q.Status == QuestStatus.Offered) em.DestroyEntity(quest); }
            em.DestroyEntity(e);
            foreach (var refund in refunds) InventoryOps.Add(em, root, refund.Item, refund.Amount);
            EconomyOps.ReconcilePopulation(em, root); Changed(em, root); ProgressionOps.ReconcileQuestContainers(em, root);
            return ResultCode.Success;
        }
        public static ResultCode Repair(EntityManager em, Entity root, Entity e)
        {
            if (e == Entity.Null || !em.HasComponent<Building>(e)) return ResultCode.InvalidTarget;
            var b = em.GetComponentData<Building>(e); if (b.Stage != LifeStage.Ruined) return ResultCode.Unavailable;
            if (b.RuinPending != 0) return ResultCode.Busy;
            var costs = BuildingCostOps.RepairTotal(em, root, e, out var duration);
            var materials = em.GetBuffer<RepairMaterial>(e); materials.Clear();
            foreach (var c in costs) materials.Add(new RepairMaterial { Item = c.Item, Amount = c.Amount });
            b.Stage = LifeStage.Repairing; b.Progress = 0; b.RepairDuration = duration;
            em.SetComponentData(e, b); GridOps.Occupy(em, root, e); return ResultCode.Success;
        }
        public static ResultCode Upgrade(EntityManager em, Entity root, Entity e)
        {
            var check = CheckUpgrade(em, root, e); if (!check.Allowed) return check.Code;
            var b = em.GetComponentData<Building>(e); var definition = em.GetComponentData<Identity>(e).Definition;
            if (!BuildingCostOps.Pay(em, root, check.Costs)) return ResultCode.InsufficientResources;
            BuildingCostOps.RecordInvestment(em, e, check.Costs);
            b.Level++; em.SetComponentData(e, b); ApplyLevel(em, root, e, false); GridOps.Occupy(em, root, e);
            MilitaryOps.ReconcileGarrisons(em, root); Changed(em, root); return ResultCode.Success;
        }
    }
}
