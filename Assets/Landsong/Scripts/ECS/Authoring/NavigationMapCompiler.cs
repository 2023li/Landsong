using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class NavigationMapCompiler
    {
        public static void Bake(MapAsset map, BlobBuilder builder, ref GridBlob blob)
        {
            if (!math.isfinite(map.ElevationStep) || map.ElevationStep <= 0)
                throw new InvalidOperationException("地图高度单位必须为正数。");
            blob.ElevationStep = map.ElevationStep;
            var identities = new HashSet<(int, int, int, int)>();
            var planes = new HashSet<(int, int, int)>();
            bool InBounds(int2 c) => c.x >= map.Min.x && c.y >= map.Min.y && c.x < map.Min.x + map.Size.x && c.y < map.Min.y + map.Size.y;
            for (int i = 0; i < map.Cells.Length; i++)
            {
                var c = map.Cells[i];
                if (!c.Exists)
                    continue;
                if (!math.isfinite(c.Height) || math.abs(c.Height - c.Elevation * map.ElevationStep) > .001f || c.Surface < 0)
                    throw new InvalidOperationException("地图地表高度必须对应整数高度等级：" + i);
                int x = map.Min.x + i % map.Size.x, z = map.Min.y + i / map.Size.x;
                planes.Add((x, z, c.Elevation));
                if (c.Traversable)
                    identities.Add((x, z, c.Surface, c.Elevation));
            }

            var surfaces = map.NavigationSurfaces ?? Array.Empty<NavigationSurface>();
            var layers = builder.Allocate(ref blob.NavigationSurfaces, surfaces.Length);
            for (int i = 0; i < surfaces.Length; i++)
            {
                var c = surfaces[i];
                if (!InBounds(c.Cell) || c.Surface <= 0 || !planes.Add((c.Cell.x, c.Cell.y, c.Elevation)) || !identities.Add((c.Cell.x, c.Cell.y, c.Surface, c.Elevation)))
                    throw new InvalidOperationException("额外导航地表越界、ID 无效或重复：" + i);
                layers[i] = c;
            }

            var links = map.Connections ?? Array.Empty<AuthoredConnection>();
            var output = builder.Allocate(ref blob.Connections, links.Length);
            var ids = new HashSet<int>();
            foreach (var c in links)
            {
                if (c.Id <= 0 || !ids.Add(c.Id) || c.Size.x < 1 || c.Size.x > (c.ProtrudingSlope ? math.max(map.Size.x, map.Size.y) : 32) || c.Size.y < 3 || c.Size.y > 128 || c.Rotation < 0 || c.Rotation > 3 || c.Rise < 0 || c.Rise > 32 || c.ProtrudingSlope && (c.Size.x < 3 || c.Size.y != 3 || c.Rise != 1 || !c.Bidirectional) || !c.ProtrudingSlope && c.Rise == 0 && c.Size.x < 3)
                    throw new InvalidOperationException("地编连接标识、尺寸、旋转或高差无效。");
                for (int x = 0; x < c.Size.x; x++)
                {
                    var a = TerrainConnectionOps.Port(c.Cell, c.Size, c.Rotation, x, 0);
                    var b = TerrainConnectionOps.Port(c.Cell, c.Size, c.Rotation, x, c.Size.y - 1);
                    if (!identities.Contains((a.x, a.y, c.EntrySurface, c.EntryElevation)) || !identities.Contains((b.x, b.y, c.ExitSurface, c.EntryElevation + c.Rise)))
                        throw new InvalidOperationException("地编连接端点没有绑定到指定地表与高度：" + c.Id);
                }

                for (int z = 1; z < c.Size.y - 1; z++)
                    for (int x = 0; x < c.Size.x; x++)
                    {
                        var at = TerrainConnectionOps.Port(c.Cell, c.Size, c.Rotation, x, z);
                        if (!InBounds(at))
                            throw new InvalidOperationException("地编连接跨度超出地图：" + c.Id);
                        var ground = map.Cells[(at.y - map.Min.y) * map.Size.x + at.x - map.Min.x];
                        float height = (c.EntryElevation + c.Rise * z / (c.Size.y - 1f)) * map.ElevationStep;
                        if (ground.Exists && ground.Height >= height - .001f)
                            throw new InvalidOperationException("地编连接穿入地形：" + c.Id);
                        foreach (var surface in surfaces)
                            if (math.all(surface.Cell == at) && math.abs(surface.Elevation * map.ElevationStep - height) < 1.5f)
                                throw new InvalidOperationException("地编连接与额外地表净空不足：" + c.Id);
                        foreach (var other in links)
                            if (other.Id != c.Id && TerrainConnectionOps.InteriorHeight(other, at, map.ElevationStep, out float crossing) && math.abs(crossing - height) < 1.5f)
                                throw new InvalidOperationException("固定通道交叉且净空不足：" + c.Id);
                    }
            }

            for (int i = 0; i < links.Length; i++)
                output[i] = links[i];
        }
    }
}
