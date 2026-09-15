using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.ECS.Authoring;
using Landsong.ECS.Editor;
using Landsong.ECS.Presentation;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Scenes;
using Object = UnityEngine.Object;

namespace Landsong.EditorTools
{
    public static class GameMapWorkflow
    {
        public const string Buildings = "初始建筑";
        public const string Spawns = "出生区域";
        public const string Overrides = "地图规则区域";
        public static void Initialize(MapContentAuthoring content)
        {
            RequireEditMode();
            var scene = content.gameObject.scene;
            var data = GameMapPaths.Data(scene.path);
            var owner = AssetDatabase.AssetPathToGUID(scene.path);
            if (All<MapContentAuthoring>(scene).Count() != 1) throw new InvalidOperationException("每张制图场景只能有一个 MapContentAuthoring。");
            Undo.RecordObject(content, "初始化地图");
            bool copiedScene = !string.IsNullOrEmpty(content.OwnerSceneGuid) && content.OwnerSceneGuid != owner;
            if (copiedScene)
            {
                content.MapId = "Map_" + owner;
                content.DisplayName = scene.name;
                content.TargetMap = null; content.EntityScene = null; content.BakeProfile = null;
                content.IncludeInMenu = false;
                content.ConfigureFromTileWorldCreator(content.UnityGrid, null, Array.Empty<GameObject>());
            }
            if (string.IsNullOrWhiteSpace(content.MapId)) content.MapId = scene.name;
            if (string.IsNullOrWhiteSpace(content.DisplayName)) content.DisplayName = scene.name;
            foreach (var guid in AssetDatabase.FindAssets("t:MapAsset", new[] { GameMapPaths.Root }))
            {
                var other = AssetDatabase.LoadAssetAtPath<MapAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (other != content.TargetMap && other.MapId == content.MapId && other.TwcSourceSceneGuid != owner)
                    throw new InvalidOperationException("地图 ID 已被其他地图使用：" + content.MapId);
            }
            GameMapPaths.Folder(data + "/Source"); GameMapPaths.Folder(data + "/Generated");
            if (content.TerrainRules == null) content.TerrainRules = AssetDatabase.LoadAssetAtPath<MapTerrainRules>(GameMapPaths.Rules);
            if (content.Catalog == null) content.Catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            var manager = content.GetComponent<TileWorldCreatorManager>();
            if (manager == null) manager = Undo.AddComponent<TileWorldCreatorManager>(content.gameObject);
            var configPath = GameMapPaths.Source(scene.path);
            var configuration = AssetDatabase.LoadAssetAtPath<Configuration>(configPath);
            if (configuration == null)
            {
                var source = manager.configuration;
                if (source == null) source = content.TwcConfiguration as Configuration;
                if (source == null) source = AssetDatabase.LoadAssetAtPath<Configuration>(GameMapPaths.Template);
                if (source == null) throw new InvalidOperationException("缺少公共 TWC 模板：" + GameMapPaths.Template);
                if (!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source), configPath)) throw new IOException("无法复制地图独立 TWC 配置。");
                AssetDatabase.ImportAsset(configPath, ImportAssetOptions.ForceSynchronousImport);
                configuration = AssetDatabase.LoadAssetAtPath<Configuration>(configPath);
            }
            Undo.RecordObject(manager, "绑定地图 TWC 配置"); manager.configuration = configuration;
            content.TwcConfiguration = configuration; content.OwnerSceneGuid = owner;
            var mapPath = GameMapPaths.Output(scene.path, "_Map.asset");
            if (content.TargetMap == null) content.TargetMap = AssetDatabase.LoadAssetAtPath<MapAsset>(mapPath);
            if (content.TargetMap == null) { content.TargetMap = ScriptableObject.CreateInstance<MapAsset>(); AssetDatabase.CreateAsset(content.TargetMap, mapPath); }
            if (!string.IsNullOrEmpty(content.TargetMap.TwcSourceSceneGuid) && content.TargetMap.TwcSourceSceneGuid != owner && !(copiedScene && AssetDatabase.GetAssetPath(content.TargetMap) == mapPath))
                throw new InvalidOperationException("目标运行地图属于其他源场景，请先解除错误引用。");
            content.TargetMap.MapId = content.MapId; content.TargetMap.TwcSourceSceneGuid = owner;
            var entityPath = GameMapPaths.Output(scene.path, "_Entities.unity");
            if (content.EntityScene == null) content.EntityScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(entityPath);
            if (content.EntityScene == null)
            {
                var active = SceneManager.GetActiveScene();
                var empty = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                try { if (!EditorSceneManager.SaveScene(empty, entityPath)) throw new IOException("无法创建实体子场景。"); }
                finally { EditorSceneManager.CloseScene(empty, true); if (active.IsValid()) SceneManager.SetActiveScene(active); }
                content.EntityScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(entityPath);
            }
            content.TargetMap.EntitySceneGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(content.EntityScene));
            Child(content, Buildings); Child(content, Spawns); Child(content, Overrides);
            var gridObject = Child(content, "__Landsong_RuntimeGrid");
            var grid = gridObject.GetComponent<Grid>();
            if (grid == null) grid = Undo.AddComponent<Grid>(gridObject);
            gridObject.transform.localPosition = new Vector3(-configuration.cellSize * .5f, 0, -configuration.cellSize * .5f);
            gridObject.transform.localRotation = Quaternion.Euler(90, 0, 0); gridObject.transform.localScale = Vector3.one;
            grid.cellSize = new Vector3(configuration.cellSize, configuration.cellSize, 1); grid.cellGap = Vector3.zero;
            var gridMap = content.MapDefinition;
            if (gridMap == null) gridMap = AssetDatabase.LoadAssetAtPath<GridMapDefinition>(GameMapPaths.Output(scene.path, "_逻辑网格.asset"));
            if (gridMap == null) { gridMap = ScriptableObject.CreateInstance<GridMapDefinition>(); AssetDatabase.CreateAsset(gridMap, GameMapPaths.Output(scene.path, "_逻辑网格.asset")); }
            var profilePath = GameMapPaths.Output(scene.path, "_烘焙配置.asset");
            if (content.BakeProfile == null) content.BakeProfile = AssetDatabase.LoadAssetAtPath<TileWorldCreatorMapBakeProfile>(profilePath);
            if (content.BakeProfile == null) { content.BakeProfile = ScriptableObject.CreateInstance<TileWorldCreatorMapBakeProfile>(); AssetDatabase.CreateAsset(content.BakeProfile, profilePath); }
            if (content.TerrainRules != null) content.BakeProfile.ApplyRules(content.TerrainRules);
            content.BakeProfile.AssignSourceConfiguration(configuration); content.BakeProfile.AssignSourceScene(owner, scene.path); content.BakeProfile.AssignOutputMap(gridMap);
            EditorUtility.SetDirty(content.BakeProfile);
            content.ConfigureFromTileWorldCreator(grid, gridMap, content.MapVisualRoots.ToArray());
            EditorUtility.SetDirty(manager); EditorUtility.SetDirty(content); EditorUtility.SetDirty(content.TargetMap);
            EditorSceneManager.MarkSceneDirty(scene); AssetDatabase.SaveAssets();
        }

        public static GameObject Child(MapContentAuthoring content, string name)
        {
            var child = content.transform.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name); Undo.RegisterCreatedObjectUndo(go, "创建地图对象结构");
            SceneManager.MoveGameObjectToScene(go, content.gameObject.scene); go.transform.SetParent(content.transform, false); return go;
        }

        public static void Bake(MapContentAuthoring content)
        {
            Initialize(content);
            RequireClosedTarget(content);
            // All runtime outputs are committed together only after source and candidate validation.
            if (!TileWorldCreatorMapBaker.BakeAndSnapMapContent(content, out _, out var error)) throw new InvalidOperationException(error);
            var candidate = CreateCandidate(content);
            try
            {
                ValidateCandidate(content, candidate);
                using (var transaction = new OutputTransaction(content))
                {
                    WriteEntityScene(content, candidate);
                    EditorUtility.CopySerialized(candidate, content.TargetMap);
                    EditorUtility.SetDirty(content.TargetMap); AssetDatabase.SaveAssets();
                    SyncMenu(content);
                    if (!EditorSceneManager.SaveScene(content.gameObject.scene)) throw new IOException("无法保存地图源场景。");
                    AssetDatabase.SaveAssets(); transaction.Commit();
                }
            }
            finally { Object.DestroyImmediate(candidate); }
        }

        public static void Validate(MapContentAuthoring content)
        {
            RequireEditMode();
            if (content.OwnerSceneGuid != AssetDatabase.AssetPathToGUID(content.gameObject.scene.path)) throw new InvalidOperationException("请先初始化当前地图。");
            var preview = TileWorldCreatorMapBaker.CreateValidationGrid(content);
            MapAsset candidate = null;
            try { candidate = CreateCandidate(content, preview); ValidateCandidate(content, candidate); }
            finally { if (candidate != null) Object.DestroyImmediate(candidate); Object.DestroyImmediate(preview); }
        }

        public static MapAsset CreateCandidate(MapContentAuthoring content, GridMapDefinition source = null)
        {
            if (source == null) source = content.MapDefinition;
            var terrain = EcsMapIncrementalImport.ReadTerrain(content, source);
            if (!content.TryCollectInitialBuildings(source, out var buildings, out var error)) throw new InvalidOperationException(error);
            var regions = content.GetComponentsInChildren<MapSpawnRegionAuthoring>(true).Select(r => r.ToSource()).ToArray();
            var overrides = content.GetComponentsInChildren<MapProjectileRegionAuthoring>(true);
            foreach (var r in overrides)
                if (r.Size.x <= 0 || r.Size.y <= 0 || !Finite(r.Size.x) || !Finite(r.Size.y) || r.transform.lossyScale.x <= 0 || r.transform.lossyScale.z <= 0 || Quaternion.Angle(r.transform.rotation, Quaternion.identity) > .01f)
                    throw new InvalidOperationException("弹体规则区域必须为正尺寸、无旋转的世界 XZ 区域：" + r.name);
            for (int z = 0; z < terrain.Size.y; z++) for (int x = 0; x < terrain.Size.x; x++)
            {
                var at = z * terrain.Size.x + x;
                var point = terrain.Origin + new Vector3((terrain.Min.x + x + .5f) * terrain.CellSize, 0, (terrain.Min.y + z + .5f) * terrain.CellSize);
                foreach (var r in overrides) if (r.Contains(point)) terrain.Cells[at].BlocksProjectile = r.BlocksProjectile;
            }
            var map = Object.Instantiate(content.TargetMap);
            map.name = content.TargetMap.name;
            map.MapId = content.MapId; map.Min = terrain.Min; map.Size = terrain.Size; map.Origin = terrain.Origin;
            map.CellSize = terrain.CellSize; map.Cells = terrain.Cells; map.InitialBuildings = buildings; map.SpawnRegions = regions; map.IsBaked = true;
            return map;
        }

        public static void ValidateCandidate(MapContentAuthoring content, MapAsset candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate.MapId)) throw new InvalidOperationException("地图 ID 不能为空。");
            if (content.BasePopulation < 0 || content.Seed == 0) throw new InvalidOperationException("基础人口不能为负，随机种子不能为零。");
            foreach (var r in content.GetComponentsInChildren<MapSpawnRegionAuthoring>(true))
                if (Quaternion.Angle(r.transform.rotation, Quaternion.identity) > .01f) throw new InvalidOperationException("出生区域使用世界轴向范围，请将旋转归零：" + r.name);
            foreach (var region in candidate.SpawnRegions)
                if (!new[] { 10, 20, 30, 40 }.Contains(region.Direction) || !Finite(region.Center) || !Finite(region.Size) || region.Size.x <= 0 || region.Size.y <= 0 || region.Size.z <= 0)
                    throw new InvalidOperationException("出生区域需填写合法方向（10/20/30/40）、坐标与正尺寸。");
            EcsMapIncrementalImport.ValidateInitialBuildings(candidate, new EcsMapIncrementalImport.TerrainSnapshot { Min = candidate.Min, Size = candidate.Size, Cells = candidate.Cells, Origin = candidate.Origin, CellSize = candidate.CellSize }, content.Catalog);
            using var grid = GameWorldAuthoring.BuildGrid(candidate);
            using var catalog = GameWorldAuthoring.BuildCatalog(content.Catalog);
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        static void RequireClosedTarget(MapContentAuthoring content)
        {
            var path = AssetDatabase.GetAssetPath(content.EntityScene);
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.path == path) throw new InvalidOperationException("请先关闭生成的实体子场景，再烘焙地图。");
                if (scene.path == EcsSceneFlow.Game && scene.isDirty) throw new InvalidOperationException("请先保存 Game 场景，以便同步地图引用。");
            }
        }
        static void WriteEntityScene(MapContentAuthoring content, MapAsset candidate)
        {
            var active = SceneManager.GetActiveScene();
            var stage = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                var root = new GameObject("GameWorld"); SceneManager.MoveGameObjectToScene(root, stage);
                var world = root.AddComponent<GameWorldAuthoring>();
                world.Catalog = content.Catalog; world.Map = content.TargetMap; world.DynastyName = content.DynastyName; world.BasePopulation = content.BasePopulation; world.Seed = content.Seed;
                foreach (var visual in content.MapVisualRoots) if (visual != null) EcsMapIncrementalImport.CloneStaticMeshes(visual, stage);
                if (!EditorSceneManager.SaveScene(stage, AssetDatabase.GetAssetPath(content.EntityScene))) throw new IOException("无法保存地图实体子场景。");
            }
            finally { EditorSceneManager.CloseScene(stage, true); if (active.IsValid()) SceneManager.SetActiveScene(active); }
        }

        public static void SyncMenu(MapContentAuthoring content)
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu);
            if (catalog == null) throw new InvalidOperationException("缺少地图菜单目录。");
            var entries = catalog.Maps.ToList(); var index = entries.FindIndex(e => e.Id == content.MapId);
            if (content.IncludeInMenu)
            {
                if (!content.TargetMap.IsBaked) throw new InvalidOperationException("未烘焙地图不能加入菜单。");
                var entry = new EcsMapMenuCatalog.Entry { Id = content.MapId, DisplayName = content.DisplayName, Description = content.Description, Thumbnail = content.Thumbnail };
                if (index >= 0) entries[index] = entry; else entries.Add(entry);
            }
            else if (index >= 0) entries.RemoveAt(index);
            catalog.Maps = entries.ToArray(); EditorUtility.SetDirty(catalog);
            SyncHost(catalog); AssetDatabase.SaveAssets();
        }

        public static void SyncHost(EcsMapMenuCatalog catalog)
        {
            var maps = GameMapPaths.BakedMapPaths().Select(p => AssetDatabase.LoadAssetAtPath<MapAsset>(p)).ToDictionary(m => m.MapId, StringComparer.Ordinal);
            var active = SceneManager.GetActiveScene(); var scene = SceneManager.GetSceneByPath(EcsSceneFlow.Game); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(EcsSceneFlow.Game, OpenSceneMode.Additive);
            else if (scene.isDirty) throw new InvalidOperationException("请先保存 Game 场景。");
            try
            {
                var host = All<EcsGameHost>(scene).Single(); var old = host.Maps ?? Array.Empty<SubScene>();
                var next = new List<SubScene>();
                foreach (var entry in catalog.Maps)
                {
                    if (!maps.TryGetValue(entry.Id, out var map)) throw new InvalidOperationException("菜单引用了未烘焙地图：" + entry.Id);
                    var asset = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid));
                    if (asset == null) throw new InvalidOperationException("地图缺少实体子场景：" + entry.Id);
                    var sub = old.FirstOrDefault(s => s != null && s.SceneAsset == asset);
                    if (sub == null) { var go = new GameObject(entry.Id); SceneManager.MoveGameObjectToScene(go, scene); go.transform.SetParent(host.transform, false); sub = go.AddComponent<SubScene>(); }
                    sub.AutoLoadScene = false; sub.SceneAsset = asset; EditorUtility.SetDirty(sub); next.Add(sub);
                }
                foreach (var sub in old) if (sub != null && !next.Contains(sub)) Object.DestroyImmediate(sub.gameObject);
                host.Catalog = catalog; host.Maps = next.ToArray(); EditorUtility.SetDirty(host);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("无法保存 Game 地图引用。");
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (active.IsValid()) SceneManager.SetActiveScene(active); }
        }

        public static IEnumerable<T> All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
        public static void RequireEditMode() { if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出 Play Mode。"); }

        // Restore files with their original GUIDs if publishing any runtime output fails.
        sealed class OutputTransaction : IDisposable
        {
            readonly Dictionary<string, byte[]> files = new Dictionary<string, byte[]>();
            readonly MapAsset map; readonly string mapJson; readonly EcsMapMenuCatalog menu; readonly string menuJson;
            bool committed;
            public OutputTransaction(MapContentAuthoring content)
            {
                map = content.TargetMap; mapJson = EditorJsonUtility.ToJson(map);
                menu = AssetDatabase.LoadAssetAtPath<EcsMapMenuCatalog>(GameMapPaths.Menu); menuJson = EditorJsonUtility.ToJson(menu);
                foreach (var path in new[] { AssetDatabase.GetAssetPath(map), AssetDatabase.GetAssetPath(content.EntityScene), GameMapPaths.Menu, EcsSceneFlow.Game })
                    files[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            public void Commit() => committed = true;
            public void Dispose()
            {
                if (committed) return;
                EditorJsonUtility.FromJsonOverwrite(mapJson, map); EditorJsonUtility.FromJsonOverwrite(menuJson, menu);
                foreach (var pair in files) if (pair.Value != null) { File.WriteAllBytes(pair.Key, pair.Value); AssetDatabase.ImportAsset(pair.Key, ImportAssetOptions.ForceUpdate); }
                var game = SceneManager.GetSceneByPath(EcsSceneFlow.Game);
                if (game.IsValid() && game.isLoaded) { EditorSceneManager.CloseScene(game, true); EditorSceneManager.OpenScene(EcsSceneFlow.Game, OpenSceneMode.Additive); }
            }
        }
    }
}
