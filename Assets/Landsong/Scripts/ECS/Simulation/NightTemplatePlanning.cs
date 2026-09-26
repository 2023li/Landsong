using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS
{
    public static class NightTemplatePlanning
    {
        public static float Difficulty(in NightSettings settings, int turn, int playerPower)
        {
            var expected = math.max(1, settings.ExpectedPowerAtTurnOne + math.max(0, turn - 1) * settings.ExpectedPowerPerTurn);
            var turnScale = 1 + math.max(0, turn - 1) * settings.DifficultyPerTurn;
            var playerScale = math.clamp(1 + (playerPower / expected - 1) * settings.PlayerPowerSensitivity, .75f, 1.25f);
            return math.clamp(turnScale * playerScale, 1, 10);
        }

        public static int Count(float weight, float difficulty, bool fixedCount, int configuredCount)
            => fixedCount ? configuredCount : math.max(0, (int)math.floor(weight * difficulty));

        static int Select(EntityManager em, Entity root, ref Random rng)
        {
            var events = em.GetComponentData<NightEventCatalog>(root).Value;
            int guaranteed = -1, priority = int.MinValue, chosen = -1;
            float totalWeight = 0;
            for (int i = 0; i < events.Value.Events.Length; i++)
            {
                ref var candidate = ref events.Value.Events[i];
                if (!NightPlanOps.Eligible(em, root, ref candidate, false)) continue;
                if (candidate.Forced != 0)
                {
                    if (candidate.Priority == priority)
                        throw new InvalidOperationException("同一晚有多个同优先级的必出夜晚。");
                    if (candidate.Priority > priority)
                    {
                        guaranteed = i;
                        priority = candidate.Priority;
                    }
                    continue;
                }
                totalWeight += candidate.Weight;
                if (rng.NextFloat() * totalWeight < candidate.Weight)
                    chosen = i;
            }
            if (guaranteed >= 0) return guaranteed;
            if (chosen >= 0) return chosen;
            throw new InvalidOperationException("没有符合条件的夜晚，也没有平安夜后备定义。");
        }

        public static void Plan(EntityManager em, Entity root, bool retry)
        {
            var state = NightPlanOps.State(em, root);
            var clock = em.GetComponentData<GameClock>(root);
            if (!retry && state.Turn == clock.Turn) return;
            EntityState.Buffer<NightEventHistory>(em, root);
            EntityState.Buffer<UnresolvedBoss>(em, root);
            EntityState.Buffer<PreparedSoldier>(em, root);
            EntityState.Buffer<PreparedHero>(em, root);
            EntityState.Buffer<PreparedBuildingDefense>(em, root);
            var night = em.GetComponentData<NightRuntimeState>(root);
            if (!retry || night.Seed == 0)
                night.Seed = math.max(1u, SimulationRandom.NextRandom(em, root));
            var rng = new Random(night.Seed);
            var settings = em.GetComponentData<NightSettings>(root);
            var catalog = em.GetComponentData<NightEventCatalog>(root).Value;
            int index = retry ? NightPlanOps.Find(em, root, state.Event) : Select(em, root, ref rng);
            if (index < 0) throw new InvalidOperationException("找不到已锁定的夜晚定义。");
            if (!retry)
            {
                night.Seed = math.max(1u, rng.NextUInt());
                rng = new Random(night.Seed);
            }
            ref var definition = ref catalog.Value.Events[index];
            if (!retry)
            {
                var power = MilitaryStrength.Calculate(em, root);
                state = new NightPlanState
                {
                    Event = definition.Id,
                    Turn = clock.Turn
                };
                state.DifficultyScale = Difficulty(settings, clock.Turn, power);
                night.StartCombatStrength = power;
                em.GetBuffer<PreparedSoldier>(root).Clear();
                em.GetBuffer<PreparedHero>(root).Clear();
                em.GetBuffer<PreparedBuildingDefense>(root).Clear();
            }
            state.CombatElapsed = state.FirstActionAt = 0;
            state.BaseThreat = 0;
            state.ClockStarted = state.Committed = state.AnySpawned = state.BossKilled = state.BossEscaped = 0;
            state.BossDefinition = EnemyId.None;
            night.Kind = definition.Kind;
            night.Duration = settings.TotalNightSeconds;
            night.Speed = 1;
            night.BossReturnTurn = 0;
            var waves = em.GetBuffer<NightWave>(root);
            waves.Clear();
            em.GetBuffer<SpawnRegion>(root).Clear();
            if (definition.Kind != NightKind.Peaceful)
            {
                var space = NightSpawnOps.Generate(em, root, definition.Waves.Length, ref rng);
                var regions = em.GetBuffer<SpawnRegion>(root);
                if (regions.Length == 0)
                    SimulationEvents.Emit(em, root, EventKind.Message, "没有可用入场区域，本夜敌军无法入场");
                for (int i = 0; i < definition.Waves.Length && regions.Length > 0; i++)
                {
                    ref var template = ref definition.Waves[i];
                    float seconds = template.AtSeconds;
                    if (template.JitterSeconds > 0)
                        seconds += rng.NextFloat(-template.JitterSeconds, template.JitterSeconds);
                    seconds = math.clamp(seconds, 0, settings.NightSeconds - .001f);
                    int region = i < regions.Length ? i : rng.NextInt(regions.Length);
                    var spawnRegion = regions[region];
                    var position = spawnRegion.Center + new float3(rng.NextFloat(-.5f, .5f) * spawnRegion.Size.x, 0,
                        rng.NextFloat(-.5f, .5f) * spawnRegion.Size.z);
                    bool legal = NightSpatialOps.SpawnPoint(em, root, region, position, out var point, space);
                    if (legal) position = point;
                    for (int j = 0; j < template.Enemies.Length; j++)
                    {
                        var row = template.Enemies[j];
                        int count = Count(row.Weight, state.DifficultyScale, row.Fixed != 0, row.FixedCount);
                        if (count <= 0) continue;
                        if ((EnemyDefinitions.Get(em, root, row.Definition).Behavior & EnemyBehaviorFlags.Boss) != 0)
                            state.BossDefinition = row.Definition;
                        var target = legal ? NightSpatialOps.Target(em, root, row.Definition, position, position, 0, false) : Entity.Null;
                        for (int remaining = count; remaining > 0; remaining -= 256)
                        {
                            waves = em.GetBuffer<NightWave>(root);
                            waves.Add(new NightWave
                            {
                                WaveIndex = i, At = seconds / settings.NightSeconds,
                                Definition = row.Definition, Count = math.min(256, remaining), PowerScale = 1,
                                Direction = spawnRegion.Direction, Region = region, Position = position,
                                Target = target == Entity.Null ? 0 : em.GetComponentData<Identity>(target).Id,
                                SpatiallyBlocked = (byte)(target == Entity.Null ? 1 : 0)
                            });
                            state.BaseThreat += math.min(256, remaining) * math.max(0, EnemyDefinitions.Get(em, root, row.Definition).NightPower);
                        }
                    }
                }
            }
            night.Threat = state.BaseThreat;
            EntityState.Set(em, root, state);
            em.SetComponentData(root, night);
        }
    }
}
