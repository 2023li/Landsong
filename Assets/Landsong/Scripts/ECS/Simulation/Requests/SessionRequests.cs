using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum PauseDecision : byte
    {
        Toggle,
        Pause,
        Resume
    }

    public struct AdvanceRequest : IGameRequest
    {
        public ulong ReviewedLossToken;
        public CommandKind Kind => CommandKind.Advance;
        public ulong Target => 0;
    }

    public struct PauseRequest : IGameRequest
    {
        public PauseDecision Decision;
        public CommandKind Kind => CommandKind.Pause;
        public ulong Target => 0;
    }

    public struct RetryDayRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.RetryDay;
        public ulong Target => 0;
    }

    public struct RetryDuskRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.RetryDusk;
        public ulong Target => 0;
    }

    public struct EndDynastyRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.EndDynasty;
        public ulong Target => 0;
    }

    public struct CreateSaveRequest : IGameRequest
    {
        public FixedString128Bytes Label;
        public CommandKind Kind => CommandKind.Save;
        public ulong Target => 0;
    }

    public struct QuickSaveRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.Save;
        public ulong Target => 0;
    }

    public struct OverwriteSaveRequest : IGameRequest
    {
        public FixedString128Bytes SlotId;
        public ulong ExpectedStamp;
        public CommandKind Kind => CommandKind.Save;
        public ulong Target => 0;
    }

    public struct LoadSaveRequest : IGameRequest
    {
        public FixedString128Bytes SlotId;
        public bool Backup;
        public CommandKind Kind => CommandKind.Load;
        public ulong Target => 0;
    }
}
