using System;
using System.Collections.Generic;
using System.Globalization;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    internal static class TileWorldCreatorMapAssetStore
    {
        internal static bool TryWriteBakedMap(TileWorldCreatorMapBakeProfile profile, Configuration configuration, IReadOnlyList<GridMapCellRecord> cells, out GridMapDefinition output, out string error)
        {
            output = profile.OutputMap;
            error = string.Empty;
            if (output == null)
            {
                error = "烘焙配置没有绑定场景独立的 GridMapDefinition 输出。";
                return false;
            }

            var configurationPath = AssetDatabase.GetAssetPath(configuration);
            var sourceGuid = AssetDatabase.AssetPathToGUID(configurationPath);
            var sourceHash = TileWorldCreatorSourceFingerprint.ComputeSourceHash(configuration, cells, profile);
            output.ReplaceBakedData(Vector2Int.zero, new Vector2Int(configuration.width, configuration.height), configuration.cellSize, profile.ElevationWorldStep, cells, sourceGuid, configuration.name, sourceHash, DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture));
            return output.TryValidate(out error);
        }

        internal static void Save(TileWorldCreatorMapBakeProfile profile, GridMapDefinition output)
        {
            EditorUtility.SetDirty(output);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        internal static bool TryFindOrCreateSceneProfile(TileWorldCreatorManager manager, out TileWorldCreatorMapBakeProfile profile, out string error)
        {
            profile = null;
            error = string.Empty;
            try
            {
                var content = manager.GetComponent<MapContentAuthoring>();
                if (content == null)
                    content = Undo.AddComponent<MapContentAuthoring>(manager.gameObject);
                GameMapWorkflow.Initialize(content);
                if (!content.UsesLegacyTerrainInput && content.TerrainRules == null)
                    throw new InvalidOperationException("请选择地形映射规则。");
                var scenePath = manager.gameObject.scene.path;
                var profilePath = GameMapPaths.Output(scenePath, "_烘焙配置.asset");
                profile = content.BakeProfile;
                if (profile == null)
                    profile = AssetDatabase.LoadAssetAtPath<TileWorldCreatorMapBakeProfile>(profilePath);
                if (profile == null)
                {
                    profile = ScriptableObject.CreateInstance<TileWorldCreatorMapBakeProfile>();
                    AssetDatabase.CreateAsset(profile, profilePath);
                }

                var mapPath = GameMapPaths.Output(scenePath, "_逻辑网格.asset");
                var map = content.MapDefinition;
                if (map == null)
                    map = AssetDatabase.LoadAssetAtPath<GridMapDefinition>(mapPath);
                if (map == null)
                {
                    map = ScriptableObject.CreateInstance<GridMapDefinition>();
                    AssetDatabase.CreateAsset(map, mapPath);
                }

                profile.ApplyContent(content);
                profile.AssignSourceConfiguration(manager.configuration);
                profile.AssignSourceScene(AssetDatabase.AssetPathToGUID(scenePath), scenePath);
                profile.AssignOutputMap(map);
                content.BakeProfile = profile;
                EditorUtility.SetDirty(profile);
                EditorUtility.SetDirty(content);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }
    }
}
