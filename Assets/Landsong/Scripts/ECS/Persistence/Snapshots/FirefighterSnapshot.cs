using System.IO;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS.Persistence
{
    public sealed class FirefighterSnapshot : EntitySnapshot
    {
        public Health Health;
        public Firefighter Task;
    }

    internal static class FirefighterSnapshotStorage
    {
        internal static void Capture(BinaryWriter writer, EntityManager em, Entity entity)
        {
            SnapshotBinary.Write(writer, em.GetComponentData<Identity>(entity));
            SnapshotBinary.Write(writer, em.GetComponentData<LocalTransform>(entity));
            SnapshotBinary.Write(writer, em.GetComponentData<Health>(entity));
            var task = em.GetComponentData<Firefighter>(entity);
            writer.Write(task.Station);
            writer.Write(task.Fire);
            writer.Write(task.WorkerSlot);
            SnapshotBinary.Write(writer, task.Start);
            SnapshotBinary.Write(writer, task.Destination);
            writer.Write((byte)task.Stage);
            writer.Write(task.DeathRecorded);
        }

        internal static FirefighterSnapshot Read(BinaryReader reader) => new FirefighterSnapshot
        {
            Identity = SnapshotBinary.Read<Identity>(reader),
            Transform = SnapshotBinary.Read<LocalTransform>(reader),
            Health = SnapshotBinary.Read<Health>(reader),
            Task = new Firefighter
            {
                Station = reader.ReadUInt64(),
                Fire = reader.ReadUInt64(),
                WorkerSlot = reader.ReadInt32(),
                Start = SnapshotBinary.Read<float3>(reader),
                Destination = SnapshotBinary.Read<float3>(reader),
                Stage = (FirefighterStage)reader.ReadByte(),
                DeathRecorded = reader.ReadByte(),
            }
        };

        internal static Entity Restore(EntityManager em, Entity root, FirefighterSnapshot record)
        {
            var entity = FirefighterOps.Spawn(em, root, record.Task, record.Identity.Id);
            em.SetComponentData(entity, record.Identity);
            em.SetComponentData(entity, record.Transform);
            em.SetComponentData(entity, record.Health);
            if (record.Task.Stage == FirefighterStage.Dead)
                em.SetComponentData(entity, new VisualState());
            return entity;
        }

        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data, FirefighterSnapshot record)
        {
            SnapshotValidation.Health(record.Health);
            var task = record.Task;
            if (!em.HasComponent<TransportWorkerSettings>(root) || task.Station == 0 || task.Fire == 0
                || task.Station == task.Fire || task.WorkerSlot < 0 || task.WorkerSlot >= 4
                || task.Stage > FirefighterStage.Dead || task.DeathRecorded > 1
                || !math.all(math.isfinite(task.Start)) || !math.all(math.isfinite(task.Destination))
                || !data.Records.OfType<BuildingSnapshot>().Any(b => b.Identity.Id == task.Station)
                || !data.Records.OfType<BuildingSnapshot>().Any(b => b.Identity.Id == task.Fire)
                || (task.Stage == FirefighterStage.Dead) != (task.DeathRecorded != 0)
                || (record.Health.Current <= 0) != (task.DeathRecorded != 0))
                throw new InvalidDataException("Invalid firefighter task");
        }
    }
}
