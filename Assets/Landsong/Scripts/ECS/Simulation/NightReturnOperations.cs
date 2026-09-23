using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // End-of-night recall uses the same orders, DBP and navigation as ordinary garrison recall.
    public static class NightReturnOps
    {
        static bool Owned(EntityManager em, Entity root, Entity unit) => !em.HasComponent<SimulationOwner>(unit) || em.GetComponentData<SimulationOwner>(unit).Root == root;
        public static void Begin(EntityManager em, Entity root)
        {
            var settings = em.GetComponentData<DayReturnSettings>(root);
            var turn = em.GetComponentData<GameClock>(root).Turn;
            using var actors = WorldQueries.OrderedEntities<Combatant>(em);
            foreach (var unit in actors)
            {
                var actor = em.GetComponentData<Combatant>(unit);
                if (!Owned(em, root, unit) || em.HasComponent<TransportWorker>(unit) || em.HasComponent<Firefighter>(unit) || actor.Faction != 0 || !EntityState.Alive(em, unit) || em.HasComponent<DayReturnState>(unit))
                    continue;
                if (em.HasComponent<Soldier>(unit))
                {
                    var soldier = em.GetComponentData<Soldier>(unit);
                    soldier.RecallState = 0;
                    em.SetComponentData(unit, soldier);
                }

                actor.Target = Entity.Null;
                actor.DeployAt = float.MaxValue;
                em.SetComponentData(unit, actor);
                if (actor.Deployed == 0)
                    continue;
                var id = em.GetComponentData<Identity>(unit).Id;
                var sample = (math.hash(new uint3((uint)id, (uint)(id >> 32), (uint)turn)) & 0xffffff) / 16777215f;
                EntityState.Set(em, unit, new DayReturnState { Remaining = math.lerp(settings.MinimumSeconds, settings.MaximumSeconds, sample), RetryIn = 1 });
                em.SetComponentData(unit, new VisualState { Visible = 1 });
                if (!Route(em, root, unit))
                    UnitOrders.Order(em, unit, default);
            }
        }

        static bool Route(EntityManager em, Entity root, Entity unit)
        {
            var actor = em.GetComponentData<Combatant>(unit);
            using var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, unit), actor.Profile.BodyRadius);
            var home = WorldQueries.Find(em, actor.HomeId);
            if (BuildingStatus.Operational(em, home) && reach.Building(em.GetComponentData<BuildingPlacementState>(home), out var point) < float.MaxValue)
            {
                UnitOrders.Order(em, unit, new UnitOrder { Kind = OrderKind.Recall, Source = actor.HomeId, Destination = point });
                return true;
            }

            // A destroyed/inaccessible home may use a reachable friendly core or garrison; never teleport.
            var best = float.MaxValue;
            var destination = EntityState.Position(em, unit);
            ulong fallback = 0;
            using var sites = WorldQueries.OrderedEntities<Building>(em);
            foreach (var site in sites)
            {
                if (!Owned(em, root, site) || !BuildingStatus.Operational(em, site))
                    continue;
                var stats = em.GetComponentData<BuildingHousingStats>(site);
                BuildingGarrisonStats statsGarrison = em.GetComponentData<BuildingGarrisonStats>(site);
                if (stats.IsCore == 0 && statsGarrison.Capacity <= 0)
                    continue;
                var distance = reach.Building(em.GetComponentData<BuildingPlacementState>(site), out var candidate);
                if (distance >= best)
                    continue;
                best = distance;
                destination = candidate;
                fallback = em.GetComponentData<Identity>(site).Id;
            }

            if (fallback == 0)
                return false;
            UnitOrders.Order(em, unit, new UnitOrder { Kind = OrderKind.Recall, Source = fallback, Destination = destination });
            return true;
        }

        static void Shelter(EntityManager em, Entity unit)
        {
            em.RemoveComponent<DayReturnState>(unit);
            var actor = em.GetComponentData<Combatant>(unit);
            actor.Deployed = 0;
            actor.Target = Entity.Null;
            em.SetComponentData(unit, actor);
            UnitOrders.Order(em, unit, default);
            em.SetComponentData(unit, new VisualState());
            if (!em.HasComponent<Soldier>(unit))
                return;
            var soldier = em.GetComponentData<Soldier>(unit);
            soldier.RecallState = 0;
            em.SetComponentData(unit, soldier);
        }

        public static float Completion(EntityManager em, Entity root)
        {
            int total = 0, returned = 0;
            using var actors = WorldQueries.OrderedEntities<Combatant>(em);
            foreach (var unit in actors)
            {
                var actor = em.GetComponentData<Combatant>(unit);
                if (!Owned(em, root, unit) || em.HasComponent<TransportWorker>(unit) || em.HasComponent<Firefighter>(unit) || actor.Faction != 0 || !EntityState.Alive(em, unit))
                    continue;
                total++;
                if (actor.Deployed == 0)
                    returned++;
            }

            return total == 0 ? 1 : returned / (float)total;
        }

        public static bool Tick(EntityManager em, Entity root, float delta)
        {
            if (em.GetComponentData<SimulationControl>(root).Paused != 0 || em.GetComponentData<Session>(root).Phase != Phase.Day)
                return Completion(em, root) >= 1;
            int pending = 0;
            using var actors = WorldQueries.OrderedEntities<DayReturnState>(em);
            foreach (var unit in actors)
            {
                var actor = em.GetComponentData<Combatant>(unit);
                if (!Owned(em, root, unit))
                    continue;
                var returning = em.GetComponentData<DayReturnState>(unit);
                returning.Remaining -= math.max(0, delta);
                returning.RetryIn -= math.max(0, delta);
                if (actor.Deployed == 0 || !EntityState.Alive(em, unit) || returning.Remaining <= 0)
                {
                    Shelter(em, unit);
                    continue;
                }
                em.SetComponentData(unit, returning);
                var order = em.GetComponentData<UnitOrder>(unit);
                var destinationExists = BuildingStatus.Operational(em, WorldQueries.Find(em, order.Source));
                if (order.Kind != OrderKind.Recall || !destinationExists || em.GetComponentData<NavigationState>(unit).Failed != 0)
                {
                    if (returning.RetryIn > 0)
                    {
                        pending++;
                        continue;
                    }

                    returning.RetryIn = 1;
                    em.SetComponentData(unit, returning);
                    Route(em, root, unit);
                    pending++;
                    continue;
                }

                var position = EntityState.Position(em, unit);
                if (math.distance(position.xz, order.Destination.xz) > .65f || math.abs(position.y - order.Destination.y) > .75f)
                {
                    pending++;
                    continue;
                }

                Shelter(em, unit);
            }

            return pending == 0;
        }
    }
}
