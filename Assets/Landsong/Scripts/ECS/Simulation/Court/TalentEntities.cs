using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct TalentDefinitionRef : IComponentData
    {
        public TalentId Definition;
    }

    public static class TalentEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, TalentId definition, float3 position, bool persistent)
        {
            ref var source = ref TalentDefinitions.Get(em, root, definition);
            var entity = em.CreateEntity();
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new TalentDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
