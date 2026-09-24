using System.IO;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;

namespace Landsong.ECS.Persistence
{
    public sealed class WorkerCargoDropSnapshot : EntitySnapshot
    {
        public Loot Loot;
    }

    internal static class WorkerCargoDropSnapshotStorage
    {
        internal static void Capture(BinaryWriter writer, EntityManager em, Entity entity)
        {
            SnapshotBinary.Write(writer, em.GetComponentData<Identity>(entity));
            SnapshotBinary.Write(writer, em.GetComponentData<LocalTransform>(entity));
            var loot = em.GetComponentData<Loot>(entity);
            writer.Write(loot.Item.Index);
            writer.Write(loot.Count);
            writer.Write(loot.SourceName.ToString());
        }

        internal static WorkerCargoDropSnapshot Read(BinaryReader reader) => new WorkerCargoDropSnapshot {
            Identity = SnapshotBinary.Read<Identity>(reader),
            Transform = SnapshotBinary.Read<LocalTransform>(reader),
            Loot = new Loot { Item = ItemId.FromIndex(reader.ReadInt32()), Count = reader.ReadInt32(), SourceName = new FixedString128Bytes(reader.ReadString()) }
        };

        internal static Entity Restore(EntityManager em, Entity root, WorkerCargoDropSnapshot record)
        {
            var entity = LootEntities.Spawn(em, root, LootId.FromIndex(0), record.Transform.Position, true);
            em.SetComponentData(entity, record.Identity);
            em.SetComponentData(entity, record.Transform);
            EntityState.Set(em, entity, record.Loot);
            EntityState.Set(em, entity, new WorkerCargoDrop());
            EntityState.Set(em, entity, new VisualState { Visible = 1 });
            return entity;
        }

        internal static void Validate(EntityManager em, Entity root, WorkerCargoDropSnapshot record)
        {
            if (LootDefinitions.Count(em, root) == 0 || !ItemDefinitions.IsValid(em, root, record.Loot.Item) || record.Loot.Count <= 0)
                throw new InvalidDataException("Invalid worker cargo drop");
        }
    }
}
