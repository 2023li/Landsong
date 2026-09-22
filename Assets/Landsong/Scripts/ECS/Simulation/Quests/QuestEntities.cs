using System;
using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct QuestDefinitionRef : IComponentData
    {
        public QuestId Definition;
    }

    public static class QuestEntities
    {
        public static Entity Spawn(EntityManager em, Entity root, QuestId definition, float3 position, bool persistent)
        {
            ref var source = ref QuestDefinitions.Get(em, root, definition);
            var entity = em.CreateEntity();
            EntityInstantiation.Initialize(em, root, entity, source.Metadata.Name, position, persistent);
            EntityState.Set(em, entity, new QuestDefinitionRef { Definition = definition });
            return entity;
        }
    }
}
