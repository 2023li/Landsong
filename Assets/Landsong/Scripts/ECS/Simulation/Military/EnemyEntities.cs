using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct EnemyDefinitionRef : IComponentData
    {
        public EnemyId Definition;
    }

    public static class EnemyEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, EnemyId definition, float3 position, bool persistent)
        {
            ref var source = ref EnemyDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<EnemyPrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked enemy prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new EnemyDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
