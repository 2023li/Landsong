using Landsong.ECS;
using Landsong.ECS.Presentation;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.Animation
{
    // One position owner: navigation moves the gameplay entity, its animated child follows.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(CombatResolutionGroup))]
    [UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
    public partial class SoldierAnimationSystem : SystemBase
    {
        EntityQuery units, views;
        static readonly FastAnimatorParameter Speed = new FastAnimatorParameter("Speed");
        static readonly FastAnimatorParameter LocomotionRate = new FastAnimatorParameter("LocomotionRate");
        static readonly FastAnimatorParameter Alerted = new FastAnimatorParameter("Alerted");
        static readonly FastAnimatorParameter Equipment = new FastAnimatorParameter("Equipment");
        static readonly FastAnimatorParameter Celebrate = new FastAnimatorParameter("Celebrate");
        static readonly FastAnimatorParameter Dead = new FastAnimatorParameter("Dead");
        static readonly FastAnimatorParameter DrawWeapon = new FastAnimatorParameter("DrawWeapon");
        static readonly FastAnimatorParameter SheatheWeapon = new FastAnimatorParameter("SheatheWeapon");
        static readonly FastAnimatorParameter Attack = new FastAnimatorParameter("Attack");
        static readonly FastAnimatorParameter Shoot = new FastAnimatorParameter("Shoot");
        static readonly FastAnimatorParameter Hit = new FastAnimatorParameter("Hit");
        static readonly FastAnimatorParameter AttackSpeed = new FastAnimatorParameter("AttackSpeed");
        protected override void OnCreate()
        {
            units = GetEntityQuery(typeof(SoldierAnimationPrefab), typeof(SoldierAnimationState), typeof(UnitAnimationSignals), typeof(Combatant), typeof(Health), typeof(SimulationOwner), typeof(LocalTransform));
            views = GetEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<SoldierAnimationViewOwner>() }, Options = EntityQueryOptions.IncludeDisabledEntities });
        }

        protected override void OnUpdate()
        {
            if (units.IsEmptyIgnoreFilter && views.IsEmptyIgnoreFilter)
                return;
            EntityManager.CompleteAllTrackedJobs();
            // Run even after the last soldier was destroyed (including snapshot replacement).
            using var instances = views.ToEntityArray(Allocator.Temp);
            foreach (var view in instances)
            {
                var unit = EntityManager.GetComponentData<SoldierAnimationViewOwner>(view).Unit;
                if (!EntityManager.HasComponent<SoldierAnimationState>(unit) || EntityManager.GetComponentData<SoldierAnimationState>(unit).View != view)
                    EntityManager.DestroyEntity(view);
            }

            using var entities = units.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
                UpdateUnit(EntityManager, entity, SystemAPI.Time.DeltaTime, InterfaceSettings.Current.ReducedMotion);
        }

        // Also exercised by isolated-world verification using actual baked prefab instances.
        public static void UpdateUnit(EntityManager em, Entity entity, float deltaTime, bool reduced)
        {
            var config = em.GetComponentData<SoldierAnimationPrefab>(entity);
            bool usesEquipment = config.Profile == UnitAnimationProfile.SwordAndTorch;
            var state = em.GetComponentData<SoldierAnimationState>(entity);
            if (!em.Exists(state.View))
                state.View = Entity.Null;
            var signal = em.GetComponentData<UnitAnimationSignals>(entity);
            var owner = em.GetComponentData<SimulationOwner>(entity).Root;
            var validSession = em.Exists(owner) && em.HasComponent<Session>(owner) && em.HasComponent<SimulationReady>(owner);
            SimulationControl sessionControl = validSession ? em.GetComponentData<SimulationControl>(owner) : default(SimulationControl);
            NightRuntimeState sessionNight = validSession ? em.GetComponentData<NightRuntimeState>(owner) : default(NightRuntimeState);
            var actor = em.GetComponentData<Combatant>(entity);
            var transform = em.GetComponentData<LocalTransform>(entity);
            var dead = em.GetComponentData<Health>(entity).Current <= 0 || em.HasComponent<Landsong.ECS.Dead>(entity) && em.IsComponentEnabled<Landsong.ECS.Dead>(entity);
            var paused = !validSession || sessionControl.Paused != 0;
            var phase = validSession ? em.GetComponentData<Session>(owner).Phase : Phase.Day;
            var celebrating = validSession && (phase == Phase.Retreat || phase == Phase.Celebration) && !dead && em.HasComponent<VisualState>(entity) && em.GetComponentData<VisualState>(entity).Celebrating == (byte)NightEndPose.Celebrate;
            var timeScale = validSession && em.GetComponentData<Session>(owner).Phase != Phase.Day && sessionNight.Kind == NightKind.Peaceful && sessionNight.Speed == 2 ? 2f : 1f;
            var dt = paused ? 0 : math.max(0, deltaTime) * timeScale;
            var visible = validSession && actor.Deployed != 0 && (!em.HasComponent<VisualState>(entity) || em.GetComponentData<VisualState>(entity).Visible != 0);
            if (dead)
            {
                if (state.Dying == 0)
                {
                    state.Dying = 1;
                    state.DeathRemaining = state.View != Entity.Null && state.WasVisible != 0 ? config.DeathSeconds : 0;
                }

                visible = validSession && state.DeathRemaining > 0;
                state.DeathRemaining = math.max(0, state.DeathRemaining - dt);
            }
            else
            {
                state.Dying = 0;
                state.DeathRemaining = 0;
            }

            if (visible && !dead && state.View == Entity.Null)
            {
                state.View = em.Instantiate(config.Prefab);
                em.AddComponentData(state.View, new Parent { Value = entity });
                em.SetComponentData(state.View, LocalTransform.Identity);
                em.AddComponentData(state.View, new SoldierAnimationViewOwner { Unit = entity });
                em.AddComponentData(state.View, new SimulationOwner { Root = owner });
                CacheRenderers(em, state.View);
                state.Initialized = 0;
                state.Speed = 0;
            }
            else if (!visible && state.View != Entity.Null)
            {
                em.DestroyEntity(state.View);
                state.View = Entity.Null;
                state.Speed = 0;
            }

            if (dt > 0)
            {
                var measured = state.Initialized == 0 || state.WasVisible == 0 ? 0 : math.distance(transform.Position.xz, state.Position.xz) / dt;
                // Teleports / deployment must not produce a one-frame sprint spike.
                measured = math.min(measured, math.max(.1f, actor.Speed * 1.5f));
                state.Speed = math.lerp(state.Speed, dead ? 0 : measured, 1 - math.exp(-15 * dt));
                state.PoseOverrideRemaining = math.max(0, state.PoseOverrideRemaining - dt);
            }

            var perception = em.HasComponent<Perception>(entity) ? em.GetComponentData<Perception>(entity) : default;
            var order = em.HasComponent<UnitOrder>(entity) ? em.GetComponentData<UnitOrder>(entity) : default;
            var target = Alive(em, actor.Target) ? actor.Target : Alive(em, perception.Enemy) ? perception.Enemy : Entity.Null;
            var activeBell = validSession && em.HasComponent<BellState>(owner) ? em.GetComponentData<BellState>(owner).ActiveBell : 0;
            var bellAlert = activeBell != 0 && order.Kind == OrderKind.Rally && order.Source == activeBell;
            if (target != Entity.Null || bellAlert)
                state.AlertRemaining = math.max(.01f, config.AlertReleaseSeconds);
            else if (dt > 0)
                state.AlertRemaining = math.max(0, state.AlertRemaining - dt);
            var alerted = !dead && config.Profile != UnitAnimationProfile.TransportWorker && state.AlertRemaining > 0;
            var nearEnemy = target != Entity.Null
                && math.distance(transform.Position.xz, em.GetComponentData<LocalTransform>(target).Position.xz)
                    <= math.max(.5f, actor.Range + config.DrawDistancePadding);
            if (nearEnemy)
                state.WeaponRemaining = math.max(.01f, config.WeaponReleaseSeconds);
            else if (dt > 0)
                state.WeaponRemaining = math.max(0, state.WeaponRemaining - dt);

            var drawStarted = false;
            var sheatheStarted = false;
            if (!usesEquipment)
                state.Equipment = SoldierEquipmentState.Sword;
            if (usesEquipment && visible && !dead && !paused && !celebrating)
            {
                switch (state.Equipment)
                {
                    case SoldierEquipmentState.Torch when nearEnemy:
                        state.Equipment = SoldierEquipmentState.DrawingSword;
                        state.EquipmentRemaining = math.max(.01f, config.DrawSeconds);
                        drawStarted = true;
                        break;
                    case SoldierEquipmentState.DrawingSword:
                        state.EquipmentRemaining = math.max(0, state.EquipmentRemaining - dt);
                        if (state.EquipmentRemaining <= 0)
                            state.Equipment = SoldierEquipmentState.Sword;
                        break;
                    case SoldierEquipmentState.Sword when !nearEnemy && state.WeaponRemaining <= 0:
                        state.Equipment = SoldierEquipmentState.SheathingSword;
                        state.EquipmentRemaining = math.max(.01f, config.SheatheSeconds);
                        sheatheStarted = true;
                        break;
                    case SoldierEquipmentState.SheathingSword:
                        state.EquipmentRemaining = math.max(0, state.EquipmentRemaining - dt);
                        if (state.EquipmentRemaining <= 0)
                            state.Equipment = SoldierEquipmentState.Torch;
                        break;
                }
            }

            state.Position = transform.Position;
            state.Initialized = 1;
            state.WasVisible = (byte)(visible ? 1 : 0);
            var binding = state.View == Entity.Null ? default : em.GetComponentData<SoldierAnimationBinding>(state.View);
            if (em.Exists(binding.Rig) && em.HasBuffer<AnimatorControllerParameterComponent>(binding.Rig))
            {
                var parameters = new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(binding.Rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(binding.Rig));
                parameters.SetFloatParameter(Speed, state.Speed);
                parameters.SetFloatParameter(LocomotionRate, state.Speed < .15f ? 1 : math.clamp(state.Speed / (alerted ? 2.5f : 1.25f), .5f, 2.25f));
                parameters.SetBoolParameter(Alerted, alerted);
                parameters.SetBoolParameter(Dead, dead);
                parameters.SetBoolParameter(Celebrate, celebrating);
                parameters.SetFloatParameter(AttackSpeed, config.AttackSeconds / math.max(.05f, actor.Interval));
                if (usesEquipment)
                {
                    parameters.SetIntParameter(Equipment, (int)state.Equipment);
                    parameters.ResetTrigger(DrawWeapon);
                    parameters.ResetTrigger(SheatheWeapon);
                }
                parameters.ResetTrigger(Attack);
                if (usesEquipment)
                    parameters.ResetTrigger(Shoot);
                parameters.ResetTrigger(Hit);
                if (drawStarted)
                    parameters.SetTrigger(DrawWeapon);
                if (sheatheStarted)
                    parameters.SetTrigger(SheatheWeapon);
                if (visible && !paused && !reduced && !dead && !celebrating)
                {
                    if (signal.AttackSequence != state.AttackSequence && state.Equipment == SoldierEquipmentState.Sword)
                    {
                        if (em.HasComponent<Soldier>(entity) && em.GetComponentData<Soldier>(entity).Weapon == SoldierWeaponKind.Bow)
                            parameters.SetTrigger(Shoot);
                        else
                            parameters.SetTrigger(Attack);
                        state.AttackSequence = signal.AttackSequence;
                    }
                    // Avoid a flinch cancelling every attack during sustained combat.
                    else if (signal.HitSequence != state.HitSequence)
                    {
                        parameters.SetTrigger(Hit);
                        state.PoseOverrideRemaining = math.max(state.PoseOverrideRemaining, config.HitSeconds);
                    }
                }

                if (!visible || paused || reduced || dead || celebrating)
                    state.AttackSequence = signal.AttackSequence;
                state.HitSequence = signal.HitSequence;

                var layers = em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig);
                var torchVisible = usesEquipment && TorchVisible(state, dead);
                var suppressHandPose = dead || state.PoseOverrideRemaining > 0
                    || state.Equipment == SoldierEquipmentState.DrawingSword
                    || state.Equipment == SoldierEquipmentState.SheathingSword;
                for (var i = 0; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    layer.speed = paused || reduced || !visible ? 0 : timeScale;
                    layers[i] = layer;
                }

                var switchingWeapon = usesEquipment && !dead && !celebrating && state.PoseOverrideRemaining <= 0
                    && (state.Equipment == SoldierEquipmentState.DrawingSword || state.Equipment == SoldierEquipmentState.SheathingSword);
                var weaponKind = em.HasComponent<Soldier>(entity) ? em.GetComponentData<Soldier>(entity).Weapon : SoldierWeaponKind.Sword;
                SetEquipmentVisual(em, binding, SwordVisible(state, config), torchVisible, suppressHandPose, celebrating, paused, switchingWeapon, (byte)weaponKind);

                var rigTransform = em.GetComponentData<LocalTransform>(binding.Rig);
                rigTransform.Rotation = quaternion.identity;
                if (!dead && state.Speed < .15f && em.Exists(actor.Target) && em.HasComponent<LocalTransform>(actor.Target))
                {
                    var direction = em.GetComponentData<LocalTransform>(actor.Target).Position - transform.Position;
                    direction.y = 0;
                    if (math.lengthsq(direction) > .001f)
                        rigTransform.Rotation = math.mul(math.inverse(transform.Rotation), quaternion.LookRotationSafe(direction, math.up()));
                }

                em.SetComponentData(binding.Rig, rigTransform);
            }

            else
            {
                state.AttackSequence = signal.AttackSequence;
                state.HitSequence = signal.HitSequence;
            }
            em.SetComponentData(entity, state);
        }

        static bool Alive(EntityManager em, Entity entity)
            => entity != Entity.Null && em.Exists(entity) && em.HasComponent<Health>(entity)
                && em.GetComponentData<Health>(entity).Current > 0 && em.HasComponent<LocalTransform>(entity);

        static bool SwordVisible(in SoldierAnimationState state, in SoldierAnimationPrefab config)
            => state.Equipment == SoldierEquipmentState.Sword
                || state.Equipment == SoldierEquipmentState.DrawingSword && state.EquipmentRemaining <= config.DrawSeconds * .5f
                || state.Equipment == SoldierEquipmentState.SheathingSword && state.EquipmentRemaining > config.SheatheSeconds * .5f;

        static bool TorchVisible(in SoldierAnimationState state, bool dead)
            => !dead && state.Equipment == SoldierEquipmentState.Torch;

        public static void SetEquipmentVisual(EntityManager em, in SoldierAnimationBinding binding,
            bool swordVisible, bool torchVisible, bool suppressHandPose, bool celebrating = false, bool paused = false, bool switchingWeapon = false,
            byte weaponKind = (byte)SoldierWeaponKind.Sword)
        {
            if (em.Exists(binding.SwordMount) && em.HasComponent<Parent>(binding.SwordMount))
            {
                var current = em.GetComponentData<Parent>(binding.SwordMount);
                if (current.Value != binding.SwordHandSocket)
                {
                    current.Value = binding.SwordHandSocket;
                    em.SetComponentData(binding.SwordMount, current);
                    em.SetComponentData(binding.SwordMount, LocalTransform.Identity);
                }
            }
            if (em.Exists(binding.SwordMount) && em.HasComponent<LocalTransform>(binding.SwordMount))
            {
                var sword = em.GetComponentData<LocalTransform>(binding.SwordMount);
                sword.Scale = swordVisible && weaponKind == (byte)SoldierWeaponKind.Sword ? 1 : 0;
                em.SetComponentData(binding.SwordMount, sword);
            }
            if (em.Exists(binding.ClubMount) && em.HasComponent<LocalTransform>(binding.ClubMount))
            {
                var club = em.GetComponentData<LocalTransform>(binding.ClubMount);
                club.Scale = swordVisible && weaponKind == (byte)SoldierWeaponKind.Club ? 1 : 0;
                em.SetComponentData(binding.ClubMount, club);
            }
            if (em.Exists(binding.BowMount) && em.HasComponent<LocalTransform>(binding.BowMount))
            {
                var bow = em.GetComponentData<LocalTransform>(binding.BowMount);
                bow.Scale = swordVisible && weaponKind == (byte)SoldierWeaponKind.Bow ? 1 : 0;
                em.SetComponentData(binding.BowMount, bow);
            }
            if (em.Exists(binding.TorchMount) && em.HasComponent<LocalTransform>(binding.TorchMount))
            {
                var torch = em.GetComponentData<LocalTransform>(binding.TorchMount);
                torch.Scale = torchVisible ? 1 : 0;
                em.SetComponentData(binding.TorchMount, torch);
            }
            if (em.Exists(binding.TorchFlame) && em.HasComponent<ParticleSystem>(binding.TorchFlame))
            {
                var particles = em.GetComponentObject<ParticleSystem>(binding.TorchFlame);
                if (!torchVisible && !particles.isStopped)
                    particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                else if (torchVisible && paused && !particles.isPaused)
                    particles.Pause(true);
                else if (torchVisible && !paused && !particles.isPlaying)
                    particles.Play(true);
            }
            if (em.Exists(binding.TorchLight) && em.HasComponent<Light>(binding.TorchLight))
                em.GetComponentObject<Light>(binding.TorchLight).enabled = torchVisible;
            if (em.Exists(binding.Rig) && em.HasBuffer<AnimatorControllerLayerComponent>(binding.Rig))
            {
                var layers = em.GetBuffer<AnimatorControllerLayerComponent>(binding.Rig);
                for (var i = 1; i < layers.Length; i++)
                {
                    var layer = layers[i];
                    layer.weight = i == binding.CelebrationLayer && celebrating ? 1
                        : i == binding.WeaponLayer && switchingWeapon && !celebrating ? 1
                        : i == binding.TorchLayer && torchVisible && !suppressHandPose && !celebrating ? 1 : 0;
                    layers[i] = layer;
                }
            }
        }

        public static void CacheRenderers(EntityManager em, Entity entity)
        {
            // Include extra render entities generated for multi-material meshes.
            using var linked = em.GetBuffer<LinkedEntityGroup>(entity).ToNativeArray(Allocator.Temp);
            var cached = em.GetBuffer<SoldierAnimationRenderer>(entity);
            cached.Clear();
            foreach (var child in linked)
                if (em.HasComponent<MaterialMeshInfo>(child.Value))
                    cached.Add(new SoldierAnimationRenderer { Entity = child.Value });
        }

        public static void SetRenderersVisible(EntityManager em, Entity entity, bool visible)
        {
            // Structural changes invalidate DynamicBuffers, so retain entity IDs in a temporary copy.
            using var renderers = em.GetBuffer<SoldierAnimationRenderer>(entity).ToNativeArray(Allocator.Temp);
            foreach (var renderer in renderers)
            {
                var target = renderer.Entity;
                if (!em.HasComponent<MaterialMeshInfo>(target))
                    continue;
                // Rukhanka gathers new meshes through enabled MaterialMeshInfo, but its skinning
                // query also includes hidden meshes. Keep registration available even before deployment.
                em.SetComponentEnabled<MaterialMeshInfo>(target, true);
                var hidden = em.HasComponent<DisableRendering>(target);
                if (visible && hidden)
                    em.RemoveComponent<DisableRendering>(target);
                else if (!visible && !hidden)
                    em.AddComponent<DisableRendering>(target);
            }
        }
    }
}
