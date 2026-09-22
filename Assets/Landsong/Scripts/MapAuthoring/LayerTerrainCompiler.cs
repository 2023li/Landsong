using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Landsong.GridSystem;
using Unity.Mathematics;
using UnityEngine;

namespace Landsong.EditorTools
{
    // Compiles Blueprint cells and their directly assigned terrain rules. No meshes, colliders,
    // Dual Grid configurations, generated instances or random visual choices enter here.
    public static class LayerTerrainCompiler
    {
        public sealed class Group
        {
            public string Guid, Name;
            public int Height;
            public int Surface => checked(Height + 1);
        }

        public sealed class Cell
        {
            public Group Group;
            public Vector2Int Position;
            public BlueprintTerrainRule Rule;
            public bool Edge;
        }

        public sealed class Result
        {
            public readonly List<Group> Groups = new List<Group>();
            public readonly List<GridMapCellRecord> Primary = new List<GridMapCellRecord>();
            public readonly List<NavigationSurface> Additional = new List<NavigationSurface>();
            public readonly Dictionary<(string, Vector2Int), Cell> Cells = new Dictionary<(string, Vector2Int), Cell>();
            public readonly List<ProtrudingSlopeCompiler.Strip> Slopes = new List<ProtrudingSlopeCompiler.Strip>();
            public Group Find(string guid) => Groups.SingleOrDefault(g => g.Guid == guid) ?? throw new InvalidOperationException("连接引用的 Layer 已不存在，请重新选择端点层。");
        }

