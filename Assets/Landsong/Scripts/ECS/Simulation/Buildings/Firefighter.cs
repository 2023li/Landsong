using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum FirefighterStage : byte { Outbound, Returning, Dead }

    public struct FireSettings : IComponentData
    {
        public BuildingId Station;
        public BuildingId Temple;
        public float FirefighterSpeed;
    }

    // A scene representative of an already employed fire-station worker.
    public struct Firefighter : IComponentData
    {
        public ulong Station;
        public ulong Fire;
        public int WorkerSlot;
        public float3 Start;
        public float3 Destination;
        public FirefighterStage Stage;
        public byte DeathRecorded;
    }
}
