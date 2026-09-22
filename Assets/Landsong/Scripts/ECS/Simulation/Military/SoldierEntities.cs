using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct SoldierDefinitionRef : IComponentData
    {
        public SoldierId Definition;
    }

    public static class SoldierEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, SoldierId definition, float3 position, bool persistent)
        {
            ref var source = ref SoldierDefinitions.Get(em, root, definition);
            var prefab = Entity.Null;
            foreach (var entry in em.GetBuffer<SoldierPrefab>(root))
                if (entry.Definition == definition)
                {
                    prefab = entry.Prefab;
                    break;
                }

            if (prefab == Entity.Null)
                throw new InvalidOperationException("Missing baked soldier prefab: " + source.Metadata.Id);
            var entity = em.Instantiate(prefab);
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new SoldierDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
