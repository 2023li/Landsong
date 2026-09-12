using System;
using System.Collections.Generic;
using System.IO;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Storage mechanics are internal. Domains own the meaning and permitted mutations.
    internal static class EntitlementStore
    {
        internal static int Level(EntityManager em, Entity root, int definition)
        {
            foreach (var grant in em.GetBuffer<Entitlement>(root))
                if (grant.Definition == definition) return grant.Level;
            return 0;
        }

        internal static void Validate(EntityManager em, Entity root, int definition, int level, ContentKind? expected = null)
        {
            if (!Sim.ValidDefinition(em, root, definition)) throw new InvalidOperationException("许可引用的内容定义无效：" + definition);
            var d = Sim.Definition(em, root, definition);
            if (expected.HasValue && d.Kind != expected.Value) throw new InvalidOperationException("许可目标类型不符：" + d.Id);
            var error = EntitlementRules.Error(d.Kind, d.Id.ToString(), d.Level, d.Flags, level);
            if (error != null) throw new InvalidOperationException(d.Id + "：" + error);
        }

        internal static void Put(EntityManager em, Entity root, int definition, int level, ContentKind expected)
        {
            Validate(em, root, definition, level, expected);
            var rows = em.GetBuffer<Entitlement>(root);
            for (var i = 0; i < rows.Length; i++)
            {
                var row = rows[i];
                if (row.Definition != definition) continue;
                row.Level = math.max(row.Level, level); rows[i] = row; return;
            }
            rows.Add(new Entitlement { Definition = definition, Level = level });
        }

        internal static void ValidateImport(EntityManager em, Entity root, Entitlement[] rows)
        {
            if (rows == null) throw new InvalidDataException("存档许可表缺失。");
            var definitions = new HashSet<int>();
            foreach (var row in rows)
            {
                if (!definitions.Add(row.Definition)) throw new InvalidDataException("存档包含重复许可：" + row.Definition);
                try { Validate(em, root, row.Definition, row.Level); }
                catch (InvalidOperationException error) { throw new InvalidDataException("存档许可无效：" + error.Message, error); }
            }
        }
    }

    public static class BlueprintOps
    {
        public static bool Has(EntityManager em, Entity root, int definition, int requiredLevel = 1)
            => requiredLevel > 0 && Sim.ValidDefinition(em, root, definition)
                && Sim.Definition(em, root, definition).Kind == ContentKind.Building
                && EntitlementStore.Level(em, root, definition) >= requiredLevel;

        public static void Grant(EntityManager em, Entity root, int definition, int level = 1)
            => EntitlementStore.Put(em, root, definition, level, ContentKind.Building);
    }

    public static class PermanentBuffOps
    {
        public static int Level(EntityManager em, Entity root, int definition)
            => Sim.ValidDefinition(em, root, definition) && Sim.Definition(em, root, definition).Kind == ContentKind.Buff
                ? EntitlementStore.Level(em, root, definition) : 0;
        public static bool Has(EntityManager em, Entity root, int definition, int requiredLevel = 1)
            => requiredLevel > 0 && Level(em, root, definition) >= requiredLevel;
        public static void Grant(EntityManager em, Entity root, int definition, int level = 1)
            => EntitlementStore.Put(em, root, definition, level, ContentKind.Buff);
    }

    public static class ProgressionFacts
    {
        public static bool QuestClaimed(EntityManager em, Entity root, int definition)
            => IsRecorded(em, root, definition, ContentKind.Quest);
        public static bool ExpeditionSucceeded(EntityManager em, Entity root, int definition)
            => IsRecorded(em, root, definition, ContentKind.Expedition);
        static bool IsRecorded(EntityManager em, Entity root, int definition, ContentKind kind)
            => Sim.ValidDefinition(em, root, definition) && Sim.Definition(em, root, definition).Kind == kind
                && EntitlementStore.Level(em, root, definition) > 0;
        internal static void RecordQuestClaim(EntityManager em, Entity root, int definition)
            => EntitlementStore.Put(em, root, definition, 1, ContentKind.Quest);
        internal static void RecordExpeditionSuccess(EntityManager em, Entity root, int definition)
            => EntitlementStore.Put(em, root, definition, 1, ContentKind.Expedition);
    }

    public static class ConditionOps
    {
        public static bool Satisfied(EntityManager em, Entity root, int definition, int required = 1)
        {
            if (required <= 0 || !Sim.ValidDefinition(em, root, definition)) return false;
            switch (Sim.Definition(em, root, definition).Kind)
            {
                case ContentKind.Building: return BlueprintOps.Has(em, root, definition, required);
                case ContentKind.Technology: return ResearchOps.Completed(em, root, definition) >= required;
                case ContentKind.Buff: return PermanentBuffOps.Has(em, root, definition, required);
                case ContentKind.Feature: return required == 1 && FeatureOps.IsUnlocked(em, root, definition);
                case ContentKind.Quest: return required == 1 && ProgressionFacts.QuestClaimed(em, root, definition);
                case ContentKind.Expedition: return required == 1 && ProgressionFacts.ExpeditionSucceeded(em, root, definition);
                default: return false;
            }
        }

        public static bool Prerequisites(EntityManager em, Entity root, int definition)
        {
            var d = Sim.Definition(em, root, definition);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var rule = Sim.GetRule(em, root, d.RuleStart + i);
                if (rule.Kind == RuleKind.Prerequisite && !Satisfied(em, root, rule.Target, math.max(1, rule.Amount))) return false;
            }
            return true;
        }
    }
}
