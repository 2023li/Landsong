using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum TalentAction
    {
        Recruit,
        Assign,
        Dismiss
    }

    public enum SocialAction
    {
        Gift,
        CompleteTask,
        ProposeMarriage
    }

    public static class SocialOps
    {
        public static void EnsureContacts(EntityManager em, Entity root)
        {
            for (int index = 0; index < TalentDefinitions.Count(em, root); index++)
            {
                var id = TalentId.FromIndex(index);
                ref var definition = ref TalentDefinitions.Get(em, root, id);
                if (!PrerequisiteEvaluation.Satisfied(em, root, ref definition.Prerequisites))
                    continue;
                bool found = false;
                using (var people = WorldQueries.OrderedEntities<Talent>(em))
                    foreach (var person in people)
                        if (em.GetComponentData<TalentDefinitionRef>(person).Definition == id && CourtOps.Alive(em, person))
                            found = true;
                if (found)
                    continue;
                var entity = TalentEntities.Spawn(em, root, id, default, true);
                EntityState.Set(em, entity, new Talent { Level = math.max(1, definition.InitialLevel) });
                EntityState.Set(em, entity, CourtOps.NewPerson(em, root, 4, 22));
                EntityState.Buffer<TraitEntry>(em, entity);
                PortraitOps.Ensure(em, root, entity);
                for (int i = 0; i < definition.InitialTraits.Length; i++)
                    em.GetBuffer<TraitEntry>(entity).Add(new TraitEntry { Definition = definition.InitialTraits[i] });
                CourtOps.RefreshTraits(em, root, entity);
            }
        }

        public static int Wage(EntityManager em, Entity root, Entity person)
        {
            var talent = em.GetComponentData<Talent>(person);
            ref var definition = ref TalentDefinitions.Get(em, root, em.GetComponentData<TalentDefinitionRef>(person).Definition);
            return math.max(0, definition.Wage.BaseAmount + (talent.Level - 1) * definition.Wage.PerLevel);
        }

        public static bool Accepts(EntityManager em, Entity root, Entity person, TalentSlotId slot)
        {
            if (!TalentSlotDefinitions.IsValid(em, root, slot) || !CourtOps.JobEligible(em, person))
                return false;
            ref var job = ref TalentSlotDefinitions.Get(em, root, slot);
            ref var talent = ref TalentDefinitions.Get(em, root, em.GetComponentData<TalentDefinitionRef>(person).Definition);
            if (job.AcceptedSpecialty != 0 && job.AcceptedSpecialty != talent.Specialty)
                return false;
            for (int i = 0; i < job.RequiredTraits.Length; i++)
            {
                bool found = false;
                foreach (var trait in em.GetBuffer<TraitEntry>(person))
                    if (trait.Definition == job.RequiredTraits[i] && trait.Revealed != 0)
                        found = true;
                if (!found)
                    return false;
            }

            return true;
        }

        public static ResultCode ChangeEmployment(EntityManager em, Entity root, ulong personId, TalentAction action, TalentSlotId slot = default)
        {
            var e = WorldQueries.Find(em, personId);
            if (e == Entity.Null || !em.HasComponent<Talent>(e))
                return ResultCode.InvalidTarget;
            var t = em.GetComponentData<Talent>(e);
            TalentSettings settingsTalent = em.GetComponentData<TalentSettings>(root);
            if (action == TalentAction.Recruit)
            {
                if (t.Recruited != 0 || !CourtOps.JobEligible(em, e))
                    return ResultCode.Unavailable;
                if (em.HasComponent<Royal>(e) && em.GetComponentData<Royal>(e).Affection < CourtOps.Rules(em, root).RecruitAffection)
                    return ResultCode.Unavailable;
                int count = 0;
                using (var all = WorldQueries.OrderedEntities<Talent>(em))
                    foreach (var person in all)
                        if (em.GetComponentData<Talent>(person).Recruited != 0 && CourtOps.JobEligible(em, person))
                            count++;
                if (count >= settingsTalent.TalentCapacity)
                    return ResultCode.NoCapacity;
                if (!InventoryOps.Remove(em, root, em.GetComponentData<CurrencySettings>(root).Gold, settingsTalent.TalentRecruitCost))
                    return ResultCode.InsufficientResources;
                t.Recruited = 1;
                t.Paid = 0;
            }
            else if (action == TalentAction.Dismiss)
            {
                t.Slot = default;
                t.Recruited = 0;
                t.Paid = 0;
            }
            else if (action == TalentAction.Assign)
            {
                if (t.Recruited == 0 || !CourtOps.JobEligible(em, e))
                    return ResultCode.Unavailable;
                if (slot.IsValid && !Accepts(em, root, e, slot))
                    return ResultCode.Unavailable;
                if (t.Slot == slot)
                    return ResultCode.Unavailable;
                if (slot.IsValid && !InventoryOps.Remove(em, root, em.GetComponentData<CurrencySettings>(root).Gold, Wage(em, root, e)))
                    return ResultCode.InsufficientResources;
                if (slot.IsValid)
                    using (var all = WorldQueries.OrderedEntities<Talent>(em))
                        foreach (var other in all)
                            if (other != e)
                            {
                                var old = em.GetComponentData<Talent>(other);
                                if (old.Slot == slot)
                                {
                                    old.Slot = default;
                                    old.Paid = 0;
                                    em.SetComponentData(other, old);
                                }
                            }

                t.Slot = slot;
                t.Paid = (byte)(slot.IsValid ? 1 : 0);
                t.WageTurn = em.GetComponentData<GameClock>(root).Turn;
            }
            else
                return ResultCode.InvalidContent;
            em.SetComponentData(e, t);
            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }

        public static ResultCode Interact(EntityManager em, Entity root, ulong personId, SocialAction action)
        {
            var e = WorldQueries.Find(em, personId);
            if (!CourtOps.Alive(em, e) || !em.HasComponent<Talent>(e))
                return ResultCode.InvalidTarget;
            var p = em.GetComponentData<Royal>(e);
            var q = CourtOps.Rules(em, root);
            var turn = em.GetComponentData<GameClock>(root).Turn;
            if (action == SocialAction.Gift)
            {
                if (p.LastGiftTurn == turn || p.Affection >= 100)
                    return ResultCode.Unavailable;
                if (!InventoryOps.Remove(em, root, em.GetComponentData<CurrencySettings>(root).Gold, q.GiftCost))
                    return ResultCode.InsufficientResources;
                p.LastGiftTurn = turn;
                p.Affection = math.min(100, p.Affection + q.GiftAffection);
            }
            else if (action == SocialAction.CompleteTask)
            {
                if (p.TaskClaimed != 0)
                    return ResultCode.Unavailable;
                ref var definition = ref TalentDefinitions.Get(em, root, em.GetComponentData<TalentDefinitionRef>(e).Definition);
                if (definition.SocialTasks.Length == 0)
                    return ResultCode.Unavailable;
                var task = definition.SocialTasks[0];
                for (int i = 1; i < definition.SocialTasks.Length; i++)
                    if (definition.SocialTasks[i].Order >= task.Order)
                        task = definition.SocialTasks[i];
                if (!task.Item.IsValid)
                    return ResultCode.Unavailable;
                if (!InventoryOps.Remove(em, root, task.Item, task.Quantity))
                    return ResultCode.InsufficientResources;
                p.TaskClaimed = 1;
                p.Affection = math.min(100, p.Affection + task.AffectionReward);
            }
            else if (action == SocialAction.ProposeMarriage)
            {
                var king = CourtOps.Monarch(em);
                if (king == Entity.Null || king == e || p.Affection < q.MarriageAffection || p.Age < q.MarriageAge || p.Retired != 0)
                    return ResultCode.Unavailable;
                var k = em.GetComponentData<Royal>(king);
                var kid = em.GetComponentData<Identity>(king).Id;
                var id = em.GetComponentData<Identity>(e).Id;
                if (!RoyalFamilyOps.CanMarry(em, root, king, e))
                    return ResultCode.Unavailable;
                RoyalFamilyOps.Marry(em, king, e, true);
                p = em.GetComponentData<Royal>(e);
                RoyalFamilyOps.ClearInvalidRequests(em, root);
                CourtOps.Log(em, root, "结为配偶，原人才岗位已腾空并停薪", id);
            }
            else
                return ResultCode.InvalidContent;
            em.SetComponentData(e, p);
            BuildingChangeNotifications.Publish(em, root);
            return ResultCode.Success;
        }

        // Pay before buildings consume effects: an unpaid worker cannot lend last turn's production bonus.
        public static void PayWages(EntityManager em, Entity root)
        {
            using var all = WorldQueries.OrderedEntities<Talent>(em);
            foreach (var e in all)
            {
                var t = em.GetComponentData<Talent>(e);
                if (t.Recruited == 0 || !t.Slot.IsValid || !CourtOps.JobEligible(em, e) || !Accepts(em, root, e, t.Slot))
                {
                    t.Paid = 0;
                    em.SetComponentData(e, t);
                    continue;
                }

                using var scope = EconomyJournalOps.For(em, root, e, EconomyReason.TalentWage);
                var turn = em.GetComponentData<GameClock>(root).Turn;
                if (t.WageTurn == turn)
                    continue;
                t.WageTurn = turn;
                t.Paid = (byte)(InventoryOps.Remove(em, root, em.GetComponentData<CurrencySettings>(root).Gold, Wage(em, root, e)) ? 1 : 0);
                if (t.Paid == 0)
                    EconomyJournalOps.Note(em, root, "工资不足，本期人才收益停用");
                em.SetComponentData(e, t);
            }
        }
    }
}
