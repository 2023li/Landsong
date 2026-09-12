using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    // Runtime instances are not automatically owned by the SubScene that supplied their prefab.
    // An explicit owner prevents a second map/session from inheriting the previous dynasty.
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial struct SimulationLifetimeSystem : ISystem
    {
        public void OnUpdate(ref SystemState state) => Cleanup(state.EntityManager);

        public static void Cleanup(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<SimulationOwner>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            using var entities = query.ToEntityArray(Allocator.Temp);
            foreach (var e in entities)
            {
                // Destroying a linked owner can already have destroyed another queried child.
                if (!em.Exists(e)) continue;
                var owner = em.GetComponentData<SimulationOwner>(e).Root;
                // Disabled roots remain valid owners while a synchronous restore stages a candidate.
                if (owner == Entity.Null || !em.Exists(owner) || !em.HasComponent<Session>(owner)) em.DestroyEntity(e);
            }
        }

        public static bool IsReleased(EntityManager em)
        {
            using var query = em.CreateEntityQuery(new EntityQueryDesc
            {
                Any = new[] { ComponentType.ReadOnly<Session>(), ComponentType.ReadOnly<SimulationOwner>() },
                Options = EntityQueryOptions.IncludeDisabledEntities
            });
            return query.CalculateEntityCount() == 0;
        }
    }
}
