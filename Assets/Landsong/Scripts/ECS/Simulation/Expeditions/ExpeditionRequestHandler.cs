using Unity.Entities;

namespace Landsong.ECS
{
    public static class ExpeditionRequestHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<StartExpeditionRequest>(payload))
                result = ExpeditionOps.Start(em, root, em.GetComponentData<StartExpeditionRequest>(payload));
            else if (em.HasComponent<ClaimExpeditionRequest>(payload))
                result = ExpeditionOps.Finish(em, root, em.GetComponentData<ClaimExpeditionRequest>(payload).Expedition, true);
            else if (em.HasComponent<AbandonExpeditionRequest>(payload))
                result = ExpeditionOps.Finish(em, root, em.GetComponentData<AbandonExpeditionRequest>(payload).Expedition, false);
            else
            {
                result = ResultCode.Unavailable;
                return false;
            }

            return true;
        }
    }
}
