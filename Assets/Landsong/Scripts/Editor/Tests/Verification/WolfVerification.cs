#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.ECS;
using Landsong.ECS.AI;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Editor;
using Pathfinding.ECS;
using Opsive.BehaviorDesigner.Runtime.Components;
using Opsive.BehaviorDesigner.Runtime.Tasks;
using Rukhanka;
using Unity.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

namespace Landsong.EditorTools
{
    public static class WolfVerification
    {
        [MenuItem("Landsong/ECS/Verification/Wolf")]
        public static string Run()
        {
            var log = new StringBuilder().AppendLine(DateTimeOffset.Now.ToString("O"));
            int checks = 0;
            void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException(label); checks++; log.AppendLine("PASS " + label); }
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Wolf combat and animation", WorldFlags.Game);
            try
            {
                var definition = WolfContentAuthoring.Definition();
                UnitAuthoringWorkflow.Validate(definition);
                var animation = definition.Prefab.GetComponent<SoldierAnimationAuthoring>();
                var visual = animation.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>();
                var controller = (AnimatorController)visual.Animator.runtimeAnimatorController;
                Check(animation.Profile == UnitAnimationProfile.GenericCreature && controller.layers.Length == 1 && !visual.Animator.avatar.isHuman, "Wolf uses a single Generic controller without humanoid masks");
                Check(controller.animationClips.Length == 5 && controller.animationClips.All(c => !c.isHumanMotion && AnimationUtility.GetAnimationEvents(c).Length == 0), "Five real wolf motions, no gameplay animation events");
                Check(!AnimationUtility.GetAnimationClipSettings(controller.animationClips.Single(c => c.name == "Attack")).loopTime, "Copied attack is non-looping despite looping vendor source");
                var events = ContentAuthoringContext.Content().Get<NightEventCatalogAsset>().Events;
                Check(events.Single(e => e.Id == "night.raid").Enemies.Count(e => e.Enemy == definition && e.Weight == 1) == 1
                    && events.Where(e => e.Id != "night.raid").All(e => e.Enemies.All(s => s.Enemy != definition)), "Wolf is registered exactly once in ordinary combat nights only");
                var profile = definition.CombatStats.Profile;
                Check(CombatProfile.Valid(profile), "Nearest-soldier profile compiles");
                profile.Traits |= TacticalTraits.SiegeOnly;
                Check(!CombatProfile.Valid(profile), "Conflicting soldier-only and building-only targeting is rejected");
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var session = em.GetComponentData<Session>(root); session.Phase = Phase.Night; em.SetComponentData(root, session);
                var control = em.GetComponentData<SimulationControl>(root); control.Paused = 0; em.SetComponentData(root, control);
                var clock = em.GetComponentData<GameClock>(root); clock.Time = 100; em.SetComponentData(root, clock);
                world.SetTime(new TimeData(100, .1f));
                using (var units = WorldQueries.Entities<Combatant>(em))
                    foreach (var unit in units) { var a = em.GetComponentData<Combatant>(unit); a.Deployed = 0; em.SetComponentData(unit, a); }
                var grid = em.GetComponentData<GridData>(root);
                var occupied = em.GetBuffer<Occupancy>(root);
                float3 origin = default; bool found = false;
                for (int y = grid.Value.Value.Min.y + 6; y < grid.Value.Value.Min.y + grid.Value.Value.Size.y - 6 && !found; y++)
                    for (int x = grid.Value.Value.Min.x + 6; x < grid.Value.Value.Min.x + grid.Value.Value.Size.x - 6; x++)
                    {
                        bool open = true;
                        for (int dy = -5; dy <= 5 && open; dy++)
                            for (int dx = -5; dx <= 5; dx++) if (!GridOps.Traversable(grid, occupied, new int2(x + dx, y + dy))) { open = false; break; }
                        if (!open) continue;
                        origin = GridOps.Position(grid, new int2(x, y), new int2(1)) + new float3(0, .5f, 0); found = true; break;
                    }
                Check(found, "Real map has a traversable wolf combat arena");
                Entity Soldier(float distance)
                {
                    var e = SoldierEntities.Spawn(em, root, SoldierDefinitions.Find(em, root, "militia"), origin + new float3(distance, 0, 0), false);
                    SoldierCombatants.Configure(em, root, e, true, 0, origin);
                    return e;
                }
                var wolf = EnemyEntities.Spawn(em, root, EnemyDefinitions.Find(em, root, "wolf"), origin, false);
                EnemyCombatants.Configure(em, root, wolf, true, 0, origin);
                var near = Soldier(3); var far = Soldier(6);
                var hero = HeroEntities.Spawn(em, root, HeroDefinitions.Find(em, root, "titan"), origin + new float3(1, 0, 0), false);
                HeroCombatants.Configure(em, root, hero, true, 0, origin);
                var threatening = em.GetComponentData<Combatant>(far); threatening.Threat = 100; threatening.Target = wolf; em.SetComponentData(far, threatening);
                Entity Perceive()
                {
                    world.GetOrCreateSystem<PerceptionSystem>().Update(world.Unmanaged);
                    em.CompleteAllTrackedJobs();
                    return em.GetComponentData<Perception>(wolf).Enemy;
                }
                void Position(Entity e, float distance) => em.SetComponentData(e, LocalTransform.FromPosition(origin + new float3(distance, 0, 0)));
                void Deploy(Entity e, bool value) { var a = em.GetComponentData<Combatant>(e); a.Deployed = (byte)(value ? 1 : 0); em.SetComponentData(e, a); }
                var perceived = Perceive();
                log.AppendLine($"Target={perceived}; near={near}; far={far}; hero={hero}; wolf traits={em.GetComponentData<Combatant>(wolf).Profile.Traits}; near soldier marker={em.HasComponent<Soldier>(near)}; near health={em.GetComponentData<Health>(near).Current}; near deployed={em.GetComponentData<Combatant>(near).Deployed}");
                Check(perceived == near, "Nearest soldier wins over a closer hero and a threatening retaliating soldier");
                Position(far, 2); Check(Perceive() == far, "Moving soldiers cause nearest-target reassessment");
                Position(far, 3); Check(Perceive() == near, "Exact distance ties use stable identity order");
                var health = em.GetComponentData<Health>(near); health.Current = 0; em.SetComponentData(near, health);
                Check(Perceive() == far, "Dead nearest soldier is replaced");
                Deploy(far, false); Check(Perceive() == Entity.Null, "Undeployed soldiers and heroes do not become fallback targets");
                Deploy(far, true); Position(far, 50); Check(Perceive() == far, "Wolf finds soldiers beyond ordinary detection radius");
                var actor = em.GetComponentData<Combatant>(far); actor.ProtectedUntil = 200; em.SetComponentData(far, actor);
                Check(Perceive() == Entity.Null, "Protected soldiers are excluded");
                actor.ProtectedUntil = 0; em.SetComponentData(far, actor);
                Position(far, 3); Perceive();
                em.SetComponentData(wolf, new TacticalState { Pursued = far, Started = 0, Origin = origin - new float3(50, 0, 0), Returning = 1 });
                TacticalOps.Update(em, root);
                var tactical = em.GetComponentData<TacticalState>(wolf);
                Check(tactical.Returning == 0 && tactical.Pursued == far && tactical.HasSlot != 0, "Wolf continuously pursues and reserves a melee slot without normal leash reset");
                bool Action(TacticalMode mode)
                {
                    em.SetComponentEnabled<TacticalActionFlag>(wolf, true);
                    em.SetComponentEnabled<EvaluateFlag>(wolf, true);
                    var actions = em.GetBuffer<TacticalActionData>(wolf);
                    var tasks = em.GetBuffer<TaskComponent>(wolf);
                    int selected = -1;
                    foreach (var action in actions)
                    {
                        var task = tasks[action.Index]; task.Status = action.Mode == mode ? TaskStatus.Queued : TaskStatus.Failure; tasks[action.Index] = task;
                        if (action.Mode != mode) continue;
                        selected = action.Index;
                        var branches = em.GetBuffer<BranchComponent>(wolf); var branch = branches[task.BranchIndex]; branch.CanExecute = true; branches[task.BranchIndex] = branch;
                    }
                    if (selected < 0) throw new InvalidOperationException("Wolf template lacks " + mode);
                    world.GetOrCreateSystem<TacticalActionSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                    return em.GetBuffer<TaskComponent>(wolf)[selected].Status != TaskStatus.Failure;
                }
                Check(!Action(TacticalMode.Retreat), "Missing building home does not cause premature wolf retreat");
                Check(Action(TacticalMode.EngageEnemy) && em.GetComponentData<Combatant>(wolf).Target == far && em.GetComponentData<Steering>(wolf).Moving != 0, "Authored tactical action steers wolf toward its soldier target");
                float before = math.distance(EntityState.Position(em, wolf), EntityState.Position(em, far));
                world.GetOrCreateSystem<NavigationSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<FallbackResolveMovementSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<AstarNavigationMoveSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Check(math.distance(EntityState.Position(em, wolf), EntityState.Position(em, far)) < before, "Real navigation advances the wolf toward the selected soldier");
                Position(wolf, 0); Position(far, 1);
                var defender = em.GetComponentData<Combatant>(far); defender.Profile.Armor = 2; defender.Profile.Reduction = .25f; defender.Target = Entity.Null; em.SetComponentData(far, defender);
                float hp = em.GetComponentData<Health>(far).Current;
                using var shots = em.CreateEntityQuery(typeof(Projectile)); int shotsBefore = shots.CalculateEntityCount();
                world.GetOrCreateSystem<CombatSystem>().Update(world.Unmanaged);
                world.GetOrCreateSystem<DamageSystem>().Update(world.Unmanaged);
                Check(em.GetComponentData<Health>(far).Current == hp - 4.5f && shots.CalculateEntityCount() == shotsBefore, "Bite deals armor-mitigated damage without spawning a projectile");
                world.GetOrCreateSystem<CombatSystem>().Update(world.Unmanaged); world.GetOrCreateSystem<DamageSystem>().Update(world.Unmanaged);
                Check(em.GetComponentData<Health>(far).Current == hp - 4.5f, "Bite respects its attack cooldown");
                SoldierAnimationSystem.UpdateUnit(em, wolf, .1f, false);
                var view = em.GetComponentData<SoldierAnimationState>(wolf).View;
                var rig = em.GetComponentData<SoldierAnimationBinding>(view).Rig;
                Check(em.HasBuffer<AnimatorControllerParameterComponent>(rig) && em.GetBuffer<AnimatorControllerLayerComponent>(rig).Length == 1, "Actual baked wolf creates its Generic Rukhanka rig");
                foreach (var renderer in em.GetBuffer<SoldierAnimationRenderer>(view))
                    if (em.HasComponent<SkinnedMeshRendererComponent>(renderer.Entity))
                    {
                        var mesh = em.GetComponentData<SkinnedMeshRendererComponent>(renderer.Entity);
                        Check(mesh.animatedRigEntity == rig && em.HasComponent<GPUAnimationEngineTag>(rig), "Skinned wolf references its own instantiated rig: " + mesh.animatedRigEntity + " / " + rig);
                    }
                Deploy(far, false); Perceive(); TacticalOps.Update(em, root);
                Check(Action(TacticalMode.Dormant) && em.GetComponentData<Steering>(wolf).Moving == 0 && em.GetComponentData<Combatant>(wolf).Target == Entity.Null, "No eligible soldier clears stale chase and attack commands");
                session.Phase = Phase.Retreat; em.SetComponentData(root, session);
                clock.PhaseTime = 1000; em.SetComponentData(root, clock);
                Check(Action(TacticalMode.Retreat), "Wolf still retreats at the normal night closure");
                var wolfHealth = em.GetComponentData<Health>(wolf); wolfHealth.Current = 0; em.SetComponentData(wolf, wolfHealth);
                SoldierAnimationSystem.UpdateUnit(em, wolf, .1f, false);
                Check(em.Exists(view), "Dead deployed wolf retains its death animation");
                SoldierAnimationSystem.UpdateUnit(em, wolf, 100, false); SoldierAnimationSystem.UpdateUnit(em, wolf, .1f, false);
                Check(!em.Exists(view), "Wolf death animation releases its view");
                var wolfId = EnemyDefinitions.Find(em, root, "wolf");
                var night = em.GetComponentData<NightRuntimeState>(root);
                night.Kind = NightKind.Invasion;
                night.Duration = em.GetComponentData<NightSettings>(root).TotalNightSeconds;
                em.SetComponentData(root, night);
                EntityState.Set(em, root, new NightPlanState { Event = "night.raid", Turn = clock.Turn, BaseThreat = 30 });
                bool wolfWave = false;
                for (int attempt = 0; attempt < 32 && !wolfWave; attempt++)
                {
                    NightPlanOps.Plan(em, root, true);
                    foreach (var wave in em.GetBuffer<NightWave>(root)) if (wave.Definition == wolfId && wave.SpatiallyBlocked == 0) wolfWave = true;
                }
                Check(wolfWave, "Ordinary night planner samples a spatially valid wolf wave from the real enemy pool");
                session.Phase = Phase.Night; em.SetComponentData(root, session);
                clock = em.GetComponentData<GameClock>(root); clock.PhaseTime = 4; em.SetComponentData(root, clock);
                var gate = em.GetComponentData<PersistenceGate>(root); gate.CheckpointPending = 0; em.SetComponentData(root, gate);
                var waves = em.GetBuffer<NightWave>(root);
                for (int i = 0; i < waves.Length; i++) { var wave = waves[i]; wave.At = 0; wave.Warned = 1; wave.WarnedAt = clock.Time - 100; waves[i] = wave; }
                NightOps.Tick(em, root, .1f);
                using (var enemies = WorldQueries.Entities<EnemyDefinitionRef>(em))
                    Check(enemies.Any(e => e != wolf && em.GetComponentData<EnemyDefinitionRef>(e).Definition == wolfId && EntityState.Alive(em, e)
                        && em.GetComponentData<Combatant>(e).Deployed != 0 && em.HasComponent<SoldierAnimationPrefab>(e)), "Actual night wave spawning deploys the animated wolf prefab");
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error) { log.AppendLine("FAIL " + error); throw; }
            finally { EditorSceneManager.ClosePreviewScene(scene); File.WriteAllText("Library/LandsongEcs/wolf-verification.txt", log.ToString()); }
        }
    }
}
#endif
