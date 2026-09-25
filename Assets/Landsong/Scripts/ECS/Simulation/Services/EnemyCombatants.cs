using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class EnemyCombatants
    {
        public static void Configure(EntityManager em, Entity root, Entity unit, bool deployed, ulong homeId, float3 home)
        {
            ref var definition = ref EnemyDefinitions.Get(em, root, em.GetComponentData<EnemyDefinitionRef>(unit).Definition);
            var value = definition.CombatStats;
            var stats = new CombatStatsSnapshot
            {
                Health = value.Vitality > 0 ? value.Vitality : value.MaximumHealth,
                Damage = value.Vitality > 0 ? value.AttackAttribute == AttackAttributeKind.Intelligence ? value.Intelligence : value.Strength : value.Damage,
                Speed = value.Vitality > 0 ? value.Agility : value.MovementSpeed,
                Range = value.AttackRange,
                Interval = value.AttackIntervalSeconds,
                ProjectileSpeed = value.ProjectileSpeed,
                Combat = value.Profile
            };
            CombatantState.Initialize(em, root, unit, stats, new Combatant { Faction = 1, IsBoss = (byte)((definition.Behavior & EnemyBehaviorFlags.Boss) != 0 ? 1 : 0), Deployed = (byte)(deployed ? 1 : 0), Home = home, HomeId = homeId, TargetMode = (byte)(((int)definition.Behavior >> 1) & 3), Threat = math.max(1, definition.ThreatValue) });
        }
    }
}
