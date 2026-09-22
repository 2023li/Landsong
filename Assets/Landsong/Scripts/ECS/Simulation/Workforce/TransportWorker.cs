using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum TransportStage : byte { Delivering, Unloading, Returning, Loading, Sheltered, Dead }

    // One representative of the existing civilian population per resource connection.
    // Cargo is presentation only; daily settlement remains the sole inventory writer.
    public struct TransportWorker : IComponentData
    {
        public ulong Provider, Consumer;
        public float3 Start, Destination;
        public int Turn, Trips;
        public float Remaining;
        public TransportStage Stage;
        public byte Variant, Carrying, Retiring, DeathRecorded;
    }

    public struct TransportWorkerSettings : IComponentData
    {
        public Entity Male, Female;
        public float Health, Speed, LoadSeconds, UnloadSeconds;
    }
}
