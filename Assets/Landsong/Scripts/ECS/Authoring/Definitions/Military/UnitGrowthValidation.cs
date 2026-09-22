using System;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring.Definitions
{
    public static class UnitGrowthValidation
    {
        public static void Soldier(SoldierGrowth growth, int population, int cost)
        {
            long ranks = growth.MaxLevel - 1L;
            if (ranks * growth.FirstLevelExperience + ranks * (ranks - 1) / 2 * growth.ExperienceStep > int.MaxValue)
                throw new InvalidOperationException("累计升级经验超过整数存储范围。");
            if (growth.MaxLevel < 1 || growth.MaxLevel > 100 || growth.FirstLevelExperience < 1 || growth.FirstLevelExperience > 1000000 || growth.ExperienceStep < 0 || growth.ExperienceStep > 1000000 || growth.BattleExperience < 0 || growth.BattleExperience > 1000000 || !math.isfinite(growth.HealthPerLevel) || !math.isfinite(growth.DamagePerLevel) || growth.HealthPerLevel < 0 || growth.HealthPerLevel > 10 || growth.DamagePerLevel < 0 || growth.DamagePerLevel > 10 || population < 0 || cost < 0)
                throw new InvalidOperationException("单位成长或招募配置无效。");
        }

        public static void Hero(HeroGrowth growth, int population, int cost)
        {
            Soldier(growth.Progression, population, cost);
            if (growth.OfferingExperience < 0 || growth.OfferingExperience > 1000000 || !math.isfinite(growth.ContactSeconds) || growth.ContactSeconds <= 0 || growth.ContactSeconds > 30 || !math.isfinite(growth.ExperiencePerSecond) || growth.ExperiencePerSecond < 0 || growth.ExperiencePerSecond > 1000 || !math.isfinite(growth.ThreatReference) || growth.ThreatReference <= 0 || !math.isfinite(growth.MaximumThreatMultiplier) || growth.MaximumThreatMultiplier < 1 || growth.MaximumThreatMultiplier > 100)
                throw new InvalidOperationException("英雄成长配置无效。");
        }
    }
}
