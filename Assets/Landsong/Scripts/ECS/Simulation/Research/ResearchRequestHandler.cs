using Unity.Entities;

namespace Landsong.ECS
{
    public static class ResearchRequestHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<QueueResearchRequest>(payload))
                result = ResearchOps.Command(em, root, em.GetComponentData<QueueResearchRequest>(payload).Technology, false);
            else if (em.HasComponent<CancelResearchRequest>(payload))
                result = ResearchOps.Command(em, root, em.GetComponentData<CancelResearchRequest>(payload).Technology, true);
            else if (em.HasComponent<PlanResearchRequest>(payload))
            {
                var request = em.GetComponentData<PlanResearchRequest>(payload);
                result = ResearchOps.Plan(em, root, request.Technology, request.ExpectedPlan.ToString());
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
