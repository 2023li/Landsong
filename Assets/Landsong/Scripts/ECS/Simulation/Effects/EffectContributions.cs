using System;
using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Entities;

namespace Landsong.ECS
{
    internal delegate void EffectDefinitionVisitor(ref DefinitionEffects effects, EffectOrigin source);
    internal delegate void TalentJobVisitor(ref TalentJobEffects effects, EffectOrigin source);
    internal readonly struct EffectOrigin
    {
        internal readonly EffectSourceKind Kind;
        internal readonly int Level;
        internal readonly ulong Owner;
        internal readonly string Name, InactiveReason;
        internal EffectOrigin(EffectSourceKind kind, int level, string name, string reason = "", ulong owner = 0)
        {
            Kind = kind;
            Level = level;
            Name = name;
            InactiveReason = reason;
            Owner = owner;
        }

        internal float Apply(float magnitude, bool levelMatches, bool targetMatches, List<EffectSource> sources, string extraReason = "")
        {
            string reason = InactiveReason;
            if (reason.Length == 0 && !levelMatches)
                reason = "适用等级不符";
            if (reason.Length == 0 && !targetMatches)
                reason = "作用对象不匹配";
            if (reason.Length == 0)
                reason = extraReason;
            float applied = reason.Length == 0 ? magnitude : 0;
            sources?.Add(new EffectSource { Kind = Kind, Level = Level, Owner = Owner, Name = Name, Reason = reason.Length == 0 ? "生效" : reason, Value = magnitude, Applied = applied });
            return applied;
        }
    }

