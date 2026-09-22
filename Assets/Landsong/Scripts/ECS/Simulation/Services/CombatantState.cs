using Landsong.ECS.Definitions;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    internal static class CombatantState
    {
        internal static void Initialize(EntityManager em, Entity root, Entity e, CombatStatsSnapshot stats, Combatant actor)
        {
            em.RemoveComponent<DayReturnState>(e);
            EntityState.Set(em, e, new UnitOrder());
            EntityState.Set(em, e, new Steering());
            EntityState.Buffer<Waypoint>(em, e);
            var home = actor.Home;
            var homeId = actor.HomeId;
            actor.Damage = stats.Damage;
            actor.Speed = stats.Speed;
            actor.TargetRevision = -1;
            actor.Range = stats.Range;
            actor.Interval = stats.Interval;
            actor.ProjectileSpeed = stats.ProjectileSpeed;
            actor.Profile = stats.Combat;
            EntityState.Set(em, e, new TacticalState { Origin = home });
            var target = WorldQueries.Find(em, homeId);
            actor.TargetAnchor = target != Entity.Null && em.HasComponent<Building>(target) ? EntityState.Position(em, target) : home;
            EntityState.Set(em, e, actor);
            EntityState.Set(em, e, new Health { Current = stats.Health, Maximum = stats.Health });
            EntityState.Set(em, e, new NavigationState { Revision = -1 });
            EntityState.Set(em, e, new Perception());
            EntityState.Set(em, e, new VisualState { Visible = actor.Deployed });
        }
    }
}
