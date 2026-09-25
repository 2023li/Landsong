using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct SurfaceNavNode : IBufferElementData
    {
        public int2 Cell;
        public float3 Position;
        public float2 Gradient, Lateral;
        public int Surface, Elevation, FirstEdge;
        public ulong Owner;
        public float Cost, SideClearance, Width;
        public byte Open, Corridor, ProtrudingSlope;
    }

    public struct SurfaceNavEdge : IBufferElementData
    {
        public int Target, Next;
    }

    public struct SurfaceNavCache : IComponentData
    {
        public int Revision;
        public uint OccupancyHash;
        public BlobAssetReference<GridBlob> Map;
    }

    public static class SurfaceNavigationGraph
    {
        const float FixedTerrainClearance = 1.5f;

        // A route through the outer column of a three-cell (or wider) slope leaves
        // only half a cell for the agent's centre. Keep that column available for
        // movement, but route across an inner column with a full cell of margin.
        public static bool OuterSlopeLane(in SurfaceNavNode node, float cellSize)
            => node.ProtrudingSlope != 0 && node.Width + .001f >= 3 * cellSize
                && node.SideClearance < cellSize - .001f;

        struct CellChain
        {
            public int First, Last;
        }

        public static void Invalidate(EntityManager em, Entity root)
        {
            if (em.HasComponent<SurfaceNavCache>(root))
                em.RemoveComponent<SurfaceNavCache>(root);
        }

        public static void Ensure(EntityManager em, Entity root)
        {
            var grid = em.GetComponentData<GridData>(root);
            var occupancyInput = em.GetBuffer<Occupancy>(root);
            uint occupancyHash = 2166136261;
            foreach (var item in occupancyInput)
                occupancyHash = (occupancyHash ^ math.hash(new uint3((uint)item.Owner, (uint)(item.Owner >> 32), math.asuint(item.MovementCost)))) * 16777619;
            if (em.HasComponent<SurfaceNavCache>(root))
            {
                var cache = em.GetComponentData<SurfaceNavCache>(root);
                if (cache.Revision == grid.Revision && cache.Map == grid.Value && cache.OccupancyHash == occupancyHash)
                    return;
            }

            var roadCost = new RoadWeatherCostOps.Context(em, root);
            int baseNodeCount = grid.Value.Value.Cells.Length + grid.Value.Value.NavigationSurfaces.Length;
            var nodes = new List<SurfaceNavNode>(baseNodeCount);
            var edges = new List<SurfaceNavEdge>();
            var byCell = new Dictionary<int2, CellChain>(grid.Value.Value.Cells.Length);
            var nextInCell = new List<int>(baseNodeCount);
            var occupied = occupancyInput;
            int Add(SurfaceNavNode n)
            {
                n.FirstEdge = -1;
                int i = nodes.Count;
                nodes.Add(n);
                nextInCell.Add(-1);
                if (byCell.TryGetValue(n.Cell, out var chain))
                {
                    nextInCell[chain.Last] = i;
                    chain.Last = i;
                    byCell[n.Cell] = chain;
                }
                else
                    byCell.Add(n.Cell, new CellChain { First = i, Last = i });
                return i;
            }

            void Link(int a, int b)
            {
                if (a < 0 || b < 0 || a == b)
                    return;
                var n = nodes[a];
                edges.Add(new SurfaceNavEdge { Target = b, Next = n.FirstEdge });
                n.FirstEdge = edges.Count - 1;
                nodes[a] = n;
            }

            for (int i = 0; i < grid.Value.Value.Cells.Length; i++)
            {
                var c = grid.Value.Value.Cells[i];
                var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                Add(new SurfaceNavNode { Cell = cell, Position = GridOps.Position(grid, cell, new int2(1)) + new float3(0, .5f, 0), Surface = c.Surface, Elevation = c.Elevation, Cost = roadCost.Effective(occupied[i]), SideClearance = float.MaxValue, Open = (byte)(c.Exists != 0 && c.Traversable != 0 && !SlopeOps.TryGet(grid, cell, out _) && (occupied[i].Owner == 0 || occupied[i].MovementCost > 0) ? 1 : 0) });
            }

            for (int i = 0; i < grid.Value.Value.NavigationSurfaces.Length; i++)
            {
                var c = grid.Value.Value.NavigationSurfaces[i];
                var p = grid.Origin + new float3((c.Cell.x + .5f) * grid.CellSize, c.Elevation * TerrainConnectionOps.HeightStep(grid) + .5f, (c.Cell.y + .5f) * grid.CellSize);
                Add(new SurfaceNavNode { Cell = c.Cell, Position = p, Surface = c.Surface, Elevation = c.Elevation, Open = 1, Cost = 1, SideClearance = float.MaxValue });
            }

            // A higher terrain surface is solid overhead even when it is not walkable.
            // Keep lower surfaces in the map, but close their navigation nodes when
            // there is less headroom than the clearance used by fixed connections.
            for (int i = 0; i < nodes.Count; i++)
            {
                var lower = nodes[i];
                if (lower.Open == 0 || !byCell.TryGetValue(lower.Cell, out var column))
                    continue;
                for (int j = column.First; j >= 0; j = nextInCell[j])
                {
                    if (j == i || j < grid.Value.Value.Cells.Length && grid.Value.Value.Cells[j].Exists == 0)
                        continue;
                    float gap = nodes[j].Position.y - lower.Position.y;
                    if (gap <= 0 || gap >= FixedTerrainClearance)
                        continue;
                    lower.Open = 0;
                    nodes[i] = lower;
                    break;
                }
            }

            // Adjacency never guesses a climb from height tolerance. Different planes need authored connections.
            int planarCount = nodes.Count;
            for (int i = 0; i < planarCount; i++)
            {
                var n = nodes[i];
                if (n.Open == 0)
                    continue;
                for (int d = 0; d < 4; d++)
                {
                    int2 delta = d == 0 ? new int2(1, 0) : d == 1 ? new int2(-1, 0) : d == 2 ? new int2(0, 1) : new int2(0, -1);
                    if (!byCell.TryGetValue(n.Cell + delta, out var others))
                        continue;
                    for (int j = others.First; j >= 0; j = nextInCell[j])
                        if (nodes[j].Open != 0 && nodes[j].Surface == n.Surface && nodes[j].Elevation == n.Elevation && math.abs(nodes[j].Position.y - n.Position.y) < .001f)
                            Link(i, j);
                }
            }

            int Endpoint(int2 cell, int surface, int elevation)
            {
                if (byCell.TryGetValue(cell, out var candidates))
                    for (int i = candidates.First; i >= 0; i = nextInCell[i])
                        if (nodes[i].Corridor == 0 && nodes[i].Open != 0 && nodes[i].Surface == surface && nodes[i].Elevation == elevation)
                            return i;
                return -1;
            }

            void Corridor(int2 cell, int2 size, int rotation, int entrySurface, int exitSurface, int elevation, int rise, bool bidirectional, ulong owner, int surface, float cost, float clearance, bool slope = false, bool road = false)
            {
                var indices = new int[size.x, size.y];
                var validLane = new bool[size.x];
                int firstLane = -1;
                for (int x = 0; x < size.x; x++)
                {
                    indices[x, 0] = Endpoint(TerrainConnectionOps.Port(cell, size, rotation, x, 0), entrySurface, elevation);
                    indices[x, size.y - 1] = Endpoint(TerrainConnectionOps.Port(cell, size, rotation, x, size.y - 1), exitSurface, elevation + rise);
                    if (indices[x, 0] < 0 || indices[x, size.y - 1] < 0)
                    {
                        if (!slope)
                            return; // A constructed connection must remain complete.
                        continue; // A blocked upper slope port must not erase the other lanes.
                    }
                    validLane[x] = true;
                    if (firstLane < 0)
                        firstLane = x;
                }
                if (firstLane < 0)
                    return;

                float height = nodes[indices[firstLane, 0]].Position.y;
                var direction = TerrainConnectionOps.Port(cell, size, rotation, 0, 1) - TerrainConnectionOps.Port(cell, size, rotation, 0, 0);
                float2 gradient = (float2)direction * (rise * TerrainConnectionOps.HeightStep(grid) / ((slope ? 1 : size.y - 1) * grid.CellSize));
                for (int x = 0; x < size.x; x++)
                {
                    if (!validLane[x])
                        continue;
                    for (int z = 1; z < size.y - 1; z++)
                    {
                        var at = TerrainConnectionOps.Port(cell, size, rotation, x, z);
                        var p = grid.Origin + new float3((at.x + .5f) * grid.CellSize, 0, (at.y + .5f) * grid.CellSize);
                        p.y = height + rise * TerrainConnectionOps.HeightStep(grid) * z / (size.y - 1f);
                        var reservation = occupied[GridOps.Index(grid, at)];
                        indices[x, z] = Add(new SurfaceNavNode { Cell = at, Position = p, Gradient = gradient, Lateral = new float2(direction.y, -direction.x), Surface = surface, Elevation = elevation, Owner = owner, Corridor = (byte)(rise == 0 && !slope ? 0 : 1), ProtrudingSlope = (byte)(slope ? 1 : 0), Open = (byte)(!slope || reservation.Owner == 0 || reservation.MovementCost > 0 ? 1 : 0), Cost = slope && reservation.Owner != 0 ? roadCost.Effective(reservation) : road && roadCost.Snowing ? cost * RoadWeatherCostOps.SnowMultiplier : cost, Width = size.x * grid.CellSize, SideClearance = math.min(x + .5f, size.x - x - .5f) * grid.CellSize });
                        if (byCell.TryGetValue(at, out var below))
                            for (int j = below.First; j >= 0; j = nextInCell[j])
                            {
                                if (j == indices[x, z])
                                    continue;
                                var n = nodes[j];
                                if (n.Corridor != 0 || p.y - n.Position.y < 0 || p.y - n.Position.y >= clearance)
                                    continue;
                                n.Open = 0;
                                nodes[j] = n; // Stair body occupies low clearance space, without removing the lower surface.
                            }
                    }
                }

                for (int x = 0; x < size.x; x++)
                {
                    if (!validLane[x])
                        continue;
                    for (int z = 0; z < size.y; z++)
                    {
                        int i = indices[x, z];
                        if (z + 1 < size.y)
                        {
                            Link(i, indices[x, z + 1]);
                            if (bidirectional)
                                Link(indices[x, z + 1], i);
                        }

                        if (x + 1 < size.x && validLane[x + 1] && z > 0 && z < size.y - 1)
                        {
                            Link(i, indices[x + 1, z]);
                            Link(indices[x + 1, z], i);
                        }
                    }
                }
            }

            for (int i = 0; i < grid.Value.Value.Connections.Length; i++)
            {
                var c = grid.Value.Value.Connections[i];
                Corridor(c.Cell, c.Size, c.Rotation, c.EntrySurface, c.ExitSurface, c.EntryElevation, c.Rise, c.Bidirectional, 0, -c.Id, 1, FixedTerrainClearance, c.ProtrudingSlope);
            }

            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var entity in buildings)
                {
                    var b = em.GetComponentData<Building>(entity);
                    BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(entity);
                    var id = em.GetComponentData<Identity>(entity);
                    var definition = em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
                    if (b.Stage == LifeStage.Construction || !TerrainConnectionOps.TryGet(em, root, definition, out var rule))
                        continue;
                    var size = Definitions.BuildingDefinitions.Get(em, root, definition).Footprint;
                    int a = GridOps.Index(grid, TerrainConnectionOps.Port(bPlacement.Cell, size, bPlacement.Rotation, 0, 0));
                    int z = GridOps.Index(grid, TerrainConnectionOps.Port(bPlacement.Cell, size, bPlacement.Rotation, 0, size.y - 1));
                    if (a < 0 || z < 0)
                        continue;
                    var entry = grid.Value.Value.Cells[a];
                    var exit = grid.Value.Value.Cells[z];
                    var road = (Definitions.BuildingDefinitions.Get(em, root, definition).PlacementAndVisuals.Category & BuildingCategory.Road) != 0;
                    Corridor(bPlacement.Cell, size, bPlacement.Rotation, entry.Surface, exit.Surface, entry.Elevation, rule.Rise, rule.Bidirectional, id.Id, 0, b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing ? rule.DamagedCost : 1, rule.Clearance, road: road);
                }

            EntityState.Buffer<SurfaceNavNode>(em, root);
            var nb = em.GetBuffer<SurfaceNavNode>(root);
            nb.Clear();
            foreach (var n in nodes)
                nb.Add(n);
            EntityState.Buffer<SurfaceNavEdge>(em, root);
            var eb = em.GetBuffer<SurfaceNavEdge>(root);
            eb.Clear();
            foreach (var e in edges)
                eb.Add(e);
            EntityState.Set(em, root, new SurfaceNavCache { Revision = grid.Revision, Map = grid.Value, OccupancyHash = occupancyHash });
        }
    }
}
