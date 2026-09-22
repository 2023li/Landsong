using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;

namespace Landsong.ECS
{
    public struct QueueResearchRequest : IGameRequest, IHistoryNamedRequest
    {
        public TechnologyId Technology;
        public CommandKind Kind => CommandKind.Research;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => TechnologyDefinitions.IsValid(em, root, Technology) ? TechnologyDefinitions.Get(em, root, Technology).Metadata.Name : default;
    }

    public struct CancelResearchRequest : IGameRequest, IHistoryNamedRequest
    {
        public TechnologyId Technology;
        public CommandKind Kind => CommandKind.CancelResearch;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => TechnologyDefinitions.IsValid(em, root, Technology) ? TechnologyDefinitions.Get(em, root, Technology).Metadata.Name : default;
    }

    public struct PlanResearchRequest : IGameRequest, IHistoryNamedRequest
    {
        public TechnologyId Technology;
        public FixedString128Bytes ExpectedPlan;
        public CommandKind Kind => CommandKind.PlanResearch;
        public ulong Target => 0;

        public FixedString128Bytes HistoryName(EntityManager em, Entity root) => TechnologyDefinitions.IsValid(em, root, Technology) ? TechnologyDefinitions.Get(em, root, Technology).Metadata.Name : default;
    }
}
