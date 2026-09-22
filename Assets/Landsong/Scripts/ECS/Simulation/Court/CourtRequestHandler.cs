using Unity.Entities;

namespace Landsong.ECS
{
    public static class CourtRequestHandler
    {
        public static bool TryExecute(EntityManager em, Entity root, Entity payload, out ResultCode result)
        {
            if (em.HasComponent<RecruitTalentRequest>(payload))
            {
                var request = em.GetComponentData<RecruitTalentRequest>(payload);
                result = SocialOps.ChangeEmployment(em, root, request.Person, TalentAction.Recruit);
                return true;
            }

            if (em.HasComponent<AssignTalentRequest>(payload))
            {
                var request = em.GetComponentData<AssignTalentRequest>(payload);
                result = SocialOps.ChangeEmployment(em, root, request.Person, TalentAction.Assign, request.Slot);
                return true;
            }

            if (em.HasComponent<DismissTalentRequest>(payload))
            {
                var request = em.GetComponentData<DismissTalentRequest>(payload);
                result = SocialOps.ChangeEmployment(em, root, request.Person, TalentAction.Dismiss);
                return true;
            }

            if (em.HasComponent<AbdicateRequest>(payload))
            {
                var request = em.GetComponentData<AbdicateRequest>(payload);
                result = DynastyOps.Abdicate(em, root, WorldQueries.Find(em, request.Successor));
                return true;
            }

            if (em.HasComponent<DesignateHeirRequest>(payload))
            {
                var request = em.GetComponentData<DesignateHeirRequest>(payload);
                result = CourtOps.Designate(em, root, WorldQueries.Find(em, request.Person));
                return true;
            }

            if (em.HasComponent<ExecuteHeirRequest>(payload))
            {
                var request = em.GetComponentData<ExecuteHeirRequest>(payload);
                result = CourtOps.Execute(em, root, WorldQueries.Find(em, request.Person), request.Confirmed);
                return true;
            }

            if (em.HasComponent<RoyalVisitRequest>(payload))
            {
                var request = em.GetComponentData<RoyalVisitRequest>(payload);
                result = CourtOps.Visit(em, root, WorldQueries.Find(em, request.Person), (int)request.Decision);
                return true;
            }

            if (em.HasComponent<ResolveMarriageRequest>(payload))
            {
                var request = em.GetComponentData<ResolveMarriageRequest>(payload);
                result = RoyalFamilyOps.Resolve(em, root, WorldQueries.Find(em, request.Person), request.Decision, request.Mate, request.ExpectedRequestTurn);
                return true;
            }

            if (em.HasComponent<PrepareMarriageRequest>(payload))
            {
                var request = em.GetComponentData<PrepareMarriageRequest>(payload);
                result = RoyalFamilyOps.Prepare(em, root, WorldQueries.Find(em, request.Person));
                return true;
            }

            if (em.HasComponent<ArrangeMarriageRequest>(payload))
            {
                var request = em.GetComponentData<ArrangeMarriageRequest>(payload);
                result = RoyalFamilyOps.Arrange(em, root, WorldQueries.Find(em, request.Person), WorldQueries.Find(em, request.Mate));
                return true;
            }

            if (em.HasComponent<RefusePersonRequest>(payload))
            {
                var request = em.GetComponentData<RefusePersonRequest>(payload);
                result = PersonRequestOps.Refuse(em, root, WorldQueries.Find(em, request.Person), request.ExpectedRequestTurn);
                return true;
            }

            if (em.HasComponent<CustomizePortraitRequest>(payload))
            {
                var request = em.GetComponentData<CustomizePortraitRequest>(payload);
                result = PortraitOps.Customize(em, root, WorldQueries.Find(em, request.Person), request.PortraitData, request.Seed);
                return true;
            }

            if (em.HasComponent<GiftPersonRequest>(payload))
            {
                var request = em.GetComponentData<GiftPersonRequest>(payload);
                result = SocialOps.Interact(em, root, request.Person, SocialAction.Gift);
                return true;
            }

            if (em.HasComponent<CompleteSocialTaskRequest>(payload))
            {
                var request = em.GetComponentData<CompleteSocialTaskRequest>(payload);
                result = SocialOps.Interact(em, root, request.Person, SocialAction.CompleteTask);
                return true;
            }

            if (em.HasComponent<ProposeMarriageRequest>(payload))
            {
                var request = em.GetComponentData<ProposeMarriageRequest>(payload);
                result = SocialOps.Interact(em, root, request.Person, SocialAction.ProposeMarriage);
                return true;
            }

            if (em.HasComponent<SelectPolicyRequest>(payload))
            {
                var request = em.GetComponentData<SelectPolicyRequest>(payload);
                result = PolicyOps.Policy(em, root, request.Policy);
                return true;
            }

            if (em.HasComponent<RefreshTalentsRequest>(payload))
            {
                SocialOps.EnsureContacts(em, root);
                result = ResultCode.Success;
                return true;
            }

            if (em.HasComponent<CancelPolicyRequest>(payload))
            {
                var request = em.GetComponentData<CancelPolicyRequest>(payload);
                var policies = em.GetBuffer<PolicyChoice>(root);
                for (int i = policies.Length - 1; i >= 0; i--)
                    if (policies[i].Definition == request.Policy)
                        policies.RemoveAt(i);
                BuildingChangeNotifications.Publish(em, root);
                result = ResultCode.Success;
                return true;
            }

            result = ResultCode.Unavailable;
            return false;
        }
    }
}
