using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct LootDefinitionRef : IComponentData
    {
        public LootId Definition;
    }

    public static class LootEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, LootId definition, float3 position, bool persistent)
        {
            ref var source = ref LootDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<LootPrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked loot prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new LootDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
