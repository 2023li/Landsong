using System;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    [InternalBufferCapacity(0)]
    public struct CompletedExpedition : IBufferElementData
    {
        public ExpeditionId Expedition;
    }

    public static class ExpeditionCompletions
    {
        public static bool Has(EntityManager em, Entity root, ExpeditionId expedition)
        {
            if (!ExpeditionDefinitions.IsValid(em, root, expedition))
                return false;
            foreach (var entry in em.GetBuffer<CompletedExpedition>(root))
                if (entry.Expedition == expedition)
                    return true;
            return false;
        }

        public static void RecordSuccess(EntityManager em, Entity root, ExpeditionId expedition)
        {
            if (!ExpeditionDefinitions.IsValid(em, root, expedition))
                throw new ArgumentOutOfRangeException(nameof(expedition));
            if (!Has(em, root, expedition))
                em.GetBuffer<CompletedExpedition>(root).Add(new CompletedExpedition { Expedition = expedition });
        }
    }
}
