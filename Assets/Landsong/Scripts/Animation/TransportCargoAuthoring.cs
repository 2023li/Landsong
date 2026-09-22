using Landsong.ECS;
using Rukhanka;
using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.Animation
{
    public struct TransportCargoBinding : IComponentData { public Entity Mount; public byte CarryLayer; }

    [DisallowMultipleComponent]
    public sealed class TransportCargoAuthoring : MonoBehaviour
    {
        [LabelText("手部物资挂点"), Required] public Transform Mount;
        [LabelText("抱物双臂动画层")] public byte CarryLayer = 1;
        sealed class Baker : Baker<TransportCargoAuthoring>
        {
            public override void Bake(TransportCargoAuthoring source)
            {
                if (source.Mount == null) throw new System.InvalidOperationException("运输工人缺少物资挂点。");
                AddComponent(GetEntity(TransformUsageFlags.Dynamic), new TransportCargoBinding { Mount = GetEntity(source.Mount.gameObject, TransformUsageFlags.Dynamic), CarryLayer = source.CarryLayer });
            }
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    [UpdateAfter(typeof(SoldierAnimationSystem))]
    [UpdateBefore(typeof(RukhankaAnimationSystemGroup))]
    public partial class TransportWorkerAnimationSystem : SystemBase
    {
        static readonly FastAnimatorParameter Handling = new FastAnimatorParameter("Handling");
        protected override void OnUpdate()
        {
            EntityManager.CompleteAllTrackedJobs();
            using var units = WorldQueries.Entities<TransportWorker>(EntityManager);
            foreach (var entity in units) UpdateVisual(EntityManager, entity);
        }

        public static void UpdateVisual(EntityManager em, Entity entity)
        {
            if (!em.HasComponent<SoldierAnimationState>(entity)) return;
            var state = em.GetComponentData<SoldierAnimationState>(entity);
            if (!em.HasComponent<TransportCargoBinding>(state.View)) return;
            var worker = em.GetComponentData<TransportWorker>(entity);
            var binding = em.GetComponentData<TransportCargoBinding>(state.View);
            var animation = em.GetComponentData<SoldierAnimationBinding>(state.View);
            bool alive = EntityState.Alive(em, entity);
            var settings = em.GetComponentData<TransportWorkerSettings>(em.GetComponentData<SimulationOwner>(entity).Root);
            bool loading = worker.Stage == TransportStage.Loading, unloading = worker.Stage == TransportStage.Unloading;
            bool cargo = alive && (loading ? worker.Remaining < settings.LoadSeconds * .5f
                : unloading ? worker.Remaining > settings.UnloadSeconds * .5f : worker.Carrying != 0);
            if (em.HasComponent<LocalTransform>(binding.Mount))
            {
                var transform = em.GetComponentData<LocalTransform>(binding.Mount);
                transform.Scale = cargo ? 1 : 0;
                em.SetComponentData(binding.Mount, transform);
            }
            if (!em.HasBuffer<AnimatorControllerParameterComponent>(animation.Rig)) return;
            var parameters = new AnimatorParametersAspect(em.GetBuffer<AnimatorControllerParameterComponent>(animation.Rig), em.GetComponentData<AnimatorControllerParameterIndexTableComponent>(animation.Rig));
            parameters.SetIntParameter(Handling, !alive ? 0 : loading ? 1 : unloading ? 2 : 0);
            var layers = em.GetBuffer<AnimatorControllerLayerComponent>(animation.Rig);
            if (binding.CarryLayer < layers.Length)
            {
                var layer = layers[binding.CarryLayer];
                layer.weight = alive && worker.Carrying != 0 && !loading && !unloading && state.PoseOverrideRemaining <= 0 ? 1 : 0;
                layers[binding.CarryLayer] = layer;
            }
        }
    }
}
