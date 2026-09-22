using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingDefinitionRef : IComponentData
    {
        public BuildingId Definition;
    }

    public static class BuildingEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, BuildingId definition, float3 position, bool persistent)
        {
            ref var source = ref BuildingDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<BuildingPrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked building prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new BuildingDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
