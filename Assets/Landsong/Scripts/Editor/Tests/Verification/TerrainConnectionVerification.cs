using System;
using System.IO;
using System.Text;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Pathfinding;
using Pathfinding.ECS;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static partial class TerrainConnectionVerification
    {
        static StringBuilder report;
        static int assertions;
        static void Check(bool ok, string message)
        {
            if (!ok)
                throw new InvalidOperationException(message);
            assertions++;
            report.AppendLine("PASS " + message);
        }

        [MenuItem("Landsong/ECS/Verification/Terrain connections")]
        public static string Run()
        {
            report = new StringBuilder().AppendLine(DateTimeOffset.Now.ToString("O"));
            assertions = 0;
            try
            {
                PlacementAndPaths();
                ActualMovement();
                WideSlopeEntryMovement();
                CrowdAvoidance();
                BuildingGraphUpdates();
                Baking();
                Integration();
                report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception e)
            {
                report.AppendLine("FAIL " + e);
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/terrain-connections.txt", report.ToString());
            }
        }

        sealed class Fixture : IDisposable
        {
            public World World = new World("Terrain connection verification", WorldFlags.Game);
            public EntityManager Em => World.EntityManager;

            public Entity Root;
            public BlobAssetReference<GridBlob> Grid;
            BlobAssetReference<BuildingCatalogBlob> Catalog;
            readonly GameObject navigationHost;
            public Fixture(int bridgeWidth = 3, int mapSize = 20, bool wideSlope = false)
            {
                bool createNavigationHost = AstarPath.active == null;
                AstarNavigationRuntime.EnsureServices(true);
                if (createNavigationHost)
                    navigationHost = AstarPath.active.gameObject;
                var map = ScriptableObject.CreateInstance<MapAsset>();
                map.Size = new Vector2Int(mapSize, mapSize);
                map.Cells = new CellSource[mapSize * mapSize];
                for (int z = 0; z < mapSize; z++)
                    for (int x = 0; x < mapSize; x++)
                    {
                        int height = wideSlope ? x >= 4 && x <= 10 && z >= 8 && z <= 15 ? 1 : 0
                            : x >= 2 && x <= 6 && (z >= 2 && z <= 4 || z >= 12 && z <= 14) ? 3 : x >= 9 && x <= 14 && z >= 8 && z <= 11 ? 2 : 0;
                        map.Cells[z * mapSize + x] = new CellSource
                        {
                            Exists = true,
                            Buildable = true,
                            Terrain = (ulong)TerrainType.陆地,
                            Traversable = true,
                            Elevation = height,
                            Height = height,
                            Surface = wideSlope && height != 0 ? 2 : 1
                        };
                    }

                if (wideSlope)
                {
                    map.ElevationStep = 1;
                    map.Connections = new[] { new AuthoredConnection
                    {
                        Id = 1,
                        Cell = new int2(4, 6),
                        Size = new int2(3, 3),
                        Rotation = 0,
                        EntrySurface = 1,
                        ExitSurface = 2,
                        EntryElevation = 0,
                        Rise = 1,
                        Bidirectional = true,
                        ProtrudingSlope = true
                    } };
                }

                try
                {
                    Grid = GameWorldMapAuthoring.BuildGrid(map);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(map);
                }

                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var content = ref builder.ConstructRoot<BuildingCatalogBlob>();
                    var definitions = builder.Allocate(ref content.Definitions, 3);
                    for (int i = 0; i < 3; i++)
                    {
                        ref var d = ref definitions[i];
                        d.Metadata.Id = "connection-" + i;
                        d.MaximumLevel = 1;
                        d.ConstructionTurns = 1;
                        d.Footprint = i == 0 ? new int2(bridgeWidth, 9) : i == 1 ? new int2(3, 5) : new int2(1);
                        d.MaximumDurability = 10;
                        d.MovementCost = 1;
                        d.PlacementAndVisuals = new BuildingPlacementAndVisuals
                        {
                            CanMove = true,
                            CanRotate = true,
                            Category = BuildingCategory.Road,
                            RuinMovementCost = 2
                        };
                        d.Capabilities.Placement.AllowedTerrains = TerrainType.All;
                        d.Capabilities.Connection = new BuildingTerrainConnection
                        {
                            Enabled = i < 2,
                            Rise = i == 1 ? 2 : 0,
                            Bidirectional = true,
                            Clearance = 1.5f,
                            DamagedCost = 2
                        };
                    }

                    Catalog = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
                }

                Root = Em.CreateEntity();
                Em.AddComponentData(Root, new BuildingCatalog { Value = Catalog });
                Em.AddComponentData(Root, new GridData { Value = Grid, CellSize = 1 });
                {
                    Em.AddComponentData(Root, new Session { Phase = Phase.Night });
                    Em.AddComponentData(Root, new GameClock() { Turn = 1 });
                    Em.AddComponentData(Root, new SimulationControl() { });
                    Em.AddComponentData(Root, new PopulationState() { });
                    Em.AddComponentData(Root, new PublicOpinionState() { });
                    Em.AddComponentData(Root, new ResearchState() { });
                    Em.AddComponentData(Root, new ExpeditionPenaltyState() { });
                    Em.AddComponentData(Root, new NightRuntimeState() { });
                    Em.AddComponentData(Root, new NightSettings() { });
                    Em.AddComponentData(Root, new NightPlanState() { });
                    Em.AddComponentData(Root, new DaySettlementState() { });
                    Em.AddComponentData(Root, new RetryState() { });
                    Em.AddComponentData(Root, new HeroSelection() { });
                    Em.AddComponentData(Root, new BellState() { });
                    Em.AddComponentData(Root, new IntelligenceModeState() { });
                    Em.AddComponentData(Root, new PersistenceGate() { });
                    Em.AddComponentData(Root, new SimulationRandomState() { State = 1 });
                    Em.AddComponentData(Root, new IdentitySequence() { NextId = 1 });
                    Em.AddComponentData(Root, new DynastyIdentity() { });
                }

                Em.AddComponent<SimulationReady>(Root);
                Em.AddBuffer<Occupancy>(Root).Resize(mapSize * mapSize, NativeArrayOptions.ClearMemory);
                var prefabs = Em.AddBuffer<BuildingPrefab>(Root);
                var prefab = Em.CreateEntity(typeof(Prefab));
                for (int i = 0; i < 3; i++)
                    Em.GetBuffer<BuildingPrefab>(Root).Add(new BuildingPrefab { Definition = BuildingId.FromIndex(i), Prefab = prefab });
                Em.AddBuffer<InventorySlot>(Root);
                Em.AddBuffer<PendingItem>(Root);
            }

            public Entity Building(int definition, int2 at) => BuildingCreation.Create(Em, Root, BuildingId.FromIndex(definition), at, 0, 1, false);
            public void Complete(Entity e)
            {
                var b = Em.GetComponentData<Building>(e);
                b.Stage = LifeStage.Operational;
                Em.SetComponentData(e, b);
                GridOps.Occupy(Em, Root, e);
            }

            public QueryScope Query(float radius = .2f) => new QueryScope(Em, Root, radius);
            public Entity Unit(float3 from, float3 to, float radius = .2f, float speed = 2)
            {
                var e = Em.CreateEntity();
                Em.AddComponentData(e, new Identity { Id = EntityIdentityAllocator.AllocateId(Em, Root) });
                Em.AddComponentData(e, LocalTransform.FromPosition(from));
                Em.AddComponentData(e, new Health { Current = 10, Maximum = 10 });
                var profile = CombatProfile.Default;
                profile.BodyRadius = radius;
                Em.AddComponentData(e, new Combatant { Deployed = 1, Speed = speed, Profile = profile });
                Em.AddComponentData(e, new NavigationState { Revision = -1 });
                Em.AddComponentData(e, new Steering { Moving = 1, Destination = to });
                Em.AddBuffer<Waypoint>(e);
                return e;
            }

            public void Tick()
            {
                GameClock sClock = Em.GetComponentData<GameClock>(Root);
                sClock.Time += .05f;
                {
                    Em.SetComponentData(Root, sClock);
                }

                World.SetTime(new TimeData(sClock.Time, .05f));
                World.GetOrCreateSystem<NavigationSystem>().Update(World.Unmanaged);
                World.GetOrCreateSystem<FallbackResolveMovementSystem>().Update(World.Unmanaged);
                World.GetOrCreateSystem<AstarNavigationMoveSystem>().Update(World.Unmanaged);
                Em.CompleteAllTrackedJobs();
            }

            public void Dispose()
            {
                World.Dispose();
                Grid.Dispose();
                Catalog.Dispose();
                if (navigationHost != null)
                    UnityEngine.Object.DestroyImmediate(navigationHost);
            }
        }

        static void BuildingGraphUpdates()
        {
            using var f = new Fixture(mapSize: 160);
            var timer = System.Diagnostics.Stopwatch.StartNew();
            AstarNavigationRuntime.EnsureGraph(f.Em, f.Root);
            double initialMs = timer.Elapsed.TotalMilliseconds;
            var graph = AstarPath.active.data.graphs.OfType<PointGraph>().Single(g => g.name == "Landsong Runtime Surfaces");
            var before = graph.nodes.ToArray();
            var entity = f.Em.CreateEntity();
            f.Em.AddBuffer<Waypoint>(entity);
            float3 start = new float3(75.5f, .5f, 80.5f), end = new float3(85.5f, .5f, 80.5f);
            Check(AstarNavigationRuntime.FindPath(start, end, f.Em.GetBuffer<Waypoint>(entity)), "A* 大图初始路径可达");
            timer.Restart();
            var occupancy = f.Em.GetBuffer<Occupancy>(f.Root);
            for (int z = 78; z <= 82; z++)
                occupancy[z * 160 + 80] = new Occupancy { Owner = 999, MovementCost = 0 };
            AstarNavigationRuntime.EnsureGraph(f.Em, f.Root);
            double updateMs = timer.Elapsed.TotalMilliseconds;
            Check(ReferenceEquals(graph, AstarPath.active.data.graphs.OfType<PointGraph>().Single(g => g.name == "Landsong Runtime Surfaces"))
                && before.SequenceEqual(graph.nodes), "普通建筑占地更新保留 A* 图及全部节点实例");
            Check(AstarNavigationRuntime.FindPath(start, end, f.Em.GetBuffer<Waypoint>(entity)), "放置障碍后 A* 可绕行");
            using var detour = f.Em.GetBuffer<Waypoint>(entity).ToNativeArray(Allocator.Temp);
            Check(detour.ToArray().All(w =>
                math.abs(w.Position.x - 80.5f) > .1f || w.Position.z < 78 || w.Position.z > 83), "更新后路径不穿过建筑占地");
            occupancy = f.Em.GetBuffer<Occupancy>(f.Root);
            for (int z = 78; z <= 82; z++) occupancy[z * 160 + 80] = default;
            AstarNavigationRuntime.EnsureGraph(f.Em, f.Root);
            Check(AstarNavigationRuntime.FindPath(start, end, f.Em.GetBuffer<Waypoint>(entity)), "拆除后 A* 路径可达");
            using var restored = f.Em.GetBuffer<Waypoint>(entity).ToNativeArray(Allocator.Temp);
            Check(restored.ToArray().All(w => math.abs(w.Position.z - 80.5f) < .1f), "拆除后恢复直接通行");
            report.AppendLine($"160x160 graph: initial={initialMs:F2}ms; occupancy update={updateMs:F2}ms");
            Check(initialMs < 1000 && updateMs < 1000, "25600 格图构建和占地更新均无秒级停顿（编辑器宽松回归门槛）");
        }

        sealed class QueryScope : IDisposable
        {
            public SurfacePathQuery Value;
            readonly EntityManager em;
            readonly Entity pathEntity;
            public QueryScope(EntityManager manager, Entity root, float radius)
            {
                em = manager;
                SurfaceNavigationGraph.Ensure(em, root);
                var nodes = em.GetBuffer<SurfaceNavNode>(root).ToNativeArray(Allocator.TempJob);
                var edges = em.GetBuffer<SurfaceNavEdge>(root).ToNativeArray(Allocator.TempJob);
                var cells = new NativeParallelMultiHashMap<int2, int>(nodes.Length, Allocator.TempJob);
                for (int i = 0; i < nodes.Length; i++)
                    cells.Add(nodes[i].Cell, i);
                Value = new SurfacePathQuery
                {
                    Grid = em.GetComponentData<GridData>(root),
                    Nodes = nodes,
                    Edges = edges,
                    Cells = cells,
                    Radius = radius
                };
                pathEntity = em.CreateEntity();
                em.AddBuffer<Waypoint>(pathEntity);
            }

            public bool Path(float3 from, float3 to) => Value.Find(from, to, em.GetBuffer<Waypoint>(pathEntity));
            public float3[] Points => em.GetBuffer<Waypoint>(pathEntity).ToNativeArray(Allocator.Temp).ToArray().Select(p => p.Position).ToArray();

            public void Dispose()
            {
                Value.Nodes.Dispose();
                Value.Edges.Dispose();
                Value.Cells.Dispose();
                em.DestroyEntity(pathEntity);
            }
        }

        static readonly float3 BridgeA = new float3(4.5f, 3.5f, 3.5f), BridgeB = new float3(4.5f, 3.5f, 13.5f);
        static readonly float3 StairsA = new float3(11.5f, .5f, 3.5f), StairsB = new float3(11.5f, 2.5f, 9.5f);
        static void PlacementAndPaths()
        {
            using var f = new Fixture();
            bool bridgeTerrain = TerrainConnectionOps.CanPlace(f.Em, f.Root, BuildingId.FromIndex(0), new int2(3, 4), 0, 0, out string bridgeReason);
            Check(bridgeTerrain && GridOps.CanPlace(f.Em, f.Root, BuildingId.FromIndex(0), new int2(3, 4), 0), "Bridge endpoints and underpass clearance accepted: " + bridgeReason);
            Check(GridOps.CanPlace(f.Em, f.Root, BuildingId.FromIndex(1), new int2(10, 4), 0), "Integer-rise stairs accepted across different elevations");
            Check(!GridOps.CanPlace(f.Em, f.Root, BuildingId.FromIndex(1), new int2(10, 4), 2), "Reversed stairs reject reversed elevation difference");
            using (var q = f.Query())
                Check(!q.Path(BridgeA, BridgeB) && !q.Path(StairsA, StairsB), "No implicit cliff climbing before construction");
            var bridge = f.Building(0, new int2(3, 4));
            var stairs = f.Building(1, new int2(10, 4));
            using (var q = f.Query())
                Check(!q.Path(BridgeA, BridgeB), "Unfinished bridge has no traversable deck");
            Check(!GridOps.CanPlace(f.Em, f.Root, BuildingId.FromIndex(2), new int2(4, 8), 0), "Bridge construction footprint prevents stacked buildings");
            f.Complete(bridge);
            f.Complete(stairs);
            using (var q = f.Query())
            {
                var crossed = q.Path(BridgeA, BridgeB);
                var bridgePoints = q.Points;
                Check(crossed && bridgePoints.All(p => p.y > 3), "Completed bridge crosses only its upper surface");
                Check(q.Path(BridgeB, BridgeA), "Completed bridge supports reverse traversal");
                Check(q.Path(new float3(1.5f, .5f, 8.5f), new float3(8.5f, .5f, 8.5f)) && q.Points.All(p => p.y < 1), "Bridge underpass remains independently traversable");
                Check(q.Path(StairsA, StairsB) && q.Path(StairsB, StairsA), "Completed stairs connect integer planes in both directions");
                Check(q.Value.Locate(new float3(4.5f, 1.5f, 8.5f), .1f) < 0, "Air between deck and ground is not a surface");
                Check(q.Path(new float3(3.5f, 3.5f, 8.5f), new float3(5.5f, 3.5f, 8.5f)), "Wide deck supports transverse movement between lanes");
            }

            BuildingLifecycle.Ruin(f.Em, f.Root, bridge);
            using (var q = f.Query())
            {
                Check(q.Path(BridgeA, BridgeB), "Damage preserves bridge connectivity");
                int n = q.Value.Locate(new float3(4.5f, 3.5f, 8.5f));
                Check(q.Value.Nodes[n].Cost == 2, "Damage raises bridge movement cost");
            }

            GridOps.Occupy(f.Em, f.Root, bridge, true);
            f.Em.DestroyEntity(bridge);
            using (var q = f.Query())
                Check(!q.Path(BridgeA, BridgeB) && q.Path(StairsA, StairsB), "Removing bridge removes only its own connection");
        }

        static void ActualMovement()
        {
            using var f = new Fixture();
            f.Complete(f.Building(0, new int2(3, 4)));
            f.Complete(f.Building(1, new int2(10, 4)));
            var uphill = f.Unit(StairsA, StairsB);
            var downhill = f.Unit(StairsB, StairsA);
            var wolfSized = f.Unit(StairsA + new float3(1, 0, 0), StairsB + new float3(1, 0, 0), .4f, 3.2f);
            var upper = f.Unit(BridgeA, BridgeB);
            var lower = f.Unit(new float3(1.5f, .5f, 8.5f), new float3(8.5f, .5f, 8.5f));
            var opposite = f.Unit(BridgeB + new float3(1, 0, 0), BridgeA + new float3(1, 0, 0));
            float maxError = 0;
            float3 maxErrorPosition = default;
            int maxErrorTick = -1;
            for (int i = 0; i < 320; i++)
            {
                f.Tick();
                var p = EntityState.Position(f.Em, uphill);
                if (p.z >= 4.5f && p.z <= 8.5f)
                {
                    var error = math.abs(p.y - (.5f + (p.z - 4.5f) * .5f));
                    if (error > maxError)
                    {
                        maxError = error;
                        maxErrorPosition = p;
                        maxErrorTick = i;
                    }
                }
            }

            report.AppendLine("DETAIL moving units: up=" + EntityState.Position(f.Em, uphill) + "; down=" + EntityState.Position(f.Em, downhill) + "; bridge=" + EntityState.Position(f.Em, upper) + "; under=" + EntityState.Position(f.Em, lower) + "; failed=" + f.Em.GetComponentData<NavigationState>(uphill).Failed + "/" + f.Em.GetComponentData<NavigationState>(downhill).Failed);
            Check(math.distance(EntityState.Position(f.Em, uphill), StairsB) < .4f && math.distance(EntityState.Position(f.Em, downhill), StairsA) < .4f, "Actual ECS NavigationSystem moves opposing units up and down stairs");
            Check(math.distance(EntityState.Position(f.Em, wolfSized), StairsB + new float3(1, 0, 0)) < .5f, "Wolf-sized fast agent traverses the stair beside other units");
            Check(maxError < .12f, "Actual ECS stair movement follows continuous logical height trajectory; error=" + maxError + "; position=" + maxErrorPosition + "; tick=" + maxErrorTick);
            Check(math.distance(EntityState.Position(f.Em, upper), BridgeB) < .4f && math.distance(EntityState.Position(f.Em, opposite), BridgeA + new float3(1, 0, 0)) < .4f, "Actual ECS units traverse wide bridge in opposite directions");
            Check(math.distance(EntityState.Position(f.Em, lower), new float3(8.5f, .5f, 8.5f)) < .4f, "Actual ECS underpass unit never snaps onto upper deck");
            using (var slope = new Fixture())
            {
                slope.Complete(slope.Building(1, new int2(10, 4)));
                using var query = slope.Query(.4f);
                var start = query.Value.Nodes.ToArray().First(n => n.Corridor != 0 && n.Cell.x == 12 && n.Cell.y == 6).Position;
                Check(query.Value.Shift(start, start + new float3(0, 2, .1f), out var projected)
                    && math.abs(projected.y - start.y) < .1f,
                    "Slope movement projects an avoidance target onto its connected surface");
                var agent = slope.Unit(start, StairsB + new float3(1, 0, 0), .4f, 3.2f);
                slope.World.SetTime(new TimeData(.05f, .05f));
                slope.World.GetOrCreateSystem<NavigationSystem>().Update(slope.World.Unmanaged);
                slope.World.GetOrCreateSystem<FallbackResolveMovementSystem>().Update(slope.World.Unmanaged);
                slope.Em.CompleteAllTrackedJobs();
                var before = EntityState.Position(slope.Em, agent);
                var resolved = slope.Em.GetComponentData<ResolvedMovement>(agent);
                resolved.targetPoint = before + new float3(3, 0, 0);
                resolved.speed = 3.2f;
                slope.Em.SetComponentData(agent, resolved);
                Check(!query.Value.Shift(before, before + new float3(.16f, 0, 0), out _),
                    "Wolf-sized lateral avoidance step exceeds stair corridor clearance");
                slope.World.GetOrCreateSystem<AstarNavigationMoveSystem>().Update(slope.World.Unmanaged);
                slope.Em.CompleteAllTrackedJobs();
                Check(EntityState.Position(slope.Em, agent).z > before.z + .01f,
                    "Blocked lateral avoidance falls back toward the connected stair waypoint");
                var stationary = EntityState.Position(slope.Em, agent);
                for (int tick = 0; tick < 16; tick++)
                {
                    var frozen = slope.Em.GetComponentData<ResolvedMovement>(agent);
                    frozen.targetPoint = EntityState.Position(slope.Em, agent);
                    frozen.speed = 0;
                    slope.Em.SetComponentData(agent, frozen);
                    slope.World.GetOrCreateSystem<AstarNavigationMoveSystem>().Update(slope.World.Unmanaged);
                    slope.Em.CompleteAllTrackedJobs();
                }
                Check(EntityState.Position(slope.Em, agent).z > stationary.z + .01f,
                    "Repeated zero-velocity avoidance on a slope resumes along the authored waypoint");
            }
            using var narrow = new Fixture(1);
            Check(!TerrainConnectionOps.CanPlace(narrow.Em, narrow.Root, BuildingId.FromIndex(0), new int2(4, 4), 0, 0, out var reason)
                && reason.Contains("3 格宽"), "Bridge authoring rejects obsolete single-width decks: " + reason);
        }

        static void WideSlopeEntryMovement()
        {
            using var f = new Fixture(wideSlope: true);
            var lower = new float3(6.816f, .5f, 6.945f);
            var upper = new float3(10.5f, 1.5f, 12.5f);
            foreach (float radius in new[] { .35f, .4f, .8f })
                foreach (bool uphill in new[] { true, false })
                {
                    var destination = uphill ? upper : lower;
                    var unit = f.Unit(uphill ? lower : upper, destination, radius, 2.5f);
                    f.Tick();
                    using (var query = f.Query(radius))
                    {
                        var path = f.Em.GetBuffer<Waypoint>(unit);
                        bool innerRoute = path.Length > 0;
                        for (int i = 0; i < path.Length; i++)
                        {
                            int node = query.Value.Locate(path[i].Position);
                            innerRoute &= node >= 0 && !SurfaceNavigationGraph.OuterSlopeLane(query.Value.Nodes[node], 1);
                        }
                        Check(innerRoute, "Wide slope route avoids edge columns for radius " + radius + ", uphill=" + uphill);
                    }

                    float3 previous = EntityState.Position(f.Em, unit);
                    int stillTicks = 0, longestStill = 0;
                    bool Arrived()
                    {
                        var grid = f.Em.GetComponentData<GridData>(f.Root);
                        var current = EntityState.Position(f.Em, unit);
                        return f.Em.GetBuffer<Waypoint>(unit).Length == 0
                            && math.all(GridOps.Cell(grid, current) == GridOps.Cell(grid, destination))
                            && math.abs(current.y - destination.y) < .03f;
                    }
                    for (int tick = 0; tick < 240 && !Arrived(); tick++)
                    {
                        f.Tick();
                        var current = EntityState.Position(f.Em, unit);
                        stillTicks = math.distancesq(current.xz, previous.xz) < .000001f ? stillTicks + 1 : 0;
                        longestStill = math.max(longestStill, stillTicks);
                        previous = current;
                    }
                    var remaining = f.Em.GetBuffer<Waypoint>(unit);
                    var resolved = f.Em.GetComponentData<ResolvedMovement>(unit);
                    Check(longestStill < 40 && Arrived(),
                        "Wide slope movement clears the mouth without a two-second stall for radius " + radius + ", uphill=" + uphill
                        + ", stillTicks=" + longestStill + ", position=" + EntityState.Position(f.Em, unit)
                        + ", next=" + (remaining.Length > 0 ? remaining[0].Position.ToString() : "none")
                        + ", resolved=" + resolved.targetPoint + "/" + resolved.speed);
                    f.Em.DestroyEntity(unit);
                }
        }

        static void CrowdAvoidance()
        {
            using var f = new Fixture();
            const float radius = .25f;
            var units = new Entity[8];
            for (int i = 0; i < units.Length / 2; i++)
            {
                float z = 14.5f + i;
                units[i * 2] = f.Unit(new float3(1.5f, .5f, z), new float3(18.5f, .5f, z), radius);
                units[i * 2 + 1] = f.Unit(new float3(18.5f, .5f, z), new float3(1.5f, .5f, z), radius);
            }

            float closest = float.MaxValue;
            int closestTick = -1, closestA = -1, closestB = -1;
            for (int tick = 0; tick < 260; tick++)
            {
                f.Tick();
                for (int a = 0; a < units.Length; a++)
                    for (int b = a + 1; b < units.Length; b++)
                    {
                        float separation = math.distance(EntityState.Position(f.Em, units[a]).xz, EntityState.Position(f.Em, units[b]).xz);
                        if (separation < closest)
                        {
                            closest = separation;
                            closestTick = tick;
                            closestA = a;
                            closestB = b;
                        }
                    }
            }

            bool reached = true;
            for (int i = 0; i < units.Length / 2; i++)
            {
                reached &= EntityState.Position(f.Em, units[i * 2]).x > 17.8f;
                reached &= EntityState.Position(f.Em, units[i * 2 + 1]).x < 2.2f;
            }
            report.AppendLine("DETAIL crowd closest=" + closest + " at tick=" + closestTick + " pair=" + closestA + "/" + closestB + "; positions=" + string.Join(",", units.Select(unit => EntityState.Position(f.Em, unit).ToString())));
            // RVOSimulator intentionally creates its SimulatorBurst only while Application.isPlaying.
            // This edit-mode fixture verifies A* routing progress; the four-scene Play regression owns
            // actual ECS RVO behavior and movement assertions.
            Check(reached, "All opposing edit-mode routing fixtures keep making progress and reach the far side; RVO separation is Play-only");

            var stacked = Enumerable.Range(0, 6).Select(i =>
            {
                float angle = i * math.PI * 2 / 6;
                return f.Unit(new float3(10.5f, .5f, 6.5f), new float3(10.5f + math.cos(angle) * 5, .5f, 6.5f + math.sin(angle) * 5), radius);
            }).ToArray();
            for (int tick = 0; tick < 15; tick++)
                f.Tick();
            float spread = stacked.Max(unit => math.distance(EntityState.Position(f.Em, unit).xz, new float2(10.5f, 6.5f)));
            Check(spread > .4f, "Exactly overlapping units deterministically recover instead of remaining motionless");
        }
    }
}
