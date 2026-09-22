#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Coordinates validation, hierarchy preparation, LOD generation and asset publication for a building view.
    public static class LS_BuildingViewOptimizationPipeline
    {
        public static GameObject Export(LS_BuildingViewOptimizer optimizer, bool interactive)
        {
            if (optimizer == null)
            {
                return null;
            }

            GameObject temporaryRoot = null;
            var createdMeshes = new List<Mesh>();
            var isAssetEditing = false;
            try
            {
                var namingConfig = LS_BuildingViewExportValidation.LoadNamingConfig();
                var resolvedView = LS_BuildingViewExportValidation.ResolveView(optimizer, namingConfig);
                LS_BuildingViewExportValidation.ValidateSettings(optimizer, namingConfig);
                var output = LS_BuildingViewAssetStore.ResolveOutputPaths(namingConfig, resolvedView);
                LS_BuildingViewAssetStore.EnsureAssetFolder(output.BuildingFolder);
                if (optimizer.EnableLodOptimization)
                {
                    LS_BuildingViewAssetStore.EnsureAssetFolder(output.MeshRoot);
                }

                var sourceRoot = optimizer.transform;
                var dynamicSourceRoots = LS_BuildingViewDynamicParts.ValidateAndCollectDynamicRoots(optimizer, sourceRoot);
                var sourceRenderers = optimizer.EnableLodOptimization ? LS_BuildingViewHierarchy.CollectStaticRenderers(sourceRoot, dynamicSourceRoots) : LS_BuildingViewHierarchy.CollectOriginalMeshRenderers(sourceRoot);
                if (sourceRenderers.Count == 0)
                {
                    throw new InvalidOperationException("没有找到可导出的 MeshRenderer / MeshFilter。");
                }

                var stats = LS_BuildingViewExportReport.CollectSourceStats(sourceRenderers);
                temporaryRoot = Object.Instantiate(optimizer.gameObject);
                temporaryRoot.name = Path.GetFileNameWithoutExtension(output.PrefabPath);
                LS_BuildingViewHierarchy.ResetRootTransform(temporaryRoot.transform);
                temporaryRoot.SetActive(true);
                LS_BuildingViewHierarchy.UnpackAllPrefabInstances(temporaryRoot);
                var dynamicPairs = LS_BuildingViewDynamicParts.ResolveDynamicPairs(optimizer, sourceRoot, temporaryRoot.transform);
                if (optimizer.EnableLodOptimization)
                {
                    LS_BuildingViewHierarchy.StripLegacyGeneratedHierarchy(temporaryRoot.transform);
                }

                LS_BuildingViewHierarchy.StripExportOnlyComponents(temporaryRoot, optimizer.EnableLodOptimization);
                LS_BuildingViewHierarchy.StripPhysics(temporaryRoot);
                LS_BuildingViewDynamicParts.ConfigureDynamicParts(dynamicPairs, stats);
                if (optimizer.EnableLodOptimization)
                {
                    LS_BuildingViewHierarchy.StripStaticRenderers(temporaryRoot.transform, dynamicPairs.Select(pair => pair.CloneRoot).ToList());
                    LS_BuildingViewHierarchy.PruneEmptyBranches(temporaryRoot.transform, dynamicPairs.Select(pair => pair.CloneRoot).ToList());
                    var dynamicRenderers = LS_BuildingViewDynamicParts.CollectDynamicRenderers(dynamicPairs);
                    stats.DynamicRenderers = dynamicRenderers.Length;
                    AssetDatabase.StartAssetEditing();
                    isAssetEditing = true;
                    var lodRenderers = LS_BuildingViewLodBuilder.BuildLevels(temporaryRoot.transform, sourceRoot, sourceRenderers, output.MeshRoot, optimizer, stats, createdMeshes, dynamicRenderers);
                    AssetDatabase.StopAssetEditing();
                    isAssetEditing = false;
                    LS_BuildingViewLodBuilder.AssignLodGroup(temporaryRoot, lodRenderers, optimizer);
                }
                else
                {
                    LS_BuildingViewExportReport.CollectDirectExportStats(temporaryRoot.transform, dynamicPairs, stats);
                }

                foreach (var renderer in temporaryRoot.GetComponentsInChildren<MeshRenderer>(true))
                    (renderer.GetComponent<EntityVisualAuthoring>() ?? renderer.gameObject.AddComponent<EntityVisualAuthoring>()).Owner = temporaryRoot;
                var exportedPrefab = LS_BuildingViewAssetStore.SaveOrReplacePrefab(temporaryRoot, output.PrefabPath);
                if (exportedPrefab == null)
                {
                    throw new InvalidOperationException($"Prefab 保存失败：{output.PrefabPath}");
                }

                var runtimePrefab = exportedPrefab; // Keep every level/stage as a reusable art asset.
                var runtimePath = AssetDatabase.GetAssetPath(runtimePrefab);
                if (resolvedView.Entry.BindLevelOneAsPlacementPreview && resolvedView.Entry.Definition != null)
                {
                    LS_BuildingVisualSlotBinding.BindExport(resolvedView.Entry.Definition, exportedPrefab, resolvedView.Purpose == LS_BuildingViewPurpose.Operational ? BuildingVisualPurpose.Operational : BuildingVisualPurpose.Construction, resolvedView.Level);
                }

                optimizer.SetLastExportedPrefabPath(runtimePath);
                EditorUtility.SetDirty(optimizer);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = runtimePrefab;
                EditorGUIUtility.PingObject(runtimePrefab);
                var report = LS_BuildingViewExportReport.BuildReport(runtimePath, resolvedView, stats, optimizer);
                Debug.Log(report, runtimePrefab);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("建筑美术资源导出完成", report, "确定");
                }

                return runtimePrefab;
            }
            catch (Exception exception)
            {
                foreach (var mesh in createdMeshes)
                {
                    if (mesh != null && !AssetDatabase.Contains(mesh))
                    {
                        Object.DestroyImmediate(mesh);
                    }
                }

                Debug.LogException(exception, optimizer);
                if (interactive)
                {
                    EditorUtility.DisplayDialog("建筑 View 导出失败", exception.Message, "确定");
                }

                return null;
            }
            finally
            {
                if (isAssetEditing)
                {
                    AssetDatabase.StopAssetEditing();
                }

                if (temporaryRoot != null)
                {
                    Object.DestroyImmediate(temporaryRoot);
                }
            }
        }

        [MenuItem("Tools/Landsong/建筑/优化并导出选中建筑 ECS 视觉")]
        private static void ExportSelected()
        {
            var selected = Selection.activeGameObject;
            var optimizer = selected == null ? null : selected.GetComponentInParent<LS_BuildingViewOptimizer>();
            if (optimizer == null)
            {
                EditorUtility.DisplayDialog("未找到建筑优化组件", "请选择挂有 LS_BuildingViewOptimizer 的场景建筑对象。", "确定");
                return;
            }

            Export(optimizer, true);
        }

        [MenuItem("Tools/Landsong/建筑/优化并导出选中建筑 ECS 视觉", true)]
        private static bool ValidateExportSelected()
        {
            return Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<LS_BuildingViewOptimizer>() != null;
        }
    }
}
#endif
