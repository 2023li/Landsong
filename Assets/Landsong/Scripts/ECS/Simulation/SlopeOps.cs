using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class SlopeOps
    {
        public static bool TryGet(GridData grid, int2 cell, out AuthoredConnection slope)
        {
            for (int i=0;i<grid.Value.Value.Connections.Length;i++)
            {
                var c=grid.Value.Value.Connections[i];
                if (c.ProtrudingSlope && TerrainConnectionOps.InteriorHeight(c,cell,TerrainConnectionOps.HeightStep(grid),out _)) { slope=c;return true; }
            }
            slope=default;return false;
        }
        public static float2 Gradient(GridData grid, AuthoredConnection slope)
        {
            int2 direction=TerrainConnectionOps.Port(slope.Cell,slope.Size,slope.Rotation,0,1)-TerrainConnectionOps.Port(slope.Cell,slope.Size,slope.Rotation,0,0);
            return (float2)direction*(TerrainConnectionOps.HeightStep(grid)/grid.CellSize);
        }
        public static float CenterHeight(GridData grid, AuthoredConnection slope) => (slope.EntryElevation+.5f)*TerrainConnectionOps.HeightStep(grid);
        public static bool TryHeight(GridData grid, float2 worldXZ, out float height)
        {
            int2 cell=(int2)math.floor((worldXZ-grid.Origin.xz)/grid.CellSize);
            if (!TryGet(grid,cell,out var slope)) { height=0;return false; }
            var center=grid.Origin.xz+((float2)cell+.5f)*grid.CellSize;
            height=grid.Origin.y+CenterHeight(grid,slope)+math.dot(Gradient(grid,slope),worldXZ-center);return true;
        }
    }
}
