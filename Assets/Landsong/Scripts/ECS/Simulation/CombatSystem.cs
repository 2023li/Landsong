using Landsong.ECS.Definitions;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public struct TargetSample
    {
        public Entity Entity;
        public float3 Position;
        public byte Faction;
        public ulong Id;
        public int Threat;
        public Entity Target;
        public float Radius;
        public byte BlocksAdvance;
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct PerceptionSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = WorldQueries.Root(em);
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            if (sControl.Paused != 0 || (s.Phase != Phase.Night))
                return;
            NightSpatialOps.RefreshTargets(em, root);
            using var actors = WorldQueries.Entities<Combatant>(em);
            var hash = new NativeParallelMultiHashMap<int2, TargetSample>(math.max(1, actors.Length), Allocator.TempJob);
            var soldiers = new NativeList<TargetSample>(actors.Length, Allocator.TempJob);
            foreach (var e in actors)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Deployed == 0 || !EntityState.Alive(em, e) || sClock.Time < a.ProtectedUntil)
                    continue;
                var position = EntityState.Position(em, e);
                var sample = new TargetSample { Entity = e, Position = position, Faction = a.Faction, Id = em.GetComponentData<Identity>(e).Id, Threat = a.Threat, Target = a.Target, Radius = a.Profile.BodyRadius,
                    BlocksAdvance = (byte)(a.Faction == 0 && em.HasComponent<Soldier>(e) && a.ProjectileSpeed <= 0 ? 1 : 0) };
                hash.Add((int2)math.floor(position.xz / 8), sample);
                if (em.HasComponent<SoldierDefinitionRef>(e)) soldiers.Add(sample);
            }

            state.Dependency = new PerceptionJob
            {
                Units = hash,
                Soldiers = soldiers.AsArray()
            }.ScheduleParallel(state.Dependency);
            state.Dependency = hash.Dispose(state.Dependency);
            state.Dependency = soldiers.Dispose(state.Dependency);
        }

        [BurstCompile]
        partial struct PerceptionJob : IJobEntity
        {
            [ReadOnly]
            public NativeParallelMultiHashMap<int2, TargetSample> Units;
            [ReadOnly] public NativeArray<TargetSample> Soldiers;
            void Execute(Entity entity, ref Perception perception, in LocalTransform transform, in Combatant actor)
            {
                var result = new Perception();
                TargetSample blocker = default;
                float blockerDistance = math.square(math.max(2.4f, actor.Range + .5f));
                if (actor.Faction == 1)
                    foreach (var soldier in Soldiers)
                    {
                        if (soldier.BlocksAdvance == 0) continue;
                        float distance = math.distancesq(transform.Position.xz, soldier.Position.xz);
                        if (distance < blockerDistance || distance == blockerDistance && soldier.Id < blocker.Id)
                        {
                            blocker = soldier;
                            blockerDistance = distance;
                        }
                    }
                var cell = (int2)math.floor(transform.Position.xz / 8);
                var nearest = float.MaxValue;
                float best = float.MaxValue, targetRadius = 0;
                ulong bestId = ulong.MaxValue;
                if ((actor.Profile.Traits & TacticalTraits.NearestSoldier) != 0)
                {
                    foreach (var other in Soldiers)
                    {
                        if (other.Faction == actor.Faction) continue;
                        float distance = math.distancesq(transform.Position, other.Position);
                        if (distance < nearest || distance == nearest && other.Id < bestId)
                        {
                            nearest = distance;
                            bestId = other.Id;
                            targetRadius = other.Radius;
                            result.Enemy = other.Entity;
                            result.EnemyPosition = other.Position;
                        }
                    }
                    result.EnemyInRange = (byte)(nearest <= math.square(actor.Range + targetRadius) ? 1 : 0);
                    if (blocker.Entity != Entity.Null)
                    {
                        result.Enemy = blocker.Entity;
                        result.EnemyPosition = blocker.Position;
                        result.EnemyInRange = (byte)(blockerDistance <= math.square(actor.Range + blocker.Radius) ? 1 : 0);
                    }
                    perception = result;
                    return;
                }
                float radius = actor.Profile.DetectionRadius;
                int extent = (int)math.ceil(radius / 8);
                for (var y = -extent; y <= extent; y++)
                    for (var x = -extent; x <= extent; x++)
                    {
                        if (!Units.TryGetFirstValue(cell + new int2(x, y), out var other, out var iterator))
                            continue;
                        do
                        {
                            if (other.Faction == actor.Faction || (actor.Profile.Traits & TacticalTraits.SiegeOnly) != 0)
                                continue;
                            var distance = math.distancesq(transform.Position, other.Position);
                            if (distance > radius * radius)
                                continue;
                            // Immediate attackers, then threat-adjusted distance; ID settles exact ties.
                            float score = distance / (1 + math.min(100, other.Threat) * .02f) - (other.Target == entity ? radius * radius : 0);
                            if (score < best || score == best && other.Id < bestId)
                            {
                                best = score;
                                bestId = other.Id;
                                nearest = distance;
                                targetRadius = other.Radius;
                                result.Enemy = other.Entity;
                                result.EnemyPosition = other.Position;
                            }
                        }
                        while (Units.TryGetNextValue(out other, ref iterator));
                    }

                result.EnemyInRange = (byte)(nearest <= (actor.Range + targetRadius) * (actor.Range + targetRadius) ? 1 : 0);
                if (blocker.Entity != Entity.Null)
                {
                    result.Enemy = blocker.Entity;
                    result.EnemyPosition = blocker.Position;
                    result.EnemyInRange = (byte)(blockerDistance <= math.square(actor.Range + blocker.Radius) ? 1 : 0);
                }
                if (actor.Faction == 1)
                {
                    result.Building = perception.Building;
                    result.BuildingPosition = perception.BuildingPosition;
                }

                perception = result;
            }
        }
    }

    // Ordered allocation needs current perception. Let unrelated systems run before the
    // EntityManager read completes the producer, instead of completing inside perception.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PerceptionSystem))]
    [UpdateBefore(typeof(CombatResolutionGroup))]
    public partial struct TacticalSlotSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = WorldQueries.Root(em);
            var session = em.GetComponentData<Session>(root);
            SimulationControl sessionControl = em.GetComponentData<SimulationControl>(root);
            if (sessionControl.Paused == 0 && (session.Phase == Phase.Night))
                TacticalOps.Update(em, root);
        }
    }

    public static class CombatOps
    {
        public static void ApplyDamage(EntityManager em, Entity root, DamageRequest damage)
        {
            if (em.GetComponentData<Session>(root).Phase == Phase.GameOver)
                return;
            if (!EntityState.Alive(em, damage.Target) || !math.isfinite(damage.Amount) || !math.isfinite(damage.Penetration) || damage.Amount <= 0)
                return;
            byte faction = em.HasComponent<Combatant>(damage.Target) ? em.GetComponentData<Combatant>(damage.Target).Faction : BuildingFactionOps.Of(em, root, damage.Target);
            if (damage.HasPayload != 0 && !BuildingFactionOps.Hostile(damage.Faction, faction)
                || damage.HasPayload == 0 && em.Exists(damage.Source) && em.HasComponent<Combatant>(damage.Source)
                    && !BuildingFactionOps.Hostile(em.GetComponentData<Combatant>(damage.Source).Faction, faction))
                return;
            if (em.HasComponent<Combatant>(damage.Target) && em.GetComponentData<GameClock>(root).Time < em.GetComponentData<Combatant>(damage.Target).ProtectedUntil)
                return;
            CombatProfile defense = em.HasComponent<Combatant>(damage.Target) ? em.GetComponentData<Combatant>(damage.Target).Profile : BuildingDefenseStats.ForNight(em, root, em.GetComponentData<BuildingDefinitionRef>(damage.Target).Definition);
            float effective = ProjectileOps.Mitigate(damage.Amount, defense.Armor, damage.Penetration, defense.Reduction);
            if (effective <= 0)
                return;
            var h = em.GetComponentData<Health>(damage.Target);
            effective = math.min(effective, h.Current);
            h.Current -= effective;
            em.SetComponentData(damage.Target, h);
            if (em.HasComponent<UnitAnimationSignals>(damage.Target))
            {
                var signal = em.GetComponentData<UnitAnimationSignals>(damage.Target);
                signal.HitSequence++;
                em.SetComponentData(damage.Target, signal);
            }

            var damagedId = em.GetComponentData<Identity>(damage.Target);
            em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.Damage, Target = damagedId.Id, Position = EntityState.Position(em, damage.Target) });
            if (em.Exists(damage.Source) && em.HasComponent<Combatant>(damage.Source))
            {
                var source = em.GetComponentData<Combatant>(damage.Source);
                if (BuildingFactionOps.Hostile(source.Faction, faction))
                {
                    HeroOps.RecordHeroContribution(em, root, damage.Source, effective);
                    HeroOps.RecordHeroContribution(em, root, damage.Target, effective);
                    source.Participated = 1;
                    em.SetComponentData(damage.Source, source);
                    if (em.HasComponent<Combatant>(damage.Target))
                    {
                        var a = em.GetComponentData<Combatant>(damage.Target);
                        a.Participated = 1;
                        em.SetComponentData(damage.Target, a);
                    }
                }
            }

            if (h.Current <= 0)
                Death(em, root, damage.Target, damage.Source);
        }

        public static float Distance(EntityManager em, Entity actor, Entity target)
        {
            var from = EntityState.Position(em, actor);
            var to = EntityState.Position(em, target);
            if (em.HasComponent<Building>(target))
            {
                BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(target);
                var grid = em.GetComponentData<GridData>(WorldQueries.Root(em));
                var half = (float2)bPlacement.Size * grid.CellSize * .5f;
                return math.distance(from.xz, math.clamp(from.xz, to.xz - half, to.xz + half));
            }

            return math.max(0, math.distance(from, to) - (em.HasComponent<Combatant>(target) ? em.GetComponentData<Combatant>(target).Profile.BodyRadius : 0));
        }

        public static void Death(EntityManager em, Entity root, Entity e, Entity source)
        {
            if (em.HasComponent<Firefighter>(e))
            {
                FirefighterOps.Kill(em, root, e);
                return;
            }
            if (em.HasComponent<TransportWorker>(e))
            {
                TransportWorkerOps.Kill(em, root, e);
                return;
            }
            if (em.HasComponent<Building>(e))
            {
                BuildingLifecycle.Ruin(em, root, e);
                return;
            }

            var id = em.GetComponentData<Identity>(e);
            if (em.HasComponent<Hero>(e))
            {
                HeroOps.KillHero(em, root, e);
                return;
            }

            var deathKind = em.HasComponent<Soldier>(e) ? EventKind.SoldierDeath : EventKind.EnemyDeath;
            foreach (var entry in em.GetBuffer<BattleReportEntry>(root))
                if (entry.Kind == deathKind && entry.Id == id.Id)
                    return;
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = deathKind, Id = id.Id, Amount = 1, SourceName = id.Name });
            em.GetBuffer<GameEvent>(root).Add(new GameEvent { Kind = EventKind.Death, Target = id.Id, Position = EntityState.Position(em, e) });
            if (em.HasComponent<Soldier>(e))
                SoldierLifeOps.RememberSoldier(em, root, e, false);
            if (em.HasComponent<Combatant>(e) && em.GetComponentData<Combatant>(e).Faction == 1)
            {
                var actor = em.GetComponentData<Combatant>(e);
                if (actor.IsBoss != 0)
                {
                    NightPlanOps.BossDeath(em, root, em.GetComponentData<EnemyDefinitionRef>(e).Definition);
                    NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
                    sNight.BossEscaped = 0;
                    sNight.BossReturnTurn = 0;
                    {
                        em.SetComponentData(root, sNight);
                    }
                }

                var enemy = em.GetComponentData<EnemyDefinitionRef>(e).Definition;
                ref var d = ref EnemyDefinitions.Get(em, root, enemy);
                NightResultOps.RecordEnemyRewards(em, root, id.Id, enemy, id.Name);
                for (var i = 0; i < d.SpecialDrops.Length; i++)
                {
                    var drop = d.SpecialDrops[i];
                    // A guaranteed drop is recorded even when no legal visual landing exists.
                    if (!NavigationOps.TryNearestOpen(em, root, EntityState.Position(em, e), 12, out var position))
                    {
                        NightResultOps.RecordItem(em, root, id.Id, drop.Order, drop.Item, drop.Quantity, id.Name);
                        continue;
                    }

                    if (LootDefinitions.Count(em, root) == 0)
                        throw new System.InvalidOperationException("No loot display definition.");
                    var loot = LootEntities.Spawn(em, root, LootId.FromIndex(0), position, false);
                    EntityState.Set(em, loot, new Loot { Item = drop.Item, Count = drop.Quantity, Rarity = (int)drop.Rarity, SourceName = id.Name });
                    EntityState.Set(em, loot, new VisualState { Visible = 1 });
                }
            }

            if (em.HasComponent<VisualState>(e))
                em.SetComponentData(e, new VisualState());
        }
    }

    [UpdateInGroup(typeof(CombatResolutionGroup))]
    public partial struct CombatSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = WorldQueries.Root(em);
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            if (sControl.Paused != 0 || (s.Phase != Phase.Night))
                return;
            var projectileDefinition = ProjectileId.FromIndex(0);
            using var all = WorldQueries.Entities<Combatant>(em);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (em.HasComponent<TransportWorker>(e)) continue;
                if (a.Faction == 0 && em.GetComponentData<UnitOrder>(e).Kind == OrderKind.Recall)
                    continue;
                if (a.Deployed == 0 || !EntityState.Alive(em, e) || !EntityState.Alive(em, a.Target) || sClock.Time < a.NextAttack || sClock.Time < a.ProtectedUntil)
                    continue;
                var targetFaction = em.HasComponent<Combatant>(a.Target) ? em.GetComponentData<Combatant>(a.Target).Faction
                    : em.HasComponent<Building>(a.Target) ? BuildingFactionOps.Of(em, root, a.Target) : a.Faction;
                if (!BuildingFactionOps.Hostile(a.Faction, targetFaction))
                    continue;
                if (CombatOps.Distance(em, e, a.Target) > a.Range + .2f)
                    continue;
                var target = a.Target;
                a.NextAttack = sClock.Time + math.max(.05f, a.Interval);
                em.SetComponentData(e, a);
                if (a.ProjectileSpeed <= 0)
                    em.GetBuffer<DamageRequest>(root).Add(new DamageRequest { Source = e, Target = target, Amount = a.Damage, Penetration = a.Profile.Penetration, Faction = a.Faction, HasPayload = 1 });
                else
                {
                    var projectile = ProjectileEntities.Spawn(em, root, projectileDefinition, EntityState.Position(em, e) + new float3(0, .3f, 0), false);
                    EntityState.Set(em, projectile, new Projectile { Source = e, Target = target, Damage = a.Damage, Speed = math.max(.1f, a.ProjectileSpeed), Lifetime = a.Profile.ProjectileLifetime, Faction = a.Faction, Penetration = a.Profile.Penetration, Radius = a.Profile.BlastRadius, Warning = a.Profile.ProjectileMode == ProjectileMode.Ground ? a.Profile.WarningSeconds : 0, Landing = EntityState.Position(em, target) + new float3(0, .3f, 0), Mode = a.Profile.ProjectileMode, SourceId = em.GetComponentData<Identity>(e).Id, TargetId = em.GetComponentData<Identity>(target).Id });
                    EntityState.Set(em, projectile, new VisualState { Visible = 1 });
                }
                if (em.HasComponent<UnitAnimationSignals>(e))
                {
                    var signal = em.GetComponentData<UnitAnimationSignals>(e);
                    signal.AttackSequence++;
                    em.SetComponentData(e, signal);
                }
            }
        }
    }

    [UpdateInGroup(typeof(CombatResolutionGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<Session>();
            var session = SystemAPI.GetComponent<Session>(root);
            SimulationControl sessionControl = SystemAPI.GetComponent<SimulationControl>(root);
            NightRuntimeState sessionNight = SystemAPI.GetComponent<NightRuntimeState>(root);
            if (sessionControl.Paused != 0 || (session.Phase != Phase.Night))
                return;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            using var blocks = ProjectileOps.Blocks(state.EntityManager, root, Allocator.TempJob);
            using var victims = new NativeList<ImpactTarget>(Allocator.TempJob);
            using var health = WorldQueries.OrderedEntities<Health>(state.EntityManager);
            var grid = SystemAPI.GetComponent<GridData>(root);
            foreach (var e in health)
            {
                var em = state.EntityManager;
                if (!EntityState.Alive(em, e) || !em.HasComponent<LocalTransform>(e) || !em.HasComponent<Identity>(e))
                    continue;
                if (!em.HasComponent<Combatant>(e) && !em.HasComponent<Building>(e))
                    continue;
                if (em.HasComponent<Combatant>(e) && em.GetComponentData<Combatant>(e).Deployed == 0)
                    continue;
                var target = new ImpactTarget
                {
                    Entity = e,
                    Id = em.GetComponentData<Identity>(e).Id,
                    Position = EntityState.Position(em, e),
                    Faction = em.HasComponent<Combatant>(e) ? em.GetComponentData<Combatant>(e).Faction : BuildingFactionOps.Of(em, root, e)
                };
                if (em.HasComponent<Building>(e))
                    target.HalfSize = (float2)em.GetComponentData<BuildingPlacementState>(e).Size * grid.CellSize * .5f;
                victims.Add(target);
            }

            state.Dependency = new ProjectileJob
            {
                Positions = SystemAPI.GetComponentLookup<LocalTransform>(true),
                Health = SystemAPI.GetComponentLookup<Health>(true),
                Grid = grid,
                Blocks = blocks,
                Victims = victims.AsArray(),
                Root = root,
                Delta = NightOps.Delta(sessionNight, SystemAPI.Time.DeltaTime),
                Commands = ecb.AsParallelWriter()
            }.ScheduleParallel(state.Dependency);
            state.Dependency.Complete();
            ecb.Playback(state.EntityManager);
            ecb.Dispose();
        }

        [BurstCompile]
        partial struct ProjectileJob : IJobEntity
        {
            [ReadOnly]
            public ComponentLookup<LocalTransform> Positions;
            [ReadOnly]
            public ComponentLookup<Health> Health;
            public Entity Root;
            public float Delta;
            public GridData Grid;
            [ReadOnly]
            public NativeArray<ProjectileBlock> Blocks;
            [ReadOnly]
            public NativeArray<ImpactTarget> Victims;
            public EntityCommandBuffer.ParallelWriter Commands;
            void Execute([EntityIndexInQuery] int index, Entity entity, ref Projectile projectile, in LocalTransform transform)
            {
                projectile.Lifetime -= Delta;
                if (projectile.Mode == ProjectileMode.Tracking && (!Positions.HasComponent(projectile.Target) || !Health.HasComponent(projectile.Target) || Health[projectile.Target].Current <= 0) || projectile.Lifetime <= 0)
                {
                    Commands.DestroyEntity(index, entity);
                    return;
                }

                if (projectile.Warning > 0)
                {
                    projectile.Warning = math.max(0, projectile.Warning - Delta);
                    return;
                }

                var target = projectile.Mode == ProjectileMode.Ground ? projectile.Landing : Positions[projectile.Target].Position + new float3(0, .3f, 0);
                var distance = math.distance(transform.Position, target);
                bool arrived = distance <= projectile.Speed * Delta + .2f;
                var next = arrived ? target : transform.Position + math.normalizesafe(target - transform.Position) * projectile.Speed * Delta;
                if (ProjectileOps.Blocked(Grid, Blocks, transform.Position, next, projectile.SourceId, projectile.TargetId))
                {
                    Commands.DestroyEntity(index, entity);
                    return;
                }

                if (arrived)
                {
                    if (projectile.Radius <= 0 && projectile.Mode == ProjectileMode.Tracking)
                        Commands.AppendToBuffer(index, Root, new DamageRequest { Source = projectile.Source, Target = projectile.Target, Amount = projectile.Damage, Penetration = projectile.Penetration, Faction = projectile.Faction, HasPayload = 1 });
                    else
                        foreach (var victim in Victims)
                        {
                            if (!BuildingFactionOps.Hostile(projectile.Faction, victim.Faction))
                                continue;
                            var point = ProjectileOps.Closest(victim, target);
                            if (math.distance(point.xz, target.xz) > math.max(.2f, projectile.Radius) || ProjectileOps.Blocked(Grid, Blocks, target, point, projectile.TargetId, victim.Id))
                                continue;
                            Commands.AppendToBuffer(index, Root, new DamageRequest { Source = projectile.Source, Target = victim.Entity, Amount = projectile.Damage, Penetration = projectile.Penetration, Faction = projectile.Faction, HasPayload = 1 });
                        }

                    Commands.DestroyEntity(index, entity);
                }
                else
                {
                    var t = transform;
                    t.Position = next;
                    Commands.SetComponent(index, entity, t);
                }
            }
        }
    }

    [UpdateInGroup(typeof(CombatResolutionGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct DamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            var root = WorldQueries.Root(em);
            var session = em.GetComponentData<Session>(root);
            SimulationControl sessionControl = em.GetComponentData<SimulationControl>(root);
            if (sessionControl.Paused != 0)
                return;
            if (session.Phase != Phase.Night)
            {
                em.GetBuffer<DamageRequest>(root).Clear();
                return;
            }

            using var requests = em.GetBuffer<DamageRequest>(root).ToNativeArray(Allocator.Temp);
            em.GetBuffer<DamageRequest>(root).Clear();
            foreach (var damage in requests)
                CombatOps.ApplyDamage(em, root, damage);
        }
    }
}
