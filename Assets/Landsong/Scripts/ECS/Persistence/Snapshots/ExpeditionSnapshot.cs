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
    public sealed class ExpeditionSnapshot : EntitySnapshot
    {
        public ExpeditionId Definition;
        public Expedition Expedition;
        public ExpeditionSupply[] Supplies = Array.Empty<ExpeditionSupply>();
    }

    internal static class ExpeditionSnapshotStorage
    {
        internal static ExpeditionSnapshot Capture(EntityManager em, Entity entity)
        {
            return new ExpeditionSnapshot
            {
                Identity = em.GetComponentData<Identity>(entity),
                Transform = em.HasComponent<LocalTransform>(entity) ? em.GetComponentData<LocalTransform>(entity) : LocalTransform.Identity,
                Definition = em.GetComponentData<ExpeditionDefinitionRef>(entity).Definition,
                Expedition = em.GetComponentData<Expedition>(entity),
                Supplies = SnapshotBuffers.Capture<ExpeditionSupply>(em, entity),
            };
        }

        internal static void Write(BinaryWriter writer, ExpeditionSnapshot record)
        {
            SnapshotBinary.Write(writer, record.Identity);
            SnapshotBinary.Write(writer, record.Transform);
            SnapshotBinary.Write(writer, record.Definition);
            SnapshotBinary.Write(writer, record.Expedition);
            SnapshotBuffers.Write(writer, record.Supplies);
        }

        internal static ExpeditionSnapshot Read(BinaryReader reader)
        {
            return new ExpeditionSnapshot
            {
                Identity = SnapshotBinary.Read<Identity>(reader),
                Transform = SnapshotBinary.Read<LocalTransform>(reader),
                Definition = SnapshotBinary.Read<ExpeditionId>(reader),
                Expedition = SnapshotBinary.Read<Expedition>(reader),
                Supplies = SnapshotBuffers.Read<ExpeditionSupply>(reader),
            };
        }

        internal static Entity Restore(EntityManager em, Entity root, ExpeditionSnapshot record)
        {
            var entity = ExpeditionEntities.Spawn(em, root, record.Definition, record.Transform.Position, true);
            EntityState.Set(em, entity, record.Identity);
            EntityState.Set(em, entity, record.Transform);
            EntityState.Set(em, entity, record.Expedition);
            SnapshotBuffers.Restore(em, entity, record.Supplies);
            return entity;
        }
    }
}
