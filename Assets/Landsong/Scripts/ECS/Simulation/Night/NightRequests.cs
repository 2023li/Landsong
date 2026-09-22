using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct PickUpRequest : IGameRequest
    {
        public ulong Loot;
        public CommandKind Kind => CommandKind.PickUp;
        public ulong Target => Loot;
    }

    public struct SetIntelligenceModeRequest : IGameRequest
    {
        public bool Enabled;
        public CommandKind Kind => CommandKind.IntelligenceMode;
        public ulong Target => 0;
    }

    public struct ReadIntelligenceRequest : IGameRequest
    {
        public ulong ExpectedFingerprint;
        public CommandKind Kind => CommandKind.ReadIntelligence;
        public ulong Target => 0;
    }

    public struct SetNightSpeedRequest : IGameRequest
    {
        public int Speed;
        public CommandKind Kind => CommandKind.NightSpeed;
        public ulong Target => 0;
    }
}
