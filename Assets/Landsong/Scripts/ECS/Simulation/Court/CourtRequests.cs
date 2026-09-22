using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum RoyalVisitDecision : byte
    {
        Refuse,
        Guarded,
        Unguarded
    }

    public struct RecruitTalentRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.RecruitTalent;
        public ulong Target => Person;
    }

    public struct AssignTalentRequest : IGameRequest
    {
        public ulong Person;
        public TalentSlotId Slot;
        public CommandKind Kind => CommandKind.AssignTalent;
        public ulong Target => Person;
    }

    public struct DismissTalentRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.DismissTalent;
        public ulong Target => Person;
    }

    public struct RefreshTalentsRequest : IGameRequest
    {
        public CommandKind Kind => CommandKind.RefreshTalents;
        public ulong Target => 0;
    }

    public struct AbdicateRequest : IGameRequest
    {
        public ulong Successor;
        public CommandKind Kind => CommandKind.Abdicate;
        public ulong Target => Successor;
    }

    public struct DesignateHeirRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.DesignateHeir;
        public ulong Target => Person;
    }

    public struct ExecuteHeirRequest : IGameRequest
    {
        public ulong Person;
        public bool Confirmed;
        public CommandKind Kind => CommandKind.ExecuteHeir;
        public ulong Target => Person;
    }

    public struct RoyalVisitRequest : IGameRequest
    {
        public ulong Person;
        public RoyalVisitDecision Decision;
        public CommandKind Kind => CommandKind.RoyalVisit;
        public ulong Target => Person;
    }

    public struct ResolveMarriageRequest : IGameRequest
    {
        public ulong Person;
        public ulong Mate;
        public int ExpectedRequestTurn;
        public int Decision;
        public CommandKind Kind => CommandKind.ResolveMarriage;
        public ulong Target => Person;
    }

    public struct PrepareMarriageRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.PrepareMarriage;
        public ulong Target => Person;
    }

    public struct ArrangeMarriageRequest : IGameRequest
    {
        public ulong Person;
        public ulong Mate;
        public CommandKind Kind => CommandKind.ArrangeMarriage;
        public ulong Target => Person;
    }

    public struct RefusePersonRequest : IGameRequest
    {
        public ulong Person;
        public int ExpectedRequestTurn;
        public CommandKind Kind => CommandKind.RefusePersonRequest;
        public ulong Target => Person;
    }

    public struct CustomizePortraitRequest : IGameRequest
    {
        public ulong Person;
        public uint Seed;
        public FixedString128Bytes PortraitData;
        public CommandKind Kind => CommandKind.CustomizePortrait;
        public ulong Target => Person;
    }

    public struct GiftPersonRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.GiftPerson;
        public ulong Target => Person;
    }

    public struct CompleteSocialTaskRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.CompleteSocialTask;
        public ulong Target => Person;
    }

    public struct ProposeMarriageRequest : IGameRequest
    {
        public ulong Person;
        public CommandKind Kind => CommandKind.ProposeMarriage;
        public ulong Target => Person;
    }

    public struct SelectPolicyRequest : IGameRequest, IHistoryNamedRequest
    {
        public PolicyId Policy;
        public CommandKind Kind => CommandKind.SelectPolicy;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => PolicyDefinitions.IsValid(em, root, Policy) ? PolicyDefinitions.Get(em, root, Policy).Metadata.Name : default;
    }

    public struct CancelPolicyRequest : IGameRequest, IHistoryNamedRequest
    {
        public PolicyId Policy;
        public CommandKind Kind => CommandKind.CancelPolicy;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => PolicyDefinitions.IsValid(em, root, Policy) ? PolicyDefinitions.Get(em, root, Policy).Metadata.Name : default;
    }
}
