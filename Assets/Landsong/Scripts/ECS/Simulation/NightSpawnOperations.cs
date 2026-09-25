using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS
{
    // Derived from the current buildings and grid for one planning/projection operation.
    // Only the chosen SpawnRegion buffer is persistent; this grid-sized working set is disposable.
    public static class NightSpawnOps
    {
        public sealed class Space
        {
            public readonly GridData Grid;
            public readonly int[] Coverage;
            public readonly List<int2> Normal = new List<int2>(), Edge = new List<int2>();
            readonly bool[] open, reachable, cityEdge;
            readonly float3 cityCenter;
            public Space(EntityManager em, Entity root)
            {
                Grid = em.GetComponentData<GridData>(root);
                SurfaceNavigationGraph.Ensure(em, root);
                var navNodes = em.GetBuffer<SurfaceNavNode>(root);
                var navEdges = em.GetBuffer<SurfaceNavEdge>(root);
                var reached = new bool[navNodes.Length];
                // Reverse adjacency in flat arrays avoids one managed List per navigation node.
                var incomingHead = new int[navNodes.Length];
                Array.Fill(incomingHead, -1);
                var incomingNext = new int[navEdges.Length];
                var incomingFrom = new int[navEdges.Length];
                for (int n = 0; n < navNodes.Length; n++)
                    for (int edge = navNodes[n].FirstEdge; edge >= 0; edge = navEdges[edge].Next)
                    {
                        int to = navEdges[edge].Target;
                        incomingFrom[edge] = n;
                        incomingNext[edge] = incomingHead[to];
                        incomingHead[to] = edge;
                    }

                var occupancy = em.GetBuffer<Occupancy>(root);
                int length = Grid.Value.Value.Cells.Length;
                Coverage = new int[length];
                open = new bool[length];
                reachable = new bool[length];
                cityEdge = new bool[length];
                var walkable = new bool[length];
                for (int i = 0; i < length; i++)
                {
                    var cell = Cell(i);
                    walkable[i] = navNodes[i].Open != 0;
                    open[i] = walkable[i] && occupancy[i].Owner == 0;
                }

                cityCenter = Grid.Origin + new float3((Grid.Value.Value.Min.x + Grid.Value.Value.Size.x * .5f) * Grid.CellSize, 0, (Grid.Value.Value.Min.y + Grid.Value.Value.Size.y * .5f) * Grid.CellSize);
                var queue = new Queue<int>();
                using var buildings = WorldQueries.OrderedEntities<Building>(em);
                foreach (var site in buildings)
                {
                    if (BuildingFactionOps.Of(em, root, site) != (byte)BuildingFaction.Settlement)
                        continue;
                    BuildingPlacementState buildingPlacement = em.GetComponentData<BuildingPlacementState>(site);
                    ref var definition = ref BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(site).Definition);
                    int padding = math.clamp(definition.PlacementAndVisuals.SpawnExclusionPadding, 0, 256);
                    var lo = math.max(buildingPlacement.Cell - padding, Grid.Value.Value.Min);
                    var hi = math.min(buildingPlacement.Cell + buildingPlacement.Size + padding, Grid.Value.Value.Min + Grid.Value.Value.Size);
                    for (int y = lo.y; y < hi.y; y++)
                        for (int x = lo.x; x < hi.x; x++)
                            Coverage[GridOps.Index(Grid, new int2(x, y))]++;
                    if (!NightSpatialOps.ValidTarget(em, root, site))
                        continue;
                    if (em.HasComponent<BuildingHousingStats>(site) && em.GetComponentData<BuildingHousingStats>(site).IsCore != 0)
                        cityCenter = GridOps.Position(Grid, buildingPlacement.Cell, buildingPlacement.Size);
                    // Reachable from any legal target perimeter; uses the same steps/elevation as combat paths.
                    for (int y = -1; y <= buildingPlacement.Size.y; y++)
                        for (int x = -1; x <= buildingPlacement.Size.x; x++)
                        {
                            if (x >= 0 && y >= 0 && x < buildingPlacement.Size.x && y < buildingPlacement.Size.y)
                                continue;
                            int at = GridOps.Index(Grid, buildingPlacement.Cell + new int2(x, y));
                            if (at < 0 || !walkable[at] || reached[at] || navNodes[at].Elevation != buildingPlacement.Elevation)
                                continue;
                            reached[at] = true;
                            queue.Enqueue(at);
                        }
                }

                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    for (int edge = incomingHead[at]; edge >= 0; edge = incomingNext[edge])
                    {
                        int next = incomingFrom[edge];
                        if (reached[next] || navNodes[next].Open == 0)
                            continue;
                        reached[next] = true;
                        queue.Enqueue(next);
                    }
                }

                for (int i = 0; i < length; i++)
                    reachable[i] = reached[i];
                for (int i = 0; i < length; i++)
                {
                    if (!open[i] || !reachable[i])
                        continue;
                    if (Coverage[i] == 0)
                        Normal.Add(Cell(i));
                    if (Grid.Value.Value.Cells[i].EdgeZone != 0)
                        Edge.Add(Cell(i));
                }

                // Flood the uncovered exterior from the authored map boundary. An
                // uncovered courtyard may be legal, but should not be a preferred
                // entry while the outside of the city remains reachable.
                var exterior = new bool[length];
                var exteriorQueue = new Queue<int>();
                void AddExterior(int at)
                {
                    if (at < 0 || Coverage[at] != 0 || exterior[at])
                        return;
                    exterior[at] = true;
                    exteriorQueue.Enqueue(at);
                }

                for (int y = 0; y < Grid.Value.Value.Size.y; y++)
                {
                    AddExterior(y * Grid.Value.Value.Size.x);
                    AddExterior(y * Grid.Value.Value.Size.x + Grid.Value.Value.Size.x - 1);
                }
                for (int x = 0; x < Grid.Value.Value.Size.x; x++)
                {
                    AddExterior(x);
                    AddExterior((Grid.Value.Value.Size.y - 1) * Grid.Value.Value.Size.x + x);
                }
                while (exteriorQueue.Count > 0)
                {
                    int at = exteriorQueue.Dequeue();
                    var cell = Cell(at);
                    for (int d = 0; d < 4; d++)
                        AddExterior(GridOps.Index(Grid, cell + Offset(d)));
                }
                foreach (var cell in Normal)
                {
                    int at = GridOps.Index(Grid, cell);
                    if (!exterior[at])
                        continue;
                    for (int d = 0; d < 4; d++)
                    {
                        int neighbor = GridOps.Index(Grid, cell + Offset(d));
                        if (neighbor < 0 || Coverage[neighbor] == 0)
                            continue;
                        cityEdge[at] = true;
                        break;
                    }
                }
            }

            public bool Legal(int2 cell, bool edgeOnly)
            {
                int at = GridOps.Index(Grid, cell);
                return at >= 0 && open[at] && reachable[at] && (edgeOnly ? Grid.Value.Value.Cells[at].EdgeZone != 0 : Coverage[at] == 0);
            }

            public bool CityEdge(int2 cell)
            {
                int at = GridOps.Index(Grid, cell);
                return at >= 0 && cityEdge[at];
            }

            public int2 Cell(int index) => Grid.Value.Value.Min + new int2(index % Grid.Value.Value.Size.x, index / Grid.Value.Value.Size.x);
            public float3 Position(int2 cell) => GridOps.Position(Grid, cell, new int2(1)) + new float3(0, .5f, 0);
            public int Direction(float3 point)
            {
                var delta = point.xz - cityCenter.xz;
                return math.abs(delta.x) > math.abs(delta.y) ? delta.x >= 0 ? 20 : 40 : delta.y >= 0 ? 10 : 30;
            }

            public SpawnRegion Region(int2 anchor, bool edgeOnly, int width)
            {
                var size = math.min(new int2(width), Grid.Value.Value.Size);
                var lo = math.clamp(anchor - size / 2, Grid.Value.Value.Min, Grid.Value.Value.Min + Grid.Value.Value.Size - size);
                var center = GridOps.Position(Grid, lo, size);
                return new SpawnRegion
                {
                    Center = center,
                    Size = new float3(size.x * Grid.CellSize, Grid.CellSize, size.y * Grid.CellSize),
                    Direction = Direction(center),
                    EdgeOnly = (byte)(edgeOnly ? 1 : 0)
                };
            }
        }

        static int2 Offset(int d) => d == 0 ? new int2(1, 0) : d == 1 ? new int2(-1, 0) : d == 2 ? new int2(0, 1) : new int2(0, -1);
        static bool Separated(SpawnRegion candidate, SpawnRegion other, float gap) => math.any(math.abs(candidate.Center.xz - other.Center.xz) >= (candidate.Size.xz + other.Size.xz) * .5f + gap);
        public static Space Generate(EntityManager em, Entity root, int waveCount, ref Random random)
        {
            var space = new Space(em, root);
            var regions = em.GetBuffer<SpawnRegion>(root);
            regions.Clear();
            if (waveCount <= 0)
                return space;
            var rules = NightPlanOps.Rules(em, root);
            int count = math.min(waveCount, random.NextInt(rules.MinSpawnRegions, rules.MaxSpawnRegions + 1));
            bool edgeOnly = space.Normal.Count == 0;
            var candidates = new List<int2>(edgeOnly ? space.Edge : space.Normal);
            for (int n = candidates.Count - 1; n > 0; n--)
            {
                int at = random.NextInt(n + 1);
                (candidates[n], candidates[at]) = (candidates[at], candidates[n]);
            }

            if (!edgeOnly)
            {
                var ordered = new List<int2>(candidates.Count);
                foreach (var cell in candidates)
                    if (space.CityEdge(cell)) ordered.Add(cell);
                foreach (var cell in candidates)
                    if (!space.CityEdge(cell)) ordered.Add(cell);
                candidates = ordered;
            }

            foreach (var cell in candidates)
            {
                var candidate = space.Region(cell, edgeOnly, rules.SpawnRegionSize);
                bool separated = true;
                foreach (var other in regions)
                    if (!Separated(candidate, other, rules.SpawnRegionGap * space.Grid.CellSize))
                    {
                        separated = false;
                        break;
                    }

                if (!separated)
                    continue;
                regions.Add(candidate);
                if (regions.Length == count)
                    break;
            }

            return space;
        }

        // White-day edits repair only unusable regions. No reroll or RNG consumption on UI refresh.
        public static void Repair(EntityManager em, Entity root, Space space)
        {
            var rules = NightPlanOps.Rules(em, root);
            for (int i = 0; i < em.GetBuffer<SpawnRegion>(root).Length; i++)
            {
                var previous = em.GetBuffer<SpawnRegion>(root)[i];
                if ((previous.EdgeOnly == 0 || space.Normal.Count == 0) && NightSpatialOps.SpawnPoint(em, root, i, previous.Center, out _, space))
                    continue;
                bool edgeOnly = space.Normal.Count == 0;
                var candidates = edgeOnly ? space.Edge : space.Normal;
                float best = float.MaxValue;
                int bestPriority = int.MaxValue;
                var replacement = previous;
                // Prefer separated regions. If edits leave too little room, waves may share
                // a legal area rather than retaining an impossible entry.
                for (int pass = 0; pass < 2 && best == float.MaxValue; pass++)
                    foreach (var cell in candidates)
                    {
                        var candidate = space.Region(cell, edgeOnly, rules.SpawnRegionSize);
                        bool separated = true;
                        for (int j = 0; j < em.GetBuffer<SpawnRegion>(root).Length; j++)
                            if (j != i && !Separated(candidate, em.GetBuffer<SpawnRegion>(root)[j], rules.SpawnRegionGap * space.Grid.CellSize))
                            {
                                separated = false;
                                break;
                            }

                        if (pass == 0 && !separated)
                            continue;
                        int priority = !edgeOnly && space.CityEdge(cell) ? 0 : 1;
                        float distance = math.distancesq(previous.Center.xz, candidate.Center.xz);
                        if (priority > bestPriority || priority == bestPriority && distance >= best)
                            continue;
                        bestPriority = priority;
                        best = distance;
                        replacement = candidate;
                    }

                if (best == float.MaxValue)
                    continue;
                var regions = em.GetBuffer<SpawnRegion>(root);
                regions[i] = replacement;
                if (em.HasBuffer<IntelGeometry>(root))
                    em.GetBuffer<IntelGeometry>(root).Clear();
            }
        }
    }
}
