using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class NightOps
    {
        public static float ClosureStartsAt(EntityManager em, Entity root)
        {
            var settings = em.GetComponentData<NightSettings>(root);
            return settings.NightPreparationSeconds + settings.NightSeconds;
        }
        public static float WaveAt(EntityManager em, Entity root, float fraction)
        {
            var preparation = em.GetComponentData<NightSettings>(root).NightPreparationSeconds;
            return preparation + fraction * em.GetComponentData<NightSettings>(root).NightSeconds;
        }

        public static float BattleVictoryElapsed(EntityManager em, Entity root)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Celebration)
                return 0;
            return math.max(0, em.GetComponentData<GameClock>(root).PhaseTime - NightPlanOps.State(em, root).CombatElapsed);
        }
        public static bool BattleAdvanceReady(EntityManager em, Entity root)
            => em.GetComponentData<Session>(root).Phase == Phase.Celebration
                && BattleVictoryElapsed(em, root) >= em.GetComponentData<NightSettings>(root).BattleAdvanceAt;
        public static bool CanEndNight(EntityManager em, Entity root)
        {
            var session = em.GetComponentData<Session>(root);
            return BattleAdvanceReady(em, root) || session.Phase == Phase.Night && em.GetComponentData<NightRuntimeState>(root).Kind == NightKind.Peaceful;
        }
        public static ResultCode EndEarly(EntityManager em, Entity root)
        {
            if (!CanEndNight(em, root))
                return ResultCode.WrongPhase;
            if (em.GetComponentData<Session>(root).Phase == Phase.Celebration || em.GetComponentData<NightRuntimeState>(root).Kind == NightKind.Peaceful)
                FinishNight(em, root, true);
            else
                BeginClosure(em, root);
            return ResultCode.Success;
        }

        public static int Intelligence(EntityManager em, Entity root) => IntelOps.Current(em, root);
        public static void Plan(EntityManager em, Entity root, bool retry) => NightPlanOps.Plan(em, root, retry);
        public static float Delta(NightRuntimeState sNight, float delta) => delta * (sNight.Kind == NightKind.Peaceful && sNight.Speed == 2 ? 2 : 1);
        public static float Progress(EntityManager em, Entity root)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            if (s.Phase != Phase.Deployment && s.Phase != Phase.Night && s.Phase != Phase.Retreat && s.Phase != Phase.Celebration)
                return 1;
            return math.saturate(sClock.PhaseTime / math.max(10, sNight.Duration));
        }

        public static ResultCode Begin(EntityManager em, Entity root, bool confirmed = false, ulong token = 0) => NightEntryOps.Begin(em, root, confirmed, token);
        public static void Tick(EntityManager em, Entity root, float delta)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            if (s.Phase == Phase.Day)
            {
                if (sPersistence.CheckpointPending == 0 && sControl.Paused == 0)
                {
                    sClock.DawnRemaining = math.max(0, sClock.DawnRemaining - math.max(0, delta));
                    em.SetComponentData(root, sClock);
                    NightReturnOps.Tick(em, root, math.max(0, delta));
                }
                return;
            }
            if (sPersistence.CheckpointPending != 0 || sControl.Paused != 0 || s.Phase == Phase.Day || s.Phase == Phase.Report || s.Phase == Phase.GameOver || s.Phase == Phase.Ended)
                return;
            delta = Delta(sNight, math.max(0, delta));
            sClock.Time += delta;
            sClock.PhaseTime += delta;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sControl);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            if (s.Phase == Phase.Night)
                UnitOrders.TickSoldierOrders(em, root);
            HeroOps.TickHeroExperience(em, root);
            if (s.Phase == Phase.Deployment)
            {
                Deploy(em, root);
                if (sClock.PhaseTime < em.GetComponentData<NightSettings>(root).NightPreparationSeconds)
                    return;
                s.Phase = Phase.Night;
                sNight.Speed = 1;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                    em.SetComponentData(root, sPersistence);
                }
            }

            if (s.Phase == Phase.Returning)
            {
                // Legacy value: never reintroduce a return or report gate.
                Dawn(em, root);
                return;
            }

            if (s.Phase == Phase.Night)
            {
                Deploy(em, root);
                var plan = NightPlanOps.State(em, root);
                plan.ClockStarted = 1;
                plan.CombatElapsed = math.min(sNight.Duration, sClock.PhaseTime);
                EntityState.Set(em, root, plan);
                if (sClock.PhaseTime >= ClosureStartsAt(em, root))
                {
                    BeginClosure(em, root);
                }
                else if (sNight.Kind == NightKind.Peaceful)
                    PeacefulOps.Tick(em, root, delta);
                else
                {
                    SpawnWaves(em, root);
                    if (BattleFinished(em, root))
                        Celebrate(em, root);
                }
            }

            {
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
                sControl = em.GetComponentData<SimulationControl>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sPersistence = em.GetComponentData<PersistenceGate>(root);
            }

            if (s.Phase == Phase.Celebration)
                TickVictory(em, root);
            if (s.Phase == Phase.Celebration && sClock.PhaseTime >= ClosureStartsAt(em, root))
            {
                BeginClosure(em, root);
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
            }
            if (s.Phase == Phase.Retreat)
            {
                TickClosure(em, root);
                if (sClock.PhaseTime >= sNight.Duration)
                {
                    using var actors = WorldQueries.Entities<Combatant>(em);
                    foreach (var e in actors)
                    {
                        var a = em.GetComponentData<Combatant>(e);
                        if (a.Faction == 0)
                            continue;
                        if (a.IsBoss != 0 && EntityState.Alive(em, e))
                        {
                            var plan = NightPlanOps.State(em, root);
                            plan.BossEscaped = 1;
                            EntityState.Set(em, root, plan);
                            sNight.BossEscaped = 1;
                            SimulationEvents.Emit(em, root, EventKind.Message, "Boss 已撤离，后续仍可能来袭");
                        }

                        em.DestroyEntity(e);
                    }

                    {
                        em.SetComponentData(root, s);
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sControl);
                        em.SetComponentData(root, sNight);
                        em.SetComponentData(root, sPersistence);
                    }

                    FinishNight(em, root);
                }
            }
        }

        static void BeginClosure(EntityManager em, Entity root)
        {
            var session = em.GetComponentData<Session>(root);
            var clock = em.GetComponentData<GameClock>(root);
            var night = em.GetComponentData<NightRuntimeState>(root);
            session.Phase = Phase.Retreat;
            clock.PhaseTime = math.max(clock.PhaseTime, ClosureStartsAt(em, root));
            night.Speed = 1;
            em.SetComponentData(root, session);
            em.SetComponentData(root, clock);
            em.SetComponentData(root, night);
            var waves = em.GetBuffer<NightWave>(root);
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i];
                if (wave.Spawned != 0) continue;
                wave.Spawned = 2;
                waves[i] = wave;
            }
            CollectAll(em, root);
            using (var visitors = WorldQueries.OrderedEntities<Opportunity>(em))
                foreach (var visitor in visitors)
                    PeacefulOps.Finish(em, root, visitor, false, true);
            using (var projectiles = WorldQueries.Entities<Projectile>(em))
                foreach (var projectile in projectiles)
                    em.DestroyEntity(projectile);
            em.GetBuffer<DamageRequest>(root).Clear();
            using var actors = WorldQueries.Entities<Combatant>(em);
            foreach (var e in actors)
            {
                if (em.HasComponent<Firefighter>(e))
                    continue;
                var actor = em.GetComponentData<Combatant>(e);
                actor.Target = Entity.Null;
                em.SetComponentData(e, actor);
                UnitOrders.Order(em, e, default);
                em.SetComponentData(e, new Steering());
                if (actor.Faction != 0 || !EntityState.Alive(em, e)) continue;
                if (em.HasComponent<Perception>(e)) em.SetComponentData(e, new Perception());
                em.SetComponentData(e, new VisualState { Visible = actor.Deployed });
            }
        }

        static void TickClosure(EntityManager em, Entity root)
        {
            var settings = em.GetComponentData<NightSettings>(root);
            var elapsed = em.GetComponentData<GameClock>(root).PhaseTime - ClosureStartsAt(em, root);
            using var actors = WorldQueries.Entities<Combatant>(em);
            foreach (var e in actors)
            {
                if (em.HasComponent<Firefighter>(e))
                    continue;
                var actor = em.GetComponentData<Combatant>(e);
                if (!EntityState.Alive(em, e)) continue;
                if (actor.Faction != 0)
                {
                    if (elapsed >= settings.RetreatDelaySeconds && em.GetComponentData<UnitOrder>(e).Kind != OrderKind.Recall)
                        UnitOrders.Order(em, e, new UnitOrder { Kind = OrderKind.Recall, Destination = actor.Home });
                }
                else if (elapsed >= settings.ClosureCelebrationAt)
                    em.SetComponentData(e, new VisualState { Visible = actor.Deployed, Celebrating = (byte)NightEndPose.Celebrate });
            }
        }

        static void TickVictory(EntityManager em, Entity root)
        {
            var settings = em.GetComponentData<NightSettings>(root);
            var elapsed = BattleVictoryElapsed(em, root);
            var celebrating = elapsed >= settings.BattleCelebrationAt && elapsed < settings.BattleAdvanceAt;
            using var actors = WorldQueries.Entities<Combatant>(em);
            foreach (var e in actors)
            {
                if (em.HasComponent<Firefighter>(e))
                    continue;
                var actor = em.GetComponentData<Combatant>(e);
                if (actor.Faction != 0 || !EntityState.Alive(em, e))
                    continue;
                var visual = em.GetComponentData<VisualState>(e);
                var wasCelebrating = visual.Celebrating != 0;
                visual.Visible = actor.Deployed;
                visual.Celebrating = (byte)(celebrating ? NightEndPose.Celebrate : NightEndPose.None);
                em.SetComponentData(e, visual);
                if (celebrating)
                {
                    em.SetComponentData(e, new Steering());
                }
                else if (wasCelebrating && em.HasComponent<Soldier>(e))
                {
                    UnitOrders.Order(em, e, default);
                    em.SetComponentData(e, new Steering());
                }
            }
        }

        static bool HasLivingEnemy(EntityManager em, Entity root)
        {
            using var all = WorldQueries.Entities<Combatant>(em);
            foreach (var e in all)
                if (em.GetComponentData<Combatant>(e).Faction == 1 && EntityState.Alive(em, e))
                    return true;
            return false;
        }

        static bool BattleFinished(EntityManager em, Entity root)
        {
            foreach (var wave in em.GetBuffer<NightWave>(root))
                if (wave.Spawned == 0)
                    return false;
            return !HasLivingEnemy(em, root);
        }

        static void Deploy(EntityManager em, Entity root)
        {
            GameClock sClock = em.GetComponentData<GameClock>(root);
            using var soldiers = WorldQueries.Entities<Soldier>(em);
            foreach (var e in soldiers)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Deployed != 0 || a.DeployAt > sClock.Time || !EntityState.Alive(em, e) || em.GetComponentData<Soldier>(e).RecallState != 0)
                    continue;
                var site = WorldQueries.Find(em, a.HomeId);
                var grid = em.GetComponentData<GridData>(root);
                var start = GridOps.Cell(grid, a.Home);
                if (site != Entity.Null)
                {
                    BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(site);
                    start = bPlacement.Cell + new int2(bPlacement.Size.x, 0);
                    if (!NavigationOps.TryNearestOpenOnSurface(em, root, GridOps.Position(grid, start, new int2(1)), 12, bPlacement.Surface, bPlacement.Elevation, out var surfaceExit))
                    {
                        a.DeployAt = float.MaxValue;
                        em.SetComponentData(e, a);
                        SimulationEvents.Emit(em, root, EventKind.Message, "驻地所在楼层出口阻塞，士兵留在驻地", a.HomeId);
                        continue;
                    }

                    a.Deployed = 1;
                    em.SetComponentData(e, a);
                    em.SetComponentData(e, new VisualState { Visible = 1 });
                    var surfaceTransform = em.GetComponentData<LocalTransform>(e);
                    surfaceTransform.Position = surfaceExit;
                    em.SetComponentData(e, surfaceTransform);
                    continue;
                }

                if (!NavigationOps.TryNearestOpen(em, root, GridOps.Position(grid, start, new int2(1)), 12, out var exit))
                {
                    a.DeployAt = float.MaxValue;
                    em.SetComponentData(e, a);
                    SimulationEvents.Emit(em, root, EventKind.Message, "驻地出口阻塞，士兵留在驻地", a.HomeId);
                    continue;
                }

                a.Deployed = 1;
                em.SetComponentData(e, a);
                em.SetComponentData(e, new VisualState { Visible = 1 });
                var t = em.GetComponentData<LocalTransform>(e);
                t.Position = exit;
                em.SetComponentData(e, t);
            }
        }

        static void SpawnWaves(EntityManager em, Entity root)
        {
            GameClock sClock = em.GetComponentData<GameClock>(root);
            var rules = NightPlanOps.Rules(em, root);
            NightSpawnOps.Space space = null;
            for (int i = 0; i < em.GetBuffer<NightWave>(root).Length; i++)
            {
                var plan = NightPlanOps.State(em, root);
                var wave = em.GetBuffer<NightWave>(root)[i];
                if (wave.Spawned != 0)
                    continue;
                var previousWaveCleared = plan.AnySpawned != 0 && !HasLivingEnemy(em, root);
                if (sClock.PhaseTime < WaveAt(em, root, wave.At) && !previousWaveCleared)
                    continue;
                if (wave.Warned == 0)
                {
                    wave.Warned = 1;
                    wave.WarnedAt = sClock.Time;
                    NightPlanOps.SetWave(em, root, i, wave);
                    SimulationEvents.Emit(em, root, EventKind.Message, wave.Direction == 10 ? "北侧出现入侵动静" : wave.Direction == 20 ? "东侧出现入侵动静" : wave.Direction == 30 ? "南侧出现入侵动静" : "西侧出现入侵动静");
                    break;
                }

                if (sClock.Time - wave.WarnedAt < rules.WarningSeconds)
                    break;
                space ??= new NightSpawnOps.Space(em, root);
                var legal = NightSpatialOps.SpawnPoint(em, root, wave.Region, wave.Position, out var point, space);
                var target = legal ? NightSpatialOps.Target(em, root, wave.Definition, point, point, wave.Target, false) : Entity.Null;
                if (!legal || target == Entity.Null)
                {
                    wave.Spawned = 2;
                    NightPlanOps.SetWave(em, root, i, wave);
                    SimulationEvents.Emit(em, root, EventKind.Message, "入侵通路不可用，该股敌军未能入场");
                    continue;
                }

                wave.Position = point;
                wave.Target = em.GetComponentData<Identity>(target).Id;
                wave.Spawned = 1;
                NightPlanOps.SetWave(em, root, i, wave);
                for (int n = 0; n < wave.Count; n++)
                {
                    // No NearestOpen relocation outside the advertised entry strip.
                    var preferred = point + new float3((n % 5 - 2) * .6f, 0, (n / 5) * .6f);
                    if (!NightSpatialOps.SpawnPoint(em, root, wave.Region, preferred, out var position, space))
                        position = point;
                    var e = EnemyEntities.Spawn(em, root, wave.Definition, position, false);
                    EnemyCombatants.Configure(em, root, e, true, wave.Target, position);
                    var actor = em.GetComponentData<Combatant>(e);
                    actor.Target = target;
                    actor.ProtectedUntil = sClock.Time + rules.ProtectionSeconds;
                    var scale = math.sqrt(math.max(.01f, wave.PowerScale));
                    actor.Damage *= scale;
                    em.SetComponentData(e, actor);
                    var health = em.GetComponentData<Health>(e);
                    health.Maximum *= scale;
                    health.Current = health.Maximum;
                    em.SetComponentData(e, health);
                }

                if (plan.AnySpawned == 0)
                    plan.FirstActionAt = sClock.Time + rules.ProtectionSeconds;
                plan.AnySpawned = 1;
                EntityState.Set(em, root, plan);
            }
        }

        public static ResultCode PickUp(EntityManager em, Entity root, Entity e)
        {
            if (e == Entity.Null || !em.Exists(e))
                return ResultCode.InvalidTarget;
            if (em.HasComponent<Loot>(e))
            {
                var loot = em.GetComponentData<Loot>(e);
                if (em.HasComponent<WorkerCargoDrop>(e) && em.GetComponentData<Session>(root).Phase == Phase.Day)
                {
                    InventoryOps.Add(em, root, loot.Item, loot.Count, true);
                    SimulationEvents.Emit(em, root, EventKind.Message, "已收回运输工人遗失物资。", category: HistoryCategory.Economy);
                }
                else
                {
                    NightResultOps.RecordItem(em, root, em.GetComponentData<Identity>(e).Id, 0, loot.Item, loot.Count, loot.SourceName);
                    em.GetBuffer<ItemPickupEvent>(root).Add(new ItemPickupEvent { Item = loot.Item, Quantity = loot.Count, Position = EntityState.Position(em, e) });
                    SimulationEvents.Emit(em, root, EventKind.Message, "已收取物资，黎明统一入库。", category: HistoryCategory.Economy);
                }
            }
            else if (em.HasComponent<Opportunity>(e))
                return PeacefulOps.Claim(em, root, e);
            else
                return ResultCode.InvalidTarget;
            em.DestroyEntity(e);
            return ResultCode.Success;
        }

        static void CollectAll(EntityManager em, Entity root)
        {
            using var loot = WorldQueries.OrderedEntities<Loot>(em);
            foreach (var e in loot)
                PickUp(em, root, e);
        }

        static void Celebrate(EntityManager em, Entity root)
        {
            SoldierOps.ReportSoldierExperience(em, root);
            HeroOps.ReportHeroExperience(em, root);
            NightReportOps.Prepare(em, root);
            var s = em.GetComponentData<Session>(root);
            s.Phase = Phase.Celebration;
            em.SetComponentData(root, s);
            using var all = WorldQueries.Entities<Combatant>(em);
            foreach (var e in all)
            {
                if (em.HasComponent<Firefighter>(e))
                    continue;
                var a = em.GetComponentData<Combatant>(e);
                if (a.Faction != 0)
                    continue;
                if (!EntityState.Alive(em, e))
                    continue;
                if (EntityState.Alive(em, e))
                {
                    var h = em.GetComponentData<Health>(e);
                    h.Current = h.Maximum;
                    em.SetComponentData(e, h);
                    em.SetComponentData(e, new VisualState { Visible = a.Deployed });
                }

                em.SetComponentData(e, new Steering());
            }
        }

        static void FinishNight(EntityManager em, Entity root, bool skippedClosure = false)
        {
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            HeroSelection sHeroSelection = em.GetComponentData<HeroSelection>(root);
            BellState sBell = em.GetComponentData<BellState>(root);
            if (s.Phase == Phase.Returning || s.Phase == Phase.Report || s.Phase == Phase.Day)
                return;
            using (var visitors = WorldQueries.OrderedEntities<Opportunity>(em))
                foreach (var visitor in visitors)
                    PeacefulOps.Finish(em, root, visitor, false, true);
            if (sNight.Kind != NightKind.Peaceful && s.Phase != Phase.Celebration)
            {
                SoldierOps.ReportSoldierExperience(em, root);
                HeroOps.ReportHeroExperience(em, root);
                bool prepared = false;
                foreach (var entry in em.GetBuffer<BattleReportEntry>(root))
                    if (entry.Kind == EventKind.NightClosure) prepared = true;
                if (!prepared) NightReportOps.Prepare(em, root);
            }

            CollectAll(em, root);
            using (var projectiles = WorldQueries.Entities<Projectile>(em))
                foreach (var projectile in projectiles)
                    em.DestroyEntity(projectile);
            em.GetBuffer<DamageRequest>(root).Clear();
            {
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sHeroSelection = em.GetComponentData<HeroSelection>(root);
                sBell = em.GetComponentData<BellState>(root);
            }

            s.Phase = Phase.Report;
            sBell.ActiveBell = 0;
            sHeroSelection.SelectedHero = Entity.Null;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sHeroSelection);
                em.SetComponentData(root, sBell);
            }

            Dawn(em, root, skippedClosure);
        }

        public static void Dawn(EntityManager em, Entity root, bool skippedClosure = false)
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase == Phase.Day || phase == Phase.GameOver || phase == Phase.Ended)
                return;
            NightReportOps.HeroTimes(em, root);
            NightPlanOps.Commit(em, root);
            BattleLifecycle.Dawn(em, root);
            BuildingFireOps.EnterDay(em, root);
            BuildingLifecycle.DawnBuildings(em, root);
            CollectAll(em, root);
            NightResultOps.Commit(em, root);
            NightReportOps.Archive(em, root);
            NightReturnOps.Begin(em, root);
            using (var transient = WorldQueries.Entities<NightTransient>(em))
                foreach (var e in transient)
                    em.DestroyEntity(e);
            using (var all = WorldQueries.Entities<Combatant>(em))
                foreach (var e in all)
                {
                    if (em.HasComponent<DayReturnState>(e) || em.HasComponent<Firefighter>(e))
                        continue;
                    var a = em.GetComponentData<Combatant>(e);
                    a.Deployed = 0;
                    a.Target = Entity.Null;
                    em.SetComponentData(e, a);
                    em.SetComponentData(e, new UnitOrder());
                    em.SetComponentData(e, new Steering());
                    em.SetComponentData(e, new VisualState());
                }

            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            RetryState sRetry = em.GetComponentData<RetryState>(root);
            HeroSelection sHeroSelection = em.GetComponentData<HeroSelection>(root);
            BellState sBell = em.GetComponentData<BellState>(root);
            sClock.Turn++;
            sClock.DawnSourceNightTime = skippedClosure ? sClock.PhaseTime : -1;
            sClock.DawnRemaining = em.GetComponentData<NightSettings>(root).DawnSeconds;
            s.Phase = Phase.Day;
            sClock.PhaseTime = 0;
            sRetry.Count = 0;
            sHeroSelection.SelectedHero = Entity.Null;
            sBell.ActiveBell = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sRetry);
                em.SetComponentData(root, sHeroSelection);
                em.SetComponentData(root, sBell);
            }

            SeasonWeatherOps.Dawn(em, root);
            Plan(em, root, false);
            QuestLifecycle.DiscoverQuests(em, root);
            QuestLifecycle.EvaluateQuests(em, root);
            SimulationEvents.Emit(em, root, EventKind.DayCheckpoint, "白天节点");
        }
    }
}
