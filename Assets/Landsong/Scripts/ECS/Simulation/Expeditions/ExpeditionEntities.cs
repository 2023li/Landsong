using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct ExpeditionDefinitionRef : IComponentData
    {
        public ExpeditionId Definition;
    }

    public static class ExpeditionEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, ExpeditionId definition, float3 position, bool persistent)
        {
            ref var source = ref ExpeditionDefinitions.Get(em, root, definition);
            var entity = em.CreateEntity();
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new ExpeditionDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
