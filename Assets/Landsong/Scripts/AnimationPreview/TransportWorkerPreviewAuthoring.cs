using Landsong.Animation;
using Rukhanka;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.AnimationPreview
{
    public struct TransportWorkerPreviewSettings : IComponentData { public Entity Male, Female; public int Mode, SpawnedMode; }
    public struct TransportWorkerPreviewUnit : IComponentData { public byte Ready; }
    public sealed class TransportWorkerPreviewAuthoring : MonoBehaviour
    {
        [LabelText("男性工人表现"), Required] public GameObject MaleView;
        [LabelText("女性工人表现"), Required] public GameObject FemaleView;
        [LabelText("动作模式"), Tooltip("0 抱物待机，1 抱物行走，2 空手行走，3 拿起，4 放下，5 死亡"), Range(0, 5)] public int Mode = 1;
        sealed class Baker : Baker<TransportWorkerPreviewAuthoring>
        {
            public override void Bake(TransportWorkerPreviewAuthoring source) => AddComponent(GetEntity(TransformUsageFlags.None),
                new TransportWorkerPreviewSettings { Male = GetEntity(source.MaleView, TransformUsageFlags.Dynamic), Female = GetEntity(source.FemaleView, TransformUsageFlags.Dynamic), Mode = source.Mode, SpawnedMode = -1 });
        }
    }

    // View-only development scene; no Session, inventory, population, or gameplay entities.
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
    public partial class TransportWorkerPreviewSystem : SystemBase
    {
        static readonly FastAnimatorParameter Speed = new FastAnimatorParameter("Speed"), Rate = new FastAnimatorParameter("LocomotionRate"), Dead = new FastAnimatorParameter("Dead"), Handling = new FastAnimatorParameter("Handling");
        protected override void OnUpdate()
        {
            using var settingsQuery = EntityManager.CreateEntityQuery(typeof(TransportWorkerPreviewSettings));
            if (settingsQuery.IsEmptyIgnoreFilter) return;
            EntityManager.CompleteAllTrackedJobs();
            var em = EntityManager; var entity = settingsQuery.GetSingletonEntity(); var settings = em.GetComponentData<TransportWorkerPreviewSettings>(entity);
            using var unitsQuery = EntityManager.CreateEntityQuery(typeof(TransportWorkerPreviewUnit));
            if (settings.SpawnedMode != settings.Mode)
            {
                using (var old = unitsQuery.ToEntityArray(Allocator.Temp))
                    foreach (var view in old) em.DestroyEntity(view);
                for (int i = 0; i < 2; i++)
                {
                    var view = em.Instantiate(i == 0 ? settings.Male : settings.Female);
                    em.AddComponent<TransportWorkerPreviewUnit>(view);
                    em.SetComponentData(view, LocalTransform.FromPositionRotation(new float3(i == 0 ? -.85f : .85f, .5f, 0), quaternion.RotateY(math.PI)));
                }
                settings.SpawnedMode = settings.Mode; em.SetComponentData(entity, settings);
                return;
            }
            using var units = unitsQuery.ToEntityArray(Allocator.Temp);
            foreach (var view in units)
            {
                var ready = em.GetComponentData<TransportWorkerPreviewUnit>(view);
                if (ready.Ready == 0) { ready.Ready = 1; em.SetComponentData(view, ready); continue; }
                var rig = em.GetComponentData<SoldierAnimationBinding>(view).Rig;
                if (!em.HasBuffer<AnimatorControllerParameterComponent>(rig)) continue;
                var parameters = new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(rig));
                parameters.SetFloatParameter(Speed, settings.Mode == 1 || settings.Mode == 2 ? 1.6f : 0);
                parameters.SetFloatParameter(Rate, 1);
                parameters.SetBoolParameter(Dead, settings.Mode == 5);
                parameters.SetIntParameter(Handling, settings.Mode == 3 ? 1 : settings.Mode == 4 ? 2 : 0);
                var layers = em.GetBuffer<AnimatorControllerLayerComponent>(rig);
                var carry = layers[1]; carry.weight = settings.Mode == 0 || settings.Mode == 1 ? 1 : 0; layers[1] = carry;
                var mount = em.GetComponentData<TransportCargoBinding>(view).Mount;
                var transform = em.GetComponentData<LocalTransform>(mount); transform.Scale = settings.Mode == 2 || settings.Mode == 5 ? 0 : 1; em.SetComponentData(mount, transform);
            }
        }
    }
}
