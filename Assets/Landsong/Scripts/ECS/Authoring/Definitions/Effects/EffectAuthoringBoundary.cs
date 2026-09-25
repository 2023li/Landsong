using System;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    // Each entry and owner must have an actual runtime consumer. Typed targets alone
    // cannot prevent an unsupported effect kind from becoming a silent no-op.
    public static class EffectAuthoringBoundary
    {
        public static bool Military(NumericEffectKind kind) => kind == NumericEffectKind.AttackMultiplier || kind == NumericEffectKind.HealthMultiplier || kind == NumericEffectKind.SoldierAttackMultiplier || kind == NumericEffectKind.SoldierSpeedMultiplier || kind == NumericEffectKind.AttackRangeMultiplier || kind == NumericEffectKind.AttackSpeedMultiplier || kind == NumericEffectKind.MovementSpeedMultiplier || kind == NumericEffectKind.Armor || kind == NumericEffectKind.DamageReduction || kind == NumericEffectKind.Penetration || kind == NumericEffectKind.ProjectileSpeedMultiplier || kind == NumericEffectKind.BlastRadius;
        static void Require(bool valid, string target)
        {
            if (!valid)
                throw new InvalidOperationException(target + "：效果没有对应的运行时结算。");
        }

        public static void Item(NumericEffectKind kind) => Require(kind == NumericEffectKind.LossMultiplier || kind == NumericEffectKind.ProductionMultiplier || kind == NumericEffectKind.CropHarvestMultiplier, "物品");
        public static void Building(NumericEffectKind kind) => Require(kind == NumericEffectKind.Armor || kind == NumericEffectKind.DamageReduction || kind == NumericEffectKind.ActionPower, "建筑");
        public static void Soldier(NumericEffectKind kind) => Require(Military(kind) || kind == NumericEffectKind.EquipmentBreakChanceMultiplier, "士兵");
        public static void Hero(NumericEffectKind kind) => Require(Military(kind) && kind != NumericEffectKind.SoldierAttackMultiplier && kind != NumericEffectKind.SoldierSpeedMultiplier, "英雄");
        public static void Talent(NumericEffectKind kind) => Require(kind == NumericEffectKind.NaturalDeathRisk, "人才");
        public static void Kingdom(KingdomEffectKind kind) => Require(Enum.IsDefined(typeof(KingdomEffectKind), kind), "王国");
        public static void Technology(DefinitionEffectsSource source)
        {
            if (source == null)
                throw new InvalidOperationException("缺少科技效果配置。");
            Require(source.Items.Length == 0 && source.Talents.Length == 0 && source.Kingdom.Length == 0 && source.FlatProduction.Length == 0, "科技");
            foreach (var entry in source.Buildings)
                Require(entry != null && Military(entry.Effect), "科技建筑效果");
        }

        public static void NoIntelligence(DefinitionEffectsSource source)
        {
            if (source == null || source.Intelligence == null || source.Intelligence.Length != 0)
                throw new InvalidOperationException("该领域不支持情报效果。");
        }

        public static void Policy(DefinitionEffectsSource source)
        {
            if (source == null || source.Intelligence == null)
                throw new InvalidOperationException("缺少政策效果配置。");
            foreach (var entry in source.Intelligence)
                Require(entry != null && entry.Level <= 1, "政策情报等级");
        }
    }
}
