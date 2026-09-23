using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class FirefighterOps
    {
        public static Entity Spawn(EntityManager em, Entity root, Firefighter task, ulong identity = 0)
        {
            var appearance = em.GetComponentData<TransportWorkerSettings>(root);
            var settings = em.GetComponentData<FireSettings>(root);
            var entity = em.Instantiate(appearance.Male);
            EntityState.Set(em, entity, new SimulationOwner { Root = root });
            EntityState.Set(em, entity, new Persistent());
            EntityState.Set(em, entity, new Identity { Id = identity == 0 ? EntityIdentityAllocator.AllocateId(em, root) : identity, Name = "消防员" });
            EntityState.Set(em, entity, LocalTransform.FromPosition(task.Start));
            var profile = CombatProfile.Default;
            profile.BodyRadius = .25f;
            CombatantState.Initialize(em, root, entity, new CombatStatsSnapshot
            {
                Health = appearance.Health,
                Speed = settings.FirefighterSpeed > 0 ? settings.FirefighterSpeed : 1.6f,
                Interval = 1,
                Combat = profile,
            }, new Combatant { Faction = 0, Deployed = 1, Home = task.Start, HomeId = task.Station });
            em.RemoveComponent<TacticalState>(entity);
            EntityState.Set(em, entity, task);
            Order(em, entity, task.Stage == FirefighterStage.Returning ? task.Start : task.Destination);
            return entity;
        }

        public static void Kill(EntityManager em, Entity root, Entity entity)
        {
            var task = em.GetComponentData<Firefighter>(entity);
            if (task.DeathRecorded != 0)
                return;
            task.DeathRecorded = 1;
            task.Stage = FirefighterStage.Dead;
            em.SetComponentData(entity, task);
            var health = em.GetComponentData<Health>(entity);
            health.Current = 0;
            em.SetComponentData(entity, health);
            em.SetComponentData(entity, new Steering());
            em.SetComponentData(entity, new VisualState());
            var station = WorldQueries.Find(em, task.Station);
            if (station != Entity.Null && em.HasComponent<BuildingWorkforceState>(station))
            {
                var workforce = em.GetComponentData<BuildingWorkforceState>(station);
                workforce.Workers = math.max(0, workforce.Workers - 1);
                em.SetComponentData(station, workforce);
            }
            PopulationOps.RemovePopulation(em, root, 1);
            WorkforceSettlement.ReconcilePopulation(em, root);
            SimulationEvents.Emit(em, root, EventKind.Death, "消防员殉职，人口 -1", em.GetComponentData<Identity>(entity).Id, -1, HistoryCategory.Military);
        }

        public static void Tick(EntityManager em, Entity root)
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase != Phase.Day && phase != Phase.Night && phase != Phase.Deployment && phase != Phase.Retreat && phase != Phase.Celebration)
                return;
            if (em.GetComponentData<SimulationControl>(root).Paused != 0 || em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0)
                return;
            using (var units = WorldQueries.OrderedEntities<Firefighter>(em))
                foreach (var entity in units)
                {
                    var task = em.GetComponentData<Firefighter>(entity);
                    var station = WorldQueries.Find(em, task.Station);
                    var fire = WorldQueries.Find(em, task.Fire);
                    var validStation = BuildingStatus.Operational(em, station);
                    var validFire = fire != Entity.Null && em.HasComponent<BuildingFireState>(fire) && em.GetComponentData<BuildingFireState>(fire).Burning != 0;
                    if (task.Stage == FirefighterStage.Dead || !EntityState.Alive(em, entity))
                    {
                        if (task.DeathRecorded == 0)
                            Kill(em, root, entity);
                        if (validFire)
                            BuildingFireOps.TaskFailed(em, root, fire, task.Station);
                        em.DestroyEntity(entity);
                        continue;
                    }
                    if (task.Stage == FirefighterStage.Outbound)
                    {
                        var workforce = validStation ? em.GetComponentData<BuildingWorkforceState>(station) : default;
                        if (!validStation || task.WorkerSlot >= workforce.Workers || !validFire
                            || em.HasComponent<NavigationState>(entity) && em.GetComponentData<NavigationState>(entity).Failed != 0)
                        {
                            if (validFire)
                                BuildingFireOps.TaskFailed(em, root, fire, task.Station);
                            em.DestroyEntity(entity);
                            continue;
                        }
                        if (math.distance(EntityState.Position(em, entity), task.Destination) <= .35f)
                        {
                            BuildingFireOps.Extinguish(em, root, fire);
                            task.Stage = FirefighterStage.Returning;
                            em.SetComponentData(entity, task);
                            Order(em, entity, task.Start);
                        }
                    }
                    else if (task.Stage == FirefighterStage.Returning)
                    {
                        if (!validStation || math.distance(EntityState.Position(em, entity), task.Start) <= .35f
                            || em.HasComponent<NavigationState>(entity) && em.GetComponentData<NavigationState>(entity).Failed != 0)
                            em.DestroyEntity(entity);
                    }
                }
            if (phase == Phase.Day || phase == Phase.Night)
                Dispatch(em, root);
        }

        static void Dispatch(EntityManager em, Entity root)
        {
            var settings = em.GetComponentData<FireSettings>(root);
            if (!settings.Station.IsValid || !em.HasComponent<TransportWorkerSettings>(root))
                return;
            var fires = new List<Entity>();
            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var building in buildings)
                    if (em.HasComponent<BuildingFireState>(building) && em.GetComponentData<BuildingFireState>(building).Burning != 0)
                        fires.Add(building);
            fires.Sort((a, b) =>
            {
                var x = em.GetComponentData<BuildingFireState>(a);
                var y = em.GetComponentData<BuildingFireState>(b);
                var compare = x.StartedTurn.CompareTo(y.StartedTurn);
                if (compare == 0) compare = x.StartStrike.CompareTo(y.StartStrike);
                return compare == 0 ? em.GetComponentData<Identity>(a).Id.CompareTo(em.GetComponentData<Identity>(b).Id) : compare;
            });
            foreach (var fire in fires)
            {
                var fireId = em.GetComponentData<Identity>(fire).Id;
                if (BuildingFireOps.HasOutboundResponder(em, root, fireId))
                    continue;
                var failedStation = em.GetComponentData<BuildingFireState>(fire).FailedStation;
                Entity selected = Entity.Null;
                float best = float.PositiveInfinity;
                float3 bestStart = default, bestTarget = default;
                int bestSlot = -1;
                using var stations = WorldQueries.OrderedEntities<Building>(em);
                foreach (var station in stations)
                {
                    if (!BuildingStatus.Operational(em, station)
                        || em.GetComponentData<BuildingDefinitionRef>(station).Definition != settings.Station)
                        continue;
                    var id = em.GetComponentData<Identity>(station).Id;
                    if (id == failedStation)
                        continue;
                    var slot = AvailableSlot(em, station, id);
                    if (slot < 0 || !TryRoute(em, root, station, fire, out var cost, out var start, out var target))
                        continue;
                    if (cost > best || cost == best && selected != Entity.Null && id >= em.GetComponentData<Identity>(selected).Id)
                        continue;
                    selected = station;
                    best = cost;
                    bestStart = start;
                    bestTarget = target;
                    bestSlot = slot;
                }
                if (selected == Entity.Null)
                    continue;
                Spawn(em, root, new Firefighter
                {
                    Station = em.GetComponentData<Identity>(selected).Id,
                    Fire = fireId,
                    WorkerSlot = bestSlot,
                    Start = bestStart,
                    Destination = bestTarget,
                    Stage = FirefighterStage.Outbound,
                });
            }
        }

        static int AvailableSlot(EntityManager em, Entity station, ulong stationId)
        {
            var workers = em.GetComponentData<BuildingWorkforceState>(station).Workers;
            if (workers <= 0)
                return -1;
            var occupied = new HashSet<int>();
            using var units = WorldQueries.Entities<Firefighter>(em);
            foreach (var unit in units)
            {
                var task = em.GetComponentData<Firefighter>(unit);
                if (task.Station == stationId && task.Stage != FirefighterStage.Dead)
                    occupied.Add(task.WorkerSlot);
            }
            for (var slot = 0; slot < workers; slot++)
                if (!occupied.Contains(slot))
                    return slot;
            return -1;
        }

        static bool TryRoute(EntityManager em, Entity root, Entity station, Entity fire, out float cost, out float3 start, out float3 destination)
        {
            cost = float.PositiveInfinity;
            start = destination = default;
            var grid = em.GetComponentData<GridData>(root);
            var occupancy = em.GetBuffer<Occupancy>(root);
            var stationPlace = em.GetComponentData<BuildingPlacementState>(station);
            var firePlace = em.GetComponentData<BuildingPlacementState>(fire);
            using var distances = BuildingRangeOps.Reach(em, root, station, Allocator.Temp);
            for (var y = -1; y <= firePlace.Size.y; y++)
                for (var x = -1; x <= firePlace.Size.x; x++)
                {
                    if (x >= 0 && x < firePlace.Size.x && y >= 0 && y < firePlace.Size.y)
                        continue;
                    var cell = firePlace.Cell + new int2(x, y);
                    var index = GridOps.Index(grid, cell);
                    if (index >= 0 && GridOps.Traversable(grid, occupancy, cell) && distances[index] < cost)
                        cost = distances[index];
                }
            if (!math.isfinite(cost) || cost > BuildingRangeOps.ActionPower(em, root, station))
                return false;
            var exit = GridOps.Position(grid, stationPlace.Cell + new int2(stationPlace.Size.x, 0), new int2(1));
            if (!NavigationOps.TryNearestOpenOnSurface(em, root, exit, 12, stationPlace.Surface, stationPlace.Elevation, out start))
                return false;
            using var reachable = new NightSpatialOps.Reach(em, root, start, .25f);
            return reachable.Building(firePlace, out destination) < float.MaxValue;
        }

        static void Order(EntityManager em, Entity unit, float3 destination)
        {
            var task = em.GetComponentData<Firefighter>(unit);
            em.SetComponentData(unit, new UnitOrder { Kind = OrderKind.Recall, Source = task.Station, Destination = destination });
            em.SetComponentData(unit, new Steering { Moving = 1, Destination = destination });
            var actor = em.GetComponentData<Combatant>(unit);
            actor.Deployed = 1;
            actor.Target = Entity.Null;
            em.SetComponentData(unit, actor);
            em.SetComponentData(unit, new VisualState { Visible = 1 });
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GameLoopSystem))]
    [UpdateBefore(typeof(PerceptionSystem))]
    public partial struct FirefighterSystem : ISystem
    {
        public void OnCreate(ref SystemState state) => state.RequireForUpdate<SimulationReady>();
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            var root = WorldQueries.Root(state.EntityManager);
            if (root != Entity.Null)
                FirefighterOps.Tick(state.EntityManager, root);
        }
    }
}
