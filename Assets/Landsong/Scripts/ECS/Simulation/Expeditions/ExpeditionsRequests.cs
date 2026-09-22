using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct StartExpeditionRequest : IGameRequest
    {
        public ulong Site;
        public ulong Captain;
        public ExpeditionId Destination;
        public int Crew;
        public FixedString128Bytes ExpectedQuote;
        public FixedList512Bytes<int> SupplyQuantities;
        public CommandKind Kind => CommandKind.StartExpedition;
        public ulong Target => Site;
    }

    public struct ClaimExpeditionRequest : IGameRequest
    {
        public ulong Expedition;
        public CommandKind Kind => CommandKind.ClaimExpedition;
        public ulong Target => Expedition;
    }

    public struct AbandonExpeditionRequest : IGameRequest
    {
        public ulong Expedition;
        public CommandKind Kind => CommandKind.AbandonExpedition;
        public ulong Target => Expedition;
    }
}
