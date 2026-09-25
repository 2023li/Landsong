using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Definitions;

namespace Landsong.ECS
{
    public enum TransportStage : byte { Delivering, Unloading, Returning, Loading, Sheltered, Dead }

    // One representative of the existing civilian population per resource connection.
    // Construction cargo is reserved when the worker spawns and settled at dusk.
    public struct TransportWorker : IComponentData
    {
        public ulong Provider, Consumer;
        public float3 Start, Destination;
        public int Turn, Trips;
        public float Remaining;
        public TransportStage Stage;
        public byte Variant, Carrying, Retiring, DeathRecorded, Delivered, CargoAssigned, CargoSettled;
    }

    [InternalBufferCapacity(2)]
    public struct TransportCargo : IBufferElementData
    {
        public ItemId Item;
        public int Amount;
    }

    [InternalBufferCapacity(0)]
    public struct TransportDeliveryEvent : IBufferElementData
    {
        public ItemId Item;
        public int Amount;
        public float3 Position;
    }

    // Distinguishes worker material lost before payment confirmation from night rewards.
    public struct WorkerCargoDrop : IComponentData { }

    public struct TransportWorkerSettings : IComponentData
    {
        public Entity Male, Female;
        public float Health, Speed, LoadSeconds, UnloadSeconds;
    }
}
