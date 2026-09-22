using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class BuildingDefenseStats
    {
        public static CombatProfile Current(EntityManager em, Entity root, BuildingId definition)
        {
            var profile = BuildingDefinitions.Get(em, root, definition).DefenseStats.Profile;
            profile.Armor = math.max(0, profile.Armor + BuildingStatEffects.Modifier(em, root, NumericEffectKind.Armor, definition));
            profile.Reduction = math.clamp(profile.Reduction + BuildingStatEffects.Modifier(em, root, NumericEffectKind.DamageReduction, definition), 0, .95f);
            profile.Penetration = math.max(0, profile.Penetration + BuildingStatEffects.Modifier(em, root, NumericEffectKind.Penetration, definition));
            profile.BlastRadius = math.clamp(profile.BlastRadius + BuildingStatEffects.Modifier(em, root, NumericEffectKind.BlastRadius, definition), 0, 32);
            return profile;
        }

        public static CombatProfile ForNight(EntityManager em, Entity root, BuildingId definition)
        {
            if (em.GetComponentData<Session>(root).Phase != Phase.Day && NightPlanOps.State(em, root).PreparedTurn == em.GetComponentData<GameClock>(root).Turn && em.HasBuffer<PreparedBuildingDefense>(root))
                foreach (var entry in em.GetBuffer<PreparedBuildingDefense>(root))
                    if (entry.Definition == definition)
                        return entry.Profile;
            return Current(em, root, definition);
        }
    }
}
