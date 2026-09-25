using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class DynastyOps
    {
        public static Entity CreateRoyal(EntityManager em, Entity root, FixedString128Bytes name, byte role, int age, ulong parent = 0)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new Identity { Id = EntityIdentityAllocator.AllocateId(em, root), Name = name });
            em.AddComponentData(e, new Persistent());
            em.AddComponentData(e, new SimulationOwner { Root = root });
            em.AddComponentData(e, CourtOps.NewPerson(em, root, role, age, parent));
            em.AddBuffer<TraitEntry>(e);
            return e;
        }

        public static ResultCode Abdicate(EntityManager em, Entity root, Entity heir)
        {
            var king = CourtOps.Monarch(em);
            if (!CourtOps.Eligible(em, king, heir) || CourtOps.State(em, root).Crown != em.GetComponentData<Identity>(heir).Id)
                return ResultCode.Unavailable;
            CourtOps.Succeed(em, root, king, Entity.Null, true);
            return ResultCode.Success;
        }

        public static void Settle(EntityManager em, Entity root)
        {
            TalentSettings settingsTalent = em.GetComponentData<TalentSettings>(root);
            using (var talents = WorldQueries.OrderedEntities<Talent>(em))
                foreach (var e in talents)
                {
                    var t = em.GetComponentData<Talent>(e);
                    if (t.Recruited == 0 || !CourtOps.JobEligible(em, e))
                        continue;
                    using var wageScope = EconomyJournalOps.For(em, root, e, EconomyReason.TalentWage);
                    ref var d = ref TalentDefinitions.Get(em, root, em.GetComponentData<TalentDefinitionRef>(e).Definition);
                    if (t.Paid != 0 && t.Slot.IsValid)
                    {
                        var turn = em.GetComponentData<GameClock>(root).Turn;
                        if (t.LastBenefitTurn == turn)
                            continue;
                        t.LastBenefitTurn = turn;
                        using var benefitScope = EconomyJournalOps.For(em, root, e, EconomyReason.TalentBenefit);
                        t.AssignedTurns++;
                        t.Experience += settingsTalent.TalentExperience;
                        var exp = math.max(1, d.BaseLevelExperience + (t.Level - 1) * d.LevelExperienceIncrement);
                        while (t.Experience >= exp && t.Level < d.MaximumLevel)
                        {
                            t.Experience -= exp;
                            t.Level++;
                            exp = math.max(1, d.BaseLevelExperience + (t.Level - 1) * d.LevelExperienceIncrement);
                        }

                        TalentIncomeDelivery.Apply(em, root, ref d.PeriodicIncome, t.Level);
                    }

                    em.SetComponentData(e, t);
                }

            if (FeatureOps.Unlocked(em, root, "Royal"))
                CourtOps.Settle(em, root);
        }
    }
}