        public static Result Compile(Configuration config, MapTerrainRules rules) => Compile(config, rules.blueprintRules, rules.overlapRules, 0);
        public static Result Compile(Configuration config, MapContentAuthoring content) => Compile(config, content.TerrainRules.blueprintRules, content.TerrainRules.overlapRules, content.EdgeWidth);
        public static Result Compile(Configuration config, IReadOnlyList<BlueprintTerrainRule> rules, TerrainOverlapRules overlap, int edgeWidth)
        {
            if (config == null || config.width <= 0 || config.height <= 0 || !float.IsFinite(config.cellSize) || config.cellSize <= 0)
                throw new InvalidOperationException("地图尺寸或 Cell Size 无效。");
            var lookup = new Dictionary<string, BlueprintTerrainRule>(StringComparer.Ordinal);
            if (overlap == null)
                throw new InvalidOperationException("请绑定同层地形覆盖排序 SO。");
            var ranks = overlap.Compile();
            foreach (var rule in rules ?? Array.Empty<BlueprintTerrainRule>())
            {
                if (rule != null && BlueprintRuleNames.Key(rule.BlueprintLayerName) != rule.BlueprintLayerName)
                    throw new InvalidOperationException("共用规则名称不能包含 Layer 前缀，请迁移规则：" + rule.BlueprintLayerName);
                if (rule == null || string.IsNullOrWhiteSpace(rule.BlueprintLayerName) || !Enum.IsDefined(typeof(BlueprintLogicKind), rule.Kind) || rule.Kind == BlueprintLogicKind.Terrain && (!TerrainTypes.Single(rule.Terrain) || !ranks.ContainsKey(rule.Terrain)) || !lookup.TryAdd(rule.BlueprintLayerName, rule))
                    throw new InvalidOperationException("Blueprint 规则包含空名称、重复图层名称、空地形标识或无效用途。");
            }

            if (lookup.Count == 0)
                throw new InvalidOperationException("请在 SO 中配置 Blueprint 地形规则。");
            var boundary = new MapBoundaryCompiler(config, edgeWidth);
            var result = new Result();
            var heights = new HashSet<int>();
            var guids = new HashSet<string>();
            var blueprintGuids = new HashSet<string>();
            var ramps = new List<(Group, BlueprintLayer)>();
            foreach (var folder in config.blueprintLayerFolders.Where(f => f != null))
            {
                var match = Regex.Match(folder.folderName ?? "", @"^Layer(0|[1-9][0-9]*)$");
                Group group = null;
                if (match.Success)
                {
                    if (!int.TryParse(match.Groups[1].Value, out int height) || height >= int.MaxValue || !heights.Add(height) || string.IsNullOrEmpty(folder.guid) || !guids.Add(folder.guid))
                        throw new InvalidOperationException("Layer 编号或 GUID 重复/无效：" + folder.folderName);
                    group = new Group
                    {
                        Guid = folder.guid,
                        Name = folder.folderName,
                        Height = height
                    };
                    result.Groups.Add(group);
                }

                foreach (var blueprint in folder.blueprintLayers.Where(b => b != null))
                {
                    if (!blueprintGuids.Add(blueprint.guid))
                        throw new InvalidOperationException("Blueprint 被重复放入文件夹：" + blueprint.layerName);
                    if (!blueprint.isEnabled)
                        continue;
                    if (group == null)
                        throw new InvalidOperationException("启用的 Blueprint 必须位于 Layer0、Layer1 等文件夹：" + blueprint.layerName);
                    if (!float.IsFinite(blueprint.defaultLayerHeight) || Mathf.Abs(blueprint.defaultLayerHeight - group.Height) > .0001f)
                        throw new InvalidOperationException(group.Name + "/" + blueprint.layerName + " 的 Default Layer Height 必须为 " + group.Height);
                    var ruleName = BlueprintRuleNames.Key(blueprint.layerName, group.Height);
                    if (!lookup.TryGetValue(ruleName, out var semantic))
                        throw new InvalidOperationException("Blueprint 没有 SO 地形规则：" + blueprint.layerName);
                    if (semantic.Kind == BlueprintLogicKind.ProtrudingSlope)
                    {
                        ramps.Add((group, blueprint));
                        continue;
                    }

                    if (semantic.Kind != BlueprintLogicKind.Terrain)
                        continue;
                    foreach (var p in blueprint.allPositions)
                    {
                        var position = Position(p, config);
                        boundary?.RequireInside(position, blueprint.layerName);
                        var key = (group.Guid, position);
                        if (result.Cells.TryGetValue(key, out var current))
                        {
                            if (ranks[semantic.Terrain] > ranks[current.Rule.Terrain])
                                continue;
                            if (semantic.Terrain == current.Rule.Terrain && !SameRule(semantic, current.Rule))
                                throw new InvalidOperationException(group.Name + " 格 " + position + " 的同种地形配置了冲突权限。");
                        }

                        result.Cells[key] = new Cell
                        {
                            Group = group,
                            Position = position,
                            Rule = semantic
                        };
                    }
                }
            }

            if (result.Groups.Count == 0 || result.Cells.Count == 0)
                throw new InvalidOperationException("没有可烘焙的 Layer 地形格。");
            foreach (var column in result.Cells.Values.GroupBy(c => c.Position).OrderBy(g => g.Key.y).ThenBy(g => g.Key.x))
            {
                var sorted = column.OrderByDescending(c => c.Group.Height).ToArray();
                var top = sorted[0];
                bool inEdge = boundary != null && boundary.Edge.Contains(top.Position);
                top.Edge = inEdge;
                result.Primary.Add(new GridMapCellRecord(new GridPosition(top.Position.x, top.Position.y), top.Rule.Buildable && !inEdge, top.Rule.Traversable, top.Group.Height, top.Group.Surface, top.Rule.Terrain, top.Edge));
                foreach (var below in sorted.Skip(1).Where(c => c.Rule.Traversable))
                    result.Additional.Add(new NavigationSurface { Cell = new int2(below.Position.x, below.Position.y), Surface = below.Group.Surface, Elevation = below.Group.Height });
            }

            result.Groups.Sort((a, b) => a.Height.CompareTo(b.Height));
            if (ramps.Any(r => r.Item2.allPositions.Count > 0) && Mathf.Abs(config.cellSize - 1) > .0001f)
                throw new InvalidOperationException("45° 单格斜坡要求 Cell Size=1，Layer 高差=1。");
            if (boundary != null)
                foreach (var ramp in ramps)
                    foreach (var p in ramp.Item2.allPositions)
                        boundary.RequireInside(Position(p, config), ramp.Item2.layerName);
            ProtrudingSlopeCompiler.Compile(result, ramps, config.width, config.height);
            return result;
        }

