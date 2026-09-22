using System;
using System.Collections.Generic;
using Pathfinding;
using Pathfinding.RVO;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.ECS
{
    /// <summary>
    /// Owns the project-level A* Pro services and converts Landsong's authored surface data into
    /// the runtime point graph consumed by A* path requests. The authored surface data remains the
    /// map schema; path search and crowd simulation are delegated to A* Pro.
    /// </summary>
    public static class AstarNavigationRuntime
    {
        const string RuntimeObjectName = "[Landsong] A* Pro Runtime";
        const string RuntimeGraphName = "Landsong Runtime Surfaces";

        static AstarPath owner;
        static PointGraph graph;
        static Entity graphRoot;
        static BlobAssetReference<GridBlob> graphMap;
        static float3 graphOrigin;
        static float graphCellSize;
        static int revision = int.MinValue;
        static uint occupancyHash;
        static PointNode[] converted;
        // ulong.GetHashCode XORs its two halves. Grid edges (i, i+1) then
        // collapse into a handful of buckets, making graph conversion quadratic.
        sealed class EdgeComparer : IEqualityComparer<ulong>
        {
            public bool Equals(ulong x, ulong y) => x == y;
            public int GetHashCode(ulong edge) => (int)math.hash(new uint2((uint)(edge >> 32), (uint)edge));
        }
        static readonly EdgeComparer edgeComparer = new EdgeComparer();
        static HashSet<ulong> authored = new HashSet<ulong>(edgeComparer);

        static ulong EdgeKey(int from, int to) => ((ulong)(uint)from << 32) | (uint)to;

        public static bool Available
        {
            get
            {
                if (AstarPath.active == null && Application.isPlaying)
                    EnsureServices();
                return AstarPath.active != null;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize() => EnsureServices();

        public static void EnsureServices(bool allowEditMode = false)
        {
            if (!Application.isPlaying && !allowEditMode)
                return;

            if (AstarPath.active == null)
            {
                var runtime = new GameObject(RuntimeObjectName)
                {
                    hideFlags = HideFlags.DontSave
                };
                UnityEngine.Object.DontDestroyOnLoad(runtime);
                var pathfinding = runtime.AddComponent<AstarPath>();
                // Runtime services must not cover the Game view when its Gizmos toggle is on.
                pathfinding.showNavGraphs = false;
                pathfinding.showUnwalkableNodes = false;
                pathfinding.showGraphsInStandalonePlayer = false;
            }

            if (RVOSimulator.active == null)
            {
                var host = AstarPath.active != null ? AstarPath.active.gameObject : new GameObject(RuntimeObjectName);
                var simulator = host.AddComponent<RVOSimulator>();
                simulator.movementPlane = MovementPlane.XZ;
                simulator.hardCollisions = true;
                simulator.symmetryBreakingBias = .1f;
                simulator.useNavmeshAsObstacle = false;
            }
        }

        public static bool EnsureGraph(EntityManager em, Entity root)
        {
            if (!Available)
                return false;

            SurfaceNavigationGraph.Ensure(em, root);
            var cache = em.GetComponentData<SurfaceNavCache>(root);
            var grid = em.GetComponentData<GridData>(root);
            if (owner == AstarPath.active && graph != null && graph.active == owner && graphRoot == root
                && graphMap == cache.Map && math.all(graphOrigin == grid.Origin) && graphCellSize == grid.CellSize
                && revision == cache.Revision && occupancyHash == cache.OccupancyHash)
                return true;

            var sourceNodes = em.GetBuffer<SurfaceNavNode>(root).ToNativeArray(Unity.Collections.Allocator.Temp);
            var sourceEdges = em.GetBuffer<SurfaceNavEdge>(root).ToNativeArray(Unity.Collections.Allocator.Temp);
            var nodes = sourceNodes.ToArray();
            var edges = sourceEdges.ToArray();
            sourceNodes.Dispose();
            sourceEdges.Dispose();

            bool rebuild = owner != AstarPath.active || graph == null || graph.active != owner
                || graphRoot != root || graphMap != cache.Map || converted == null || converted.Length != nodes.Length;
            if (!rebuild)
                for (int i = 0; i < nodes.Length; i++)
                    if (converted[i].position != (Int3)(Vector3)nodes[i].Position)
                    {
                        rebuild = true;
                        break;
                    }

            owner = AstarPath.active;
            if (rebuild)
            {
                if (graph != null && graph.active == owner)
                    owner.data.RemoveGraph(graph);
                graph = owner.data.AddGraph<PointGraph>();
                if (graph == null)
                    throw new InvalidOperationException("A* Pro 无法创建 Landsong 运行时导航图。");
                graph.name = RuntimeGraphName;
                graph.drawGizmos = false;
                graph.maxDistance = 0;
                graph.raycast = false;
                converted = new PointNode[nodes.Length];
                authored.Clear();
            }

            owner.AddWorkItem(context =>
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    var point = rebuild ? graph.AddNode((Int3)(Vector3)nodes[i].Position) : converted[i];
                    bool walkable = nodes[i].Open != 0;
                    if (point.Walkable != walkable) point.Walkable = walkable;
                    uint penalty = (uint)math.round(math.max(0, nodes[i].Cost - 1) * Int3.Precision);
                    if (point.Penalty != penalty) point.Penalty = penalty;
                    converted[i] = point;
                }

                // GraphNode.Connect replaces the direction flags of an existing node pair.
                // Therefore two opposite OneWay calls do not produce a two-way edge: the
                // second call silently overwrites the first. Aggregate authored directions
                // first and create mutual edges exactly once using A*'s TwoWay invariant.
                var nextAuthored = new HashSet<ulong>(edgeComparer);
                for (int i = 0; i < nodes.Length; i++)
                {
                    var from = converted[i];
                    if (!from.Walkable)
                        continue;
                    for (int edge = nodes[i].FirstEdge; edge >= 0; edge = edges[edge].Next)
                    {
                        int target = edges[edge].Target;
                        if (target < 0 || target >= converted.Length || !converted[target].Walkable)
                            continue;
                        nextAuthored.Add(EdgeKey(i, target));
                    }
                }

                // Disconnect changed pairs first; Connect replaces both direction flags.
                foreach (ulong edge in authored)
                    if (!nextAuthored.Contains(edge))
                        GraphNode.Disconnect(converted[(int)(edge >> 32)], converted[(int)(uint)edge]);

                // Initial construction uses one exact connection array per node. Repeated
                // Connect calls would allocate/copy a larger array for every added edge.
                var degrees = rebuild ? new int[nodes.Length] : null;
                if (rebuild)
                {
                    foreach (ulong edge in nextAuthored)
                    {
                        int a = (int)(edge >> 32), b = (int)(uint)edge;
                        degrees[a]++;
                        if (!nextAuthored.Contains(EdgeKey(b, a))) degrees[b]++;
                    }
                    for (int i = 0; i < nodes.Length; i++)
                    {
                        converted[i].connections = new Connection[degrees[i]];
                        degrees[i] = 0;
                    }
                }

                foreach (ulong edge in nextAuthored)
                {
                    int source = (int)(edge >> 32);
                    int target = (int)(uint)edge;
                    bool mutual = nextAuthored.Contains(EdgeKey(target, source));
                    if (mutual && source > target)
                        continue;
                    var from = converted[source];
                    var to = converted[target];
                    if (!rebuild && authored.Contains(edge)
                        && authored.Contains(EdgeKey(target, source)) == mutual)
                        continue;
                    uint cost = (uint)math.max(1, (to.position - from.position).costMagnitude);
                    if (rebuild)
                    {
                        from.connections[degrees[source]++] = new Connection(to, cost, true, mutual);
                        to.connections[degrees[target]++] = new Connection(from, cost, mutual, true);
                    }
                    else
                        GraphNode.Connect(from, to, cost, mutual ? OffMeshLinks.Directionality.TwoWay : OffMeshLinks.Directionality.OneWay);
                }

                if (rebuild)
                {
                    foreach (var point in converted) point.SetConnectivityDirty();
                    graph.optimizeForSparseGraph = true;
                    graph.RebuildNodeLookup();
                }
                authored = nextAuthored;
                context.SetGraphDirty(graph);
            });
            owner.FlushWorkItems();
            graphRoot = root;
            graphMap = cache.Map;
            graphOrigin = grid.Origin;
            graphCellSize = grid.CellSize;
            revision = cache.Revision;
            occupancyHash = cache.OccupancyHash;
            return true;
        }

        public static bool FindPath(float3 from, float3 destination, DynamicBuffer<Waypoint> result)
        {
            result.Clear();
            if (!Available || graph == null || graph.active != AstarPath.active)
                return false;

            var request = ABPath.Construct((Vector3)from, (Vector3)destination);
            var constraint = request.traversalConstraint;
            constraint.graphMask = GraphMask.FromGraph(graph);
            request.traversalConstraint = constraint;
            AstarPath.StartPath(request);
            request.BlockUntilCalculated();
            if (request.error || request.path == null || request.path.Count == 0)
                return false;

            // Graph node positions deliberately preserve authored layer, bridge and stair heights.
            // Skip the start node unless it is also the complete path.
            int first = request.path.Count == 1 ? 0 : 1;
            for (int i = first; i < request.path.Count; i++)
                result.Add(new Waypoint { Position = (Vector3)request.path[i].position });
            return result.Length > 0;
        }
    }
}
