using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct ProjectileDefinitionRef : IComponentData
    {
        public ProjectileId Definition;
    }

    public static class ProjectileEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, ProjectileId definition, float3 position, bool persistent)
        {
            ref var source = ref ProjectileDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<ProjectilePrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked projectile prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new ProjectileDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
