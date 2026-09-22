using Landsong.ECS.Definitions;
using Unity.Collections;

namespace Landsong.ECS
{
    public struct AcceptQuestRequest : IGameRequest
    {
        public ulong Quest;
        public CommandKind Kind => CommandKind.AcceptQuest;
        public ulong Target => Quest;
    }

    public struct RejectQuestRequest : IGameRequest
    {
        public ulong Quest;
        public CommandKind Kind => CommandKind.RejectQuest;
        public ulong Target => Quest;
    }

    public struct ClaimQuestRequest : IGameRequest
    {
        public ulong Quest;
        public CommandKind Kind => CommandKind.ClaimQuest;
        public ulong Target => Quest;
    }

    public struct AbandonQuestRequest : IGameRequest
    {
        public ulong Quest;
        public CommandKind Kind => CommandKind.AbandonQuest;
        public ulong Target => Quest;
    }

    public struct SubmitQuestRequest : IGameRequest
    {
        public ulong Quest;
        public ItemId Item;
        public int Quantity;
        public FixedString64Bytes RequirementKey;
        public FixedString128Bytes ExpectedQuote;
        public CommandKind Kind => CommandKind.SubmitQuest;
        public ulong Target => Quest;
    }

    public struct RecruitQuestRequest : IGameRequest
    {
        public ulong Provider;
        public int OfferSlot;
        public CommandKind Kind => CommandKind.RecruitQuest;
        public ulong Target => Provider;
    }

    public enum QuestTrackingMode : byte
    {
        Automatic,
        Manual,
        Unpinned
    }

    public struct TrackQuestRequest : IGameRequest
    {
        public ulong Quest;
        public QuestTrackingMode Mode;
        public CommandKind Kind => CommandKind.TrackQuest;
        public ulong Target => Quest;
    }

    public struct CameraMovedRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.CameraMoved;
        public ulong Target => 0;
    }

    public struct CameraZoomedRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.CameraZoomed;
        public ulong Target => 0;
    }
}
