using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class BellOps
    {
        public static void Bell(EntityManager em, Entity root, Entity bell)
        {
            if (!BuildingStatus.Operational(em, bell))
                return;
            var radius = em.GetComponentData<BuildingBellStats>(bell).Radius;
            if (radius <= 0)
                return;
            var id = em.GetComponentData<Identity>(bell).Id;
            NightRuntimeState stateNight = em.GetComponentData<NightRuntimeState>(root);
            HeroSelection stateHeroSelection = em.GetComponentData<HeroSelection>(root);
            BellState stateBell = em.GetComponentData<BellState>(root);
            var previous = stateBell.ActiveBell;
            var cancel = previous == id;
            stateBell.ActiveBell = cancel ? 0 : id;
            {
                em.SetComponentData(root, stateNight);
                em.SetComponentData(root, stateHeroSelection);
                em.SetComponentData(root, stateBell);
            }

            using var all = WorldQueries.Entities<Combatant>(em);
            foreach (var e in all)
            {
                var a = em.GetComponentData<Combatant>(e);
                if (a.Faction != 0 || a.Deployed == 0 || !EntityState.Alive(em, e))
                    continue;
                var order = em.GetComponentData<UnitOrder>(e);
                bool engaged = EntityState.Alive(em, a.Target) && math.distance(EntityState.Position(em, e), EntityState.Position(em, a.Target)) <= a.Range;
                if ((order.Kind == OrderKind.Rally || order.Kind == OrderKind.Capture) && order.Source == previous && previous != 0)
                {
                    if (engaged || e == stateHeroSelection.SelectedHero)
                        em.SetComponentData(e, new UnitOrder());
                    else
                        UnitOrders.ReturnToAnchor(em, root, e);
                }

                if (cancel || e == stateHeroSelection.SelectedHero || em.HasComponent<Soldier>(e) && em.GetComponentData<Soldier>(e).RecallState != 0)
                    continue;
                if (engaged)
                    continue;
                if (math.distancesq(EntityState.Position(em, e), EntityState.Position(em, bell)) > radius * radius)
                    continue;
                using var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, e));
                if (reach.Building(em.GetComponentData<BuildingPlacementState>(bell), out var point) < float.MaxValue)
                    UnitOrders.Order(em, e, new UnitOrder { Kind = OrderKind.Rally, Destination = point, Source = id });
            }

            if (stateNight.Kind == NightKind.Peaceful && !cancel)
                PeacefulOps.Bell(em, root, bell);
        }
    }
}
