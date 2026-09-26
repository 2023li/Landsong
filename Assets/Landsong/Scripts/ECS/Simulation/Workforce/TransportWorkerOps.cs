using System.Collections.Generic;
using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Landsong.ECS
{
    public static class TransportWorkerOps
    {
        static bool Owned(EntityManager em, Entity root, Entity entity) => em.GetComponentData<SimulationOwner>(entity).Root == root;

        static bool EndpointBlockedByBuilding(GridData grid, DynamicBuffer<Occupancy> occupancy, float3 point)
        {
            var cell = GridOps.Cell(grid, point);
            int index = GridOps.Index(grid, cell);
            if (index < 0 || math.abs(GridOps.Position(grid, cell, new int2(1)).y + .5f - point.y) > .55f)
                return false; // A separate upper surface is not blocked by lower-ground occupancy.
            var reservation = occupancy[index];
            return reservation.Owner != 0 && reservation.MovementCost <= 0;
        }

        static bool ReleaseCoveredWorker(EntityManager em, Entity root, Entity entity, GridData grid, DynamicBuffer<Occupancy> occupancy)
        {
            var position = EntityState.Position(em, entity);
            if (!EndpointBlockedByBuilding(grid, occupancy, position))
                return false;
            int index = GridOps.Index(grid, GridOps.Cell(grid, position));
            var ground = grid.Value.Value.Cells[index];
            if (!NavigationOps.TryNearestOpenOnSurface(em, root, position, 12, ground.Surface, ground.Elevation, out var safe))
                return false;
            var transform = em.GetComponentData<LocalTransform>(entity);
            transform.Position = safe;
            em.SetComponentData(entity, transform);
            em.GetBuffer<Waypoint>(entity).Clear();
            var navigation = em.GetComponentData<NavigationState>(entity);
            navigation.Revision = -1;
            navigation.StalledSeconds = 0;
            em.SetComponentData(entity, navigation);
            return true;
        }

        public static Entity Spawn(EntityManager em, Entity root, TransportWorker worker)
        {
            var settings = em.GetComponentData<TransportWorkerSettings>(root);
            var entity = em.Instantiate(worker.Variant == 0 ? settings.Male : settings.Female);
            EntityState.Set(em, entity, new SimulationOwner { Root = root });
            EntityState.Set(em, entity, new Persistent());
            EntityState.Set(em, entity, new Identity { Id = EntityIdentityAllocator.AllocateId(em, root), Name = worker.Variant == 0 ? "运输工人（男）" : "运输工人（女）" });
            EntityState.Set(em, entity, LocalTransform.FromPosition(worker.Start));
            var profile = CombatProfile.Default;
            profile.BodyRadius = .25f;
            CombatantState.Initialize(em, root, entity, new CombatStatsSnapshot {
                Health = settings.Health, Speed = settings.Speed, Interval = 1, Combat = profile
            }, new Combatant { Faction = 0, Deployed = (byte)(worker.Stage == TransportStage.Sheltered || worker.Stage == TransportStage.Dead ? 0 : 1), Home = worker.Start, HomeId = worker.Provider });
            // Transport owns steering. No soldier definition, economic Soldier, or combat behaviour tree.
            em.RemoveComponent<TacticalState>(entity);
            EntityState.Set(em, entity, worker);
            EntityState.Buffer<TransportCargo>(em, entity);
            return entity;
        }

        public static void Kill(EntityManager em, Entity root, Entity entity)
        {
            var worker = em.GetComponentData<TransportWorker>(entity);
            if (worker.DeathRecorded != 0) return;
            if (worker.CargoAssigned != 0 && worker.Delivered == 0 && worker.CargoSettled == 0)
            {
                using var cargo = em.GetBuffer<TransportCargo>(entity).ToNativeArray(Allocator.Temp);
                var items = cargo.ToArray();
                var position = EntityState.Position(em, entity);
                if (NavigationOps.TryNearestOpen(em, root, position, 12, out var open)) position = open;
                foreach (var item in items)
                {
                    if (LootDefinitions.Count(em, root) == 0) throw new System.InvalidOperationException("运输工人物资掉落缺少掉落预制体。");
                    var drop = LootEntities.Spawn(em, root, LootId.FromIndex(0), position, true);
                    EntityState.Set(em, drop, new Loot { Item = item.Item, Count = item.Amount, SourceName = "运输工人遗失物资" });
                    EntityState.Set(em, drop, new WorkerCargoDrop());
                    EntityState.Set(em, drop, new VisualState { Visible = 1 });
                }
                em.GetBuffer<TransportCargo>(entity).Clear();
            }
            else if (worker.CargoSettled != 0)
                em.GetBuffer<TransportCargo>(entity).Clear();
            worker.DeathRecorded = 1;
            worker.Stage = TransportStage.Dead;
            worker.Carrying = 0;
            worker.Retiring = 1;
            em.SetComponentData(entity, worker);
            var health = em.GetComponentData<Health>(entity); health.Current = 0; em.SetComponentData(entity, health);
            em.SetComponentData(entity, new Steering());
            em.SetComponentData(entity, new VisualState());
            PopulationOps.RemovePopulation(em, root, 1);
            WorkforceSettlement.ReconcilePopulation(em, root);
            SimulationEvents.Emit(em, root, EventKind.Death, "运输工人遇袭死亡，人口 -1", em.GetComponentData<Identity>(entity).Id, -1, HistoryCategory.Military);
        }

        public static void Tick(EntityManager em, Entity root, float delta, bool reconcile = true)
        {
            if (!em.HasComponent<TransportWorkerSettings>(root) || em.GetComponentData<SimulationControl>(root).Paused != 0
                || em.GetComponentData<PersistenceGate>(root).CheckpointPending != 0) return;
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase != Phase.Day && phase != Phase.Deployment && phase != Phase.Night && phase != Phase.Retreat && phase != Phase.Celebration) return;
            bool day = phase == Phase.Day;
            int turn = em.GetComponentData<GameClock>(root).Turn;
            float dt = math.max(0, day ? delta : NightOps.Delta(em.GetComponentData<NightRuntimeState>(root), delta));
            using (var workers = WorldQueries.OrderedEntities<TransportWorker>(em))
                foreach (var entity in workers)
                {
                    if (!Owned(em, root, entity)) continue;
                    var worker = em.GetComponentData<TransportWorker>(entity);
                    if (day && worker.Turn != turn && (worker.Stage == TransportStage.Sheltered || worker.Stage == TransportStage.Dead))
                    { em.DestroyEntity(entity); continue; }
                    if (worker.Stage == TransportStage.Dead || worker.Stage == TransportStage.Sheltered) continue;
                    if (!EntityState.Alive(em, entity)) { Kill(em, root, entity); continue; }
                    var settings = em.GetComponentData<TransportWorkerSettings>(root);
                    var provider = WorldQueries.Find(em, worker.Provider);
                    var consumer = WorldQueries.Find(em, worker.Consumer);
                    // A paid construction delivery finishes its route after dusk, even if
                    // the building has completed construction in the settlement.
                    bool committedDelivery = worker.CargoAssigned != 0 && worker.CargoSettled != 0 && worker.Delivered == 0;
                    if (!committedDelivery && (!day || !BuildingRangeOps.IsProvider(em, provider) || !em.Exists(consumer)
                        || !em.HasComponent<Building>(consumer) || em.GetComponentData<Building>(consumer).Stage == LifeStage.Ruined))
                        worker.Retiring = 1;
                    if (worker.Retiring != 0 && worker.Stage != TransportStage.Returning)
                    {
                        if (worker.Stage == TransportStage.Loading) worker.Carrying = (byte)(worker.Remaining < settings.LoadSeconds * .5f ? 1 : 0);
                        if (worker.Stage == TransportStage.Unloading) worker.Carrying = (byte)(worker.Remaining > settings.UnloadSeconds * .5f ? 1 : 0);
                        worker.Stage = TransportStage.Returning;
                        worker.Remaining = 0;
                    }
                    // A later building can cover the perimeter cell chosen when this worker
                    // spawned. A* can route to a nearby node, but delivery still compares
                    // against the old exact coordinate, so choose a reachable new port.
                    var grid = em.GetComponentData<GridData>(root);
                    var occupancy = em.GetBuffer<Occupancy>(root);
                    if (ReleaseCoveredWorker(em, root, entity, grid, occupancy))
                    {
                        // An unloading worker whose cell was covered has not reached
                        // the replacement delivery point yet.
                        if (worker.Stage == TransportStage.Unloading)
                        {
                            worker.Stage = TransportStage.Delivering;
                            worker.Remaining = 0;
                        }
                        occupancy = em.GetBuffer<Occupancy>(root);
                    }
                    if (worker.Stage == TransportStage.Delivering && em.Exists(consumer)
                        && EndpointBlockedByBuilding(grid, occupancy, worker.Destination))
                    {
                        using var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, entity), .25f);
                        if (reach.Building(em.GetComponentData<BuildingPlacementState>(consumer), out var destination) < float.MaxValue)
                        {
                            worker.Destination = destination;
                            var navigation = em.GetComponentData<NavigationState>(entity);
                            navigation.Revision = -1;
                            em.SetComponentData(entity, navigation);
                        }
                    }
                    if (worker.Stage == TransportStage.Returning && em.Exists(provider)
                        && EndpointBlockedByBuilding(grid, occupancy, worker.Start))
                    {
                        var placement = em.GetComponentData<BuildingPlacementState>(provider);
                        using var reach = new NightSpatialOps.Reach(em, root, EntityState.Position(em, entity), .25f);
                        if (reach.Building(placement, out var start) < float.MaxValue)
                        {
                            worker.Start = start;
                            var navigation = em.GetComponentData<NavigationState>(entity);
                            navigation.Revision = -1;
                            em.SetComponentData(entity, navigation);
                        }
                    }
                    bool Arrived(float3 point) => math.distance(EntityState.Position(em, entity), point) <= .2f;
                    switch (worker.Stage)
                    {
                        case TransportStage.Delivering when Arrived(worker.Destination):
                            worker.Stage = TransportStage.Unloading; worker.Remaining = settings.UnloadSeconds; break;
                        case TransportStage.Unloading:
                            worker.Remaining = math.max(0, worker.Remaining - dt);
                            if (worker.Remaining <= 0)
                            {
                                worker.Stage = TransportStage.Returning; worker.Carrying = 0; worker.Trips++;
                                if (worker.Delivered == 0)
                                {
                                    worker.Delivered = 1;
                                    foreach (var cargo in em.GetBuffer<TransportCargo>(entity))
                                        em.GetBuffer<TransportDeliveryEvent>(root).Add(new TransportDeliveryEvent { Item = cargo.Item, Amount = cargo.Amount, Position = worker.Destination });
                                    if (worker.CargoSettled != 0)
                                        em.GetBuffer<TransportCargo>(entity).Clear();
                                }
                            }
                            break;
                        case TransportStage.Returning when Arrived(worker.Start):
                            worker.Stage = TransportStage.Sheltered; worker.Carrying = 0;
                            break;
                        case TransportStage.Loading:
                            // Older snapshots may contain the former repeat-trip state.
                            worker.Stage = TransportStage.Sheltered; worker.Carrying = 0; worker.Remaining = 0;
                            break;
                    }
                    em.SetComponentData(entity, worker);
                    var actor = em.GetComponentData<Combatant>(entity);
                    actor.Target = Entity.Null;
                    actor.Deployed = (byte)(worker.Stage == TransportStage.Sheltered ? 0 : 1);
                    em.SetComponentData(entity, actor);
                    em.SetComponentData(entity, new VisualState { Visible = actor.Deployed });
                    em.SetComponentData(entity, new UnitOrder { Kind = OrderKind.Recall, Source = worker.Provider, Destination = worker.Start });
                    em.SetComponentData(entity, new Steering {
                        Moving = (byte)(worker.Stage == TransportStage.Delivering || worker.Stage == TransportStage.Returning ? 1 : 0),
                        Destination = worker.Stage == TransportStage.Delivering ? worker.Destination : worker.Start
                    });
                }
            if (day && reconcile && em.GetComponentData<GameClock>(root).DawnRemaining <= 0) Reconcile(em, root, turn);
        }

        // Called once per second by the system. Public for deterministic verification.
        public static void Reconcile(EntityManager em, Entity root, int turn)
        {
            if (!em.HasComponent<TransportWorkerSettings>(root) || em.GetComponentData<Session>(root).Phase != Phase.Day) return;
            var consumers = new HashSet<ulong>();
            int active = 0;
            using (var workers = WorldQueries.Entities<TransportWorker>(em))
                foreach (var entity in workers)
                {
                    if (!Owned(em, root, entity)) continue;
                    var worker = em.GetComponentData<TransportWorker>(entity);
                    consumers.Add(worker.Consumer);
                    if (worker.DeathRecorded == 0) active++;
                }
            int population = PopulationOps.Population(em, root);
            using var buildings = WorldQueries.OrderedEntities<Building>(em);
            foreach (var consumer in buildings)
            {
                if (active >= population) break;
                if (!Owned(em, root, consumer) || consumers.Contains(em.GetComponentData<Identity>(consumer).Id) || !NeedsConnection(em, root, consumer)) continue;
                var provider = ResourceNetworkOps.Provider(em, root, consumer);
                if (provider == Entity.Null || !Endpoints(em, root, provider, consumer, out var start, out var destination)) continue;
                var b = em.GetComponentData<Building>(consumer);
                var costs = b.Stage == LifeStage.Construction
                    ? BuildingCostOps.ConstructionStage(em, root, em.GetComponentData<BuildingDefinitionRef>(consumer).Definition,
                        em.GetComponentData<BuildingConstructionState>(consumer).Progress + 1)
                    : null;
                if (costs != null && !BuildingCostOps.CanPay(em, root, costs)) continue;
                var spawned = Spawn(em, root, new TransportWorker {
                    Provider = em.GetComponentData<Identity>(provider).Id, Consumer = em.GetComponentData<Identity>(consumer).Id,
                    Start = start, Destination = destination, Turn = turn, Stage = TransportStage.Delivering, Carrying = 1, Variant = (byte)(active % 2),
                    CargoAssigned = (byte)(costs != null && costs.Count > 0 ? 1 : 0)
                });
                if (costs != null && costs.Count > 0)
                {
                    using var scope = EconomyJournalOps.For(em, root, consumer, EconomyReason.Construction);
                    BuildingCostOps.Pay(em, root, costs);
                    var cargo = em.GetBuffer<TransportCargo>(spawned);
                    foreach (var cost in costs) cargo.Add(new TransportCargo { Item = cost.Item, Amount = cost.Amount });
                }
                active++;
            }
        }

        public static Entity ConstructionWorker(EntityManager em, Entity root, Entity consumer)
        {
            var id = em.GetComponentData<Identity>(consumer).Id;
            var turn = em.GetComponentData<GameClock>(root).Turn;
            using var workers = WorldQueries.Entities<TransportWorker>(em);
            foreach (var worker in workers)
                if (Owned(em, root, worker))
                {
                    var state = em.GetComponentData<TransportWorker>(worker);
                    if (state.Consumer == id && state.Turn == turn && state.CargoAssigned != 0) return worker;
                }
            return Entity.Null;
        }

        public static void SettleCargo(EntityManager em, Entity root, Entity entity)
        {
            var worker = em.GetComponentData<TransportWorker>(entity);
            if (worker.CargoAssigned == 0 || worker.CargoSettled != 0) return;
            var consumer = WorldQueries.Find(em, worker.Consumer);
            using (var scope = EconomyJournalOps.For(em, root, consumer, EconomyReason.Construction))
                foreach (var cargo in em.GetBuffer<TransportCargo>(entity))
                    EconomyJournalOps.RecordSettlementOnly(em, root, cargo.Item, -cargo.Amount);
            worker.CargoSettled = 1;
            if (worker.DeathRecorded == 0 && worker.Delivered == 0)
            {
                worker.Retiring = 0;
                if (worker.Stage != TransportStage.Unloading) worker.Carrying = 1;
                if (worker.Stage != TransportStage.Delivering && worker.Stage != TransportStage.Unloading)
                {
                    worker.Stage = TransportStage.Delivering;
                    worker.Remaining = 0;
                }
            }
            else
                em.GetBuffer<TransportCargo>(entity).Clear();
            em.SetComponentData(entity, worker);
        }

        public static void CloseDay(EntityManager em, Entity root)
        {
            using var workers = WorldQueries.Entities<TransportWorker>(em);
            foreach (var entity in workers)
            {
                if (!Owned(em, root, entity)) continue;
                SettleCargo(em, root, entity);
            }
        }

        public static bool NeedsConnection(EntityManager em, Entity root, Entity entity)
        {
            var b = em.GetComponentData<Building>(entity);
            if (b.Stage == LifeStage.Ruined) return false;
            var definition = em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
            if (b.Stage == LifeStage.Construction)
                return BuildingCostOps.ConstructionStage(em, root, definition, em.GetComponentData<BuildingConstructionState>(entity).Progress + 1).Count > 0;
            if (b.Stage == LifeStage.Repairing) return true;
            return BuildingCostOps.ProductionInputs(em, root, definition, b.Level).Count > 0 || BuildingCostOps.Maintenance(em, root, definition, b.Level).Count > 0;
        }

        static bool Endpoints(EntityManager em, Entity root, Entity provider, Entity consumer, out float3 start, out float3 destination)
        {
            start = destination = default;
            var placement = em.GetComponentData<BuildingPlacementState>(provider);
            var grid = em.GetComponentData<GridData>(root);
            var exit = GridOps.Position(grid, placement.Cell + new int2(placement.Size.x, 0), new int2(1));
            if (!NavigationOps.TryNearestOpenOnSurface(em, root, exit, 12, placement.Surface, placement.Elevation, out start)) return false;
            using var reach = new NightSpatialOps.Reach(em, root, start, .25f);
            return reach.Building(em.GetComponentData<BuildingPlacementState>(consumer), out destination) < float.MaxValue
                && math.distance(start, destination) > .5f;
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup), OrderFirst = true)]
    [UpdateAfter(typeof(GameLoopSystem))]
    [UpdateBefore(typeof(PerceptionSystem))]
    public partial struct TransportWorkerSystem : ISystem
    {
        double nextScan;
        public void OnCreate(ref SystemState state) { state.RequireForUpdate<SimulationReady>(); state.RequireForUpdate<TransportWorkerSettings>(); }
        public void OnUpdate(ref SystemState state)
        {
            state.Dependency.Complete();
            var root = WorldQueries.Root(state.EntityManager);
            bool scan = SystemAPI.Time.ElapsedTime >= nextScan;
            if (scan) nextScan = SystemAPI.Time.ElapsedTime + 1;
            if (root != Entity.Null) TransportWorkerOps.Tick(state.EntityManager, root, SystemAPI.Time.DeltaTime, scan);
        }
    }
}
