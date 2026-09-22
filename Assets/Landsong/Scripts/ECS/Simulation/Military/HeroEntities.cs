using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct HeroDefinitionRef : IComponentData
    {
        public HeroId Definition;
    }

    public static class HeroEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, HeroId definition, float3 position, bool persistent)
        {
            ref var source = ref HeroDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<HeroPrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked hero prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new HeroDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
