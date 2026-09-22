using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class TurnSettlement
    {
        public static void Settle(EntityManager em, Entity root)
        {
            ResearchOps.Settle(em, root);
            DynastyOps.Settle(em, root);
            var turn = em.GetComponentData<GameClock>(root).Turn;
            ExpeditionOps.Settle(em, root);
            QuestLifecycle.EvaluateQuests(em, root); // Production/research in this settlement can complete a task before expiry.
            using (var quests = WorldQueries.OrderedEntities<Quest>(em))
                foreach (var e in quests)
                {
                    var q = em.GetComponentData<Quest>(e);
                    if (q.Mainline == 0 && (q.Status == QuestStatus.Active || q.Status == QuestStatus.Completed) && q.Deadline > 0 && turn + 1 >= q.Deadline)
                    {
                        QuestLifecycle.FailQuest(em, root, e, "期限已到");
                    }
                }

            QuestOfferOps.Settle(em, root);
            QuestLifecycle.DiscoverQuests(em, root);
            QuestLifecycle.EvaluateQuests(em, root);
        }
    }
}
