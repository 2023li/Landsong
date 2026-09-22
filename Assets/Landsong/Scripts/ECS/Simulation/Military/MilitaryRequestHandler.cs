using Unity.Entities;

namespace Landsong.ECS
{
    public static class MilitaryRequestHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<RecruitSoldiersRequest>(payload))
            {
                var request = em.GetComponentData<RecruitSoldiersRequest>(payload);
                result = SoldierOps.RecruitSoldiers(em, root, request);
                return true;
            }

            if (em.HasComponent<RecruitHeroRequest>(payload))
            {
                var request = em.GetComponentData<RecruitHeroRequest>(payload);
                result = HeroOps.Recruit(em, root, request);
                return true;
            }

            if (em.HasComponent<AssignSoldierRequest>(payload))
            {
                var request = em.GetComponentData<AssignSoldierRequest>(payload);
                result = GarrisonOps.Assign(em, root, request);
                return true;
            }

            if (em.HasComponent<UnassignSoldierRequest>(payload))
            {
                var request = em.GetComponentData<UnassignSoldierRequest>(payload);
                result = GarrisonOps.Assign(em, root, new AssignSoldierRequest { Soldier = request.Soldier });
                return true;
            }

            if (em.HasComponent<SwapSoldiersRequest>(payload))
            {
                var request = em.GetComponentData<SwapSoldiersRequest>(payload);
                result = GarrisonOps.Swap(em, request);
                return true;
            }

            if (em.HasComponent<RenameSoldierRequest>(payload))
            {
                var request = em.GetComponentData<RenameSoldierRequest>(payload);
                result = SoldierOps.Rename(em, root, request);
                return true;
            }

            if (em.HasComponent<DismissSoldierRequest>(payload))
            {
                var request = em.GetComponentData<DismissSoldierRequest>(payload);
                result = SoldierOps.Dismiss(em, root, request);
                return true;
            }

            if (em.HasComponent<SetSoldierAttentionRequest>(payload))
            {
                var request = em.GetComponentData<SetSoldierAttentionRequest>(payload);
                result = SoldierOps.SetAttention(em, root, request);
                return true;
            }

            if (em.HasComponent<FillGarrisonRequest>(payload))
            {
                var request = em.GetComponentData<FillGarrisonRequest>(payload);
                result = GarrisonOps.Fill(em, root, request.Garrison);
                return true;
            }

            if (em.HasComponent<RecallGarrisonRequest>(payload))
            {
                var request = em.GetComponentData<RecallGarrisonRequest>(payload);
                result = GarrisonOps.RecallGarrison(em, root, request.Garrison, request.Cancel);
                return true;
            }

            if (em.HasComponent<WakeHeroRequest>(payload))
            {
                var request = em.GetComponentData<WakeHeroRequest>(payload);
                result = em.GetComponentData<Session>(root).Phase != Phase.Night ? ResultCode.Unavailable : HeroOps.Wake(em, root, WorldQueries.Find(em, request.Sanctum));
                return true;
            }

            if (em.HasComponent<SelectHeroRequest>(payload))
            {
                var request = em.GetComponentData<SelectHeroRequest>(payload);
                result = HeroCommandOps.Select(em, root, request);
                return true;
            }

            if (em.HasComponent<MoveHeroRequest>(payload))
            {
                var request = em.GetComponentData<MoveHeroRequest>(payload);
                result = HeroCommandOps.Move(em, root, request);
                return true;
            }

            if (em.HasComponent<FocusHeroRequest>(payload))
            {
                var request = em.GetComponentData<FocusHeroRequest>(payload);
                result = HeroCommandOps.Focus(em, root, request);
                return true;
            }

            if (em.HasComponent<RecallHeroRequest>(payload))
            {
                result = HeroCommandOps.Recall(em, root);
                return true;
            }

            if (em.HasComponent<RingBellRequest>(payload))
            {
                var target = WorldQueries.Find(em, em.GetComponentData<RingBellRequest>(payload).Building);
                if (!BuildingStatus.Operational(em, target) || em.GetComponentData<BuildingBellStats>(target).Radius <= 0)
                    result = ResultCode.InvalidTarget;
                else
                {
                    BellOps.Bell(em, root, target);
                    result = ResultCode.Success;
                }

                return true;
            }

            result = default;
            return false;
        }
    }
}
