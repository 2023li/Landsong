using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class HeroCombatants
    {
        public static void Configure(EntityManager em, Entity root, Entity unit, bool deployed, ulong homeId, float3 home)
        {
            var id = em.GetComponentData<HeroDefinitionRef>(unit).Definition;
            ref var definition = ref HeroDefinitions.Get(em, root, id);
            var stats = HeroCombatStats.ForNight(em, root, id);
            if (em.HasComponent<Hero>(unit))
                UnitProgression.ApplyGrowth(ref stats, definition.Growth.Progression, em.GetComponentData<Hero>(unit).Experience);
            EntityState.Set(em, unit, new HeroCombat());
            CombatantState.Initialize(em, root, unit, stats, new Combatant { Faction = 0, IsHero = 1, Deployed = (byte)(deployed ? 1 : 0), Home = home, HomeId = homeId, TargetMode = definition.TargetMode, Threat = math.max(1, definition.ThreatValue) });
        }
    }
}
