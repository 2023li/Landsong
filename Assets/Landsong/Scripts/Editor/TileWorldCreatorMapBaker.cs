using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.GridSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class TileWorldCreatorMapBaker
    {
        [MenuItem("Landsong/地图/烘焙当前地图")]
        private static void BakeSelectedMapContent()
        {
            var manager = ResolveSelectedManager(out var selectionError);
            if (manager == null)
            {
                EditorUtility.DisplayDialog("Landsong 地图烘焙", selectionError, "确定");
                return;
            }

            var content = manager.GetComponent<MapContentAuthoring>();
            if (content == null)
                content = Undo.AddComponent<MapContentAuthoring>(manager.gameObject);
            try
            {
                GameMapWorkflow.Bake(content);
                Debug.Log("地图烘焙完成。", content);
            }
            catch (Exception error)
            {
                Debug.LogException(error, content);
                EditorUtility.DisplayDialog("地图烘焙失败", error.Message, "确定");
            }
        }

        public static bool BakeAndSnapMapContent(MapContentAuthoring content, out GridMapDefinition bakedMap, out string error)
        {
            bakedMap = null;
            error = string.Empty;
            if (content == null)
            {
                error = "MapContentAuthoring 为空。";
                return false;
            }

            return BakeAndSnapMapContent(content.GetComponent<TileWorldCreatorManager>(), out bakedMap, out error);
        }

        internal static bool BakeAndSnapMapContent(TileWorldCreatorManager manager, out GridMapDefinition bakedMap, out string error)
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

            if (!CreateOrReuseProfileAndBake(manager, out _, out bakedMap, out error))
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

        private static bool CreateOrReuseProfileAndBake(TileWorldCreatorManager manager, out TileWorldCreatorMapBakeProfile profile, out GridMapDefinition bakedMap, out string error)
        {
            profile = null;
            bakedMap = null;
            error = string.Empty;
            if (manager == null || manager.configuration == null)
            {
                error = "场景中没有有效的 TileWorldCreatorManager 或 TWC Configuration。";
                return false;
            }

            if (!TileWorldCreatorMapAssetStore.TryFindOrCreateSceneProfile(manager, out profile, out error))
            {
                return false;
            }

            TileWorldCreatorWorldGenerator.EnsureDeterministicSeed(manager.configuration, profile);
            return GenerateAndBake(manager, profile, out bakedMap, out error);
        }

        private static bool GenerateAndBake(TileWorldCreatorManager manager, TileWorldCreatorMapBakeProfile profile, out GridMapDefinition bakedMap, out string error)
        {
            bakedMap = null;
            error = string.Empty;
            if (!TileWorldCreatorBakeValidation.TryValidate(manager, profile, out var configuration, out error) || !TileWorldCreatorLayerResolver.SyncLayerReferences(profile, configuration, out error) || !TileWorldCreatorWorldGenerator.TryGenerate(manager, profile, out error))
            {
                return false;
            }

            if (!TileWorldCreatorCellCompiler.TryBakeCells(profile, configuration, out var cells, out error))
            {
                return false;
            }

            if (!TileWorldCreatorMapAssetStore.TryWriteBakedMap(profile, configuration, cells, out var output, out error))
            {
                return false;
            }

            TileWorldCreatorGridInstaller.SyncRuntimeGrid(manager, output);
            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            TileWorldCreatorMapAssetStore.Save(profile, output);
            bakedMap = output;
            return true;
        }

        private static TileWorldCreatorManager ResolveSelectedManager(out string error)
        {
            error = string.Empty;
            if (Selection.activeGameObject != null && Selection.activeGameObject.TryGetComponent<TileWorldCreatorManager>(out var selectedManager) && selectedManager.configuration != null)
            {
                return selectedManager;
            }

            if (Selection.activeObject is Configuration configuration)
            {
                var matches = Resources.FindObjectsOfTypeAll<TileWorldCreatorManager>().Where(manager => manager != null && manager.gameObject.scene.IsValid() && manager.gameObject.scene.isLoaded && manager.configuration == configuration).ToArray();
                if (matches.Length == 1)
                {
                    return matches[0];
                }

                error = matches.Length == 0 ? "当前打开的场景中没有使用所选 TWC Configuration 的 TileWorldCreatorManager。" : "多个已打开场景正在使用所选 TWC Configuration。请直接选择目标场景中的 TileWorldCreatorManager。";
                return null;
            }

            error = "请选择目标场景中带有 TileWorldCreatorManager 的对象，或选择当前只被一个已打开场景使用的 TWC Configuration。";
            return null;
        }

        // Public use-case boundaries retained for existing authoring workflows and batch callers.
        public static GridMapDefinition CreateValidationGrid(MapContentAuthoring content) => TileWorldCreatorValidationGrid.CreateValidationGrid(content);
        public static void BakeSceneFromCommandLine() => TileWorldCreatorBakeCommandLine.BakeSceneFromCommandLine();
    }
}
