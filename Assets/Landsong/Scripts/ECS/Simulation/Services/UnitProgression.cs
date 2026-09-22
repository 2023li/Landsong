using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class UnitProgression
    {
        public static int Level(SoldierGrowth g, int experience)
        {
            int level = 1;
            while (level < math.max(1, g.MaxLevel))
            {
                int cost = g.FirstLevelExperience + (level - 1) * g.ExperienceStep;
                if (cost <= 0 || experience < cost)
                    break;
                experience -= cost;
                level++;
            }

            return level;
        }

        public static int LevelThreshold(SoldierGrowth g, int level)
        {
            long n = math.clamp(level - 1, 0, math.max(0, g.MaxLevel - 1));
            return (int)Math.Min(int.MaxValue, n * g.FirstLevelExperience + n * (n - 1) / 2 * g.ExperienceStep);
        }

        internal static void ApplyGrowth(ref CombatStatsSnapshot stats, SoldierGrowth growth, int experience)
        {
            int ranks = UnitProgression.Level(growth, experience) - 1;
            stats.Health *= 1 + ranks * growth.HealthPerLevel;
            stats.Damage *= 1 + ranks * growth.DamagePerLevel;
        }
    }
}
