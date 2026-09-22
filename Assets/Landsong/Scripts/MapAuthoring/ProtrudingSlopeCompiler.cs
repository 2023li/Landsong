using System;
using System.Collections.Generic;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.EditorTools
{
    // The painted cells are outside the platform. All heights come from Layer folders.
    public static class ProtrudingSlopeCompiler
    {
        public sealed class Strip
        {
            public string BlueprintGuid;
            public int Height, Rotation;
            public Vector2Int Direction;
            public Vector2Int[] Cells;
            public AuthoredConnection Connection;
        }

        static readonly Vector2Int[] Directions =
        {
            Vector2Int.up,
            Vector2Int.right,
            Vector2Int.down,
            Vector2Int.left
        };
        public static void Compile(LayerTerrainCompiler.Result map, List<(LayerTerrainCompiler.Group, BlueprintLayer)> layers, int width, int depth)
        {
            var top = map.Cells.Values.GroupBy(c => c.Position).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Group.Height).First());
            var used = new HashSet<Vector2Int>();
            bool Ground(Vector2Int p, int h) => top.TryGetValue(p, out var c) && c.Group.Height == h && c.Rule.Traversable && !c.Edge;
            bool High(Vector2Int p, int h) => top.TryGetValue(p, out var c) && c.Group.Height == h;
            foreach (var item in layers.OrderBy(p => p.Item1.Height).ThenBy(p => p.Item2.guid, StringComparer.Ordinal))
            {
                int height = item.Item1.Height;
                var bp = item.Item2;
                if (height < 1)
                    throw new InvalidOperationException("斜坡必须属于 Layer1 或更高层：" + bp.layerName);
                var pending = new Dictionary<Vector2Int, int>();
                foreach (var p in bp.allPositions)
                {
                    var cell = new Vector2Int(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
                    if ((p - (Vector2)cell).sqrMagnitude > .000001f || cell.x < 0 || cell.y < 0 || cell.x >= width || cell.y >= depth || !used.Add(cell))
                        throw new InvalidOperationException("斜坡格重复、非整数或越界：" + p);
                    if (!Ground(cell, height - 1))
                        throw new InvalidOperationException("突出斜坡必须画在平台外侧、n-1 层可通行地面：" + cell);
                    var choices = Enumerable.Range(0, 4).Where(i => Ground(cell + Directions[i], height)).ToArray();
                    if (choices.Length != 1)
                        throw new InvalidOperationException("斜坡必须只面向一条直边，不能位于内角或孤立位置：" + cell);
                    int rotation = choices[0];
                    var d = Directions[rotation];
                    if (!Ground(cell - d, height - 1))
                        throw new InvalidOperationException("斜坡下口没有 n-1 层可通行地面：" + cell);
                    pending.Add(cell, rotation);
                }

                while (pending.Count > 0)
                {
                    var first = pending.OrderBy(p => p.Key.y).ThenBy(p => p.Key.x).First();
                    int rotation = first.Value;
                    var d = Directions[rotation];
                    var lateral = new Vector2Int(d.y, -d.x);
                    var start = first.Key;
                    while (pending.TryGetValue(start - lateral, out int r) && r == rotation)
                        start -= lateral;
                    var cells = new List<Vector2Int>();
                    var at = start;
                    while (pending.TryGetValue(at, out int r) && r == rotation)
                    {
                        cells.Add(at);
                        pending.Remove(at);
                        at += lateral;
                    }

                    if (cells.Count < 3)
                        throw new InvalidOperationException("斜坡至少需要左、中、右连续三格：" + start);
                    bool Edge(Vector2Int p) => High(p + d, height) && !High(p, height);
                    int before = 0, after = 0;
                    for (var p = start - lateral; Edge(p); p -= lateral)
                        before++;
                    for (var p = cells[cells.Count - 1] + lateral; Edge(p); p += lateral)
                        after++;
                    if (before < 1 || after < 1 || before + cells.Count + after < 6)
                        throw new InvalidOperationException("斜坡只能出现在至少六格长的直边，两端须各避开一个角格：" + start);
                    var lower = map.Groups.Single(g => g.Height == height - 1);
                    var size = new int2(cells.Count, 3);
                    var entry = new int2(start.x - d.x, start.y - d.y);
                    map.Slopes.Add(new Strip { BlueprintGuid = bp.guid, Height = height, Rotation = rotation, Direction = d, Cells = cells.ToArray(), Connection = new AuthoredConnection { Id = 1000000000 + map.Slopes.Count, Cell = entry - TerrainConnectionOps.Rotated(int2.zero, size, rotation), Size = size, Rotation = rotation, EntrySurface = lower.Surface, ExitSurface = item.Item1.Surface, EntryElevation = height - 1, Rise = 1, Bidirectional = true, ProtrudingSlope = true } });
                }
            }
        }
    }
}
