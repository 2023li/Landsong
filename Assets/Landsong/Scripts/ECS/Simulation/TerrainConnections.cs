using Landsong.ECS.Definitions;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Sirenix.OdinInspector;

namespace Landsong.ECS
{
    [Serializable]
    public struct NavigationSurface
    {
        [LabelText("逻辑格坐标")]
        public int2 Cell;
        [LabelText("地表 ID")]
        public int Surface;
        [LabelText("整数高度等级")]
        public int Elevation;
    }

    [Serializable]
    public struct AuthoredConnection
    {
        [LabelText("稳定连接 ID")]
        public int Id;
        [LabelText("占地左下格")]
        public int2 Cell;
        [LabelText("宽度与长度（格）")]
        public int2 Size;
        [LabelText("朝向（0～3）")]
        public int Rotation;
        [LabelText("入口地表 ID")]
        public int EntrySurface;
        [LabelText("出口地表 ID")]
        public int ExitSurface;
        [LabelText("入口高度等级")]
        public int EntryElevation;
        [LabelText("上升高度等级")]
        public int Rise;
        [LabelText("允许双向通行")]
        public bool Bidirectional;
        [LabelText("突出式斜坡（单格坡面、仅道路可建）")]
        public bool ProtrudingSlope;
    }

    public static class TerrainConnectionOps
    {
        public static float HeightStep(GridData grid) => grid.Value.Value.ElevationStep > 0 ? grid.Value.Value.ElevationStep : 1;
        public static bool TryGet(EntityManager em, Entity root, BuildingId definition, out BuildingTerrainConnection connection)
        {
            if (!BuildingDefinitions.IsValid(em, root, definition))
            {
                connection = default;
                return false;
            }

            connection = BuildingDefinitions.Get(em, root, definition).Capabilities.Connection;
            return connection.Enabled;
        }

        public static int2 Rotated(int2 local, int2 size, int rotation) => rotation == 0 ? local : rotation == 1 ? new int2(local.y, size.x - 1 - local.x) : rotation == 2 ? size - 1 - local : new int2(size.y - 1 - local.y, local.x);
        public static int2 Port(int2 cell, int2 size, int rotation, int lane, int row) => cell + Rotated(new int2(lane, row), size, rotation);
        public static bool InteriorHeight(AuthoredConnection c, int2 cell, float step, out float height)
        {
            int2 p = cell - c.Cell;
            int2 local = c.Rotation == 0 ? p : c.Rotation == 1 ? new int2(c.Size.x - 1 - p.y, p.x) : c.Rotation == 2 ? c.Size - 1 - p : new int2(p.y, c.Size.y - 1 - p.x);
            height = 0;
            if (local.x < 0 || local.x >= c.Size.x || local.y <= 0 || local.y >= c.Size.y - 1)
                return false;
            height = (c.EntryElevation + c.Rise * local.y / (c.Size.y - 1f)) * step;
            return true;
        }

