using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    public struct VisualOwner : IComponentData
    {
        public Entity Owner, Slot;
        public float4 OriginalColor;
        public int HarvestRemaining;
        public Unity.Collections.FixedString128Bytes CropId;
        public float GrowthFrom, GrowthTo;
        public byte OperationalOnly;
    }
    public sealed class EntityVisualAuthoring : MonoBehaviour
    {
        public GameObject Owner;
        sealed class Baker : Baker<EntityVisualAuthoring>
        {
            public override void Bake(EntityVisualAuthoring authoring)
            {
                var renderer = GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    var owner = GetEntity(authoring.Owner == null ? authoring.gameObject : authoring.Owner, TransformUsageFlags.Dynamic);
                    var entity = GetEntity(TransformUsageFlags.Dynamic);
                    var color = renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty("_BaseColor") ? renderer.sharedMaterial.GetColor("_BaseColor") : Color.white;
                    var slot = GetComponentInParent<BuildingVisualSlotAuthoring>(); var part = GetComponentInParent<BuildingVisualPartAuthoring>();
                    AddComponent(entity, new VisualOwner { Owner = owner, Slot = slot == null ? Entity.Null : GetEntity(slot.gameObject, TransformUsageFlags.Dynamic), OriginalColor = new float4(color.r, color.g, color.b, color.a), HarvestRemaining = part == null ? 0 : part.HarvestRemaining, CropId = new Unity.Collections.FixedString128Bytes(part == null ? "" : part.CropId ?? ""), GrowthFrom = part == null ? 0 : part.GrowthFrom, GrowthTo = part == null ? 1 : part.GrowthTo, OperationalOnly = (byte)(part != null && part.OperationalOnly ? 1 : 0) });
                    AddComponent(entity, new URPMaterialPropertyBaseColor { Value = new float4(color.r, color.g, color.b, color.a) });
                }
            }
        }
    }
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct EntityVisualSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var em = state.EntityManager;
            foreach (var (_, selected, entity) in SystemAPI.Query<RefRO<Building>, RefRW<BuildingVisualSelection>>().WithEntityAccess()) selected.ValueRW.Slot = BuildingVisualResolver.Select(em, entity);
            foreach (var (owner, color, entity) in SystemAPI.Query<RefRO<VisualOwner>, RefRW<URPMaterialPropertyBaseColor>>().WithEntityAccess())
            {
                var root = owner.ValueRO.Owner; if (!em.Exists(root)) continue;
                var visible = true; var ruined = false;
                if (em.HasComponent<VisualState>(root)) visible = em.GetComponentData<VisualState>(root).Visible != 0;
                if(em.HasComponent<ExternalVisual>(root)&&em.GetComponentData<ExternalVisual>(root).Active!=0)visible=false;
                if (em.HasComponent<Building>(root))
                {
                    var b = em.GetComponentData<Building>(root); var visual = owner.ValueRO;
                    ruined = b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing;
                    if (visual.Slot != Entity.Null) visible &= (em.HasComponent<BuildingVisualSelection>(root) ? em.GetComponentData<BuildingVisualSelection>(root).Slot : BuildingVisualResolver.Select(em, root)) == visual.Slot;
                    if (visual.OperationalOnly != 0) visible &= b.Stage == LifeStage.Operational;
                    if (visual.HarvestRemaining > 0) visible &= b.HarvestRemaining == visual.HarvestRemaining;
                    if (!visual.CropId.IsEmpty)
                    {
                        var simulation = em.GetComponentData<SimulationOwner>(root).Root;
                        if (b.Crop < 0) visible = false;
                        else { var crop = Sim.Definition(em, simulation, b.Crop); var progress = math.saturate(b.CropProgress / (float)math.max(1, crop.Duration)); visible &= crop.Id == visual.CropId && progress >= visual.GrowthFrom && (visual.GrowthTo >= 1 ? progress <= 1 : progress < visual.GrowthTo); }
                    }
                }
                color.ValueRW.Value = ruined ? new float4(.25f, .23f, .21f, 1) : owner.ValueRO.OriginalColor;
                if (em.HasComponent<MaterialMeshInfo>(entity)) em.SetComponentEnabled<MaterialMeshInfo>(entity, visible);
            }
        }
    }
}
