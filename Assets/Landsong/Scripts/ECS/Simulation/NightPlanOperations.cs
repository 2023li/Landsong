using Landsong.ECS.Definitions;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS
{
    public static class NightPlanOps
    {
        public static int WaveCount(in NightWaveGeneratorSettings generator, int threat, int totalWaves, int cost, bool boss, ref Random rng)
        {
            if (boss)
                return 1;
            if (generator.Kind == NightWaveGeneratorKind.FixedCount)
                return generator.FixedCount;
            return math.clamp((int)(threat / (float)totalWaves / math.max(1, cost) *
                rng.NextFloat(generator.MinimumCountScale, generator.MaximumCountScale)), 1, 256);
        }

        public static float DefaultWaveAt(in NightSettings settings, int index, int count)
        {
            if (index <= 0 || count <= 1)
                return 0;
            var interval = math.min(settings.WaveIntervalSeconds, settings.NightSeconds * .5f / math.max(1, count - 1));
            return index * interval / settings.NightSeconds;
        }

        public static void SetWave(EntityManager em, Entity root, int index, NightWave wave)
        {
            var waves = em.GetBuffer<NightWave>(root);
            waves[index] = wave;
        }

        public static NightPlanState State(EntityManager em, Entity root) => em.HasComponent<NightPlanState>(root) ? em.GetComponentData<NightPlanState>(root) : new NightPlanState();
        public static NightRules Rules(EntityManager em, Entity root) => em.GetComponentData<NightRules>(root);
        public static int Find(EntityManager em, Entity root, FixedString64Bytes id)
        {
            var blob = em.GetComponentData<NightEventCatalog>(root).Value;
            for (var i = 0; i < blob.Value.Events.Length; i++)
                if (blob.Value.Events[i].Id == id)
                    return i;
            return -1;
        }

        public static bool Eligible(EntityManager em, Entity root, ref NightEventDefinition e, bool returning)
        {
            GameClock sClock = em.GetComponentData<GameClock>(root);
            if (sClock.Turn < e.MinTurn || e.MaxTurn > 0 && sClock.Turn > e.MaxTurn || e.Interval > 0 && (sClock.Turn - e.MinTurn) % e.Interval != 0 || e.ReturnOnly != 0 && !returning)
                return false;
            if (em.HasBuffer<NightEventHistory>(root))
                foreach (var h in em.GetBuffer<NightEventHistory>(root))
                    if (h.Event == e.Id && (e.Once != 0 && h.Count > 0 || sClock.Turn <= h.LastTurn + e.Cooldown))
                        return false;
            ref var conditions = ref e.Conditions;
            if (sClock.Turn < conditions.MinimumTurn || !PrerequisiteEvaluation.Satisfied(em, root, ref conditions.Completions))
                return false;
            for (int i = 0; i < conditions.Technologies.Length; i++)
            {
                var requirement = conditions.Technologies[i];
                if (TechnologyProgression.Read(em, root, requirement.Technology).Completions < math.max(1, requirement.Count))
                    return false;
            }

            for (int i = 0; i < conditions.Items.Length; i++)
            {
                var requirement = conditions.Items[i];
                if (InventoryOps.Count(em, root, requirement.Item) < requirement.Quantity)
                    return false;
            }

            for (int i = 0; i < conditions.Buildings.Length; i++)
            {
                var requirement = conditions.Buildings[i];
                int count = 0;
                using var buildings = WorldQueries.OrderedEntities<Building>(em);
                foreach (var building in buildings)
                    if (BuildingStatus.Operational(em, building) && em.GetComponentData<BuildingDefinitionRef>(building).Definition == requirement.Building && em.GetComponentData<Building>(building).Level >= math.max(1, requirement.MinimumLevel))
                        count++;
                if (count < math.max(1, requirement.Count))
                    return false;
            }

            return true;
        }

        static int Select(EntityManager em, Entity root, ref Random rng, out EnemyId boss)
        {
            boss = EnemyId.None;
            GameClock sClock = em.GetComponentData<GameClock>(root);
            var settings = em.GetComponentData<NightSettings>(root);
            var blob = em.GetComponentData<NightEventCatalog>(root).Value;
            // Pending threats are checked again at dawn, never consumed by a forecast or a retry.
            int returnEvent = -1, returnPriority = int.MinValue, due = int.MaxValue;
            foreach (var pending in em.GetBuffer<UnresolvedBoss>(root))
            {
                var at = Find(em, root, pending.Event);
                if (pending.DueTurn > sClock.Turn || at < 0 || !Eligible(em, root, ref blob.Value.Events[at], true))
                    continue;
                var pendingPriority = blob.Value.Events[at].Priority;
                if (returnEvent < 0 || pendingPriority > returnPriority || pendingPriority == returnPriority && (pending.DueTurn < due || pending.DueTurn == due && pending.Definition.Index < boss.Index))
                {
                    returnEvent = at;
                    returnPriority = pendingPriority;
                    due = pending.DueTurn;
                    boss = pending.Definition;
                }
            }

            if (returnEvent >= 0)
                return returnEvent;
            bool battle = sClock.Turn >= settings.FirstInvasion && rng.NextFloat() < settings.InvasionChance;
            bool bossTurn = sClock.Turn >= settings.FirstBoss && (sClock.Turn - settings.FirstBoss) % math.max(1, settings.BossInterval) == 0;
            int chosen = -1, priority = int.MinValue;
            float total = 0;
            for (var pass = 0; pass < 2 && chosen < 0; pass++)
                for (var i = 0; i < blob.Value.Events.Length; i++)
                {
                    ref var e = ref blob.Value.Events[i];
                    if (e.ReturnOnly != 0 || !Eligible(em, root, ref e, false))
                        continue;
                    bool unresolved = false;
                    if (e.Kind == NightKind.Boss)
                        foreach (var pending in em.GetBuffer<UnresolvedBoss>(root))
                            for (int n = 0; n < e.Enemies.Length; n++)
                                if (e.Enemies[n].Definition == pending.Definition)
                                    unresolved = true;
                    if (unresolved)
                        continue; // A normal draw cannot bypass a pending boss's delay or return prerequisites.
                    if (pass == 0 ? e.Forced == 0 && (e.Kind == NightKind.Boss ? !bossTurn : e.Kind == NightKind.Invasion ? !battle : battle || bossTurn) : e.Kind != NightKind.Peaceful)
                        continue;
                    if (e.Priority < priority)
                        continue;
                    if (e.Priority > priority)
                    {
                        priority = e.Priority;
                        total = 0;
                        chosen = -1;
                    }

                    total += e.Weight;
                    if (rng.NextFloat() * total < e.Weight)
                        chosen = i;
                }

            if (chosen < 0)
                throw new InvalidOperationException("No eligible peaceful night fallback.");
            return chosen;
        }

        static EnemyId Enemy(EntityManager em, Entity root, ref NightEventDefinition e, bool boss, ref Random rng)
        {
            var blob = em.GetComponentData<NightEventCatalog>(root).Value;
            float total = 0;
            EnemyId chosen = EnemyId.None;
            for (int i = 0; i < e.Enemies.Length; i++)
            {
                var p = e.Enemies[i];
                if (((EnemyDefinitions.Get(em, root, p.Definition).Behavior & EnemyBehaviorFlags.Boss) != 0) != boss)
                    continue;
                total += p.Weight;
                if (rng.NextFloat() * total < p.Weight)
                    chosen = p.Definition;
            }

            return chosen;
        }

        public static void Plan(EntityManager em, Entity root, bool retry)
        {
            var state = State(em, root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            RetryState sRetry = em.GetComponentData<RetryState>(root);
            SimulationRandomState sRandom = em.GetComponentData<SimulationRandomState>(root);
            if (!retry && state.Turn == sClock.Turn)
                return;
            EntityState.Buffer<NightEventHistory>(em, root);
            EntityState.Buffer<UnresolvedBoss>(em, root);
            EntityState.Buffer<PreparedSoldier>(em, root);
            EntityState.Buffer<PreparedHero>(em, root);
            EntityState.Buffer<PreparedBuildingDefense>(em, root);
            sNight.Seed = math.max(1u, SimulationRandom.NextRandom(em, root));
            sRandom.State = em.GetComponentData<SimulationRandomState>(root).State;
            var rng = new Random(sNight.Seed);
            var settings = em.GetComponentData<NightSettings>(root);
            var blob = em.GetComponentData<NightEventCatalog>(root).Value;
            int index = retry ? Find(em, root, state.Event) : Select(em, root, ref rng, out _);
            if (index < 0)
                throw new InvalidOperationException("Missing locked night event.");
            ref var e = ref blob.Value.Events[index];
            if (!retry)
            {
                sNight.StartCombatStrength = MilitaryStrength.Calculate(em, root);
                float raw = sClock.Turn * settings.ThreatPerTurn + sNight.StartCombatStrength * settings.StrengthRatio;
                var rules = Rules(em, root);
                raw = math.min(raw, rules.ThreatFloor + sNight.StartCombatStrength * rules.ThreatPerStrengthCap);
                state = new NightPlanState
                {
                    Turn = sClock.Turn,
                    Event = e.Id,
                    BaseThreat = math.max(1, (int)(raw * e.BudgetScale)),
                    BossDefinition = EnemyId.None
                };
                if (e.Kind == NightKind.Boss)
                {
                    foreach (var pending in em.GetBuffer<UnresolvedBoss>(root))
                        if (pending.Event == e.Id && pending.DueTurn <= sClock.Turn)
                        {
                            state.BossDefinition = pending.Definition;
                            break;
                        }

                    if (!state.BossDefinition.IsValid)
                        state.BossDefinition = Enemy(em, root, ref e, true, ref rng);
                }

                sNight.Kind = e.Kind;
                sNight.Duration = settings.TotalNightSeconds;
                em.GetBuffer<PreparedSoldier>(root).Clear();
                em.GetBuffer<PreparedHero>(root).Clear();
                em.GetBuffer<PreparedBuildingDefense>(root).Clear();
            }

            state.CombatElapsed = 0;
            state.FirstActionAt = 0;
            state.ClockStarted = state.Committed = state.AnySpawned = state.BossKilled = state.BossEscaped = 0;
            sNight.Threat = math.max(1, (int)(state.BaseThreat * (1 - math.min(settings.RetryCap, sRetry.Count * settings.RetryStep))));
            sNight.Speed = 1;
            {
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sRetry);
                em.SetComponentData(root, sRandom);
            } // Target hashing uses this plan's seed, including day/dusk retry.

            var waves = em.GetBuffer<NightWave>(root);
            waves.Clear();
            em.GetBuffer<SpawnRegion>(root).Clear();
            if (sNight.Kind != NightKind.Peaceful)
            {
                var space = NightSpawnOps.Generate(em, root, e.WaveCount, ref rng);
                var regions = em.GetBuffer<SpawnRegion>(root);
                if (regions.Length == 0)
                    SimulationEvents.Emit(em, root, EventKind.Message, "没有可用入场区域，本夜敌军无法入场");
                for (int i = 0; i < e.WaveCount && regions.Length > 0; i++)
                {
                    var d = sNight.Kind == NightKind.Boss && i == e.WaveCount - 1 ? state.BossDefinition : Enemy(em, root, ref e, false, ref rng);
                    if (!d.IsValid)
                        continue;
                    int region = i < regions.Length ? i : rng.NextInt(regions.Length);
                    var r = regions[region];
                    var position = r.Center + new float3(rng.NextFloat(-.5f, .5f) * r.Size.x, 0, rng.NextFloat(-.5f, .5f) * r.Size.z);
                    bool legal = NightSpatialOps.SpawnPoint(em, root, region, position, out var point, space);
                    if (legal)
                        position = point;
                    var cost = math.max(1, EnemyDefinitions.Get(em, root, d).ThreatValue);
                    int count = WaveCount(e.WaveGenerator, sNight.Threat, e.WaveCount, cost,
                        (EnemyDefinitions.Get(em, root, d).Behavior & EnemyBehaviorFlags.Boss) != 0, ref rng);
                    var target = legal ? NightSpatialOps.Target(em, root, d, position, position, 0, false) : Entity.Null;
                    // Persist this night's selected region and wave together; day edits repair blocked regions before dusk.
                    waves = em.GetBuffer<NightWave>(root);
                    waves.Add(new NightWave { At = e.WaveTimes.Length == 0 ? DefaultWaveAt(settings, i, e.WaveCount) : e.WaveTimes[i], Definition = d, Count = count, Direction = r.Direction, Region = region, Position = position, Target = target == Entity.Null ? 0 : em.GetComponentData<Identity>(target).Id, SpatiallyBlocked = (byte)(target == Entity.Null ? 1 : 0) });
                }

                float power = 0;
                foreach (var w in waves)
                    power += w.Count * math.max(1, EnemyDefinitions.Get(em, root, w.Definition).ThreatValue);
                for (int i = 0; i < waves.Length; i++)
                {
                    var w = waves[i];
                    w.PowerScale = sNight.Threat / math.max(1, power);
                    waves[i] = w;
                }
            }

            EntityState.Set(em, root, state);
            {
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sRetry);
                em.SetComponentData(root, sRandom);
            }
        }

        public static void Prepare(EntityManager em, Entity root)
        {
            var state = State(em, root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            if (state.PreparedTurn != sClock.Turn)
            {
                EntityState.Buffer<PreparedSoldier>(em, root);
                EntityState.Buffer<PreparedHero>(em, root);
                EntityState.Buffer<PreparedBuildingDefense>(em, root);
                em.GetBuffer<PreparedSoldier>(root).Clear();
                em.GetBuffer<PreparedHero>(root).Clear();
                em.GetBuffer<PreparedBuildingDefense>(root).Clear();
                for (int i = 0; i < SoldierDefinitions.Count(em, root); i++)
                {
                    var id = SoldierId.FromIndex(i);
                    em.GetBuffer<PreparedSoldier>(root).Add(new PreparedSoldier { Definition = id, Stats = SoldierCombatStats.Current(em, root, id) });
                }

                for (int i = 0; i < HeroDefinitions.Count(em, root); i++)
                {
                    var id = HeroId.FromIndex(i);
                    em.GetBuffer<PreparedHero>(root).Add(new PreparedHero { Definition = id, Stats = HeroCombatStats.Current(em, root, id) });
                }

                for (int i = 0; i < BuildingDefinitions.Count(em, root); i++)
                {
                    var id = BuildingId.FromIndex(i);
                    em.GetBuffer<PreparedBuildingDefense>(root).Add(new PreparedBuildingDefense { Definition = id, Profile = BuildingDefenseStats.Current(em, root, id) });
                }

                state.PreparedTurn = sClock.Turn;
                EntityState.Set(em, root, state);
            }

            Reproject(em, root);
        }

        public static void Reproject(EntityManager em, Entity root)
        {
            if (em.GetBuffer<NightWave>(root).Length == 0)
                return;
            var space = new NightSpawnOps.Space(em, root);
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase == Phase.Day || phase == Phase.Settlement)
                NightSpawnOps.Repair(em, root, space);
            for (int i = 0; i < em.GetBuffer<NightWave>(root).Length; i++)
            {
                var w = em.GetBuffer<NightWave>(root)[i];
                w.SpatiallyBlocked = 1;
                w.Target = 0;
                if (w.Region >= 0 && w.Region < em.GetBuffer<SpawnRegion>(root).Length)
                    w.Direction = em.GetBuffer<SpawnRegion>(root)[w.Region].Direction;
                if (NightSpatialOps.SpawnPoint(em, root, w.Region, w.Position, out var point, space))
                {
                    w.Position = point;
                    var target = NightSpatialOps.Target(em, root, w.Definition, point, point, 0, false);
                    w.Target = target == Entity.Null ? 0 : em.GetComponentData<Identity>(target).Id;
                    w.SpatiallyBlocked = (byte)(target == Entity.Null ? 1 : 0);
                }

                NightPlanOps.SetWave(em, root, i, w);
            }
        }

        public static void BossDeath(EntityManager em, Entity root, EnemyId definition)
        {
            var state = State(em, root);
            if (state.BossDefinition == definition)
            {
                state.BossKilled = 1;
                EntityState.Set(em, root, state);
            }
        }

        public static void Commit(EntityManager em, Entity root)
        {
            var p = State(em, root);
            if (p.Committed != 0 || p.Turn == 0)
                return;
            var at = Find(em, root, p.Event);
            if (at < 0)
                return;
            ref var e = ref em.GetComponentData<NightEventCatalog>(root).Value.Value.Events[at];
            p.Committed = 1;
            EntityState.Set(em, root, p);
            if (e.Kind != NightKind.Peaceful && p.AnySpawned == 0)
                return;
            var history = em.GetBuffer<NightEventHistory>(root);
            int found = -1;
            for (int i = 0; i < history.Length; i++)
                if (history[i].Event == e.Id)
                    found = i;
            var h = found < 0 ? new NightEventHistory
            {
                Event = e.Id
            }

            : history[found];
            h.LastTurn = p.Turn;
            h.Count++;
            if (found < 0)
                history.Add(h);
            else
                history[found] = h;
            var pending = em.GetBuffer<UnresolvedBoss>(root);
            if (p.BossKilled != 0 || p.BossEscaped != 0)
                for (int i = pending.Length - 1; i >= 0; i--)
                    if (pending[i].Definition == p.BossDefinition)
                        pending.RemoveAt(i);
            if (e.Kind == NightKind.Boss && p.BossKilled == 0 && !e.FollowUp.IsEmpty)
            {
                for (int i = pending.Length - 1; i >= 0; i--)
                    if (pending[i].Definition == p.BossDefinition)
                        pending.RemoveAt(i);
                pending.Add(new UnresolvedBoss { Event = e.FollowUp, Definition = p.BossDefinition, DueTurn = p.Turn + e.ReturnDelay });
            }

            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            sNight.BossReturnTurn = 0;
            foreach (var b in pending)
                if (sNight.BossReturnTurn == 0 || b.DueTurn < sNight.BossReturnTurn)
                    sNight.BossReturnTurn = b.DueTurn;
            {
                em.SetComponentData(root, sNight);
            }
        }
    }
}
