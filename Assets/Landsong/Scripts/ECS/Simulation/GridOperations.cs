using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class GridOps
    {
        // Legacy map import entry point; terrain identity itself uses stable enum values.
        public static ulong TerrainBit(FixedString64Bytes key) => (ulong)TerrainTypes.ParseLegacy(key.ToString());
        public static bool TerrainAllowed(EntityManager em, Entity root, BuildingId definition, ulong terrain)
        {
            ref var placement = ref BuildingDefinitions.Get(em, root, definition).Capabilities.Placement;
            return terrain <= (ulong)TerrainType.All && TerrainTypes.Allows((TerrainType)terrain, placement.AllowedTerrains, placement.ExcludedTerrains);
        }

        public static int Index(GridData grid, int2 cell)
        {
            var p = cell - grid.Value.Value.Min;
            return math.any(p < 0) || math.any(p >= grid.Value.Value.Size) ? -1 : p.y * grid.Value.Value.Size.x + p.x;
        }

        public static int2 Cell(GridData grid, float3 position) => (int2)math.floor((position.xz - grid.Origin.xz) / grid.CellSize);
        public static bool RaycastSurface(GridData grid, float3 origin, float3 direction, out float3 hit)
        {
            hit = default;
            if (math.abs(direction.y) < .000001f || grid.CellSize <= 0)
                return false;
            var min = grid.Origin.xz + (float2)grid.Value.Value.Min * grid.CellSize;
            var max = min + (float2)grid.Value.Value.Size * grid.CellSize;
            var enter = 0f;
            var exit = float.MaxValue;
            for (var axis = 0; axis < 2; axis++)
            {
                var o = origin.xz[axis];
                var d = direction.xz[axis];
                if (math.abs(d) < .000001f)
                {
                    if (o < min[axis] || o >= max[axis])
                        return false;
                    continue;
                }

                var a = (min[axis] - o) / d;
                var b = (max[axis] - o) / d;
                enter = math.max(enter, math.min(a, b));
                exit = math.min(exit, math.max(a, b));
            }

            if (enter > exit)
                return false;
            var cell = Cell(grid, origin + direction * (enter + .00001f));
            // Grid DDA visits only cells crossed by the ray, not every map cell or visual collider.
            for (var guard = 0; guard < grid.Value.Value.Size.x + grid.Value.Value.Size.y + 2 && enter <= exit; guard++)
            {
                var index = Index(grid, cell);
                if (index < 0)
                    return false;
                var x = math.abs(direction.x) < .000001f ? float.MaxValue : (grid.Origin.x + (cell.x + (direction.x > 0 ? 1 : 0)) * grid.CellSize - origin.x) / direction.x;
                var z = math.abs(direction.z) < .000001f ? float.MaxValue : (grid.Origin.z + (cell.y + (direction.z > 0 ? 1 : 0)) * grid.CellSize - origin.z) / direction.z;
                var next = math.min(exit, math.min(x, z));
                var ground = grid.Value.Value.Cells[index];
                float2 gradient = SlopeOps.TryGet(grid, cell, out var slope) ? SlopeOps.Gradient(grid, slope) : float2.zero;
                float level = slope.ProtrudingSlope ? SlopeOps.CenterHeight(grid, slope) : ground.Height;
                var center = grid.Origin.xz + ((float2)cell + .5f) * grid.CellSize;
                float denominator = direction.y - math.dot(gradient, direction.xz);
                var at = math.abs(denominator) < .000001f ? -1 : (grid.Origin.y + level + math.dot(gradient, origin.xz - center) - origin.y) / denominator;
                if (ground.Exists != 0 && at >= math.max(0, enter - .00001f) && at <= next + .00001f)
                {
                    hit = origin + direction * at;
                    return true;
                }

                if (next >= exit)
                    return false;
                if (x <= z)
                    cell.x += direction.x > 0 ? 1 : -1;
                if (z <= x)
                    cell.y += direction.z > 0 ? 1 : -1;
                enter = next;
            }

            return false;
        }

        public static float3 Position(GridData grid, int2 cell, int2 size)
        {
            var index = Index(grid, cell);
            float height = index < 0 ? 0 : grid.Value.Value.Cells[index].Height;
            if (SlopeOps.TryGet(grid, cell, out var slope))
                height = SlopeOps.CenterHeight(grid, slope);
            return grid.Origin + new float3((cell.x + size.x * .5f) * grid.CellSize, height, (cell.y + size.y * .5f) * grid.CellSize);
        }

        public static bool CanPlace(EntityManager em, Entity root, BuildingId definition, int2 cell, int rotation, ulong ignore = 0)
        {
            var grid = em.GetComponentData<GridData>(root);
            ref var d = ref BuildingDefinitions.Get(em, root, definition);
            if (rotation < 0 || rotation > 3 || (!d.PlacementAndVisuals.CanRotate && rotation != 0 && ignore == 0))
                return false;
            var footprint = (rotation & 1) == 0 ? d.Footprint : d.Footprint.yx;
            for (int y = 0; y < footprint.y; y++)
                for (int x = 0; x < footprint.x; x++)
                    if (SlopeOps.TryGet(grid, cell + new int2(x, y), out _) && (!BuildingRoadOps.IsRoad(em, root, definition) || math.any(footprint != new int2(1))))
                        return false;
            if (TerrainConnectionOps.TryGet(em, root, definition, out _))
                return TerrainConnectionOps.CanPlace(em, root, definition, cell, rotation, ignore, out _);
            var size = (rotation & 1) == 0 ? d.Footprint : d.Footprint.yx;
            var occupied = em.GetBuffer<Occupancy>(root);
            var first = Index(grid, cell);
            if (first < 0)
                return false;
            var level = grid.Value.Value.Cells[first];
            for (var y = 0; y < size.y; y++)
                for (var x = 0; x < size.x; x++)
                {
                    var at = Index(grid, cell + new int2(x, y));
                    if (at < 0)
                        return false;
                    var c = grid.Value.Value.Cells[at];
                    if (c.Exists == 0 || c.EdgeZone != 0 || c.Buildable == 0 && !SlopeOps.TryGet(grid, cell + new int2(x, y), out _) || c.Elevation != level.Elevation || c.Surface != level.Surface || !TerrainAllowed(em, root, definition, c.Terrain))
                        return false;
                    if (occupied[at].Owner != 0 && occupied[at].Owner != ignore)
                        return false;
                }

            return true;
        }

        public static void Occupy(EntityManager em, Entity root, Entity entity, bool clear = false)
        {
            var b = em.GetComponentData<Building>(entity);
            BuildingPlacementState bPlacement = em.GetComponentData<BuildingPlacementState>(entity);
            var id = em.GetComponentData<Identity>(entity).Id;
            var grid = em.GetComponentData<GridData>(root);
            var occupancy = em.GetBuffer<Occupancy>(root);
            var cost = em.GetComponentData<BuildingNavigationStats>(entity).MovementCost;
            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing)
                cost = math.max(.01f, BuildingDefinitions.Get(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition).PlacementAndVisuals.RuinMovementCost);
            // Construction occupancy prevents stacked buildings; the independent lower ground stays open.
            if (TerrainConnectionOps.TryGet(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition, out _))
                cost = 1;
            if (cost <= 0 && SlopeOps.TryGet(grid, bPlacement.Cell, out _) && BuildingRoadOps.IsRoad(em, root, em.GetComponentData<BuildingDefinitionRef>(entity).Definition))
                cost = 1;
            for (var y = 0; y < bPlacement.Size.y; y++)
                for (var x = 0; x < bPlacement.Size.x; x++)
                {
                    var at = Index(grid, bPlacement.Cell + new int2(x, y));
                    if (at < 0)
                        continue;
                    if (!clear || occupancy[at].Owner == id)
                        occupancy[at] = clear ? default : new Occupancy
                        {
                            Owner = id,
                            MovementCost = cost
                        };
                }

            grid.Revision++;
            em.SetComponentData(root, grid);
        }

        public static bool Traversable(GridData grid, DynamicBuffer<Occupancy> occupied, int2 cell)
        {
            var at = Index(grid, cell);
            return at >= 0 && grid.Value.Value.Cells[at].Exists != 0 && grid.Value.Value.Cells[at].Traversable != 0 && (occupied[at].Owner == 0 || occupied[at].MovementCost > 0);
        }
    }
}
