using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class SoldierCombatStats
    {
        public static CombatStatsSnapshot Current(EntityManager em, Entity root, SoldierId definition)
        {
            var stats = SoldierDefinitions.Get(em, root, definition).CombatStats;
            float Modifier(NumericEffectKind kind) => SoldierEffects.Modifier(em, root, kind, definition);
            var profile = stats.Profile;
            profile.Armor = math.max(0, profile.Armor + Modifier(NumericEffectKind.Armor));
            profile.Reduction = math.clamp(profile.Reduction + Modifier(NumericEffectKind.DamageReduction), 0, .95f);
            profile.Penetration = math.max(0, profile.Penetration + Modifier(NumericEffectKind.Penetration));
            profile.BlastRadius = math.clamp(profile.BlastRadius + Modifier(NumericEffectKind.BlastRadius), 0, 32);
            return new CombatStatsSnapshot
            {
                Health = stats.MaximumHealth * math.max(.1f, 1 + Modifier(NumericEffectKind.HealthMultiplier)),
                Damage = stats.Damage * math.max(0, 1 + Modifier(NumericEffectKind.AttackMultiplier) + Modifier(NumericEffectKind.SoldierAttackMultiplier)),
                Speed = stats.MovementSpeed * math.max(.1f, 1 + Modifier(NumericEffectKind.MovementSpeedMultiplier) + Modifier(NumericEffectKind.SoldierSpeedMultiplier)),
                Range = math.max(.1f, stats.AttackRange * (1 + Modifier(NumericEffectKind.AttackRangeMultiplier))),
                Interval = math.max(.05f, stats.AttackIntervalSeconds / math.max(.1f, 1 + Modifier(NumericEffectKind.AttackSpeedMultiplier))),
                ProjectileSpeed = stats.ProjectileSpeed == 0 ? 0 : math.max(.1f, stats.ProjectileSpeed * (1 + Modifier(NumericEffectKind.ProjectileSpeedMultiplier))),
                Combat = profile
            };
        }

        public static CombatStatsSnapshot ForNight(EntityManager em, Entity root, SoldierId definition)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day && NightPlanOps.State(em, root).PreparedTurn == em.GetComponentData<GameClock>(root).Turn && em.HasBuffer<PreparedSoldier>(root))
                foreach (var entry in em.GetBuffer<PreparedSoldier>(root))
                    if (entry.Definition == definition)
                        return entry.Stats;
            return Current(em, root, definition);
        }
    }
}
