using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public enum EffectDomain
    {
        [LabelText("全局效果")] Global,
        [LabelText("军事准备")] Military,
        [LabelText("个人风险")] Personal,
        [LabelText("王室与政策")] Court,
        [LabelText("情报")] Intelligence
    }

    public enum EffectSourceKind
    {
        [LabelText("永久增益")] Buff,
        [LabelText("政策")] Policy,
        [LabelText("人才")] Talent,
        [LabelText("人才岗位")] TalentSlot,
        [LabelText("人才特性")] TalentTrait,
        [LabelText("人物特性")] PersonalTrait,
        [LabelText("君王特性")] MonarchTrait,
        [LabelText("科技")] Technology,
        [LabelText("建筑")] Building,
        [LabelText("政治遗产")] Legacy,
        [LabelText("临时朝局")] Temporary,
        [LabelText("动荡")] Disorder
    }

    public enum EffectValueUnit
    {
        [LabelText("固定值")] Flat,
        [LabelText("比例值")] Ratio
    }

    // A query is an operation context, never another copy of persistent gameplay state.
    public readonly struct EffectQuery
    {
        public readonly RuleKind Kind;
        public readonly int TargetDefinition, TargetBuildingDefinition;
        public readonly Entity Person;
        public readonly EffectDomain Domain;

        public EffectQuery(RuleKind kind, int targetDefinition = -1, int targetBuildingDefinition = -1,
            EffectDomain domain = EffectDomain.Global, Entity person = default)
        {
            Kind = kind;
            TargetDefinition = targetDefinition;
            TargetBuildingDefinition = targetBuildingDefinition;
            Domain = domain;
            Person = person;
        }
    }

    public sealed class EffectSource
    {
        public EffectSourceKind Kind;
        public int Definition, Level, RuleIndex;
        public ulong Owner;
        public string Name, Reason;
        public float Value, Applied;
    }

    public sealed class EffectQuote
    {
        public float Value;
        public EffectValueUnit Unit;
        public readonly List<EffectSource> Sources = new List<EffectSource>();
    }

    // Shared by runtime matching and the authoring compiler. Building modules retain their own
    // local/range rules; this table describes the general Modifiers module only.
    public static class EffectRules
    {
        public static bool IsMilitary(RuleKind kind) => kind == RuleKind.AttackBonus || kind == RuleKind.HealthBonus
            || kind == RuleKind.SoldierAttackBonus || kind == RuleKind.SoldierSpeedBonus || kind == RuleKind.RangeBonus
            || kind == RuleKind.AttackSpeedBonus || kind == RuleKind.SpeedBonus || kind == RuleKind.ArmorBonus
            || kind == RuleKind.DamageReductionBonus || kind == RuleKind.PenetrationBonus
            || kind == RuleKind.ProjectileSpeedBonus || kind == RuleKind.BlastRadiusBonus;

        public static EffectValueUnit Unit(RuleKind kind) => kind == RuleKind.FlatProductionBonus
            || kind == RuleKind.ResearchOutput || kind == RuleKind.PublicOpinion || kind == RuleKind.Intelligence
            || kind == RuleKind.ActionPowerBonus || kind == RuleKind.Attraction || kind == RuleKind.ArmorBonus
            || kind == RuleKind.PenetrationBonus || kind == RuleKind.BlastRadiusBonus
            ? EffectValueUnit.Flat : EffectValueUnit.Ratio;

        public static bool SupportsOwner(ContentKind owner, RuleKind kind)
        {
            if (kind == RuleKind.Intelligence)
                return owner == ContentKind.Buff || owner == ContentKind.Policy || owner == ContentKind.Technology;
            if (owner == ContentKind.Technology) return IsMilitary(kind);
            return (owner == ContentKind.Buff || owner == ContentKind.Policy || owner == ContentKind.Talent
                || owner == ContentKind.TalentSlot || owner == ContentKind.RoyalTrait)
                && (IsMilitary(kind) || kind == RuleKind.LossModifier || kind == RuleKind.ProductionBonus
                    || kind == RuleKind.CropHarvestBonus || kind == RuleKind.PublicOpinion || kind == RuleKind.PlotRisk
                    || kind == RuleKind.NaturalDeathRisk || kind == RuleKind.ResearchOutput
                    || kind == RuleKind.ActionPowerBonus || kind == RuleKind.FlatProductionBonus);
        }

        public static bool SupportsTarget(RuleKind kind, ContentKind? target)
        {
            if (!target.HasValue) return kind != RuleKind.FlatProductionBonus;
            if (kind == RuleKind.Intelligence) return target == ContentKind.Technology; // prerequisite, not recipient
            if (kind == RuleKind.LossModifier || kind == RuleKind.ProductionBonus
                || kind == RuleKind.CropHarvestBonus || kind == RuleKind.FlatProductionBonus) return target == ContentKind.Item;
            if (IsMilitary(kind))
            {
                // Buildings consume only the frozen defensive profile in CombatOps. Their health and
                // production do not consume a combatant payload; accepting those targets would be a no-op.
                if (target == ContentKind.Building) return kind == RuleKind.ArmorBonus || kind == RuleKind.DamageReductionBonus;
                if (kind == RuleKind.SoldierAttackBonus || kind == RuleKind.SoldierSpeedBonus) return target == ContentKind.Soldier;
                return target == ContentKind.Soldier || target == ContentKind.Hero;
            }
            if (kind == RuleKind.ActionPowerBonus) return target == ContentKind.Building;
            if (kind == RuleKind.NaturalDeathRisk) return target == ContentKind.Talent;
            // Research, opinion and plot chance are kingdom-wide values; a content target has no meaning.
            return false;
        }

        public static bool LevelMatches(Rule rule, int level, EffectDomain domain) => rule.Level == 0
            || (domain == EffectDomain.Military ? rule.Level <= level : rule.Level == level);

        public static bool SupportsTalentEffect(ContentKind owner, int timing, int effect)
        {
            if (owner != ContentKind.Talent && owner != ContentKind.TalentSlot) return false;
            if (timing == 0) return effect == 100 || effect == 140 || effect == 150;
            return owner == ContentKind.Talent && timing == 10 && (effect == 10 || effect == 20 || effect == 30);
        }
    }

    public static class EffectOps
    {
        public static EffectQuote Query(EntityManager em, Entity root, EffectQuery query)
        {
            var result = new EffectQuote { Unit = EffectRules.Unit(query.Kind) };
            result.Value = Evaluate(em, root, query, result.Sources);
            return result;
        }

        public static float Value(EntityManager em, Entity root, EffectQuery query) => Evaluate(em, root, query, null);

        public static float Modifier(EntityManager em, Entity root, RuleKind kind, int target,
            List<AttractionSource> sources = null)
        {
            var query = new EffectQuery(kind, target);
            if (sources == null) return Value(em, root, query);
            var result = Query(em, root, query);
            foreach (var source in result.Sources)
                if (source.Applied != 0) sources.Add(new AttractionSource
                    { Definition = source.Definition, Label = source.Name, Value = source.Applied });
            return result.Value;
        }

        public static float FlatProduction(EntityManager em, Entity root, int item, int building)
            => Value(em, root, new EffectQuery(RuleKind.FlatProductionBonus, item, building));

        public static float PersonalRisk(EntityManager em, Entity root, Entity person)
            => Value(em, root, new EffectQuery(RuleKind.NaturalDeathRisk,
                em.GetComponentData<Identity>(person).Definition, domain: EffectDomain.Personal, person: person));

        static string TalentReason(EntityManager em, Entity root, Entity person, Talent talent)
        {
            if (talent.Recruited == 0) return "未雇佣";
            if (talent.Slot < 0) return "未任职";
            if (!CourtOps.JobEligible(em, person) || !SocialOps.Accepts(em, root, person, talent.Slot)) return "任职条件未满足";
            return talent.Paid == 0 ? "工资未支付" : "";
        }

        static float Evaluate(EntityManager em, Entity root, EffectQuery query, List<EffectSource> sources)
        {
            if ((query.Kind == RuleKind.Intelligence) != (query.Domain == EffectDomain.Intelligence))
                throw new ArgumentException("情报效果必须使用情报查询上下文。", nameof(query));
            if (query.Domain == EffectDomain.Military && !EffectRules.IsMilitary(query.Kind))
                throw new ArgumentException("军事准备只接受军事属性。", nameof(query));
            if (query.Domain == EffectDomain.Personal && (query.Kind != RuleKind.NaturalDeathRisk || query.Person == Entity.Null))
                throw new ArgumentException("个人风险查询必须明确指定人物。", nameof(query));
            float total = 0;
            void Add(int definition, int level, EffectSourceKind kind, Entity owner = default, string reason = "", bool talentEffects = false)
            {
                if (definition < 0) return;
                total += AddDefinition(em, root, query, definition, level, kind, owner, reason, talentEffects, sources);
            }

            if (query.Domain == EffectDomain.Intelligence)
            {
                using var buildings = Sim.OrderedEntities<Building>(em);
                foreach (var entity in buildings)
                {
                    var building = em.GetComponentData<Building>(entity);
                    Add(em.GetComponentData<Identity>(entity).Definition, building.Level, EffectSourceKind.Building, entity);
                }
                var content = em.GetComponentData<ContentCatalog>(root).Value;
                for (int definition = 0; definition < content.Value.Definitions.Length; definition++)
                {
                    var kind = content.Value.Definitions[definition].Kind;
                    if (kind == ContentKind.Buff)
                    {
                        int level = PermanentBuffOps.Level(em, root, definition);
                        Add(definition, level, EffectSourceKind.Buff, reason: level == 0 ? "尚未获得" : "");
                    }
                    else if (kind == ContentKind.Technology)
                    {
                        int level = ResearchOps.Completed(em, root, definition);
                        Add(definition, level, EffectSourceKind.Technology, reason: level == 0 ? "尚未获得" : "");
                    }
                    else if (kind == ContentKind.Policy)
                    {
                        bool selected = false;
                        if (em.HasBuffer<PolicyChoice>(root))
                            foreach (var policy in em.GetBuffer<PolicyChoice>(root)) if (policy.Definition == definition) selected = true;
                        Add(definition, 1, EffectSourceKind.Policy,
                            reason: selected && CourtOps.PolicyActive(em, root, definition) ? "" : "未采用或政策条件未满足");
                    }
                }
                return total;
            }

            if (query.Domain != EffectDomain.Court)
            {
                foreach (var grant in em.GetBuffer<Entitlement>(root))
                    if (Sim.Definition(em, root, grant.Definition).Kind == ContentKind.Buff)
                        Add(grant.Definition, PermanentBuffOps.Level(em, root, grant.Definition), EffectSourceKind.Buff);

                using var people = Sim.OrderedEntities<Talent>(em);
                foreach (var person in people)
                {
                    var talent = em.GetComponentData<Talent>(person);
                    var reason = TalentReason(em, root, person, talent);
                    Add(em.GetComponentData<Identity>(person).Definition, talent.Level, EffectSourceKind.Talent, person, reason, true);
                    Add(talent.Slot, talent.Level, EffectSourceKind.TalentSlot, person, reason, true);
                    foreach (var trait in em.GetBuffer<TraitEntry>(person))
                    {
                        if (Sim.Definition(em, root, trait.Definition).Kind == ContentKind.RoyalTrait) continue;
                        // Personal queries enumerate the subject's traits below, once and without job gates.
                        if (query.Domain == EffectDomain.Personal && query.Person == person) continue;
                        Add(trait.Definition, talent.Level, EffectSourceKind.TalentTrait, person,
                            reason.Length != 0 ? reason : trait.Active == 0 ? "特性未激活" : "", true);
                    }
                }
            }

            if (em.HasBuffer<PolicyChoice>(root))
                foreach (var policy in em.GetBuffer<PolicyChoice>(root))
                    Add(policy.Definition, 1, EffectSourceKind.Policy,
                        reason: CourtOps.PolicyActive(em, root, policy.Definition) ? "" : "政策条件未满足");

            var traitOwner = query.Domain == EffectDomain.Personal ? query.Person : CourtOps.Monarch(em);
            if (traitOwner != Entity.Null && em.HasBuffer<TraitEntry>(traitOwner))
                foreach (var trait in em.GetBuffer<TraitEntry>(traitOwner))
                    Add(trait.Definition, 1, query.Domain == EffectDomain.Personal ? EffectSourceKind.PersonalTrait : EffectSourceKind.MonarchTrait,
                        traitOwner, trait.Active != 0 ? "" : "特性未激活");

            var court = CourtOps.State(em, root);
            int turn = em.GetComponentData<Session>(root).Turn;
            if (query.Kind == RuleKind.ProductionBonus || query.Kind == RuleKind.SoldierAttackBonus)
            {
                bool production = query.Kind == RuleKind.ProductionBonus;
                total += AddState(production ? court.LegacyProduction : court.LegacyAttack, EffectSourceKind.Legacy, "政治遗产", "", sources);
                total += AddState(production ? court.TemporaryProduction : court.TemporaryAttack, EffectSourceKind.Temporary, "临时朝局",
                    turn <= court.TemporaryUntil ? "" : "已到期", sources);
                total += AddState(-court.Disorder, EffectSourceKind.Disorder, "动荡", turn <= court.DisorderUntil ? "" : "已到期", sources);
            }

            if (query.Domain == EffectDomain.Military)
            {
                var content = em.GetComponentData<ContentCatalog>(root).Value;
                for (int definition = 0; definition < content.Value.Definitions.Length; definition++)
                    if (content.Value.Definitions[definition].Kind == ContentKind.Technology)
                    {
                        int level = ResearchOps.Completed(em, root, definition);
                        if (level > 0) Add(definition, level, EffectSourceKind.Technology);
                    }
                using var buildings = Sim.OrderedEntities<Building>(em);
                foreach (var entity in buildings)
                {
                    var building = em.GetComponentData<Building>(entity);
                    var stats = em.GetComponentData<BuildingStats>(entity);
                    var reason = !Sim.Operational(em, entity) ? "建筑未运营" : building.Maintained == 0 ? "维护未满足"
                        : building.Workers < stats.RequiredWorkers ? "工人不足" : "";
                    Add(em.GetComponentData<Identity>(entity).Definition, building.Level, EffectSourceKind.Building, entity, reason);
                }
            }
            return total;
        }

        static float AddState(float value, EffectSourceKind kind, string name, string reason, List<EffectSource> sources)
        {
            float applied = reason.Length == 0 ? value : 0;
            if (value != 0) sources?.Add(new EffectSource
                { Kind = kind, Definition = -1, RuleIndex = -1, Name = name, Reason = reason.Length == 0 ? "生效" : reason, Value = value, Applied = applied });
            return applied;
        }

        static float AddDefinition(EntityManager em, Entity root, EffectQuery query, int definition, int level,
            EffectSourceKind kind, Entity owner, string sourceReason, bool talentEffects, List<EffectSource> sources)
        {
            var content = Sim.Definition(em, root, definition);
            float total = 0;
            for (int offset = 0; offset < content.RuleCount; offset++)
            {
                int index = content.RuleStart + offset;
                var rule = Sim.GetRule(em, root, index);
                bool scaled = talentEffects && rule.Kind == RuleKind.TalentEffect && rule.B == 0
                    && EffectRules.SupportsTalentEffect(content.Kind, rule.B, rule.Amount)
                    && (query.Kind == RuleKind.AttackBonus && rule.Amount == 140
                        || query.Kind == RuleKind.ProductionBonus && rule.Amount == 100
                        || query.Kind == RuleKind.PublicOpinion && rule.Amount == 150);
                if (rule.Kind != query.Kind && !scaled) continue;
                string reason = sourceReason;
                if (!EffectRules.LevelMatches(rule, level, query.Domain) && reason.Length == 0) reason = "适用等级不符";
                if (query.Kind == RuleKind.Intelligence)
                {
                    if (owner != Entity.Null && reason.Length == 0)
                    {
                        var building = em.GetComponentData<Building>(owner);
                        reason = building.Stage != LifeStage.Operational ? "建筑未运营" : building.Maintained == 0 ? "维护未满足"
                            : building.Workers < rule.B ? "工人不足（需要 " + rule.B + "）" : "";
                    }
                    if (reason.Length == 0 && rule.Target >= 0 && ResearchOps.Completed(em, root, rule.Target) == 0)
                        reason = "需要科技：" + Sim.Definition(em, root, rule.Target).Name;
                }
                else
                {
                    if (reason.Length == 0 && rule.Target >= 0 && rule.Target != query.TargetDefinition) reason = "作用对象不匹配";
                    if (reason.Length == 0 && query.Kind == RuleKind.FlatProductionBonus && rule.Secondary >= 0
                        && rule.Secondary != query.TargetBuildingDefinition) reason = "目标建筑不匹配";
                }
                float value = scaled ? ScaledTalentValue(em, root, rule, level) / (query.Kind == RuleKind.PublicOpinion ? 1 : 100)
                    : query.Kind == RuleKind.FlatProductionBonus || query.Kind == RuleKind.Intelligence ? rule.Amount : rule.Value;
                float applied = reason.Length == 0 ? value : 0;
                total += applied;
                if (sources != null)
                {
                    var identity = owner == Entity.Null ? default : em.GetComponentData<Identity>(owner);
                    sources.Add(new EffectSource { Kind = kind, Definition = definition, Level = level, RuleIndex = index,
                        Owner = identity.Id, Name = owner == Entity.Null ? content.Name.ToString() : identity.Name + " / " + content.Name,
                        Reason = reason.Length == 0 ? "生效" : reason, Value = value, Applied = applied });
                }
            }
            return total;
        }

        internal static float ScaledTalentValue(EntityManager em, Entity root, Rule rule, int level)
        {
            float value = rule.Value + rule.Extra * (level - 1);
            if (rule.C == 20 && rule.Target >= 0) value *= InventoryOps.Count(em, root, rule.Target) / 100f;
            else if (rule.C == 30) value *= level;
            else if (rule.C == 40) value *= Sim.Population(em, root);
            else if (rule.C == 50)
            {
                int count = 0;
                using var buildings = Sim.OrderedEntities<Building>(em);
                foreach (var building in buildings)
                    if (Sim.Operational(em, building) && (rule.Target < 0 || em.GetComponentData<Identity>(building).Definition == rule.Target)) count++;
                value *= count;
            }
            return value;
        }
    }
}
