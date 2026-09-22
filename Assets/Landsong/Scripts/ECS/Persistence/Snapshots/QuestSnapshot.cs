using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Landsong.ECS.Persistence
{
    public sealed class QuestSnapshot : EntitySnapshot
    {
        public QuestId Definition;
        public Quest Quest;
        public QuestProgress[] Progress = Array.Empty<QuestProgress>();
    }

    internal static class QuestSnapshotStorage
    {
        internal static QuestSnapshot Capture(EntityManager em, Entity entity)
        {
            return new QuestSnapshot
            {
                Identity = em.GetComponentData<Identity>(entity),
                Transform = em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity) : LocalTransform.Identity,
                Definition = em.GetComponentData<QuestDefinitionRef>(entity).Definition,
                Quest = em.GetComponentData<Quest>(entity),
                Progress = SnapshotBuffers.Capture<QuestProgress>(em, entity),
            };
        }

        internal static void Write(BinaryWriter writer, QuestSnapshot record)
        {
            SnapshotBinary.Write(writer, record.Identity);
            SnapshotBinary.Write(writer, record.Transform);
            SnapshotBinary.Write(writer, record.Definition);
            SnapshotBinary.Write(writer, record.Quest);
            SnapshotBuffers.Write(writer, record.Progress);
        }

        internal static QuestSnapshot Read(BinaryReader reader)
        {
            return new QuestSnapshot
            {
                Identity = SnapshotBinary.Read<Identity>(reader),
                Transform = SnapshotBinary.Read<LocalTransform>(reader),
                Definition = SnapshotBinary.Read<QuestId>(reader),
                Quest = SnapshotBinary.Read<Quest>(reader),
                Progress = SnapshotBuffers.Read<QuestProgress>(reader),
            };
        }

        internal static Entity Restore(EntityManager em, Entity root, QuestSnapshot record)
        {
            var entity = QuestEntities.Spawn(em, root, record.Definition, record.Transform.Position, true);
            EntityState.Set(em, entity, record.Identity);
            EntityState.Set(em, entity, record.Transform);
            EntityState.Set(em, entity, record.Quest);
            SnapshotBuffers.Restore(em, entity, record.Progress);
            return entity;
        }
    }
}
