using Landsong.Animation;
using Sirenix.OdinInspector;
using Rukhanka;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.AnimationPreview
{
    public struct AnimationPreviewSettings : IComponentData
    {
        public Entity Prefab;
        public int Count, Spawned, SlotOffset;
    }
    public struct AnimationPreviewUnit : IComponentData
    {
        public int Slot, Cycle;
        public float StartedAt;
        public byte Ready;
    }

    public sealed class AnimationPreviewAuthoring : MonoBehaviour
    {
        [LabelText("士兵预制体"), Required] public GameObject SoldierPrefab;
        [LabelText("预览单位数量"), Range(1, 200)] public int Count = 9;
        sealed class Baker : Baker<AnimationPreviewAuthoring>
        {
            public override void Bake(AnimationPreviewAuthoring source)
            {
                AddComponent(GetEntity(TransformUsageFlags.None), new AnimationPreviewSettings {
                    Prefab = GetEntity(source.SoldierPrefab, TransformUsageFlags.Dynamic), Count = source.Count, SlotOffset = 0
                });
            }
        }
    }

    // This development-only scene has no gameplay Session and never changes a saved dynasty.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
    public partial class AnimationPreviewSystem : SystemBase
    {
        // Set by the owned Play verification before the SubScene's first frame.
        public static bool StartHiddenForVerification;
        public static bool SuppressPopulationForVerification;
        EntityQuery settingsQuery, unitQuery;
        static readonly FastAnimatorParameter Speed = new FastAnimatorParameter("Speed");
        static readonly FastAnimatorParameter LocomotionRate = new FastAnimatorParameter("LocomotionRate");
        static readonly FastAnimatorParameter Alerted = new FastAnimatorParameter("Alerted");
        static readonly FastAnimatorParameter Equipment = new FastAnimatorParameter("Equipment");
        static readonly FastAnimatorParameter Dead = new FastAnimatorParameter("Dead");
        static readonly FastAnimatorParameter DrawWeapon = new FastAnimatorParameter("DrawWeapon");
        static readonly FastAnimatorParameter SheatheWeapon = new FastAnimatorParameter("SheatheWeapon");
        static readonly FastAnimatorParameter AttackSpeed = new FastAnimatorParameter("AttackSpeed");
        static readonly FastAnimatorParameter Attack = new FastAnimatorParameter("Attack");
        static readonly FastAnimatorParameter Hit = new FastAnimatorParameter("Hit");
        protected override void OnCreate()
        {
            settingsQuery = GetEntityQuery(typeof(AnimationPreviewSettings));
            unitQuery = GetEntityQuery(typeof(AnimationPreviewUnit), typeof(SoldierAnimationBinding));
        }
        protected override void OnUpdate()
        {
            if (SuppressPopulationForVerification) return;
            if (settingsQuery.IsEmptyIgnoreFilter) return;
            var em = EntityManager;
            em.CompleteAllTrackedJobs();
            var elapsed = (float)SystemAPI.Time.ElapsedTime;
            var configEntity = settingsQuery.GetSingletonEntity();
            var config = em.GetComponentData<AnimationPreviewSettings>(configEntity);
            if (config.Spawned != config.Count)
            {
                using var old = unitQuery.ToEntityArray(Allocator.Temp);
                foreach (var entity in old) em.DestroyEntity(entity);
                int columns = config.Count == 1 ? 1 : config.Count <= 9 ? 3 : (int)math.ceil(math.sqrt(config.Count));
                for (int i = 0; i < config.Count; i++)
                {
                    var unit = em.Instantiate(config.Prefab);
                    em.SetComponentData(unit, LocalTransform.FromPositionRotation(
                        new float3((i % columns - (columns - 1) * .5f) * 2.6f, .5f, i / columns * 3.2f), quaternion.RotateY(math.PI)));
                    em.AddComponentData(unit, new AnimationPreviewUnit {
                        Slot = (i + config.SlotOffset) % 9, Cycle = -1, StartedAt = elapsed, Ready = 0
                    });
                    if (StartHiddenForVerification)
                    {
                        SoldierAnimationSystem.CacheRenderers(em, unit);
                        SoldierAnimationSystem.SetRenderersVisible(em, unit, false);
                    }
                }
                config.Spawned = config.Count; em.SetComponentData(configEntity, config);
            }
            using var units = unitQuery.ToEntityArray(Allocator.Temp);
            foreach (var unit in units)
            {
                var preview = em.GetComponentData<AnimationPreviewUnit>(unit);
                var binding = em.GetComponentData<SoldierAnimationBinding>(unit);
                var rig = binding.Rig;
                if (!em.HasBuffer<AnimatorControllerParameterComponent>(rig)) continue;
                if (preview.Ready == 0)
                {
                    // Let Rukhanka initialize a newly instantiated controller before
                    // sending one-shot triggers; initialization clears trigger values.
                    preview.StartedAt = elapsed;
                    preview.Cycle = -1;
                    preview.Ready = 1;
                    em.SetComponentData(unit, preview);
                    continue;
                }
                var parameters = new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(rig));
                parameters.SetFloatParameter(Speed, preview.Slot == 1 || preview.Slot == 6 ? 1.25f : preview.Slot == 2 ? 2.5f : 0);
                parameters.SetFloatParameter(LocomotionRate, 1);
                parameters.SetBoolParameter(Alerted, preview.Slot == 2 || preview.Slot == 3 || preview.Slot == 7);
                parameters.SetBoolParameter(Dead, preview.Slot == 5);
                parameters.SetFloatParameter(AttackSpeed, 1);
                parameters.ResetTrigger(Attack);
                parameters.ResetTrigger(Hit);
                bool equipment = binding.TorchLayer != 0;
                if (equipment)
                {
                    parameters.ResetTrigger(DrawWeapon);
                    parameters.ResetTrigger(SheatheWeapon);
                }
                var localElapsed = elapsed - preview.StartedAt;
                var cycle = (int)(localElapsed / 3f);
                var phase = localElapsed - cycle * 3f;
                var drawing = preview.Slot == 7 && phase < 1.5f;
                var sheathing = preview.Slot == 8 && phase < 1.667f;
                var swordInHand = preview.Slot == 3 || preview.Slot == 8 && phase < .84f || preview.Slot == 7 && phase >= .75f;
                var torchVisible = preview.Slot != 3 && preview.Slot != 5 && preview.Slot != 7
                    && (preview.Slot != 8 || phase >= 1.667f);
                var transition = drawing || sheathing;
                if (equipment) parameters.SetIntParameter(Equipment, drawing ? (int)SoldierEquipmentState.DrawingSword
                    : sheathing ? (int)SoldierEquipmentState.SheathingSword
                    : swordInHand ? (int)SoldierEquipmentState.Sword : (int)SoldierEquipmentState.Torch);
                SoldierAnimationSystem.SetEquipmentVisual(em, binding, swordInHand, torchVisible,
                    transition || preview.Slot == 4 || preview.Slot == 5);
                if (cycle != preview.Cycle)
                {
                    if (preview.Slot == 3) parameters.SetTrigger(Attack);
                    if (preview.Slot == 4) parameters.SetTrigger(Hit);
                    if (equipment && preview.Slot == 7) parameters.SetTrigger(DrawWeapon);
                    if (equipment && preview.Slot == 8) parameters.SetTrigger(SheatheWeapon);
                    preview.Cycle = cycle; em.SetComponentData(unit, preview);
                }
            }
        }
    }
}