    // Enumerates effect owners only. Recipients and arithmetic belong to the explicit item,
    // soldier, hero, building, kingdom and intelligence services.
    internal static class EffectContributions
    {
        internal static bool LevelMatches(int required, int actual, EffectDomain domain) => required == 0 || (domain == EffectDomain.Military ? required <= actual : required == actual);
        internal static void Visit(EntityManager em, Entity root, EffectDomain domain, EffectDefinitionVisitor visit, TalentJobVisitor jobs = null, Entity person = default)
        {
            if (domain == EffectDomain.Intelligence)
            {
                for (int i = 0; i < BuffDefinitions.Count(em, root); i++)
                {
                    var id = BuffId.FromIndex(i);
                    ref var definition = ref BuffDefinitions.Get(em, root, id);
                    int level = PermanentBuffs.Level(em, root, id);
                    visit(ref definition.Effects, new EffectOrigin(EffectSourceKind.Buff, level, definition.Metadata.Name.ToString(), level == 0 ? "尚未获得" : ""));
                }

                for (int i = 0; i < TechnologyDefinitions.Count(em, root); i++)
                {
                    var id = TechnologyId.FromIndex(i);
                    ref var definition = ref TechnologyDefinitions.Get(em, root, id);
                    int level = ResearchOps.Completed(em, root, id);
                    visit(ref definition.Effects, new EffectOrigin(EffectSourceKind.Technology, level, definition.Metadata.Name.ToString(), level == 0 ? "尚未获得" : ""));
                }

                for (int i = 0; i < PolicyDefinitions.Count(em, root); i++)
                {
                    var id = PolicyId.FromIndex(i);
                    ref var definition = ref PolicyDefinitions.Get(em, root, id);
                    bool selected = false;
                    foreach (var policy in em.GetBuffer<PolicyChoice>(root))
                        if (policy.Definition == id)
                            selected = true;
                    visit(ref definition.Effects, new EffectOrigin(EffectSourceKind.Policy, 1, definition.Metadata.Name.ToString(), selected && CourtOps.PolicyActive(em, root, id) ? "" : "未采用或政策条件未满足"));
                }

                return;
            }

            if (domain != EffectDomain.Court)
            {
                foreach (var buff in em.GetBuffer<OwnedBuff>(root))
                {
                    ref var definition = ref BuffDefinitions.Get(em, root, buff.Buff);
                    visit(ref definition.Effects, new EffectOrigin(EffectSourceKind.Buff, buff.Level, definition.Metadata.Name.ToString()));
                }

                using var people = WorldQueries.OrderedEntities<Talent>(em);
                foreach (var entity in people)
                {
                    var talent = em.GetComponentData<Talent>(entity);
                    var identity = em.GetComponentData<Identity>(entity);
                    var reason = TalentReason(em, root, entity, talent);
                    ref var definition = ref TalentDefinitions.Get(em, root, em.GetComponentData<TalentDefinitionRef>(entity).Definition);
                    var source = new EffectOrigin(EffectSourceKind.Talent, talent.Level, identity.Name + " / " + definition.Metadata.Name, reason, identity.Id);
                    visit(ref definition.Effects, source);
                    jobs?.Invoke(ref definition.JobEffects, source);
                    if (talent.Slot.IsValid)
                    {
                        ref var slot = ref TalentSlotDefinitions.Get(em, root, talent.Slot);
                        source = new EffectOrigin(EffectSourceKind.TalentSlot, talent.Level, identity.Name + " / " + slot.Metadata.Name, reason, identity.Id);
                        visit(ref slot.Effects, source);
                        jobs?.Invoke(ref slot.JobEffects, source);
                    }
                }
            }

            foreach (var policy in em.GetBuffer<PolicyChoice>(root))
            {
                ref var definition = ref PolicyDefinitions.Get(em, root, policy.Definition);
                visit(ref definition.Effects, new EffectOrigin(EffectSourceKind.Policy, 1, definition.Metadata.Name.ToString(), CourtOps.PolicyActive(em, root, policy.Definition) ? "" : "政策条件未满足"));
            }

            Entity owner = domain == EffectDomain.Personal ? person : CourtOps.Monarch(em);
            if (owner != Entity.Null && em.HasBuffer<TraitEntry>(owner))
            {
                var identity = em.GetComponentData<Identity>(owner);
                foreach (var trait in em.GetBuffer<TraitEntry>(owner))
                {
                    ref var definition = ref RoyalTraitDefinitions.Get(em, root, trait.Definition);
                    visit(ref definition.Effects, new EffectOrigin(domain == EffectDomain.Personal ? EffectSourceKind.PersonalTrait : EffectSourceKind.MonarchTrait, 1, identity.Name + " / " + definition.Metadata.Name, trait.Active != 0 ? "" : "特性未激活", identity.Id));
                }
            }

            if (domain == EffectDomain.Military)
                foreach (var progress in em.GetBuffer<TechnologyProgress>(root))
                {
                    if (progress.Completions <= 0)
                        continue;
                    ref var definition = ref TechnologyDefinitions.Get(em, root, progress.Technology);
                    visit(ref definition.Effects, new EffectOrigin(EffectSourceKind.Technology, progress.Completions, definition.Metadata.Name.ToString()));
                }
        }

        static string TalentReason(EntityManager em, Entity root, Entity person, Talent talent)
        {
            if (talent.Recruited == 0)
                return "未雇佣";
            if (!talent.Slot.IsValid)
                return "未任职";
            if (!CourtOps.JobEligible(em, person) || !SocialOps.Accepts(em, root, person, talent.Slot))
                return "任职条件未满足";
            return talent.Paid == 0 ? "工资未支付" : "";
        }

        internal static float CourtState(EntityManager em, Entity root, bool production, List<EffectSource> sources)
        {
            var court = CourtOps.State(em, root);
            int turn = em.GetComponentData<GameClock>(root).Turn;
            float value = new EffectOrigin(EffectSourceKind.Legacy, 0, "政治遗产").Apply(production ? court.LegacyProduction : court.LegacyAttack, true, true, sources);
            value += new EffectOrigin(EffectSourceKind.Temporary, 0, "临时朝局", turn <= court.TemporaryUntil ? "" : "已到期").Apply(production ? court.TemporaryProduction : court.TemporaryAttack, true, true, sources);
            value += new EffectOrigin(EffectSourceKind.Disorder, 0, "动荡", turn <= court.DisorderUntil ? "" : "已到期").Apply(-court.Disorder, true, true, sources);
            return value;
        }
    }
}
