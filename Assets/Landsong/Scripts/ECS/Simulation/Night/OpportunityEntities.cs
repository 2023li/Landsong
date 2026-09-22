using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct OpportunityDefinitionRef : IComponentData
    {
        public OpportunityId Definition;
    }

    public static class OpportunityEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, OpportunityId definition, float3 position, bool persistent)
        {
            ref var source = ref OpportunityDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<OpportunityPrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked opportunity prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new OpportunityDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
