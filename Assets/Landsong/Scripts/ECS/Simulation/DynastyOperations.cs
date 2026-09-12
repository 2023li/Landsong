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
            em.AddComponentData(e, new Identity { Id = Sim.AllocateId(em, root), Definition = -1, Name = name });
            em.AddComponentData(e, new Persistent());
            em.AddComponentData(e, new SimulationOwner { Root = root });
            em.AddComponentData(e, CourtOps.NewPerson(em, root, role, age, parent));
            em.AddBuffer<TraitEntry>(e); return e;
        }
        public static ResultCode Abdicate(EntityManager em, Entity root, Entity heir)
        {
            var king = CourtOps.Monarch(em);
            if (!CourtOps.Eligible(em, king, heir) || CourtOps.State(em, root).Crown != em.GetComponentData<Identity>(heir).Id) return ResultCode.Unavailable;
            CourtOps.Succeed(em, root, king, Entity.Null, true); return ResultCode.Success;
        }
        public static ResultCode TalentCommand(EntityManager em, Entity root, Command c) => SocialOps.TalentCommand(em, root, c);
        public static void Settle(EntityManager em, Entity root)
        {
            var settings = em.GetComponentData<DynastySettings>(root);
            using (var talents = Sim.OrderedEntities<Talent>(em)) foreach (var e in talents)
            {
                var t = em.GetComponentData<Talent>(e); if (t.Recruited == 0 || !CourtOps.JobEligible(em, e)) continue;
                using var wageScope = EconomyJournalOps.For(em, root, e, EconomyReason.TalentWage);
                var d = Sim.Definition(em, root, em.GetComponentData<Identity>(e).Definition);
                if (t.Paid != 0 && t.Slot >= 0)
                {
                    var turn = em.GetComponentData<Session>(root).Turn; if (t.LastBenefitTurn == turn) continue; t.LastBenefitTurn = turn;
                    using var benefitScope = EconomyJournalOps.For(em, root, e, EconomyReason.TalentBenefit);
                    t.AssignedTurns++; t.Experience += settings.TalentExperience;
                    var exp = math.max(1, d.Cost + (t.Level - 1) * d.Duration);
                    while (t.Experience >= exp && t.Level < d.Capacity) { t.Experience -= exp; t.Level++; exp = math.max(1, d.Cost + (t.Level - 1) * d.Duration); }
                    // Validate every license before income, then protect the complete periodic benefit.
                    // Periodic items retain their existing store-available policy (no pending overflow).
                    for (var i = 0; i < d.RuleCount; i++)
                    {
                        var r = Sim.GetRule(em, root, d.RuleStart + i);
                        if (r.Kind == RuleKind.TalentEffect && r.B == 10 && r.Amount == 30)
                            RewardOps.ValidateEntitlement(em, root, r.Target, checked((int)math.floor(EffectOps.ScaledTalentValue(em, root, r, t.Level))));
                    }
                    using (var benefits = new RewardTransaction(em, root))
                    {
                        for (var i = 0; i < d.RuleCount; i++)
                        {
                            var r = Sim.GetRule(em, root, d.RuleStart + i);
                            if (r.Kind == RuleKind.RewardItem) InventoryOps.Add(em, root, r.Target, checked(r.Amount + (int)(r.Extra * (t.Level - 1))));
                            if (r.Kind == RuleKind.TalentEffect && r.B == 10)
                            {
                                var value = checked((int)math.floor(EffectOps.ScaledTalentValue(em, root, r, t.Level)));
                                if (r.Amount == 10 && r.Target >= 0) InventoryOps.Add(em, root, r.Target, value);
                                if (r.Amount == 20) { var s = em.GetComponentData<Session>(root); s.ResearchPoints = checked(s.ResearchPoints + value); em.SetComponentData(root, s); }
                                if (r.Amount == 30) RewardOps.GrantEntitlement(em, root, r.Target, value);
                            }
                        }
                        benefits.Commit();
                    }
                    var traits = em.GetBuffer<TraitEntry>(e);
                    for (var i = 0; i < traits.Length; i++) { var trait = traits[i]; var tr = Sim.Definition(em, root, trait.Definition); if (tr.Kind == ContentKind.RoyalTrait) continue; if (ConditionOps.Prerequisites(em, root, trait.Definition)) { trait.Revealed = (byte)(t.Level >= tr.Level && t.AssignedTurns >= tr.Duration ? 1 : 0); trait.Active = (byte)(trait.Revealed != 0 && t.Level >= tr.Capacity && t.AssignedTurns >= tr.Cost ? 1 : 0); } traits[i] = trait; }
                }
                em.SetComponentData(e, t);
            }
            CourtOps.Settle(em, root);
        }
    }
}
