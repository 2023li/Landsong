using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Editor;
using Landsong.GridSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace Landsong.EditorTools
{
    [InitializeOnLoad]
    public static class MapPopulationTools
    {
        static MapPopulationTools()
        {
            MapContentAuthoring.PopulationAction = (content, action) => action switch
            {
                "Defaults" => FillDefaults(content),
                "Preview" => "树林单独规划：\n" + Preview(content, MapGenerationKind.Forest).Report + "\n建筑单独规划（按当前树林占用）：\n" + Preview(content, MapGenerationKind.Buildings).Report,
                "Forest" => Generate(content, MapGenerationKind.Forest),
                "Buildings" => Generate(content, MapGenerationKind.Buildings),
                "ClearAll" => Clear(content, null),
                "ClearGenerated" => Clear(content, MapGenerationKind.Manual),
                _ => throw new InvalidOperationException("未知地图美化操作。")};
        }

        public sealed class Placement
        {
            public BuildingDefinitionAsset Definition;
            public int2 Cell, Size;
            public int Rotation, Level, Patch = -1;
            public Vector3 Position;
        }

        public sealed class Plan
        {
            public readonly List<Placement> Items = new List<Placement>();
            public readonly List<string> Reports = new List<string>();
            public string Report => string.Join("\n", Reports);
        }

        // Uses current Blueprint data, never stale baked terrain or visual collider heights.
        public static Plan Preview(MapContentAuthoring content, MapGenerationKind kind)
        {
            RequireEditable(content);
            var source = TileWorldCreatorMapBaker.CreateValidationGrid(content);
            var map = ScriptableObject.CreateInstance<MapAsset>();
            try
            {
                var terrain = EcsMapIncrementalImport.ReadTerrain(content, source);
                map.Min = terrain.Min;
                map.Size = terrain.Size;
                map.Origin = terrain.Origin;
                map.CellSize = terrain.CellSize;
                map.Cells = terrain.Cells;
                map.ElevationStep = source.ElevationWorldStep;
                MapNavigationBaker.Bake(content, map);
                return BuildPlan(content, map, kind);
            }
            finally
            {
                Object.DestroyImmediate(source);
                Object.DestroyImmediate(map);
            }
        }

        public static string Generate(MapContentAuthoring content, MapGenerationKind kind)
        {
            var plan = Preview(content, kind);
            // Failed/empty planning must not erase the previous result.
            if (plan.Items.Count == 0)
                return plan.Report + "\n没有可放置内容，保留原有生成结果。";
            Apply(content, kind, plan);
            return plan.Report + "\n已替换本类型生成内容；可撤销。保存并烘焙后用于游戏。";
        }

        public static void Apply(MapContentAuthoring content, MapGenerationKind kind, Plan plan)
        {
            RequireEditable(content);
            Transaction("生成地图内容", () =>
            {
                ClearObjects(content, kind);
                var parent = GameMapWorkflow.Child(content, GameMapWorkflow.Buildings).transform;
                foreach (var item in plan.Items)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(item.Definition.Prefab, content.gameObject.scene);
                    Undo.RegisterCreatedObjectUndo(go, "生成地图内容");
                    Undo.SetTransformParent(go.transform, parent, "生成地图内容");
                    go.transform.SetPositionAndRotation(item.Position, Quaternion.Euler(0, item.Rotation * 90, 0));
                    var preview = go.GetComponent<InitialBuildingPreview>() ?? Undo.AddComponent<InitialBuildingPreview>(go);
                    Undo.RecordObject(preview, "生成地图内容");
                    preview.Definition = item.Definition;
                    preview.Level = item.Level;
                    preview.GenerationKind = kind;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(preview);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                }

                EditorSceneManager.MarkSceneDirty(content.gameObject.scene);
            });
        }

        // null = all authored buildings, including the palace; Manual = both generated categories only.
        public static string Clear(MapContentAuthoring content, MapGenerationKind? kind)
        {
            RequireEditable(content);
            int count = 0;
            Transaction("移除地图预制建筑", () =>
            {
                count = ClearObjects(content, kind);
            });
            return $"已移除 {count} 个初始建筑，可撤销。";
        }

        static int ClearObjects(MapContentAuthoring content, MapGenerationKind? kind)
        {
            var roots = BuildingRoots(content).Where(t =>
            {
                var preview = t.GetComponent<InitialBuildingPreview>();
                return kind == null || (preview != null && (kind == MapGenerationKind.Manual ? preview.GenerationKind != MapGenerationKind.Manual : preview.GenerationKind == kind));
            }).ToArray();
            foreach (var t in roots)
                Undo.DestroyObjectImmediate(t.gameObject);
            return roots.Length;
        }

        static Transform[] BuildingRoots(MapContentAuthoring content)
        {
            var folder = content.transform.Find(GameMapWorkflow.Buildings);
            var roots = content.Previews.Select(p => p.transform).Concat(folder == null ? Array.Empty<Transform>() : folder.GetComponentsInChildren<BuildingVisualAuthoring>(true).Select(v => v.transform)).Where(t => t.GetComponentInParent<MapContentAuthoring>() == content).Distinct().ToArray();
            return roots.Where(t => !roots.Any(parent => parent != t && t.IsChildOf(parent))).ToArray();
        }

        static void Transaction(string name, Action action)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
            try
            {
                action();
                Undo.CollapseUndoOperations(group);
            }
            catch
            {
                Undo.RevertAllDownToGroup(group);
                throw;
            }
        }

        static void RequireEditable(MapContentAuthoring content)
        {
            if (Application.isPlaying || content == null || EditorUtility.IsPersistent(content) || !content.gameObject.scene.IsValid())
                throw new InvalidOperationException("请在地图制图场景的编辑模式中操作。");
        }

        public static Plan BuildPlan(MapContentAuthoring content, MapAsset map, MapGenerationKind kind)
        {
            var settings = content.PopulationTools ?? throw new InvalidOperationException("缺少生成配置。");
            if (kind == MapGenerationKind.Manual)
                throw new InvalidOperationException("请选择树林或随机建筑。");
            if (settings.Layer < -1)
                throw new InvalidOperationException("限定 Layer 不能小于 -1。");
            var random = new Random(unchecked(settings.Seed ^ (kind == MapGenerationKind.Forest ? 0x35A719 : 0x739125)));
            using var space = new PlacementSpace(content, map, kind);
            var plan = new Plan();
            if (kind == MapGenerationKind.Forest)
                Forest(space, settings, random, plan);
            else
                Scatter(space, settings, random, plan);
            return plan;
        }

        static void Scatter(PlacementSpace space, MapPopulationSettings s, Random rng, Plan plan)
        {
            if (s.Buildings == null || s.Buildings.Count == 0)
                throw new InvalidOperationException("请先填写随机建筑列表。");
            foreach (var entry in s.Buildings)
            {
                if (entry == null || entry.Count < 0 || entry.Count > 10000)
                    throw new InvalidOperationException("每种建筑数量须在 0–10000 之间。");
                space.Validate(entry.Definition, entry.Level);
            }

            foreach (var entry in s.Buildings)
            {
                var cells = space.Cells.ToList();
                Shuffle(cells, rng);
                int placed = 0;
                foreach (var cell in cells)
                {
                    if (placed >= entry.Count)
                        break;
                    if (!space.TryPlace(entry.Definition, entry.Level, cell, s, rng, out var item))
                        continue;
                    plan.Items.Add(item);
                    placed++;
                }

                plan.Reports.Add($"{entry.Definition.Metadata.Name}：{placed}/{entry.Count}" + (placed < entry.Count ? "（合法空位不足）" : ""));
            }
        }

        static void Forest(PlacementSpace space, MapPopulationSettings s, Random rng, Plan plan)
        {
            if (s.Trees == null || s.Trees.Count == 0)
                throw new InvalidOperationException("请先填写树种列表。");
            if (s.Trees.Any(t => t == null || t.Weight < 1) || s.Trees.Select(t => t.Definition).Distinct().Count() != s.Trees.Count)
                throw new InvalidOperationException("树种不能重复或为空，权重必须为正数。");
            if (s.PatchCount < 0 || s.PatchCount > 100 || s.IsolatedCount < 0 || s.IsolatedCount > 10000 || s.IsolatedSpacing < 1 || s.IsolatedSpacing > 100 || !(s.Radius.x >= 1 && s.Radius.y >= s.Radius.x && s.Radius.y <= 40) || !(s.Density > 0 && s.Density <= 1) || !(s.DominantRatio >= .5f && s.DominantRatio <= 1))
                throw new InvalidOperationException("树林参数无效：片数 0–100，半径 1–40，密度 (0,1]，主树种比例 [0.5,1]，零散树 0–10000、间距 1–100。");
            foreach (var tree in s.Trees)
                space.Validate(tree.Definition, 1);
            var centers = space.Cells.ToList();
            Shuffle(centers, rng);
            var patches = new List<(int2 Cell, float Radius)>();
            int patchCount = 0;
            foreach (var center in centers)
            {
                if (patchCount >= s.PatchCount)
                    break;
                float radius = Mathf.Lerp(s.Radius.x, s.Radius.y, (float)rng.NextDouble());
                if (patches.Any(p => math.distance((float2)p.Cell, center) < p.Radius + radius + 2))
                    continue;
                int dominant = PickTree(s.Trees, rng);
                var main = s.Trees[dominant].Definition;
                if (!space.TryPlace(main, 1, center, s, rng, out var first))
                    continue;
                first.Patch = patchCount;
                plan.Items.Add(first);
                int total = 1, majority = 1;
                patches.Add((center, radius));
                var cells = space.Cells.Where(c => math.distance((float2)c, center) <= radius && space.Elevation(c) == space.Elevation(center)).ToList();
                Shuffle(cells, rng);
                float phase = (float)rng.NextDouble() * Mathf.PI * 2;
                foreach (var cell in cells)
                {
                    var offset = (float2)(cell - center);
                    float distance = math.length(offset) / radius;
                    // Irregular perimeter and reduced density toward the edge create separate groves.
                    float edge = .88f + .12f * Mathf.Sin(Mathf.Atan2(offset.y, offset.x) * 3 + phase);
                    if (distance > edge || rng.NextDouble() > s.Density * (1 - .65f * distance * distance))
                        continue;
                    int choice = s.Trees.Count == 1 || rng.NextDouble() < s.DominantRatio ? dominant : PickTree(s.Trees, rng, dominant);
                    if (!space.TryPlace(s.Trees[choice].Definition, 1, cell, s, rng, out var item))
                        continue;
                    item.Patch = patchCount;
                    plan.Items.Add(item);
                    total++;
                    if (choice == dominant)
                        majority++;
                }

                plan.Reports.Add($"树林 {++patchCount}：{total} 棵，主树种 {main.Metadata.Name} {majority} 棵");
            }

            var singles = new List<int2>();
            int before = plan.Items.Count;
            Shuffle(centers, rng);
            foreach (var cell in centers)
            {
                if (singles.Count >= s.IsolatedCount)
                    break;
                if (patches.Any(p => math.distance((float2)cell, p.Cell) <= p.Radius + s.IsolatedSpacing) || singles.Any(p => math.distance((float2)cell, p) < s.IsolatedSpacing))
                    continue;
                if (!space.TryPlace(s.Trees[PickTree(s.Trees, rng)].Definition, 1, cell, s, rng, out var item))
                    continue;
                plan.Items.Add(item);
                singles.Add(cell);
            }

            plan.Reports.Add($"树林片数：{patchCount}/{s.PatchCount}；零散树：{plan.Items.Count - before}/{s.IsolatedCount}；总计 {plan.Items.Count} 棵。未达数量表示合法空间不足。");
        }

        static int PickTree(List<MapTreeChoice> trees, Random rng, int exclude = -1)
        {
            double total = trees.Where((t, i) => i != exclude).Sum(t => (double)t.Weight), value = rng.NextDouble() * total;
            for (int i = 0; i < trees.Count; i++)
                if (i != exclude && (value -= trees[i].Weight) < 0)
                    return i;
            throw new InvalidOperationException("没有可用树种。");
        }

        static void Shuffle<T>(List<T> values, Random rng)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        sealed class PlacementSpace : IDisposable
        {
            readonly World world;
            readonly BlobAssetReference<GridBlob> grid;
            readonly BlobAssetReference<BuildingCatalogBlob> catalog;
            readonly BuildingCatalogAsset source;
            readonly Entity root;
            readonly GridData data;
            ulong owner;
            public readonly List<int2> Cells = new List<int2>();
            public PlacementSpace(MapContentAuthoring content, MapAsset map, MapGenerationKind replace)
            {
                source = content.Buildings ?? throw new InvalidOperationException("请指定游戏内容目录。");
                try
                {
                    grid = GameWorldMapAuthoring.BuildGrid(map);
                    catalog = BuildingCatalogBaking.Compile(MapWorldComposition.Content(content));
                    world = new World("Map population planning");
                    var em = world.EntityManager;
                    root = em.CreateEntity();
                    data = new GridData
                    {
                        Value = grid,
                        Origin = map.Origin,
                        CellSize = map.CellSize
                    };
                    em.AddComponentData(root, data);
                    em.AddComponentData(root, new BuildingCatalog { Value = catalog });
                    em.AddBuffer<Occupancy>(root).Resize(map.Cells.Length, NativeArrayOptions.ClearMemory);
                    for (int z = 0; z < map.Size.y; z++)
                        for (int x = 0; x < map.Size.x; x++)
                        {
                            var c = map.Cells[z * map.Size.x + x];
                            if (c.Exists && !c.EdgeZone)
                                Cells.Add(new int2(map.Min.x + x, map.Min.y + z));
                        }

                    var layout = new GridLayoutService(content.UnityGrid);
                    foreach (var t in BuildingRoots(content))
                    {
                        var p = t.GetComponent<InitialBuildingPreview>();
                        if (p != null && p.GenerationKind == replace)
                            continue;
                        var definition = p != null ? p.Definition : t.GetComponent<BuildingVisualAuthoring>()?.Definition;
                        if (definition == null)
                            throw new InvalidOperationException("已有建筑缺少定义：" + t.name);
                        var rotation = BuildingOrientationUtility.FromWorldRotation(t.rotation, layout.PlaneMode);
                        var size = rotation.GetEffectiveSize(definition.Footprint);
                        var point = layout.WorldToGridPoint(t.position);
                        Reserve(new int2(Mathf.RoundToInt(point.x - size.x * .5f), Mathf.RoundToInt(point.y - size.y * .5f)), new int2(size.x, size.y));
                    }
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Validate(BuildingDefinitionAsset definition, int level)
            {
                if (definition == null || definition.Prefab == null || !PrefabUtility.IsPartOfPrefabAsset(definition.Prefab) || !source.Definitions.Contains(definition) || level < 1 || level > definition.MaximumLevel)
                    throw new InvalidOperationException("列表需要内容目录中已注册、带预制体的建筑定义及有效等级。");
                if (definition.Capabilities.Connection.Enabled)
                    throw new InvalidOperationException("随机生成暂不支持桥梁等连接建筑，请手工放置：" + definition.name);
            }

            public int Elevation(int2 cell) => grid.Value.Cells[GridOps.Index(data, cell)].Elevation;
            public bool TryPlace(BuildingDefinitionAsset definition, int level, int2 cell, MapPopulationSettings settings, Random rng, out Placement item)
            {
                item = null;
                if (settings.Layer >= 0 && Elevation(cell) != settings.Layer)
                    return false;
                var index = new BuildingCatalogIndex(source).Resolve(definition);
                bool rotate = settings.RandomRotation && definition.PlacementAndVisuals.CanRotate;
                int start = rotate ? rng.Next(4) : 0;
                for (int i = 0; i < (rotate ? 4 : 1); i++)
                {
                    int rotation = (start + i) % 4;
                    if (!GridOps.CanPlace(world.EntityManager, root, index, cell, rotation))
                        continue;
                    var size = catalog.Value.Definitions[index.Index].Footprint;
                    if ((rotation & 1) != 0)
                        size = size.yx;
                    item = new Placement
                    {
                        Definition = definition,
                        Level = level,
                        Cell = cell,
                        Size = size,
                        Rotation = rotation,
                        Position = GridOps.Position(data, cell, size)
                    };
                    Reserve(cell, size);
                    return true;
                }

                return false;
            }

            void Reserve(int2 cell, int2 size)
            {
                var occupied = world.EntityManager.GetBuffer<Occupancy>(root);
                owner++;
                for (int y = 0; y < size.y; y++)
                    for (int x = 0; x < size.x; x++)
                    {
                        int index = GridOps.Index(data, cell + new int2(x, y));
                        if (index >= 0)
                            occupied[index] = new Occupancy
                            {
                                Owner = owner
                            };
                    }
            }

            public void Dispose()
            {
                world?.Dispose();
                if (catalog.IsCreated)
                    catalog.Dispose();
                if (grid.IsCreated)
                    grid.Dispose();
            }
        }

        public static string FillDefaults(MapContentAuthoring content)
        {
            RequireEditable(content);
            Undo.RecordObject(content, "填充地图生成列表");
            var settings = content.PopulationTools ??= new MapPopulationSettings();
            if (content.Buildings == null)
                throw new InvalidOperationException("请先指定游戏内容目录。");
            if (settings.Trees.Count == 0)
                foreach (var d in content.Buildings.Definitions.Where(d => d != null && d.Metadata.Id.StartsWith("b树木", StringComparison.Ordinal)).OrderBy(d => d.Metadata.Id, StringComparer.Ordinal))
                    settings.Trees.Add(new MapTreeChoice { Definition = d });
            if (settings.Buildings.Count == 0)
                foreach (var id in new[]
                {
                    "b小石堆",
                    "b小土堆"
                }

                )
                {
                    var definition = Array.Find(content.Buildings.Definitions, asset => asset.Metadata.Id == id);
                    if (definition != null)
                        settings.Buildings.Add(new MapBuildingChoice { Definition = definition });
                }

            EditorUtility.SetDirty(content);
            return $"已配置 {settings.Trees.Count} 种树、{settings.Buildings.Count} 种随机建筑。已有非空列表保持原样。";
        }
    }
}
