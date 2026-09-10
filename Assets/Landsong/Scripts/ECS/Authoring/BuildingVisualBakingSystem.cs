using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    // A multi-material MeshRenderer is split into additional entities by Entities Graphics.
    // Those entities must inherit the same slot/owner, otherwise hidden levels leak submeshes.
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [UpdateInGroup(typeof(PostBakingSystemGroup))]
    public partial class BuildingVisualBakingSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            var em = EntityManager;
            using var query = em.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<AdditionalEntityParent>(), ComponentType.ReadOnly<RenderMeshUnmanaged>() }, Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities });
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                var parent = em.GetComponentData<AdditionalEntityParent>(e).Parent; if (!em.HasComponent<VisualOwner>(parent)) continue;
                var owner = em.GetComponentData<VisualOwner>(parent); Material material = em.GetComponentData<RenderMeshUnmanaged>(e).materialForSubMesh;
                var color = material != null && material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : Color.white;
                owner.OriginalColor = new float4(color.r, color.g, color.b, color.a); Sim.Set(em, e, owner); Sim.Set(em, e, new URPMaterialPropertyBaseColor { Value = owner.OriginalColor });
            }
        }
    }
}
