using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class UnitOrders
    {
        public static void Order(EntityManager em, Entity unit, UnitOrder order)
        {
            em.SetComponentData(unit, order);
            em.SetComponentData(unit, new Steering());
            var a = em.GetComponentData<Combatant>(unit);
            a.Target = Entity.Null;
            em.SetComponentData(unit, a);
            em.SetComponentData(unit, new NavigationState { Revision = -1 });
            em.GetBuffer<Waypoint>(unit).Clear();
        }

        internal static void ReturnToAnchor(EntityManager em, Entity root, Entity unit)
        {
            var actor = em.GetComponentData<Combatant>(unit);
            var site = WorldQueries.Find(em, actor.HomeId);
            var destination = actor.Home;
            if (BuildingStatus.Operational(em, site))
            {
                using var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, unit));
                if (reach.Building(em.GetComponentData<BuildingPlacementState>(site), out var point) < float.MaxValue)
                    destination = point;
            }

            UnitOrders.Order(em, unit, new UnitOrder { Kind = OrderKind.Move, Destination = destination });
        }

        public static void TickSoldierOrders(EntityManager em, Entity root)
        {
            HeroSelection stateHeroSelection = em.GetComponentData<HeroSelection>(root);
            BellState stateBell = em.GetComponentData<BellState>(root);
            if (stateBell.ActiveBell != 0 && !BuildingStatus.Operational(em, WorldQueries.Find(em, stateBell.ActiveBell)))
            {
                stateBell.ActiveBell = 0;
                {
                    em.SetComponentData(root, stateHeroSelection);
                    em.SetComponentData(root, stateBell);
                }
            }

            using var actors = WorldQueries.Entities<Combatant>(em);
            foreach (var e in actors)
            {
                var actor = em.GetComponentData<Combatant>(e);
                if (actor.Faction != 0 || !EntityState.Alive(em, e))
                    continue;
                var order = em.GetComponentData<UnitOrder>(e);
                if (order.Kind == OrderKind.Rally && order.Source != stateBell.ActiveBell)
                {
                    if (e == stateHeroSelection.SelectedHero || EntityState.Alive(em, actor.Target) && math.distance(EntityState.Position(em, e), EntityState.Position(em, actor.Target)) <= actor.Range)
                        em.SetComponentData(e, new UnitOrder());
                    else
                        UnitOrders.ReturnToAnchor(em, root, e);
                }

                if (!em.HasComponent<Soldier>(e))
                    continue;
                var soldier = em.GetComponentData<Soldier>(e);
                if (soldier.RecallState != 1)
                    continue;
                if (!BuildingStatus.Operational(em, WorldQueries.Find(em, soldier.Garrison)) || em.GetComponentData<NavigationState>(e).Failed != 0)
                {
                    soldier.RecallState = 0;
                    em.SetComponentData(e, soldier);
                    UnitOrders.Order(em, e, default);
                    SimulationEvents.Emit(em, root, EventKind.Message, "回营中断：驻地荒废或路径不可达", soldier.Garrison);
                    continue;
                }

                if (math.distance(EntityState.Position(em, e).xz, order.Destination.xz) > .65f)
                    continue;
                soldier.RecallState = 2;
                actor.Deployed = 0;
                actor.DeployAt = float.MaxValue;
                actor.Target = Entity.Null;
                em.SetComponentData(e, soldier);
                em.SetComponentData(e, actor);
                UnitOrders.Order(em, e, default);
                em.SetComponentData(e, new VisualState());
            }
        }
    }
}
