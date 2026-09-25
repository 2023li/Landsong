using System;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct ClaimedQuest : IBufferElementData
    {
        public QuestId Quest;
    }

    [InternalBufferCapacity(0)]
    public struct QuestRefreshCooldown : IBufferElementData
    {
        public QuestId Quest;
        public int NextTurn;
    }

    public static class QuestCompletions
    {
        public static bool Has(EntityManager em, Entity root, QuestId quest)
        {
            if (!QuestDefinitions.IsValid(em, root, quest))
                return false;
            foreach (var entry in em.GetBuffer<ClaimedQuest>(root))
                if (entry.Quest == quest)
                    return true;
            return false;
        }

        public static void RecordClaim(EntityManager em, Entity root, QuestId quest)
        {
            if (!QuestDefinitions.IsValid(em, root, quest))
                throw new ArgumentOutOfRangeException(nameof(quest));
            if (!Has(em, root, quest))
                em.GetBuffer<ClaimedQuest>(root).Add(new ClaimedQuest { Quest = quest });
        }
    }
}
