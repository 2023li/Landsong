using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using GiantGrey.TileWorldCreator;
using GiantGrey.TileWorldCreator.Components;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class TileWorldCreatorMapBaker
    {
        private const string RuntimeGridObjectName = "__Landsong_RuntimeGrid";
        private const string SceneBakeFolderSuffix = "_烘焙数据";
        private const float PositionTolerance = 0.0001f;
        private const string ScenePathArgument = "-landsongScenePath";

        private sealed class MutableCell
        {
            public GridPosition Position;
            public bool Buildable;
            public bool Traversable;
            public int ElevationLevel;
            public int SurfaceLayer;
            public string PrimaryTerrainKey;
            public readonly HashSet<string> OverlayTerrainKeys =
                new HashSet<string>(StringComparer.Ordinal);
            public bool HasElevationOverride;
            public bool HasSurfaceLayerOverride;
        }

        [MenuItem("Landsong/地图/TWC/烘焙地图并吸附初始建筑")]
        private static void BakeSelectedMapContent()
        {
            var manager = ResolveSelectedManager(out var selectionError);
            if (manager == null)
            {
                EditorUtility.DisplayDialog(
                    "Landsong 地图烘焙",
                    selectionError,
                    "确定");
                return;
            }

            if (!BakeAndSnapMapContent(
                    manager,
                    out var map,
                    out var error))
            {
                Debug.LogError($"Landsong 地图烘焙与初始建筑吸附失败：{error}", manager);
                EditorUtility.DisplayDialog("地图烘焙与吸附未完成", error, "确定");
                return;
            }

            Selection.activeObject = map;
            EditorGUIUtility.PingObject(map);
            Debug.Log(
                $"地图烘焙与初始建筑吸附完成：{map.Cells.Count} 个 X/Z 逻辑格，Hash={map.SourceHash}。请保存当前场景。",
                map);
        }

        public static bool BakeAndSnapMapContent(
            MapContentAuthoring content,
            out GridMapDefinition bakedMap,
            out string error)
        {
            bakedMap = null;
            error = string.Empty;
            if (content == null)
            {
                error = "MapContentAuthoring 为空。";
                return false;
            }

            return BakeAndSnapMapContent(
                content.GetComponent<TileWorldCreatorManager>(),
                out bakedMap,
                out error);
        }

        private static bool BakeAndSnapMapContent(
            TileWorldCreatorManager manager,
            out GridMapDefinition bakedMap,
            out string error)
        {
            bakedMap = null;
            error = string.Empty;
            if (Application.isPlaying)
            {
                error = "运行时不允许烘焙或修改初始建筑预览。";
                return false;
            }

            if (manager == null || manager.configuration == null)
            {
                error = "MapContentAuthoring 必须与已配置的 TileWorldCreatorManager 位于同一个对象上。";
                return false;
            }

            if (!CreateOrReuseProfileAndBake(
                    manager,
                    out _,
                    out bakedMap,
                    out error))
            {
                return false;
            }

            var content = manager.GetComponent<MapContentAuthoring>();
            if (content == null)
            {
                error = "烘焙完成后没有生成 MapContentAuthoring。";
                return false;
            }

            if (!content.TrySnapInitialBuildingsToGrid(out var snapError))
            {
                error = $"逻辑地图已经烘焙，但初始建筑未吸附：{snapError}";
                return false;
            }

            EditorSceneManager.MarkSceneDirty(content.gameObject.scene);
            return true;
        }

        private static bool CreateOrReuseProfileAndBake(
            TileWorldCreatorManager manager,
            out TileWorldCreatorMapBakeProfile profile,
            out GridMapDefinition bakedMap,
            out string error)
        {
            profile = null;
            bakedMap = null;
            error = string.Empty;
            if (manager == null || manager.configuration == null)
            {
                error = "场景中没有有效的 TileWorldCreatorManager 或 TWC Configuration。";
                return false;
            }

            if (!TryFindOrCreateSceneProfile(manager, out profile, out error))
            {
                return false;
            }

            var configuration = manager.configuration;
            if (profile.RequireDeterministicGlobalSeed && !configuration.useGlobalRandomSeed)
            {
                configuration.useGlobalRandomSeed = true;
                if (configuration.globalRandomSeed == 0)
                {
                    configuration.globalRandomSeed = 1;
                }

                EditorUtility.SetDirty(configuration);
            }

            return GenerateAndBake(manager, profile, out bakedMap, out error);
        }

        /// <summary>
        /// CI/batch entry point. Pass -landsongScenePath Assets/Path/Map.unity.
        /// It creates or reuses the scene configuration's bake profile, enables
        /// deterministic generation, bakes gameplay data and saves the scene.
        /// </summary>
        public static void BakeSceneFromCommandLine()
        {
            var scenePath = GetCommandLineArgument(ScenePathArgument);
            if (string.IsNullOrWhiteSpace(scenePath)
                || !scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !File.Exists(Path.GetFullPath(scenePath)))
            {
                throw new InvalidOperationException(
                    $"Pass a valid Unity scene using {ScenePathArgument} Assets/Path/Map.unity.");
            }

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var managers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TileWorldCreatorManager>(true))
                .Where(manager => manager != null)
                .ToArray();
            if (managers.Length != 1 || managers[0].configuration == null)
            {
                throw new InvalidOperationException(
                    $"Scene '{scenePath}' must contain exactly one configured TileWorldCreatorManager.");
            }

            var manager = managers[0];
            if (!BakeAndSnapMapContent(manager, out var map, out var error))
            {
                throw new InvalidOperationException(error);
            }

            if (!EditorSceneManager.SaveScene(scene))
            {
                throw new InvalidOperationException($"Could not save baked scene '{scenePath}'.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                $"Landsong TWC bake complete: scene='{scenePath}', cells={map.Cells.Count}, hash={map.SourceHash}",
                map);
        }

        private static bool GenerateAndBake(
            TileWorldCreatorManager manager,
            TileWorldCreatorMapBakeProfile profile,
            out GridMapDefinition bakedMap,
            out string error)
        {
            bakedMap = null;
            error = string.Empty;
            if (manager == null || profile == null)
            {
                error = "TWC Manager 或场景烘焙配置为空。";
                return false;
            }

            var configuration = profile.SourceConfiguration as Configuration;
            if (configuration == null || manager.configuration != configuration)
            {
                error = "场景烘焙配置与当前 TileWorldCreatorManager 使用的 Configuration 不一致。";
                return false;
            }

            if (!Approximately(manager.transform.lossyScale, Vector3.one))
            {
                error = "TileWorldCreatorManager scale must be (1, 1, 1). Use Configuration Cell Size for grid scale.";
                return false;
            }

            if (profile.RequireDeterministicGlobalSeed && !configuration.useGlobalRandomSeed)
            {
                error = "Enable Use Global Random Seed on the TWC Configuration before baking.";
                return false;
            }

            if (!SyncLayerReferences(profile, configuration, out error))
            {
                return false;
            }

            if (profile.RegenerateTileWorldBeforeBake)
            {
                try
                {
                    manager.ExecuteBlueprintLayers();
                    manager.ExecuteBuildLayers(ExecutionMode.FromScratch);
                }
                catch (Exception exception)
                {
                    error = $"TWC regeneration failed before Landsong bake: {exception.Message}";
                    Debug.LogException(exception, manager);
                    return false;
                }
            }

            if (!TryBakeCells(profile, configuration, out var cells, out error))
            {
                return false;
            }

            var output = profile.OutputMap;
            if (output == null)
            {
                error = "烘焙配置没有绑定场景独立的 GridMapDefinition 输出。";
                return false;
            }

            var configurationPath = AssetDatabase.GetAssetPath(configuration);
            var sourceGuid = AssetDatabase.AssetPathToGUID(configurationPath);
            var sourceHash = ComputeSourceHash(configuration, cells, profile);
            output.ReplaceBakedData(
                Vector2Int.zero,
                new Vector2Int(configuration.width, configuration.height),
                configuration.cellSize,
                profile.ElevationWorldStep,
                cells,
                sourceGuid,
                configuration.name,
                sourceHash,
                DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));

            if (!output.TryValidate(out error))
            {
                return false;
            }

            SyncRuntimeGrid(manager, output);
            EditorUtility.SetDirty(output);
            EditorUtility.SetDirty(profile);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            AssetDatabase.SaveAssets();
            bakedMap = output;
            return true;
        }

        private static bool TryBakeCells(
            TileWorldCreatorMapBakeProfile profile,
            Configuration configuration,
            out List<GridMapCellRecord> records,
            out string error)
        {
            records = new List<GridMapCellRecord>();
            error = string.Empty;
            var baseLayer = ResolveLayer(
                configuration,
                profile.BaseLayerGuid,
                profile.BaseLayerName);
            if (baseLayer == null)
            {
                error = $"Required base layer '{profile.BaseLayerName}' was not found.";
                return false;
            }

            if (!TryConvertWorldHeightToElevationLevel(
                    baseLayer.defaultLayerHeight,
                    profile.ElevationWorldStep,
                    baseLayer.layerName,
                    out var baseElevationLevel,
                    out error))
            {
                return false;
            }

            var cells = new Dictionary<GridPosition, MutableCell>();
            foreach (var twcPosition in baseLayer.allPositions)
            {
                if (!TryConvertPosition(twcPosition, configuration, out var position, out error))
                {
                    return false;
                }

                if (cells.ContainsKey(position))
                {
                    error = $"Base layer contains duplicate logical cell {position}.";
                    return false;
                }

                cells.Add(position, new MutableCell
                {
                    Position = position,
                    Buildable = profile.BaseBuildable,
                    Traversable = profile.BaseTraversable,
                    ElevationLevel = baseElevationLevel,
                    PrimaryTerrainKey = profile.BaseTerrainKey
                });
            }

            if (cells.Count == 0)
            {
                error = "The TWC base layer is empty.";
                return false;
            }

            var usedLayerGuids = new HashSet<string>(StringComparer.Ordinal);
            for (var bindingIndex = 0; bindingIndex < profile.LayerBindings.Count; bindingIndex++)
            {
                var binding = profile.LayerBindings[bindingIndex];
                if (binding == null)
                {
                    error = $"Layer binding {bindingIndex} is null.";
                    return false;
                }

                var layer = ResolveLayer(configuration, binding.LayerGuid, binding.LayerName);
                if (layer == null)
                {
                    error = $"Bound TWC layer '{binding.LayerName}' was not found.";
                    return false;
                }

                if (!usedLayerGuids.Add(layer.guid))
                {
                    error = $"TWC layer '{layer.layerName}' is bound more than once.";
                    return false;
                }

                foreach (var twcPosition in layer.allPositions)
                {
                    if (!TryConvertPosition(twcPosition, configuration, out var position, out error))
                    {
                        return false;
                    }

                    if (!cells.TryGetValue(position, out var cell))
                    {
                        error = $"Layer '{layer.layerName}' contains {position}, but that cell is outside the base layer.";
                        return false;
                    }

                    if (!ApplyBinding(cell, binding, layer, profile.ElevationWorldStep, out error))
                    {
                        return false;
                    }
                }
            }

            records = cells.Values
                .OrderBy(cell => cell.Position.Z)
                .ThenBy(cell => cell.Position.X)
                .Select(cell => new GridMapCellRecord(
                    cell.Position,
                    cell.Buildable,
                    cell.Traversable,
                    cell.ElevationLevel,
                    cell.SurfaceLayer,
                    cell.PrimaryTerrainKey,
                    cell.OverlayTerrainKeys))
                .ToList();
            return true;
        }

        private static bool ApplyBinding(
            MutableCell cell,
            TileWorldCreatorLayerBinding binding,
            BlueprintLayer layer,
            float elevationWorldStep,
            out string error)
        {
            error = string.Empty;
            var layerName = layer.layerName;
            var terrainKey = binding.TerrainKey;
            if (!string.IsNullOrEmpty(terrainKey))
            {
                if (binding.ReplacePrimaryTerrain)
                {
                    cell.PrimaryTerrainKey = terrainKey;
                    cell.OverlayTerrainKeys.Remove(terrainKey);
                }
                else if (!string.Equals(cell.PrimaryTerrainKey, terrainKey, StringComparison.Ordinal))
                {
                    cell.OverlayTerrainKeys.Add(terrainKey);
                }
            }

            ApplyPermission(ref cell.Buildable, binding.Buildable);
            ApplyPermission(ref cell.Traversable, binding.Traversable);

            if (binding.UseLayerHeight)
            {
                if (!TryConvertWorldHeightToElevationLevel(
                        layer.defaultLayerHeight,
                        elevationWorldStep,
                        layerName,
                        out var elevationLevel,
                        out error))
                {
                    return false;
                }

                if (cell.HasElevationOverride && cell.ElevationLevel != elevationLevel)
                {
                    error = $"Cell {cell.Position} has conflicting elevation layers; latest is '{layerName}'.";
                    return false;
                }

                cell.ElevationLevel = elevationLevel;
                cell.HasElevationOverride = true;
            }

            if (binding.OverrideSurfaceLayer)
            {
                if (cell.HasSurfaceLayerOverride && cell.SurfaceLayer != binding.SurfaceLayer)
                {
                    error = $"Cell {cell.Position} has conflicting surface layers; latest is '{layerName}'.";
                    return false;
                }

                cell.SurfaceLayer = binding.SurfaceLayer;
                cell.HasSurfaceLayerOverride = true;
            }

            return true;
        }

        private static bool TryConvertWorldHeightToElevationLevel(
            float worldHeight,
            float elevationWorldStep,
            string layerName,
            out int elevationLevel,
            out string error)
        {
            elevationLevel = 0;
            error = string.Empty;
            if (float.IsNaN(worldHeight)
                || float.IsInfinity(worldHeight)
                || float.IsNaN(elevationWorldStep)
                || float.IsInfinity(elevationWorldStep)
                || elevationWorldStep <= 0f)
            {
                error = $"TWC layer '{layerName}' has an invalid height or the bake profile height step is invalid.";
                return false;
            }

            var ratio = worldHeight / elevationWorldStep;
            elevationLevel = Mathf.RoundToInt(ratio);
            var convertedHeight = elevationLevel * elevationWorldStep;
            var tolerance = Mathf.Max(0.00001f, elevationWorldStep * 0.0001f);
            if (Mathf.Abs(convertedHeight - worldHeight) <= tolerance)
            {
                return true;
            }

            error = $"TWC layer '{layerName}' height {worldHeight:0.####} is not a multiple of the configured height step {elevationWorldStep:0.####}.";
            return false;
        }

        private static void ApplyPermission(ref bool value, GridCellPermissionOverride permission)
        {
            switch (permission)
            {
                case GridCellPermissionOverride.Allow:
                    value = true;
                    break;
                case GridCellPermissionOverride.Deny:
                    value = false;
                    break;
            }
        }

        private static bool SyncLayerReferences(
            TileWorldCreatorMapBakeProfile profile,
            Configuration configuration,
            out string error)
        {
            error = string.Empty;
            var baseLayer = ResolveLayer(configuration, profile.BaseLayerGuid, profile.BaseLayerName);
            if (baseLayer == null)
            {
                error = $"Base layer '{profile.BaseLayerName}' was not found.";
                return false;
            }

            profile.SetResolvedBaseLayer(baseLayer.guid, baseLayer.layerName);
            for (var i = 0; i < profile.LayerBindings.Count; i++)
            {
                var binding = profile.LayerBindings[i];
                if (binding == null)
                {
                    error = $"Layer binding {i} is null.";
                    return false;
                }

                var layer = ResolveLayer(configuration, binding.LayerGuid, binding.LayerName);
                if (layer == null)
                {
                    error = $"Layer '{binding.LayerName}' was not found.";
                    return false;
                }

                binding.SetResolvedLayer(layer.guid, layer.layerName);
            }

            return true;
        }

        private static BlueprintLayer ResolveLayer(
            Configuration configuration,
            string layerGuid,
            string layerName)
        {
            if (configuration == null)
            {
                return null;
            }

            if (!string.IsNullOrWhiteSpace(layerGuid))
            {
                var byGuid = configuration.GetBlueprintLayerByGuid(layerGuid.Trim());
                if (byGuid != null)
                {
                    return byGuid;
                }
            }

            var normalizedName = string.IsNullOrWhiteSpace(layerName) ? string.Empty : layerName.Trim();
            BlueprintLayer match = null;
            for (var folderIndex = 0; folderIndex < configuration.blueprintLayerFolders.Count; folderIndex++)
            {
                var folder = configuration.blueprintLayerFolders[folderIndex];
                if (folder?.blueprintLayers == null)
                {
                    continue;
                }

                for (var layerIndex = 0; layerIndex < folder.blueprintLayers.Count; layerIndex++)
                {
                    var candidate = folder.blueprintLayers[layerIndex];
                    if (candidate == null
                        || !string.Equals(candidate.layerName, normalizedName, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (match != null)
                    {
                        return null;
                    }

                    match = candidate;
                }
            }

            return match;
        }

        private static bool TryConvertPosition(
            Vector2 twcPosition,
            Configuration configuration,
            out GridPosition position,
            out string error)
        {
            var x = Mathf.RoundToInt(twcPosition.x);
            var z = Mathf.RoundToInt(twcPosition.y);
            position = new GridPosition(x, z);
            error = string.Empty;
            if (Mathf.Abs(twcPosition.x - x) > PositionTolerance
                || Mathf.Abs(twcPosition.y - z) > PositionTolerance)
            {
                error = $"TWC position {twcPosition} is not an integer logical X/Z cell.";
                return false;
            }

            if (x < 0 || z < 0 || x >= configuration.width || z >= configuration.height)
            {
                error = $"TWC cell {position} is outside configuration bounds {configuration.width}x{configuration.height}.";
                return false;
            }

            return true;
        }

        private static void SyncRuntimeGrid(
            TileWorldCreatorManager manager,
            GridMapDefinition map)
        {
            var child = manager.transform.Find(RuntimeGridObjectName);
            GameObject gridObject;
            if (child == null)
            {
                gridObject = new GameObject(RuntimeGridObjectName);
                Undo.RegisterCreatedObjectUndo(gridObject, "Create Landsong Runtime Grid");
                gridObject.transform.SetParent(manager.transform, false);
            }
            else
            {
                gridObject = child.gameObject;
                Undo.RecordObject(gridObject.transform, "Sync Landsong Runtime Grid");
            }

            var grid = gridObject.GetComponent<UnityEngine.Grid>();
            if (grid == null)
            {
                grid = Undo.AddComponent<UnityEngine.Grid>(gridObject);
            }

            Undo.RecordObject(grid, "Sync Landsong Runtime Grid");
            var halfCell = map.CellSize * 0.5f;
            gridObject.transform.localPosition = new Vector3(-halfCell, 0f, -halfCell);
            gridObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            gridObject.transform.localScale = Vector3.one;
            grid.cellLayout = GridLayout.CellLayout.Rectangle;
            grid.cellSwizzle = GridLayout.CellSwizzle.XYZ;
            grid.cellGap = Vector3.zero;
            grid.cellSize = new Vector3(map.CellSize, map.CellSize, 1f);

            var content = manager.GetComponent<MapContentAuthoring>();
            if (content == null)
            {
                content = Undo.AddComponent<MapContentAuthoring>(manager.gameObject);
            }

            Undo.RecordObject(content, "Configure Landsong Map Content");
            var visualRoots = manager
                .GetComponentsInChildren<LayerIdentifier>(true)
                .Select(identifier => identifier == null ? null : identifier.gameObject)
                .Where(root => root != null)
                .Distinct()
                .ToArray();
            content.ConfigureFromTileWorldCreator(grid, map, visualRoots);
            EditorUtility.SetDirty(grid);
            EditorUtility.SetDirty(content);
        }

        private static bool TryFindOrCreateSceneProfile(
            TileWorldCreatorManager manager,
            out TileWorldCreatorMapBakeProfile profile,
            out string error)
        {
            profile = null;
            error = string.Empty;
            if (!TryGetSceneBakePaths(
                    manager,
                    out var scenePath,
                    out var sceneName,
                    out var bakeFolder,
                    out error))
            {
                return false;
            }

            if (!TryEnsureSceneOwnedConfiguration(
                    manager,
                    sceneName,
                    bakeFolder,
                    out var configuration,
                    out error))
            {
                return false;
            }

            var preferredProfilePath = $"{bakeFolder}/{sceneName}_烘焙配置.asset";
            var preferredMapPath = $"{bakeFolder}/{sceneName}_逻辑网格.asset";
            profile = AssetDatabase.LoadAssetAtPath<TileWorldCreatorMapBakeProfile>(preferredProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TileWorldCreatorMapBakeProfile>();
                profile.name = $"{sceneName}_烘焙配置";
                AssetDatabase.CreateAsset(profile, preferredProfilePath);
            }

            var map = AssetDatabase.LoadAssetAtPath<GridMapDefinition>(preferredMapPath);
            if (map == null)
            {
                map = ScriptableObject.CreateInstance<GridMapDefinition>();
                map.name = $"{sceneName}_逻辑网格";
                AssetDatabase.CreateAsset(map, preferredMapPath);
            }

            profile.AssignSourceConfiguration(configuration);
            profile.AssignSourceScene(AssetDatabase.AssetPathToGUID(scenePath), scenePath);
            profile.AssignOutputMap(map);
            EditorUtility.SetDirty(profile);
            EditorUtility.SetDirty(map);
            AssetDatabase.SaveAssets();
            return true;
        }

        private static bool TryGetSceneBakePaths(
            TileWorldCreatorManager manager,
            out string scenePath,
            out string sceneName,
            out string bakeFolder,
            out string error)
        {
            scenePath = string.Empty;
            sceneName = string.Empty;
            bakeFolder = string.Empty;
            error = string.Empty;
            if (manager == null || !manager.gameObject.scene.IsValid())
            {
                error = "TileWorldCreatorManager 不属于有效场景。";
                return false;
            }

            scenePath = manager.gameObject.scene.path?.Replace('\\', '/') ?? string.Empty;
            if (string.IsNullOrWhiteSpace(scenePath)
                || !scenePath.StartsWith("Assets/", StringComparison.Ordinal)
                || !scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                error = "请先保存地图场景，再创建该场景独立的烘焙数据。";
                return false;
            }

            sceneName = Path.GetFileNameWithoutExtension(scenePath);
            var sceneFolder = Path.GetDirectoryName(scenePath)?.Replace('\\', '/') ?? "Assets";
            bakeFolder = string.Equals(
                    Path.GetFileName(sceneFolder),
                    $"{sceneName}{SceneBakeFolderSuffix}",
                    StringComparison.Ordinal)
                ? sceneFolder
                : $"{sceneFolder}/{sceneName}{SceneBakeFolderSuffix}";
            EnsureAssetFolder(bakeFolder);
            return true;
        }

        private static bool TryEnsureSceneOwnedConfiguration(
            TileWorldCreatorManager manager,
            string sceneName,
            string bakeFolder,
            out Configuration configuration,
            out string error)
        {
            configuration = null;
            error = string.Empty;
            if (manager == null || manager.configuration == null)
            {
                error = "TileWorldCreatorManager 没有绑定 TWC Configuration。";
                return false;
            }

            var targetPath = $"{bakeFolder}/{sceneName}_TWC配置.asset";
            var currentPath = AssetDatabase.GetAssetPath(manager.configuration)?.Replace('\\', '/');
            if (string.Equals(currentPath, targetPath, StringComparison.Ordinal))
            {
                configuration = manager.configuration;
                return true;
            }

            configuration = AssetDatabase.LoadAssetAtPath<Configuration>(targetPath);
            if (configuration == null)
            {
                if (string.IsNullOrWhiteSpace(currentPath)
                    || !AssetDatabase.CopyAsset(currentPath, targetPath))
                {
                    error = $"无法把 TWC Configuration 复制到场景独立目录：{targetPath}";
                    return false;
                }

                AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceSynchronousImport);
                configuration = AssetDatabase.LoadAssetAtPath<Configuration>(targetPath);
            }

            if (configuration == null)
            {
                error = $"无法加载场景独立的 TWC Configuration：{targetPath}";
                return false;
            }

            configuration.name = $"{sceneName}_TWC配置";
            Undo.RecordObject(manager, "绑定场景独立 TWC 配置");
            manager.configuration = configuration;
            EditorUtility.SetDirty(configuration);
            EditorUtility.SetDirty(manager);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            return true;
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            var normalized = folderPath.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var segments = normalized.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }

                current = next;
            }
        }

        private static string GetCommandLineArgument(string argumentName)
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], argumentName, StringComparison.Ordinal))
                {
                    return arguments[i + 1].Replace('\\', '/');
                }
            }

            return string.Empty;
        }

        private static string ComputeSourceHash(
            Configuration configuration,
            IReadOnlyList<GridMapCellRecord> cells,
            TileWorldCreatorMapBakeProfile profile)
        {
            var builder = new StringBuilder();
            builder.Append(configuration.width).Append('|')
                .Append(configuration.height).Append('|')
                .Append(configuration.cellSize.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(configuration.globalRandomSeed).Append('|')
                .Append(profile.ElevationWorldStep.ToString("R", CultureInfo.InvariantCulture)).AppendLine();
            for (var i = 0; i < cells.Count; i++)
            {
                var cell = cells[i];
                builder.Append(cell.Position.X).Append(',')
                    .Append(cell.Position.Z).Append(',')
                    .Append(cell.Buildable ? '1' : '0').Append(',')
                    .Append(cell.Traversable ? '1' : '0').Append(',')
                    .Append(cell.ElevationLevel).Append(',')
                    .Append(cell.SurfaceLayer).Append(',')
                    .Append(cell.PrimaryTerrainKey);
                for (var keyIndex = 0; keyIndex < cell.OverlayTerrainKeys.Count; keyIndex++)
                {
                    builder.Append(',').Append(cell.OverlayTerrainKeys[keyIndex]);
                }

                builder.AppendLine();
            }

            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(builder.ToString());
                var hash = sha.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
            }
        }

        private static TileWorldCreatorManager ResolveSelectedManager(out string error)
        {
            error = string.Empty;
            if (Selection.activeGameObject != null
                && Selection.activeGameObject.TryGetComponent<TileWorldCreatorManager>(out var selectedManager)
                && selectedManager.configuration != null)
            {
                return selectedManager;
            }

            if (Selection.activeObject is Configuration configuration)
            {
                var matches = Resources.FindObjectsOfTypeAll<TileWorldCreatorManager>()
                    .Where(manager => manager != null
                                      && manager.gameObject.scene.IsValid()
                                      && manager.gameObject.scene.isLoaded
                                      && manager.configuration == configuration)
                    .ToArray();
                if (matches.Length == 1)
                {
                    return matches[0];
                }

                error = matches.Length == 0
                    ? "当前打开的场景中没有使用所选 TWC Configuration 的 TileWorldCreatorManager。"
                    : "多个已打开场景正在使用所选 TWC Configuration。请直接选择目标场景中的 TileWorldCreatorManager。";
                return null;
            }

            error = "请选择目标场景中带有 TileWorldCreatorManager 的对象，或选择当前只被一个已打开场景使用的 TWC Configuration。";
            return null;
        }

        private static bool Approximately(Vector3 left, Vector3 right)
        {
            return (left - right).sqrMagnitude <= 0.000001f;
        }
    }

}
