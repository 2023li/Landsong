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
    }
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GameLoopSystem))]
    public partial struct PerceptionSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; var root = Sim.Root(em); var s = em.GetComponentData<Session>(root);
            if (s.Paused != 0 || (s.Phase != Phase.Night && s.Phase != Phase.Retreat)) return;
            NightSpatialOps.RefreshTargets(em, root);
            using var actors = Sim.Entities<Combatant>(em);
            var hash = new NativeParallelMultiHashMap<int2, TargetSample>(math.max(1, actors.Length), Allocator.TempJob);
            foreach (var e in actors)
            {
                var a = em.GetComponentData<Combatant>(e); if (a.Deployed == 0 || !Sim.Alive(em, e) || s.Time < a.ProtectedUntil) continue;
                var position = Sim.Position(em, e);
                hash.Add((int2)math.floor(position.xz / 8), new TargetSample { Entity = e, Position = position, Faction = a.Faction, Id = em.GetComponentData<Identity>(e).Id, Threat = a.Threat, Target = a.Target, Radius = a.Profile.BodyRadius });
            }
            state.Dependency = new PerceptionJob { Units = hash }.ScheduleParallel(state.Dependency);
            state.Dependency = hash.Dispose(state.Dependency);
            state.Dependency.Complete(); TacticalOps.Update(em, root);
        }
        [BurstCompile] partial struct PerceptionJob : IJobEntity
        {
            [ReadOnly] public NativeParallelMultiHashMap<int2, TargetSample> Units;
            void Execute(Entity entity, ref Perception perception, in LocalTransform transform, in Combatant actor)
            {
                var result = new Perception(); var cell = (int2)math.floor(transform.Position.xz / 8); var nearest = float.MaxValue; float best = float.MaxValue, targetRadius = 0; ulong bestId = ulong.MaxValue;
                float radius = actor.Profile.DetectionRadius; int extent = (int)math.ceil(radius / 8);
                for (var y = -extent; y <= extent; y++) for (var x = -extent; x <= extent; x++)
                {
                    if (!Units.TryGetFirstValue(cell + new int2(x, y), out var other, out var iterator)) continue;
                    do
                    {
                        if (other.Faction == actor.Faction || (actor.Profile.Traits & TacticalTraits.SiegeOnly) != 0) continue;
                        var distance = math.distancesq(transform.Position, other.Position);
                        if (distance > radius * radius) continue;
                        // Immediate attackers, then threat-adjusted distance; ID settles exact ties.
                        float score = distance / (1 + math.min(100, other.Threat) * .02f) - (other.Target == entity ? radius * radius : 0);
                        if (score < best || score == best && other.Id < bestId) { best = score; bestId = other.Id; nearest = distance; targetRadius = other.Radius; result.Enemy = other.Entity; result.EnemyPosition = other.Position; }
                    } while (Units.TryGetNextValue(out other, ref iterator));
                }
                result.EnemyInRange = (byte)(nearest <= (actor.Range + targetRadius) * (actor.Range + targetRadius) ? 1 : 0);
                if (actor.Faction == 1) { result.Building = perception.Building; result.BuildingPosition = perception.BuildingPosition; }
                perception = result;
            }
        }
    }

    public static class CombatOps
    {
        public static void ApplyDamage(EntityManager em, Entity root, DamageRequest damage)
        {
            if (em.GetComponentData<Session>(root).Phase == Phase.GameOver) return;
            if (!Sim.Alive(em, damage.Target) || !math.isfinite(damage.Amount) || !math.isfinite(damage.Penetration) || damage.Amount <= 0) return;
            byte faction = em.HasComponent<Combatant>(damage.Target) ? em.GetComponentData<Combatant>(damage.Target).Faction : (byte)0;
            if (damage.HasPayload != 0 && damage.Faction == faction || damage.HasPayload == 0 && em.Exists(damage.Source) && em.HasComponent<Combatant>(damage.Source) && em.GetComponentData<Combatant>(damage.Source).Faction == faction) return;
            if (em.HasComponent<Combatant>(damage.Target) && em.GetComponentData<Session>(root).Time < em.GetComponentData<Combatant>(damage.Target).ProtectedUntil) return;
            CombatProfile defense = em.HasComponent<Combatant>(damage.Target) ? em.GetComponentData<Combatant>(damage.Target).Profile : MilitaryOps.Stats(em, root, em.GetComponentData<Identity>(damage.Target).Definition, false).Combat;
            float effective = ProjectileOps.Mitigate(damage.Amount, defense.Armor, damage.Penetration, defense.Reduction); if (effective <= 0) return;
            var h = em.GetComponentData<Health>(damage.Target); effective = math.min(effective, h.Current); h.Current -= effective; em.SetComponentData(damage.Target, h);
            var damagedId=em.GetComponentData<Identity>(damage.Target);em.GetBuffer<GameEvent>(root).Add(new GameEvent{Kind=EventKind.Damage,Target=damagedId.Id,Definition=damagedId.Definition,Position=Sim.Position(em,damage.Target)});
            if (em.Exists(damage.Source) && em.HasComponent<Combatant>(damage.Source))
            {
                var source = em.GetComponentData<Combatant>(damage.Source);
                var targetFaction = em.HasComponent<Combatant>(damage.Target) ? em.GetComponentData<Combatant>(damage.Target).Faction : 0;
                if (source.Faction != targetFaction)
                {
                    MilitaryOps.RecordHeroContribution(em, root, damage.Source, effective);
                    MilitaryOps.RecordHeroContribution(em, root, damage.Target, effective);
                    source.Participated = 1; em.SetComponentData(damage.Source, source);
                    if (em.HasComponent<Combatant>(damage.Target)) { var a = em.GetComponentData<Combatant>(damage.Target); a.Participated = 1; em.SetComponentData(damage.Target, a); }
                }
            }
            if (h.Current <= 0) Death(em, root, damage.Target, damage.Source);
        }
        public static float Distance(EntityManager em, Entity actor, Entity target)
        {
            var from = Sim.Position(em, actor); var to = Sim.Position(em, target);
            if (em.HasComponent<Building>(target))
            {
                var b = em.GetComponentData<Building>(target); var grid = em.GetComponentData<GridData>(Sim.Root(em));
                var half = (float2)b.Size * grid.CellSize * .5f;
                return math.distance(from.xz, math.clamp(from.xz, to.xz - half, to.xz + half));
            }
            return math.max(0, math.distance(from, to) - (em.HasComponent<Combatant>(target) ? em.GetComponentData<Combatant>(target).Profile.BodyRadius : 0));
        }
        public static void Death(EntityManager em, Entity root, Entity e, Entity source)
        {
            if (em.HasComponent<Building>(e)) { BuildingOps.Ruin(em, root, e); return; }
            var id = em.GetComponentData<Identity>(e);
            if (em.HasComponent<Hero>(e)) { MilitaryOps.KillHero(em, root, e); return; }
            foreach (var entry in em.GetBuffer<BattleReportEntry>(root)) if (entry.Kind == EventKind.Death && entry.Id == id.Id) return;
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Death, Id = id.Id, Definition = id.Definition, Amount = 1, SourceName = id.Name });
            em.GetBuffer<GameEvent>(root).Add(new GameEvent{Kind=EventKind.Death,Target=id.Id,Definition=id.Definition,Position=Sim.Position(em,e)});
            if(em.HasComponent<Soldier>(e))MilitaryOps.RememberSoldier(em,root,e,false);
            if (em.HasComponent<Combatant>(e) && em.GetComponentData<Combatant>(e).Faction == 1)
            {
                var actor = em.GetComponentData<Combatant>(e);
                if (actor.IsBoss != 0) { NightPlanOps.BossDeath(em, root, id.Definition); var s = em.GetComponentData<Session>(root); s.BossEscaped = 0; s.BossReturnTurn = 0; em.SetComponentData(root, s); }
                var d = Sim.Definition(em, root, id.Definition);
                for (var i = 0; i < d.RuleCount; i++)
                {
                    var rule = Sim.GetRule(em, root, d.RuleStart + i);
                    if (rule.Kind == RuleKind.RewardItem || rule.Kind == RuleKind.RewardBlueprint || rule.Kind == RuleKind.RewardBuff || rule.Kind == RuleKind.RewardFeature) { NightResultOps.Record(em, root, id.Id, i, rule.Kind, rule.Target, rule.Amount, id.Name); continue; }
                    if (rule.Kind != RuleKind.SpecialDrop || rule.Target < 0) continue;
                    // A rare reward remains guaranteed even if the corpse has no legal visual landing.
                    if (!NavigationOps.TryNearestOpen(em, root, Sim.Position(em, e), 12, out var position)) { NightResultOps.Record(em, root, id.Id, i, RuleKind.RewardItem, rule.Target, rule.Amount, id.Name); continue; }
                    var lootDefinition = Sim.FirstDefinition(em, root, ContentKind.Loot);
                    var loot = Sim.Spawn(em, root, lootDefinition, position, false);
                    Sim.Set(em, loot, new Loot { Item = rule.Target, Count = rule.Amount, Rarity = rule.B, SourceName = id.Name }); Sim.Set(em, loot, new VisualState { Visible = 1 });
                }
            }
            if (em.HasComponent<VisualState>(e)) em.SetComponentData(e, new VisualState());
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(PerceptionSystem))]
    [UpdateAfter(typeof(Opsive.BehaviorDesigner.Runtime.Groups.BehaviorTreeSystemGroup))]
    public partial struct CombatSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; var root = Sim.Root(em); var s = em.GetComponentData<Session>(root);
            if (s.Paused != 0 || (s.Phase != Phase.Night && s.Phase != Phase.Retreat)) return;
            var projectileDefinition = Sim.FirstDefinition(em, root, ContentKind.Projectile);
            using var all = Sim.Entities<Combatant>(em);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Faction == 0 && em.GetComponentData<UnitOrder>(e).Kind == OrderKind.Recall) continue;
                if (a.Deployed == 0 || !Sim.Alive(em, e) || !Sim.Alive(em, a.Target) || s.Time < a.NextAttack || s.Time < a.ProtectedUntil || (s.Phase == Phase.Retreat && a.Faction == 1)) continue;
                if (CombatOps.Distance(em, e, a.Target) > a.Range + .2f) continue;
                var target = a.Target;
                a.NextAttack = s.Time + math.max(.05f, a.Interval); em.SetComponentData(e, a);
                var projectile = Sim.Spawn(em, root, projectileDefinition, Sim.Position(em, e) + new float3(0, .3f, 0), false);
                Sim.Set(em, projectile, new Projectile { Source = e, Target = target, Damage = a.Damage, Speed = math.max(.1f, a.ProjectileSpeed), Lifetime = a.Profile.ProjectileLifetime, Faction = a.Faction, Penetration = a.Profile.Penetration, Radius = a.Profile.BlastRadius, Warning = a.Profile.ProjectileMode == ProjectileMode.Ground ? a.Profile.WarningSeconds : 0, Landing = Sim.Position(em, target) + new float3(0, .3f, 0), Mode = a.Profile.ProjectileMode, SourceId = em.GetComponentData<Identity>(e).Id, TargetId = em.GetComponentData<Identity>(target).Id });
                Sim.Set(em, projectile, new VisualState { Visible = 1 });
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatSystem))]
    public partial struct ProjectileSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var root = SystemAPI.GetSingletonEntity<Session>(); var session = SystemAPI.GetComponent<Session>(root);
            if (session.Paused != 0 || (session.Phase != Phase.Night && session.Phase != Phase.Retreat)) return;
            var ecb = new EntityCommandBuffer(Allocator.TempJob);
            using var blocks = ProjectileOps.Blocks(state.EntityManager, root, Allocator.TempJob);
            using var victims = new NativeList<ImpactTarget>(Allocator.TempJob); using var health = Sim.OrderedEntities<Health>(state.EntityManager);
            var grid = SystemAPI.GetComponent<GridData>(root);
            foreach (var e in health)
            {
                var em = state.EntityManager; if (!Sim.Alive(em, e) || !em.HasComponent<LocalTransform>(e) || !em.HasComponent<Identity>(e)) continue;
                if (!em.HasComponent<Combatant>(e) && !em.HasComponent<Building>(e)) continue;
                if (em.HasComponent<Combatant>(e) && em.GetComponentData<Combatant>(e).Deployed == 0) continue;
                var target = new ImpactTarget { Entity = e, Id = em.GetComponentData<Identity>(e).Id, Position = Sim.Position(em, e), Faction = em.HasComponent<Combatant>(e) ? em.GetComponentData<Combatant>(e).Faction : (byte)0 };
                if (em.HasComponent<Building>(e)) target.HalfSize = (float2)em.GetComponentData<Building>(e).Size * grid.CellSize * .5f; victims.Add(target);
            }
            state.Dependency = new ProjectileJob { Positions = SystemAPI.GetComponentLookup<LocalTransform>(true), Health = SystemAPI.GetComponentLookup<Health>(true), Grid = grid, Blocks = blocks, Victims = victims.AsArray(), Root = root, Delta = NightOps.Delta(session, SystemAPI.Time.DeltaTime), Commands = ecb.AsParallelWriter() }.ScheduleParallel(state.Dependency);
            state.Dependency.Complete(); ecb.Playback(state.EntityManager); ecb.Dispose();
        }
        [BurstCompile] partial struct ProjectileJob : IJobEntity
        {
            [ReadOnly] public ComponentLookup<LocalTransform> Positions;
            [ReadOnly] public ComponentLookup<Health> Health;
            public Entity Root;
            public float Delta;
            public GridData Grid;
            [ReadOnly] public NativeArray<ProjectileBlock> Blocks;
            [ReadOnly] public NativeArray<ImpactTarget> Victims;
            public EntityCommandBuffer.ParallelWriter Commands;
            void Execute([EntityIndexInQuery] int index, Entity entity, ref Projectile projectile, in LocalTransform transform)
            {
                projectile.Lifetime -= Delta;
                if (projectile.Mode == ProjectileMode.Tracking && (!Positions.HasComponent(projectile.Target) || !Health.HasComponent(projectile.Target) || Health[projectile.Target].Current <= 0) || projectile.Lifetime <= 0)
                { Commands.DestroyEntity(index, entity); return; }
                if (projectile.Warning > 0) { projectile.Warning = math.max(0, projectile.Warning - Delta); return; }
                var target = projectile.Mode == ProjectileMode.Ground ? projectile.Landing : Positions[projectile.Target].Position + new float3(0, .3f, 0); var distance = math.distance(transform.Position, target);
                bool arrived = distance <= projectile.Speed * Delta + .2f;
                var next = arrived ? target : transform.Position + math.normalizesafe(target - transform.Position) * projectile.Speed * Delta;
                if (ProjectileOps.Blocked(Grid, Blocks, transform.Position, next, projectile.SourceId, projectile.TargetId)) { Commands.DestroyEntity(index, entity); return; }
                if (arrived)
                {
                    if (projectile.Radius <= 0 && projectile.Mode == ProjectileMode.Tracking)
                        Commands.AppendToBuffer(index, Root, new DamageRequest { Source = projectile.Source, Target = projectile.Target, Amount = projectile.Damage, Penetration = projectile.Penetration, Faction = projectile.Faction, HasPayload = 1 });
                    else foreach (var victim in Victims)
                    {
                        if (victim.Faction == projectile.Faction) continue; var point = ProjectileOps.Closest(victim, target);
                        if (math.distance(point.xz, target.xz) > math.max(.2f, projectile.Radius) || ProjectileOps.Blocked(Grid, Blocks, target, point, projectile.TargetId, victim.Id)) continue;
                        Commands.AppendToBuffer(index, Root, new DamageRequest { Source = projectile.Source, Target = victim.Entity, Amount = projectile.Damage, Penetration = projectile.Penetration, Faction = projectile.Faction, HasPayload = 1 });
                    }
                    Commands.DestroyEntity(index, entity);
                }
                else { var t = transform; t.Position = next; Commands.SetComponent(index, entity, t); }
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(ProjectileSystem))]
    public partial struct DamageSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager; var root = Sim.Root(em);
            var session = em.GetComponentData<Session>(root); if (session.Paused != 0) return;
            if (session.Phase != Phase.Night && session.Phase != Phase.Retreat) { em.GetBuffer<DamageRequest>(root).Clear(); return; }
            using var requests = em.GetBuffer<DamageRequest>(root).ToNativeArray(Allocator.Temp); em.GetBuffer<DamageRequest>(root).Clear();
            foreach (var damage in requests) CombatOps.ApplyDamage(em, root, damage);
        }
    }
}
