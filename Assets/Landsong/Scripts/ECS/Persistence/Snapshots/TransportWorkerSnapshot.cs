using System.IO;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS.Persistence
{
    public sealed class TransportWorkerSnapshot : EntitySnapshot
    {
        public Health Health;
        public TransportWorker Worker;
    }

    internal static class TransportWorkerSnapshotStorage
    {
        internal static void Capture(BinaryWriter writer, EntityManager em, Entity entity)
        {
            SnapshotBinary.Write(writer, em.GetComponentData<Identity>(entity));
            SnapshotBinary.Write(writer, em.GetComponentData<LocalTransform>(entity));
            SnapshotBinary.Write(writer, em.GetComponentData<Health>(entity));
            var worker = em.GetComponentData<TransportWorker>(entity);
            writer.Write(worker.Provider); writer.Write(worker.Consumer);
            SnapshotBinary.Write(writer, worker.Start); SnapshotBinary.Write(writer, worker.Destination);
            writer.Write(worker.Turn); writer.Write(worker.Trips); writer.Write(worker.Remaining);
            writer.Write((byte)worker.Stage); writer.Write(worker.Variant); writer.Write(worker.Carrying);
            writer.Write(worker.Retiring); writer.Write(worker.DeathRecorded);
        }

        internal static TransportWorkerSnapshot Read(BinaryReader reader) => new TransportWorkerSnapshot {
            Identity = SnapshotBinary.Read<Identity>(reader), Transform = SnapshotBinary.Read<LocalTransform>(reader), Health = SnapshotBinary.Read<Health>(reader),
            Worker = new TransportWorker {
                Provider = reader.ReadUInt64(), Consumer = reader.ReadUInt64(), Start = SnapshotBinary.Read<float3>(reader), Destination = SnapshotBinary.Read<float3>(reader),
                Turn = reader.ReadInt32(), Trips = reader.ReadInt32(), Remaining = reader.ReadSingle(), Stage = (TransportStage)reader.ReadByte(),
                Variant = reader.ReadByte(), Carrying = reader.ReadByte(), Retiring = reader.ReadByte(), DeathRecorded = reader.ReadByte()
            }
        };

        internal static Entity Restore(EntityManager em, Entity root, TransportWorkerSnapshot record)
        {
            var entity = TransportWorkerOps.Spawn(em, root, record.Worker);
            em.SetComponentData(entity, record.Identity);
            em.SetComponentData(entity, record.Transform);
            em.SetComponentData(entity, record.Health);
            return entity;
        }

        internal static void Validate(EntityManager em, Entity root, SnapshotCodec.Snapshot data, TransportWorkerSnapshot record)
        {
            if (!em.HasComponent<TransportWorkerSettings>(root)) throw new InvalidDataException("Missing transport worker configuration");
            var config = em.GetComponentData<TransportWorkerSettings>(root);
            var w = record.Worker;
            SnapshotValidation.Health(record.Health);
            if (!em.Exists(config.Male) || !em.Exists(config.Female) || w.Provider == 0 || w.Consumer == 0 || w.Provider == w.Consumer
                || w.Provider > data.Ids.NextId || w.Consumer > data.Ids.NextId || w.Turn < 1 || w.Turn > data.Clock.Turn || w.Trips < 0
                || !math.all(math.isfinite(w.Start)) || !math.all(math.isfinite(w.Destination)) || !math.isfinite(w.Remaining) || w.Remaining < 0
                || w.Stage > TransportStage.Dead || w.Variant > 1 || w.Carrying > 1 || w.Retiring > 1 || w.DeathRecorded > 1
                || (w.Stage == TransportStage.Dead) != (w.DeathRecorded == 1) || (record.Health.Current <= 0) != (w.DeathRecorded == 1))
                throw new InvalidDataException("Invalid transport worker");
        }
    }
}
