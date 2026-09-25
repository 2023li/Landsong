using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class SoldierCombatants
    {
        public static void Configure(EntityManager em, Entity root, Entity unit, bool deployed, ulong homeId, float3 home)
        {
            var id = em.GetComponentData<SoldierDefinitionRef>(unit).Definition;
            ref var definition = ref SoldierDefinitions.Get(em, root, id);
            var stats = em.HasComponent<Soldier>(unit) ? SoldierOps.SoldierStats(em, root, unit) : SoldierCombatStats.ForNight(em, root, id);
            if (!em.HasComponent<Soldier>(unit))
                SoldierCombatStats.ApplyWeapon(em, root, ref stats, SoldierWeaponKind.None);
            CombatantState.Initialize(em, root, unit, stats, new Combatant { Faction = 0, Deployed = (byte)(deployed ? 1 : 0), Home = home, HomeId = homeId, TargetMode = definition.TargetMode, Threat = math.max(1, definition.ThreatValue) });
        }
    }
}