        public static AuthoredConnection Connection(Result map, LayerTerrainConnection source)
        {
            if (source == null || source.Id <= 0 || source.Width < 1 || source.Width > 32)
                throw new InvalidOperationException("连接 ID 或宽度无效。");
            var entry = map.Find(source.EntryLayerGuid);
            var exit = map.Find(source.ExitLayerGuid);
            CheckEndpoint(map, entry, new int2(source.EntryCell.x, source.EntryCell.y));
            CheckEndpoint(map, exit, new int2(source.ExitCell.x, source.ExitCell.y));
            var delta = source.ExitCell - source.EntryCell;
            if ((delta.x == 0) == (delta.y == 0))
                throw new InvalidOperationException("连接端点必须沿 X 或 Z 轴，且不能重合。");
            int length = Mathf.Abs(delta.x) + Mathf.Abs(delta.y) + 1;
            int rotation = delta.y > 0 ? 0 : delta.x > 0 ? 1 : delta.y < 0 ? 2 : 3;
            int rise = exit.Height - entry.Height;
            if (length < 3 || length > 128 || rise < 0 || rise > 32)
                throw new InvalidOperationException("连接长度需为 3～128 格；入口选择低层，出口选择同层或高层（高差最多 32）。");
            if (rise == 0 && source.Width < 3)
                throw new InvalidOperationException("同层桥梁至少需要 3 格宽。");
            var size = new int2(source.Width, length);
            var origin = new int2(source.EntryCell.x, source.EntryCell.y) - TerrainConnectionOps.Rotated(int2.zero, size, rotation);
            var connection = new AuthoredConnection
            {
                Id = source.Id,
                Cell = origin,
                Size = size,
                Rotation = rotation,
                EntrySurface = entry.Surface,
                ExitSurface = exit.Surface,
                EntryElevation = entry.Height,
                Rise = rise,
                Bidirectional = source.Bidirectional
            };
            for (int x = 0; x < source.Width; x++)
            {
                CheckEndpoint(map, entry, TerrainConnectionOps.Port(connection.Cell, connection.Size, rotation, x, 0));
                CheckEndpoint(map, exit, TerrainConnectionOps.Port(connection.Cell, connection.Size, rotation, x, length - 1));
            }

            return connection;
        }

        static void CheckEndpoint(Result map, Group group, int2 p)
        {
            if (!map.Cells.TryGetValue((group.Guid, new Vector2Int(p.x, p.y)), out var cell) || !cell.Rule.Traversable)
                throw new InvalidOperationException("连接整排端点必须位于允许通行的地表：" + group.Name + " / " + p);
        }

        static Vector2Int Position(Vector2 p, Configuration config)
        {
            if (!float.IsFinite(p.x) || !float.IsFinite(p.y) || Mathf.Abs(p.x - Mathf.Round(p.x)) > .0001f || Mathf.Abs(p.y - Mathf.Round(p.y)) > .0001f || p.x < 0 || p.y < 0 || p.x >= config.width || p.y >= config.height)
                throw new InvalidOperationException("Blueprint 逻辑格越界或不是整数坐标：" + p);
            return new Vector2Int((int)p.x, (int)p.y);
        }

        static bool SameRule(BlueprintTerrainRule a, BlueprintTerrainRule b) => a.Kind == b.Kind && a.Terrain == b.Terrain && a.Buildable == b.Buildable && a.Traversable == b.Traversable;
    }
}