        public static bool CanPlace(EntityManager em, Entity root, BuildingId definition, int2 cell, int rotation, ulong ignore, out string reason)
        {
            reason = "";
            if (!TryGet(em, root, definition, out var rule))
                return false;
            var size = BuildingDefinitions.Get(em, root, definition).Footprint;
            var grid = em.GetComponentData<GridData>(root);
            var occupied = em.GetBuffer<Occupancy>(root);
            if (rule.Rise == 0 && size.x < 3)
            {
                reason = "桥梁至少需要 3 格宽";
                return false;
            }
            if (size.x < 1 || size.x > 32 || size.y < 3 || size.y > 128 || rotation < 0 || rotation > 3)
            {
                reason = "通行建筑需要 1～32 格宽、3～128 格长";
                return false;
            }

            var start = GridOps.Index(grid, Port(cell, size, rotation, 0, 0));
            var end = GridOps.Index(grid, Port(cell, size, rotation, 0, size.y - 1));
            if (start < 0 || end < 0)
            {
                reason = "连接端点超出地图";
                return false;
            }

            var a = grid.Value.Value.Cells[start];
            var b = grid.Value.Value.Cells[end];
            if (b.Elevation - a.Elevation != rule.Rise || math.abs(b.Height - a.Height - rule.Rise * HeightStep(grid)) > .001f)
            {
                reason = "桥头/阶梯两端高差不匹配";
                return false;
            }

            for (int z = 0; z < size.y; z++)
                for (int x = 0; x < size.x; x++)
                {
                    int at = GridOps.Index(grid, Port(cell, size, rotation, x, z));
                    if (SlopeOps.TryGet(grid, Port(cell, size, rotation, x, z), out _))
                    {
                        reason = "斜坡格只允许普通道路，不能作为其他通行建筑占地";
                        return false;
                    }

                    if (at < 0 || grid.Value.Value.Cells[at].EdgeZone != 0 || occupied[at].Owner != 0 && occupied[at].Owner != ignore)
                    {
                        reason = "连接跨度越界、进入边缘区或被建筑占用";
                        return false;
                    }

                    var ground = grid.Value.Value.Cells[at];
                    bool endpoint = z == 0 || z == size.y - 1;
                    if (endpoint)
                    {
                        var expected = z == 0 ? a : b;
                        if (ground.Exists == 0 || ground.Buildable == 0 || ground.Traversable == 0 || ground.Surface != expected.Surface || ground.Elevation != expected.Elevation)
                        {
                            reason = "整排端口必须接到同一可建造且可通行地表";
                            return false;
                        }
                    }
                    else if (ground.Exists != 0)
                    {
                        float height = math.lerp(a.Height, b.Height, z / (float)(size.y - 1));
                        if (ground.Height >= height - .001f)
                        {
                            reason = "跨度内部地形穿入通行面";
                            return false;
                        }

                        if (rule.Rise == 0 && ground.Traversable != 0 && height - ground.Height < rule.Clearance)
                        {
                            reason = "桥下通行净空不足";
                            return false;
                        }
                    }

                    if (!endpoint)
                    {
                        float height = math.lerp(a.Height, b.Height, z / (float)(size.y - 1));
                        var atCell = Port(cell, size, rotation, x, z);
                        for (int i = 0; i < grid.Value.Value.NavigationSurfaces.Length; i++)
                        {
                            var surface = grid.Value.Value.NavigationSurfaces[i];
                            if (math.any(surface.Cell != atCell))
                                continue;
                            float delta = surface.Elevation * HeightStep(grid) - height;
                            if (math.abs(delta) < .001f || delta > 0 && delta < rule.Clearance || rule.Rise == 0 && delta < 0 && -delta < rule.Clearance)
                            {
                                reason = "连接与额外地表净空不足";
                                return false;
                            }
                        }

                        for (int i = 0; i < grid.Value.Value.Connections.Length; i++)
                            if (InteriorHeight(grid.Value.Value.Connections[i], atCell, HeightStep(grid), out float other) && math.abs(other - height) < rule.Clearance)
                            {
                                reason = "连接与固定通道交叉且净空不足";
                                return false;
                            }
                    }

                    // Terrain requirements describe supports, not empty air under the span.
                    if (endpoint)
                    {
                        if (!GridOps.TerrainAllowed(em, root, definition, ground.Terrain))
                        {
                            reason = "连接端点地形不符合允许/排除规则";
                            return false;
                        }
                    }
                }

            return true;
        }

        public static float AnchorHeight(EntityManager em, Entity root, BuildingId definition, int2 cell, int rotation)
        {
            var grid = em.GetComponentData<GridData>(root);
            var size = BuildingDefinitions.Get(em, root, definition).Footprint;
            var at = GridOps.Index(grid, Port(cell, size, rotation, 0, 0));
            return at < 0 ? 0 : grid.Value.Value.Cells[at].Height;
        }
    }
}
