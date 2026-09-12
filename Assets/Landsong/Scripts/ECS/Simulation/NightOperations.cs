using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class NightOps
    {
        public const float CelebrationSeconds = 10;
        public static int Intelligence(EntityManager em, Entity root)
            => IntelOps.Current(em, root);
        public static void Plan(EntityManager em, Entity root, bool retry) => NightPlanOps.Plan(em, root, retry);

        public static float Delta(Session s, float delta) => delta * (s.NightKind == NightKind.Peaceful && s.NightSpeed == 2 ? 2 : 1);
        public static float Progress(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            if (s.Phase == Phase.Deployment) return 0;
            if (s.Phase != Phase.Night) return 1;
            return math.saturate((s.NightKind == NightKind.Peaceful ? s.PhaseTime : NightPlanOps.State(em, root).CombatElapsed) / math.max(10, s.NightDuration));
        }

        public static ResultCode Begin(EntityManager em, Entity root, bool confirmed = false, ulong token = 0)
            => NightEntryOps.Begin(em, root, confirmed, token);

        public static void Tick(EntityManager em, Entity root, float delta)
        {
            var s = em.GetComponentData<Session>(root);
            if (s.CheckpointPending != 0 || s.Paused != 0 || s.Phase == Phase.Day || s.Phase == Phase.Report || s.Phase == Phase.GameOver || s.Phase == Phase.Ended) return;
            delta = Delta(s, math.max(0, delta)); s.Time += delta; s.PhaseTime += delta; em.SetComponentData(root, s);
            if (s.Phase == Phase.Night || s.Phase == Phase.Retreat) MilitaryOps.TickSoldierOrders(em, root);
            MilitaryOps.TickHeroExperience(em, root);
            if (s.Phase == Phase.Deployment)
            { s.Phase = Phase.Night; s.PhaseTime = 0; s.NightSpeed = 1; em.SetComponentData(root, s); Deploy(em, root); return; }
            if (s.Phase == Phase.Night)
            {
                Deploy(em, root);
                if (s.NightKind == NightKind.Peaceful)
                {
                    PeacefulOps.Tick(em, root, delta);
                    if (s.PhaseTime >= math.max(10, s.NightDuration)) Dawn(em, root);
                    return;
                }
                var plan = NightPlanOps.State(em, root);
                if (plan.ClockStarted == 0 && plan.AnySpawned != 0 && s.Time >= plan.FirstActionAt) plan.ClockStarted = 1;
                if (plan.ClockStarted != 0) plan.CombatElapsed = math.min(s.NightDuration, plan.CombatElapsed + math.min(delta, math.max(0, s.Time - plan.FirstActionAt)));
                Sim.Set(em, root, plan);
                SpawnWaves(em, root); s = em.GetComponentData<Session>(root);
                if (plan.ClockStarted != 0 && plan.CombatElapsed >= s.NightDuration)
                {
                    s.Phase = Phase.Retreat; s.PhaseTime = 0; em.SetComponentData(root, s);
                    using var actors = Sim.Entities<Combatant>(em);
                    foreach (var e in actors)
                    { var a = em.GetComponentData<Combatant>(e); if (a.Faction != 0) em.SetComponentData(e, new UnitOrder { Kind = OrderKind.Recall, Destination = a.Home }); }
                }
                else if (BattleFinished(em, root)) Celebrate(em, root);
            }
            else if (s.Phase == Phase.Retreat && s.PhaseTime >= math.max(0, em.GetComponentData<GameSettings>(root).RetreatSeconds))
            {
                using var actors = Sim.Entities<Combatant>(em);
                foreach (var e in actors)
                {
                    var a = em.GetComponentData<Combatant>(e); if (a.Faction == 0) continue;
                    if (a.IsBoss != 0 && Sim.Alive(em, e))
                    { var plan = NightPlanOps.State(em, root); plan.BossEscaped = 1; Sim.Set(em, root, plan); s.BossEscaped = 1; Sim.Emit(em, root, EventKind.Message, "Boss 已撤离，后续仍可能来袭"); }
                    em.DestroyEntity(e);
                }
                em.SetComponentData(root, s); Celebrate(em, root);
            }
            else if (s.Phase == Phase.Celebration && s.PhaseTime >= CelebrationSeconds)
            { CollectAll(em, root); s = em.GetComponentData<Session>(root); s.Phase = Phase.Report; s.PhaseTime = 0; em.SetComponentData(root, s); }
        }
        static bool BattleFinished(EntityManager em, Entity root)
        {
            foreach (var wave in em.GetBuffer<NightWave>(root)) if (wave.Spawned == 0) return false;
            using var all = Sim.Entities<Combatant>(em);
            foreach (var e in all) if (em.GetComponentData<Combatant>(e).Faction == 1 && Sim.Alive(em, e)) return false;
            return true;
        }
        static void Deploy(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            using var soldiers = Sim.Entities<Soldier>(em);
            foreach (var e in soldiers)
            {
                var a = em.GetComponentData<Combatant>(e); if (a.Deployed != 0 || a.DeployAt > s.Time || !Sim.Alive(em, e) || em.GetComponentData<Soldier>(e).RecallState != 0) continue;
                var site = Sim.Find(em, a.HomeId); var grid = em.GetComponentData<GridData>(root);
                var start = GridOps.Cell(grid, a.Home);
                if (site != Entity.Null) { var b = em.GetComponentData<Building>(site); start = b.Cell + new int2(b.Size.x, 0); }
                if (!NavigationOps.TryNearestOpen(em, root, GridOps.Position(grid, start, new int2(1)), 12, out var exit))
                { a.DeployAt = float.MaxValue; em.SetComponentData(e, a); Sim.Emit(em, root, EventKind.Message, "驻地出口阻塞，士兵留在驻地", a.HomeId); continue; }
                a.Deployed = 1; em.SetComponentData(e, a); em.SetComponentData(e, new VisualState { Visible = 1 });
                var t = em.GetComponentData<LocalTransform>(e); t.Position = exit; em.SetComponentData(e, t);
            }
        }
        static void SpawnWaves(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root); var rules = NightPlanOps.Rules(em, root);
            for (int i = 0; i < em.GetBuffer<NightWave>(root).Length; i++)
            {
                var plan = NightPlanOps.State(em, root); var wave = em.GetBuffer<NightWave>(root)[i];
                if (wave.Spawned != 0 || s.PhaseTime < rules.EntryLeadSeconds) continue;
                // If earlier waves were cancelled there is no running clock yet; try the next entry instead of deadlocking.
                if (plan.AnySpawned != 0 && (plan.ClockStarted == 0 || wave.At * s.NightDuration > plan.CombatElapsed)) continue;
                if (wave.Warned == 0)
                {
                    wave.Warned = 1; wave.WarnedAt = s.Time; NightPlanOps.SetWave(em, root, i, wave);
                    Sim.Emit(em, root, EventKind.Message, wave.Direction == 10 ? "北侧出现入侵动静" : wave.Direction == 20 ? "东侧出现入侵动静" : wave.Direction == 30 ? "南侧出现入侵动静" : "西侧出现入侵动静");
                    break;
                }
                if (s.Time - wave.WarnedAt < rules.WarningSeconds) break;
                var legal = NightSpatialOps.SpawnPoint(em, root, wave.Region, wave.Position, out var point);
                var target = legal ? NightSpatialOps.Target(em, root, wave.Definition, point, point, wave.Target, false) : Entity.Null;
                if (!legal || target == Entity.Null)
                {
                    wave.Spawned = 2; NightPlanOps.SetWave(em, root, i, wave);
                    Sim.Emit(em, root, EventKind.Message, "入侵通路不可用，该股敌军未能入场"); continue;
                }
                wave.Position = point; wave.Target = em.GetComponentData<Identity>(target).Id; wave.Spawned = 1; NightPlanOps.SetWave(em, root, i, wave);
                for (int n = 0; n < wave.Count; n++)
                {
                    // No NearestOpen relocation outside the advertised entry strip.
                    var preferred = point + new float3((n % 5 - 2) * .6f, 0, (n / 5) * .6f);
                    if (!NightSpatialOps.SpawnPoint(em, root, wave.Region, preferred, out var position)) position = point;
                    var e = Sim.Spawn(em, root, wave.Definition, position, false);
                    MilitaryOps.ConfigureCombatant(em, root, e, 1, false, true, wave.Target, position);
                    var actor = em.GetComponentData<Combatant>(e); actor.Target = target; actor.ProtectedUntil = s.Time + rules.ProtectionSeconds;
                    var scale = math.sqrt(math.max(.01f, wave.PowerScale)); actor.Damage *= scale; em.SetComponentData(e, actor);
                    var health = em.GetComponentData<Health>(e); health.Maximum *= scale; health.Current = health.Maximum; em.SetComponentData(e, health);
                }
                if (plan.AnySpawned == 0) plan.FirstActionAt = s.Time + rules.ProtectionSeconds;
                plan.AnySpawned = 1; Sim.Set(em, root, plan);
            }
        }
        public static ResultCode PickUp(EntityManager em, Entity root, Entity e)
        {
            if (e == Entity.Null || !em.Exists(e)) return ResultCode.InvalidTarget;
            if (em.HasComponent<Loot>(e)) { var loot = em.GetComponentData<Loot>(e); NightResultOps.Record(em, root, em.GetComponentData<Identity>(e).Id, 0, RuleKind.RewardItem, loot.Item, loot.Count, loot.SourceName); em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.Reward, Target = em.GetComponentData<Identity>(e).Id, Definition = loot.Item, Amount = loot.Count, Position = Sim.Position(em, e) }); Sim.Emit(em, root, EventKind.Message, "已收取特殊战利品，黎明统一入库。", category: HistoryCategory.Economy); }
            else if (em.HasComponent<Opportunity>(e)) return PeacefulOps.Claim(em, root, e);
            else return ResultCode.InvalidTarget;
            em.DestroyEntity(e); return ResultCode.Success;
        }
        static void CollectAll(EntityManager em, Entity root)
        {
            using var loot = Sim.OrderedEntities<Loot>(em); foreach (var e in loot) PickUp(em, root, e);
        }
        static void Celebrate(EntityManager em, Entity root)
        {
            MilitaryOps.ReportSoldierExperience(em, root);
            MilitaryOps.ReportHeroExperience(em, root);
            var pose = NightReportOps.Prepare(em, root);
            var s = em.GetComponentData<Session>(root); s.Phase = Phase.Celebration; s.PhaseTime = 0; em.SetComponentData(root, s);
            using var all = Sim.Entities<Combatant>(em);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e); if (a.Faction != 0) continue;
                if (!Sim.Alive(em, e)) continue;
                if (Sim.Alive(em, e))
                {
                    var h = em.GetComponentData<Health>(e); h.Current = h.Maximum; em.SetComponentData(e, h);
                    em.SetComponentData(e, new VisualState { Visible = a.Deployed, Celebrating = (byte)pose });
                }
                em.SetComponentData(e, new Steering());
            }
        }
        public static void Dawn(EntityManager em, Entity root)
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase == Phase.Day || phase == Phase.GameOver || phase == Phase.Ended) return;
            NightReportOps.HeroTimes(em, root);
            NightPlanOps.Commit(em, root);
            MilitaryOps.Dawn(em, root);
            BuildingOps.DawnBuildings(em, root);
            CollectAll(em, root);
            NightResultOps.Commit(em, root);
            NightReportOps.Archive(em, root);
            using (var transient = Sim.Entities<NightTransient>(em)) foreach (var e in transient) em.DestroyEntity(e);
            using (var all = Sim.Entities<Combatant>(em)) foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e); a.Deployed = 0; a.Target = Entity.Null; em.SetComponentData(e, a);
                em.SetComponentData(e, new UnitOrder()); em.SetComponentData(e, new Steering()); em.SetComponentData(e, new VisualState());
            }
            var s = em.GetComponentData<Session>(root); s.Turn++; s.Phase = Phase.Day; s.PhaseTime = 0; s.RetryCount = 0; s.SelectedHero = Entity.Null; s.ActiveBell = 0; em.SetComponentData(root, s);
            Plan(em, root, false); ProgressionOps.DiscoverQuests(em, root); ProgressionOps.EvaluateQuests(em, root);
            Sim.Emit(em, root, EventKind.DayCheckpoint, "白天节点");
        }
    }
}
