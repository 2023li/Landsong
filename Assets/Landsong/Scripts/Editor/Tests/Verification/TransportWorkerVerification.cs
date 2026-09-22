#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Definitions;
using Landsong.ECS.Editor;
using Landsong.ECS.Persistence;
using Pathfinding.ECS;
using Rukhanka;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class TransportWorkerVerification
    {
        [MenuItem("Landsong/ECS/Verification/Transport workers")]
        public static string Run()
        {
            var log = new StringBuilder().AppendLine(DateTimeOffset.Now.ToString("O"));
            int checks = 0;
            void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException(label); checks++; log.AppendLine("PASS " + label); }
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Transport worker verification", WorldFlags.Game);
            try
            {
                var settings = AssetDatabase.LoadAssetAtPath<GameObject>(TransportWorkerContentAuthoring.Template).GetComponent<TransportWorkerSettingsAuthoring>();
                Check(settings != null && settings.Male != null && settings.Female != null, "Shared world template registers both worker variants");
                var male = settings.Male.GetComponent<SoldierAnimationAuthoring>(); var female = settings.Female.GetComponent<SoldierAnimationAuthoring>();
                var maleView = male.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>(); var femaleView = female.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>();
                Check(maleView.Animator.runtimeAnimatorController == femaleView.Animator.runtimeAnimatorController && maleView.Animator.avatar != femaleView.Animator.avatar, "Two humanoid models share one worker controller");
                var controller = (AnimatorController)maleView.Animator.runtimeAnimatorController;
                Check(controller.animationClips.Length == 7 && controller.animationClips.All(c => c.isHumanMotion && AnimationUtility.GetAnimationEvents(c).Length == 0), "Seven owned humanoid clips without gameplay animation events");
                Check(male.VisualPrefab.GetComponent<TransportCargoAuthoring>().Mount.parent == maleView.Animator.GetBoneTransform(HumanBodyBones.RightHand)
                    && female.VisualPrefab.GetComponent<TransportCargoAuthoring>().Mount.parent == femaleView.Animator.GetBoneTransform(HumanBodyBones.RightHand), "Grey cargo is attached to each model's real hand bone");
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager; var root = WorldQueries.Root(em); WorldInitialization.Initialize(em, root);
                Check(em.HasComponent<TransportWorkerSettings>(root), "Formal map inherits worker configuration through the shared template");
                var gate = em.GetComponentData<PersistenceGate>(root); gate.CheckpointPending = 0; em.SetComponentData(root, gate);
                var control = em.GetComponentData<SimulationControl>(root); control.Paused = 0; em.SetComponentData(root, control);
                var clock = em.GetComponentData<GameClock>(root); clock.DawnRemaining = 0; em.SetComponentData(root, clock);
                var core = WorldQueries.Entities<Building>(em).Where(e => em.GetComponentData<BuildingHousingStats>(e).IsCore != 0).First();
                var coreCell = em.GetComponentData<BuildingPlacementState>(core).Cell;
                var definition = BuildingDefinitions.Find(em, root, "b木材加工厂");
                int created = 0;
                for (int ring = 5; ring < 35 && created < 2; ring++)
                    for (int side = 0; side < 4 && created < 2; side++)
                    {
                        var cell = coreCell + (side == 0 ? new int2(ring, 0) : side == 1 ? new int2(-ring, 0) : side == 2 ? new int2(0, ring) : new int2(0, -ring));
                        if (!GridOps.CanPlace(em, root, definition, cell, 0)) continue;
                        var building = BuildingCreation.Create(em, root, definition, cell, 0, 1, true);
                        var workers = em.GetComponentData<BuildingWorkforceState>(building); workers.Workers = 4; em.SetComponentData(building, workers);
                        created++;
                    }
                Check(created == 2, "Real map has two production consumers for independent resource routes");
                var inventory = em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).ToArray();
                TransportWorkerOps.Reconcile(em, root, clock.Turn);
                using var initial = WorldQueries.OrderedEntities<TransportWorker>(em);
                Check(initial.Length >= 2, "Resource provider selection creates workers on actual supply connections");
                Check(initial.Select(e => em.GetComponentData<TransportWorker>(e).Consumer).Distinct().Count() == initial.Length, "At most one worker represents each consumer connection");
                Check(initial.Select(e => em.GetComponentData<TransportWorker>(e).Variant).Distinct().Count() == 2, "Both model variants spawn");
                var worker = initial[0]; var id = em.GetComponentData<Identity>(worker).Id;
                Check(!em.HasComponent<Soldier>(worker) && !em.HasComponent<SoldierDefinitionRef>(worker), "Transport worker is a civilian, never a soldier or wolf's soldier target");
                var route = em.GetComponentData<TransportWorker>(worker);
                Check(math.distance(EntityState.Position(em, worker), route.Start) < .01f && route.Carrying == 1, "Worker appears at the route start already carrying cargo");
                TransportWorkerOps.Reconcile(em, root, clock.Turn);
                using (var again = WorldQueries.Entities<TransportWorker>(em)) Check(again.Length == initial.Length, "Repeated reconciliation creates no duplicates");
                SoldierAnimationSystem.UpdateUnit(em, worker, .1f, false); TransportWorkerAnimationSystem.UpdateVisual(em, worker);
                var view = em.GetComponentData<SoldierAnimationState>(worker).View; var rig = em.GetComponentData<SoldierAnimationBinding>(view).Rig;
                Check(em.HasComponent<RigDefinitionComponent>(rig) && em.HasComponent<GPUAnimationEngineTag>(rig), "Actual worker prefab bakes a complete Rukhanka rig");
                var mount = em.GetComponentData<TransportCargoBinding>(view).Mount;
                Check(em.GetComponentData<LocalTransform>(mount).Scale == 1, "Cargo visible on outbound trip");
                float elapsed = 0;
                void Tick(int count)
                {
                    for (int i = 0; i < count; i++)
                    {
                        elapsed += .1f; world.SetTime(new TimeData(elapsed, .1f));
                        if (em.GetComponentData<Session>(root).Phase != Phase.Day)
                        { var now = em.GetComponentData<GameClock>(root); now.Time = elapsed; em.SetComponentData(root, now); }
                        TransportWorkerOps.Tick(em, root, .1f, false);
                        world.GetOrCreateSystem<NavigationSystem>().Update(world.Unmanaged);
                        world.GetOrCreateSystem<FallbackResolveMovementSystem>().Update(world.Unmanaged);
                        world.GetOrCreateSystem<AstarNavigationMoveSystem>().Update(world.Unmanaged);
                        em.CompleteAllTrackedJobs();
                    }
                }
                var start = EntityState.Position(em, worker); Tick(20);
                Check(math.distance(start, EntityState.Position(em, worker)) > .2f, "Navigation moves workers during daytime without DayReturnState");
                int safety = 0;
                while (em.GetComponentData<TransportWorker>(worker).Trips == 0 && safety++ < 2000) Tick(1);
                Check(em.GetComponentData<TransportWorker>(worker).Trips == 1 && em.GetComponentData<TransportWorker>(worker).Carrying == 0, "Real path reaches destination, unloads, then returns empty");
                SoldierAnimationSystem.UpdateUnit(em, worker, .1f, false); TransportWorkerAnimationSystem.UpdateVisual(em, worker);
                Check(em.GetComponentData<LocalTransform>(mount).Scale == 0, "Cargo hidden on empty return");
                safety = 0;
                while (em.GetComponentData<TransportWorker>(worker).Stage != TransportStage.Delivering && safety++ < 2000) Tick(1);
                Check(em.GetComponentData<TransportWorker>(worker).Stage == TransportStage.Delivering && em.GetComponentData<TransportWorker>(worker).Carrying == 1, "A full return and load starts another daytime trip");
                Check(inventory.SequenceEqual(em.GetBuffer<InventorySlot>(root).ToNativeArray(Allocator.Temp).ToArray()), "Walking and unloading never debit or duplicate actual inventory");
                Tick(10); start = EntityState.Position(em, worker);
                control.Paused = 1; em.SetComponentData(root, control); Tick(10);
                Check(math.distance(start, EntityState.Position(em, worker)) < .001f, "Pause freezes worker movement and handling");
                control.Paused = 0; em.SetComponentData(root, control);
                var bytes = SnapshotCodec.Capture(em, root); var review = SnapshotCodec.Capture(em, root, false); Tick(10);
                Check(review.SequenceEqual(SnapshotCodec.Capture(em, root, false)), "Worker motion cannot invalidate economy confirmation fingerprints");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes)); worker = WorldQueries.Find(em, id);
                Check(math.distance(start, EntityState.Position(em, worker)) < .001f && em.GetComponentData<TransportWorker>(worker).Carrying == 1, "Save/load restores worker position, model and cargo");
                var begin = NightEntryOps.Begin(em, root, false, 0);
                if (begin == ResultCode.ConfirmationRequired) begin = NightEntryOps.Begin(em, root, true, em.GetComponentData<NightEntryReview>(root).Token);
                worker = WorldQueries.Find(em, id);
                Check(begin == ResultCode.Success && em.GetComponentData<Session>(root).Phase == Phase.Deployment
                    && em.HasComponent<TransportWorker>(worker) && math.distance(start, EntityState.Position(em, worker)) < .001f,
                    "Actual transactional day settlement preserves the worker's exposed position");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes)); worker = WorldQueries.Find(em, id);
                var session = em.GetComponentData<Session>(root); session.Phase = Phase.Deployment; em.SetComponentData(root, session);
                Tick(1);
                Check(em.GetComponentData<TransportWorker>(worker).Stage == TransportStage.Returning && em.GetComponentData<TransportWorker>(worker).Retiring == 1, "Dusk interrupts outbound work and recalls from current position");
                bytes = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes)); worker = WorldQueries.Find(em, id);
                Check(em.GetComponentData<TransportWorker>(worker).Retiring == 1, "Dusk checkpoint and restore retain the exposed return trip");
                // Keep a second worker on its route to test hostile targeting independently of shelter.
                Entity victim; using (var workers = WorldQueries.OrderedEntities<TransportWorker>(em)) victim = workers.First(e => e != worker);
                safety = 0; while (em.GetComponentData<TransportWorker>(worker).Stage != TransportStage.Sheltered && safety++ < 2000) Tick(1);
                Check(em.GetComponentData<Combatant>(worker).Deployed == 0 && math.distance(EntityState.Position(em, worker), route.Start) <= .2f, "Worker becomes hidden only after physically reaching the start");
                int population = PopulationOps.Population(em, root); Tick(10); Check(PopulationOps.Population(em, root) == population, "Safe return costs no population");
                var victimState = em.GetComponentData<TransportWorker>(victim); victimState.Stage = TransportStage.Returning; victimState.Retiring = 1; em.SetComponentData(victim, victimState);
                var victimActor = em.GetComponentData<Combatant>(victim); victimActor.Deployed = 1; em.SetComponentData(victim, victimActor);
                em.SetComponentData(victim, new VisualState { Visible = 1 });
                SoldierAnimationSystem.UpdateUnit(em, victim, .1f, false);
                session.Phase = Phase.Night; em.SetComponentData(root, session);
                using (var actors = WorldQueries.Entities<Combatant>(em)) foreach (var entity in actors)
                    if (entity != victim) { var actor = em.GetComponentData<Combatant>(entity); actor.Deployed = 0; em.SetComponentData(entity, actor); }
                var enemy = EnemyEntities.Spawn(em, root, EnemyId.FromIndex(0), EntityState.Position(em, victim) + new float3(.5f, 0, 0), false);
                EnemyCombatants.Configure(em, root, enemy, true, 0, EntityState.Position(em, enemy));
                var attacker = em.GetComponentData<Combatant>(enemy); attacker.Profile = CombatProfile.Default; attacker.Damage = 1000; attacker.Range = 2; attacker.ProjectileSpeed = 0; attacker.Target = victim; em.SetComponentData(enemy, attacker);
                world.GetOrCreateSystem<PerceptionSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                Check(em.GetComponentData<Perception>(enemy).Enemy == victim, "Ordinary hostile perception finds an exposed returning civilian");
                int reports = em.GetBuffer<BattleReportEntry>(root).Length;
                world.GetOrCreateSystem<CombatSystem>().Update(world.Unmanaged); world.GetOrCreateSystem<DamageSystem>().Update(world.Unmanaged); em.CompleteAllTrackedJobs();
                Check(!EntityState.Alive(em, victim) && em.GetComponentData<TransportWorker>(victim).DeathRecorded == 1, "Actual hostile combat damage kills the worker");
                SoldierAnimationSystem.UpdateUnit(em, victim, .1f, false); TransportWorkerAnimationSystem.UpdateVisual(em, victim);
                var dying = em.GetComponentData<SoldierAnimationState>(victim);
                Check(dying.Dying == 1 && em.Exists(dying.View) && dying.DeathRemaining > 0, "Death retains the visible animation until its display duration finishes");
                Check(PopulationOps.Population(em, root) == population - 1, "Worker death removes exactly one citizen");
                CombatOps.Death(em, root, victim, enemy); Check(PopulationOps.Population(em, root) == population - 1, "Repeated death notification cannot charge population twice");
                Check(em.GetBuffer<BattleReportEntry>(root).Length == reports, "Civilian loss is never counted as enemy reward or soldier death");
                Check(NightReturnOps.Completion(em, root) == 1, "Workers do not block the soldiers' dawn return completion");
                // Fixed building population must also remain mortal when no housed/base civilians exist.
                using (var buildings = WorldQueries.Entities<Building>(em)) foreach (var building in buildings)
                { var housing = em.GetComponentData<BuildingHousingState>(building); housing.Population = 0; em.SetComponentData(building, housing); }
                em.SetComponentData(root, new PopulationState()); population = PopulationOps.Population(em, root);
                PopulationOps.RemovePopulation(em, root, 1); Check(population > 0 && PopulationOps.Population(em, root) == population - 1, "Palace-provided population is reduced through the persisted base adjustment");
                session.Phase = Phase.Day; em.SetComponentData(root, session);
                var nextDay = em.GetComponentData<GameClock>(root); nextDay.Turn++; em.SetComponentData(root, nextDay);
                var deceasedId = em.GetComponentData<Identity>(victim).Id;
                TransportWorkerOps.Tick(em, root, .1f, false);
                Check(WorldQueries.Find(em, deceasedId) == Entity.Null, "Next day clears the prior worker death record without charging again");
                em.DestroyEntity(root); SimulationLifetimeSystem.Cleanup(em);
                using (var survivors = WorldQueries.Entities<TransportWorker>(em)) Check(survivors.Length == 0, "Unloading the session releases every owned transport worker");
                log.AppendLine("Assertions: " + checks); File.WriteAllText("Library/LandsongEcs/transport-worker-verification.txt", log.ToString()); return log.ToString();
            }
            catch (Exception e) { File.WriteAllText("Library/LandsongEcs/transport-worker-verification.txt", log + "\nFAIL " + e); throw; }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
    }
}
#endif
