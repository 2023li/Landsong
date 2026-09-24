#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.Animation;
using Landsong.ECS;
using Landsong.ECS.AI;
using Landsong.ECS.Authoring;
using Landsong.ECS.Editor;
using Landsong.ECS.Persistence;
using Rukhanka;
using Rukhanka.Hybrid;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class SoldierAnimationVerification
    {
        [MenuItem("Landsong/动画/验证罗马士兵接入")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式。");
            var report = new StringBuilder();
            var count = 0;
            void Check(bool condition, string label)
            {
                if (!condition)
                    throw new InvalidOperationException("FAIL " + label);
                count++;
                report.AppendLine("PASS " + label);
            }

            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Soldier animation integration verification", WorldFlags.Game);
            try
            {
                var definition = SoldierAnimationSetup.Militia();
                var prefab = definition.Prefab;
                var config = prefab.GetComponent<SoldierAnimationAuthoring>();
                var authoring = config.VisualPrefab.GetComponent<SoldierAnimationVisualAuthoring>();
                Check(prefab.GetComponentsInChildren<Renderer>(true).Length == 0 && prefab.GetComponentsInChildren<Animator>(true).Length == 0, "Logical prefab contains no model, renderer or Animator");
                Check(authoring != null && authoring.Animator.avatar.isValid && authoring.Animator.avatar.isHuman, "Valid Humanoid character and explicit animator binding");
                Check(!authoring.Animator.applyRootMotion && !authoring.Animator.GetComponent<RigDefinitionAuthoring>().applyRootMotion, "Both Animator and Rukhanka disable root motion");
                var controller = authoring.Animator.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;
                Check(controller != null && controller.animationClips.Length >= 9, "Controller contains locomotion, combat, torch, draw and sheathe motions");
                Check(controller.layers.Length == 4 && controller.layers[1].avatarMask != null && controller.layers[2].avatarMask != null,
                    "Controller separates base, left hand, right hand and celebration arms");
                var celebrationLayer = controller.layers[authoring.CelebrationLayer];
                var celebrationMask = celebrationLayer.avatarMask;
                for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
                {
                    var part = (AvatarMaskBodyPart)i;
                    bool arms = part == AvatarMaskBodyPart.LeftArm || part == AvatarMaskBodyPart.RightArm || part == AvatarMaskBodyPart.LeftFingers || part == AvatarMaskBodyPart.RightFingers;
                    Check(celebrationMask.GetHumanoidBodyPartActive(part) == arms, "Celebration mask isolates arms: " + part);
                }
                Check(celebrationLayer.stateMachine.states.Any(s => s.state.motion != null && s.state.motion.name == "Victory"), "Victory asset is bound only to the celebration arms layer");
                var sword = authoring.SwordMount != null ? authoring.SwordMount.Find("RomanSword") : null;
                Check(sword != null && sword.GetComponent<MeshRenderer>() != null && sword.GetComponentsInChildren<Collider>().Length == 0
                    && authoring.SwordMount.parent == authoring.SwordHandSocket && authoring.SwordMount.localScale == Vector3.zero,
                    "Roman sword starts hidden at the right-hand socket without gameplay colliders");
                Check(authoring.TorchMount != null && authoring.TorchMount.GetComponentsInChildren<MeshRenderer>().Length == 2
                    && authoring.TorchMount.GetComponentsInChildren<Collider>().Length == 0
                    && authoring.TorchFlameParticles != null && authoring.TorchLight != null
                    && authoring.TorchFlameParticles.transform.IsChildOf(authoring.TorchMount)
                    && authoring.TorchLight.transform.IsChildOf(authoring.TorchMount), "Torch socket contains two rigid meshes, flame particles and a point light");
                var package = SoldierAnimationSetup.CurrentPackage() + "/";
                Check(config.VisualPrefab.GetComponentsInChildren<Renderer>(true).All(r => r is ParticleSystemRenderer
                    || r.sharedMaterials.All(m => m.shader != null && AssetDatabase.GetAssetPath(m).StartsWith(package, StringComparison.Ordinal))), "Soldier mesh materials belong to the referenced militia package");
                Check(definition.Prefab == prefab, "Production militia definition references animated prefab");
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                VerifyPersistence(world, root, Check);
                var session = em.GetComponentData<Session>(root);
                GameClock sessionClock = em.GetComponentData<GameClock>(root);
                SimulationControl sessionControl = em.GetComponentData<SimulationControl>(root);
                NightRuntimeState sessionNight = em.GetComponentData<NightRuntimeState>(root);
                session.Phase = Phase.Night;
                sessionNight.Kind = NightKind.Invasion;
                sessionClock.Time = 1;
                sessionControl.Paused = 0;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                }

                Entity Spawn(string id, byte faction, float3 position)
                {
                    if (faction == 0)
                    {
                        var e = SoldierEntities.Spawn(em, root, SoldierDefinitions.Find(em, root, id), position, false);
                        SoldierCombatants.Configure(em, root, e, true, 0, position);
                        return e;
                    }
                    else
                    {
                        var e = EnemyEntities.Spawn(em, root, EnemyDefinitions.Find(em, root, id), position, false);
                        EnemyCombatants.Configure(em, root, e, true, 0, position);
                        return e;
                    }
                }

                var soldier = Spawn("militia", 0, new float3(0, .5f, 0));
                var enemy = Spawn("raider", 1, new float3(1, .5f, 0));
                Check(em.HasComponent<AnimatedUnitVisual>(soldier) && em.HasBuffer<TacticalActionData>(soldier), "Normal Sim.Spawn retains DBP tasks and opts into native animation");
                Check(em.GetComponentData<SoldierAnimationState>(soldier).View == Entity.Null && !em.HasComponent<SoldierAnimationBinding>(soldier), "Spawning the gameplay unit does not instantiate its view");
                if (em.HasBuffer<LinkedEntityGroup>(soldier))
                    foreach (var child in em.GetBuffer<LinkedEntityGroup>(soldier))
                        Check(!em.HasComponent<MaterialMeshInfo>(child.Value) && !em.HasComponent<RigDefinitionComponent>(child.Value), "Logical linked group contains no renderer or rig");
                SoldierAnimationSystem.UpdateUnit(em, soldier, .1f, false);
                var view = em.GetComponentData<SoldierAnimationState>(soldier).View;
                var binding = em.GetComponentData<SoldierAnimationBinding>(view);
                Check(em.Exists(binding.Rig) && em.HasBuffer<AnimatorControllerLayerComponent>(binding.Rig), "Rukhanka controller was baked and remapped to the spawned rig");
                Check(em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig).Length == 4 && em.Exists(binding.SwordMount)
                    && em.Exists(binding.SwordHandSocket) && em.Exists(binding.TorchMount),
                    "Rukhanka baked all four layers and remapped equipment sockets");
                Check(em.Exists(binding.TorchFlame) && em.HasComponent<ParticleSystem>(binding.TorchFlame)
                    && em.Exists(binding.TorchLight) && em.HasComponent<Light>(binding.TorchLight),
                    "Torch particle and light bake as companion components and remap to the spawned view");
                Check(em.GetComponentObject<ParticleSystem>(binding.TorchFlame).isPlaying
                    && em.GetComponentObject<Light>(binding.TorchLight).enabled,
                    "Patrol torch starts its baked flame and light");
                Check(em.GetComponentData<Parent>(view).Value == soldier && em.GetComponentData<Parent>(binding.Rig).Value == view, "Independent view and rig follow the gameplay transform through ECS Parent");
                Check(!em.HasComponent<Identity>(view) && !em.HasComponent<Persistent>(view) && !em.HasBuffer<TacticalActionData>(view), "View does not duplicate identity, persistence or DBP tasks");
                AnimatorParametersAspect Parameters() => new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(binding.Rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(binding.Rig));
                void Update(float dt = .1f, bool reduced = false) => SoldierAnimationSystem.UpdateUnit(em, soldier, dt, reduced);
                bool Visible()
                {
                    foreach (var renderer in em.GetBuffer<SoldierAnimationRenderer>(view))
                        if (!em.IsComponentEnabled<MaterialMeshInfo>(renderer.Entity) || em.HasComponent<DisableRendering>(renderer.Entity))
                            return false;
                    return true;
                }

                Update();
                Check(em.GetComponentData<SoldierAnimationState>(soldier).View == view, "Repeated updates reuse the deployed instance");
                Check(em.GetBuffer<SoldierAnimationRenderer>(view).Length > 0 && Visible(), "All generated render entities are collected and visible");
                var attachedRenderers = 0;
                foreach (var renderer in em.GetBuffer<SoldierAnimationRenderer>(view))
                    if (em.GetName(renderer.Entity).StartsWith("RomanSword", StringComparison.Ordinal))
                        attachedRenderers++;
                Check(attachedRenderers > 0, "Rigid weapon render entities are baked with the animated soldier");
                Check(em.GetComponentData<Parent>(binding.SwordMount).Value == binding.SwordHandSocket
                    && em.GetComponentData<LocalTransform>(binding.SwordMount).Scale == 0
                    && em.GetComponentData<LocalTransform>(binding.TorchMount).Scale == 1, "Patrol starts with hidden sword and visible torch");
                var tr = em.GetComponentData<LocalTransform>(soldier);
                tr.Position.x += .25f;
                em.SetComponentData(soldier, tr);
                Update();
                Check(Parameters().GetFloatParameter("Speed") > 1 && !Parameters().GetBoolParameter("Alerted"), "Normal patrol movement selects walking without alert");
                em.SetComponentData(root, new BellState { ActiveBell = 77 });
                em.SetComponentData(soldier, new UnitOrder { Kind = OrderKind.Rally, Source = 77 });
                Update();
                Check(Parameters().GetBoolParameter("Alerted") && em.GetComponentData<SoldierAnimationState>(soldier).Equipment == SoldierEquipmentState.Torch,
                    "Matching active bell selects alert running while retaining the torch");
                em.SetComponentData(root, new BellState());
                em.SetComponentData(soldier, new UnitOrder());
                Update(config.AlertReleaseSeconds + .1f);
                Check(!Parameters().GetBoolParameter("Alerted"), "Alert release delay returns movement to patrol walking");
                var actor = em.GetComponentData<Combatant>(soldier);
                actor.Target = enemy;
                actor.NextAttack = 0;
                actor.ProtectedUntil = 0;
                em.SetComponentData(soldier, actor);
                world.SetTime(new TimeData(1, .1f));
                world.GetOrCreateSystem<CombatSystem>().Update(world.Unmanaged);
                em.CompleteAllTrackedJobs();
                Check(em.GetComponentData<UnitAnimationSignals>(soldier).AttackSequence == 1, "Real combat attack produces one animation signal");
                Update();
                Check(Parameters().GetBoolParameter("DrawWeapon") && !Parameters().GetBoolParameter("Attack")
                    && em.GetComponentData<SoldierAnimationState>(soldier).Equipment == SoldierEquipmentState.DrawingSword
                    && em.GetComponentData<LocalTransform>(binding.TorchMount).Scale == 0,
                    "Near enemy starts draw and queues an early attack signal");
                Update(config.DrawSeconds * .6f);
                Check(em.GetComponentData<Parent>(binding.SwordMount).Value == binding.SwordHandSocket
                    && em.GetComponentData<LocalTransform>(binding.SwordMount).Scale == 1
                    && em.GetComponentData<LocalTransform>(binding.TorchMount).Scale == 0, "Draw midpoint shows the hand sword and hides the torch");
                Check(em.GetComponentObject<ParticleSystem>(binding.TorchFlame).isStopped
                    && !em.GetComponentObject<Light>(binding.TorchLight).enabled,
                    "Drawing the sword stops the baked torch flame and point light");
                Update(config.DrawSeconds);
                Check(Parameters().GetBoolParameter("Attack")
                    && em.GetComponentData<SoldierAnimationState>(soldier).Equipment == SoldierEquipmentState.Sword, "Queued attack plays after draw completes");
                Update();
                Check(!Parameters().GetBoolParameter("Attack"), "Attack does not retrigger on the next update");
                CombatOps.ApplyDamage(em, root, new DamageRequest { Source = enemy, Target = soldier, Amount = 1 });
                Update();
                Check(Parameters().GetBoolParameter("Hit"), "Actual effective damage drives the hit trigger");
                actor = em.GetComponentData<Combatant>(soldier);
                actor.Target = Entity.Null;
                em.SetComponentData(soldier, actor);
                em.SetComponentData(soldier, new Perception());
                Update(config.WeaponReleaseSeconds + .1f);
                Check(Parameters().GetBoolParameter("SheatheWeapon")
                    && em.GetComponentData<SoldierAnimationState>(soldier).Equipment == SoldierEquipmentState.SheathingSword, "Threat release delay starts one sheathe transition");
                Update(config.SheatheSeconds * .6f);
                Check(em.GetComponentData<LocalTransform>(binding.SwordMount).Scale == 0
                    && em.GetComponentData<LocalTransform>(binding.TorchMount).Scale == 0, "Sheathe midpoint hides both stored items during the transition");
                Update(config.SheatheSeconds);
                Check(em.GetComponentData<SoldierAnimationState>(soldier).Equipment == SoldierEquipmentState.Torch
                    && em.GetComponentData<LocalTransform>(binding.TorchMount).Scale == 1, "Sheathe completes by restoring the patrol torch");
                Check(em.GetComponentObject<ParticleSystem>(binding.TorchFlame).isPlaying
                    && em.GetComponentObject<Light>(binding.TorchLight).enabled,
                    "Sheathing the sword resumes the baked torch flame and point light");
                session.Phase = Phase.Retreat;
                em.SetComponentData(root, session);
                em.SetComponentData(soldier, new VisualState { Visible = 1, Celebrating = (byte)NightEndPose.Celebrate });
                Update();
                Check(Parameters().GetBoolParameter("Celebrate") && em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[binding.CelebrationLayer].weight == 1, "Closure activates the Rukhanka victory arms layer");
                Check(em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[0].weight == 1 && em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[binding.TorchLayer].weight == 0, "Celebration keeps the base body while overriding the torch arm pose");
                em.SetComponentData(soldier, new VisualState { Visible = 1 });
                session.Phase = Phase.Night;
                em.SetComponentData(root, session);
                Update();
                Check(!Parameters().GetBoolParameter("Celebrate") && em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[binding.CelebrationLayer].weight == 0, "Leaving closure releases the arms layer");
                var positionBefore = em.GetComponentData<LocalTransform>(soldier);
                sessionControl.Paused = 1;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                }

                Update();
                Check(em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[0].speed == 0, "Pause freezes the animation clock");
                Check(em.GetComponentData<LocalTransform>(soldier).Equals(positionBefore), "Animation never changes the gameplay position");
                sessionControl.Paused = 0;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                }

                Update(.1f, true);
                Check(em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[0].speed == 0, "Reduced-motion setting freezes animation");
                Update();
                Check(em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig)[0].speed == 1, "Resume restores animation speed");
                var health = em.GetComponentData<Health>(soldier);
                health.Current = 0;
                em.SetComponentData(soldier, health);
                Update();
                Check(Parameters().GetBoolParameter("Dead") && Visible(), "Previously visible dead unit starts its death presentation");
                var remaining = em.GetComponentData<SoldierAnimationState>(soldier).DeathRemaining;
                sessionControl.Paused = 1;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                }

                Update(10);
                Check(em.GetComponentData<SoldierAnimationState>(soldier).DeathRemaining == remaining && Visible(), "Pause preserves the death window");
                using var deadGroup = em.GetBuffer<LinkedEntityGroup>(view).ToNativeArray(Allocator.Temp);
                sessionControl.Paused = 0;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionNight);
                }

                Update(config.DeathSeconds + 1);
                Update();
                Check(em.GetComponentData<SoldierAnimationState>(soldier).View == Entity.Null && deadGroup.All(e => !em.Exists(e.Value)), "Death window releases the full view group including bones and weapon");
                var hidden = Spawn("militia", 0, default);
                em.SetComponentData(hidden, new VisualState());
                health = em.GetComponentData<Health>(hidden);
                health.Current = 0;
                em.SetComponentData(hidden, health);
                SoldierAnimationSystem.UpdateUnit(em, hidden, .1f, false);
                Check(em.GetComponentData<SoldierAnimationState>(hidden).DeathRemaining == 0, "Never-visible units do not create ghost corpses");
                Check(em.GetComponentData<SoldierAnimationState>(hidden).View == Entity.Null, "Never-visible death creates no rig, bones or render entities");
                em.DestroyEntity(soldier);
                var active = Spawn("militia", 0, default);
                SoldierAnimationSystem.UpdateUnit(em, active, .1f, false);
                var activeView = em.GetComponentData<SoldierAnimationState>(active).View;
                using var activeGroup = em.GetBuffer<LinkedEntityGroup>(activeView).ToNativeArray(Allocator.Temp);
                // Remove every animated gameplay entity to exercise the empty-unit-query cleanup path.
                using (var all = WorldQueries.Entities<SoldierAnimationState>(em))
                    foreach (var unit in all)
                        em.DestroyEntity(unit);
                world.GetOrCreateSystemManaged<SoldierAnimationSystem>().Update();
                Check(activeGroup.All(e => !em.Exists(e.Value)), "Orphan cleanup runs after the last gameplay soldier is destroyed");
                active = Spawn("militia", 0, default);
                SoldierAnimationSystem.UpdateUnit(em, active, .1f, false);
                activeView = em.GetComponentData<SoldierAnimationState>(active).View;
                using var sessionGroup = em.GetBuffer<LinkedEntityGroup>(activeView).ToNativeArray(Allocator.Temp);
                em.DestroyEntity(root);
                SimulationLifetimeSystem.Cleanup(em);
                Check(!em.Exists(active) && sessionGroup.All(e => !em.Exists(e.Value)) && SimulationLifetimeSystem.IsReleased(em), "Session cleanup releases logic and the entire independent view group");
                report.AppendLine("Assertions: " + count);
                return report.ToString();
            }
            catch (Exception error)
            {
                report.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/soldier-animation-verification.txt", report.ToString());
            }
        }

        static void VerifyPersistence(World world, Entity root, Action<bool, string> check)
        {
            var em = world.EntityManager;
            using var soldiers = WorldQueries.Entities<Soldier>(em);
            check(soldiers.Length > 0, "Real map provides persistent garrison soldiers");
            var unit = soldiers[0];
            var identity = em.GetComponentData<Identity>(unit);
            var data = em.GetComponentData<Soldier>(unit);
            var system = world.GetOrCreateSystemManaged<SoldierAnimationSystem>();
            void Update() => system.Update();
            void Deploy(bool value)
            {
                var actor = em.GetComponentData<Combatant>(unit);
                actor.Deployed = (byte)(value ? 1 : 0);
                em.SetComponentData(unit, actor);
                em.SetComponentData(unit, new VisualState { Visible = (byte)(value ? 1 : 0) });
            }

            Update();
            using var views = em.CreateEntityQuery(typeof(SoldierAnimationViewOwner));
            using var rigs = em.CreateEntityQuery(typeof(RigDefinitionComponent));
            check(views.CalculateEntityCount() == 0 && rigs.CalculateEntityCount() == 0, "Off-duty garrison creates zero runtime views or rigs");
            Deploy(true);
            var snapshot = SnapshotCodec.Capture(em, root);
            Update();
            var view = em.GetComponentData<SoldierAnimationState>(unit).View;
            check(em.Exists(view) && snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Creating a view does not change snapshot bytes");
            using var linked = em.GetBuffer<LinkedEntityGroup>(view).ToNativeArray(Allocator.Temp);
            foreach (var failure in new[]
            {
                "prepared",
                "root-published",
                "before-retire"
            }

            )
            {
                var threw = false;
                try
                {
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot), probe: at =>
                    {
                        if (at == failure)
                            throw new InvalidOperationException("probe");
                    });
                }
                catch (InvalidOperationException error)when (error.Message == "probe")
                {
                    threw = true;
                }

                Update();
                check(threw && em.Exists(unit) && em.GetComponentData<SoldierAnimationState>(unit).View == view && em.Exists(view), "Restore rollback preserves the existing view: " + failure);
            }

            Deploy(false);
            Update();
            check(linked.All(e => !em.Exists(e.Value)) && em.Exists(unit) && em.GetComponentData<Identity>(unit).Equals(identity) && em.GetComponentData<Soldier>(unit).Equals(data), "Returning releases the view while preserving identity, garrison and experience");
            em.SetComponentData(unit, new UnitAnimationSignals { AttackSequence = 7, HitSequence = 8 });
            Update();
            Deploy(true);
            Update();
            var nextView = em.GetComponentData<SoldierAnimationState>(unit).View;
            var rig = em.GetComponentData<SoldierAnimationBinding>(nextView).Rig;
            var parameters = new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(rig));
            check(nextView != view && !parameters.GetBoolParameter("Attack") && !parameters.GetBoolParameter("Hit"), "Redeployment creates a fresh view without replaying off-duty signals");
            snapshot = SnapshotCodec.Capture(em, root);
            using var replaced = em.GetBuffer<LinkedEntityGroup>(nextView).ToNativeArray(Allocator.Temp);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot));
            Update();
            check(!em.Exists(unit) && replaced.All(e => !em.Exists(e.Value)), "Successful restore cleans the old independent view before animation");
            check(snapshot.SequenceEqual(SnapshotCodec.Capture(em, root)), "Visual reconstruction leaves restored snapshot bytes unchanged");
            using var restored = WorldQueries.Entities<Soldier>(em);
            foreach (var soldier in restored)
            {
                var actor = em.GetComponentData<Combatant>(soldier);
                check((em.GetComponentData<SoldierAnimationState>(soldier).View != Entity.Null) == (actor.Deployed != 0 && em.GetComponentData<VisualState>(soldier).Visible != 0), "Restored model existence follows restored deployment and visibility");
                actor.Deployed = 0;
                em.SetComponentData(soldier, actor);
            }

            Update();
        }
    }
}
#endif
