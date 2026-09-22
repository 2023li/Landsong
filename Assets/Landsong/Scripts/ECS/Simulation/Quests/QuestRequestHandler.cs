using Unity.Entities;

namespace Landsong.ECS
{
    public static class QuestRequestHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<AcceptQuestRequest>(payload))
                result = QuestLifecycle.ChangeStatus(em, root, em.GetComponentData<AcceptQuestRequest>(payload).Quest, QuestAction.Accept);
            else if (em.HasComponent<RejectQuestRequest>(payload))
                result = QuestLifecycle.ChangeStatus(em, root, em.GetComponentData<RejectQuestRequest>(payload).Quest, QuestAction.Reject);
            else if (em.HasComponent<ClaimQuestRequest>(payload))
                result = QuestLifecycle.ChangeStatus(em, root, em.GetComponentData<ClaimQuestRequest>(payload).Quest, QuestAction.Claim);
            else if (em.HasComponent<AbandonQuestRequest>(payload))
                result = QuestLifecycle.ChangeStatus(em, root, em.GetComponentData<AbandonQuestRequest>(payload).Quest, QuestAction.Abandon);
            else if (em.HasComponent<SubmitQuestRequest>(payload))
            {
                var request = em.GetComponentData<SubmitQuestRequest>(payload);
                result = QuestOps.Submit(em, root, WorldQueries.Find(em, request.Quest), request.Item, request.Quantity, request.RequirementKey.ToString(), request.ExpectedQuote.ToString());
            }
            else if (em.HasComponent<RecruitQuestRequest>(payload))
            {
                var request = em.GetComponentData<RecruitQuestRequest>(payload);
                result = QuestOfferOps.Generate(em, root, WorldQueries.Find(em, request.Provider), request.OfferSlot, true);
            }
            else
            {
                result = ResultCode.Unavailable;
                return false;
            }

            return true;
        }
    }
}
