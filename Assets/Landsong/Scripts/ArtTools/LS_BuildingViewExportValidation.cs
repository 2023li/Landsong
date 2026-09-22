#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityMeshSimplifier;

namespace Landsong.VisualSystem
{
    // Validates the authoring source and export configuration before any assets change.
    internal static class LS_BuildingViewExportValidation
    {
        internal static LS_BuildingViewNamingConfig.ResolvedView ResolveView(LS_BuildingViewOptimizer optimizer, LS_BuildingViewNamingConfig namingConfig)
        {
            if (!namingConfig.TryValidate(out var configError))
            {
                throw new InvalidOperationException($"建筑 View 命名配置无效：{configError}");
            }

            if (!namingConfig.TryResolveAuthoringName(optimizer.gameObject.name, out var resolvedView, out var resolveError))
            {
                throw new InvalidOperationException(resolveError);
            }

            if (resolvedView.Entry.Definition == null)
            {
                throw new InvalidOperationException($"固定命名映射“{resolvedView.Entry.NormalizedAuthoringPrefix}”未绑定有效 ECS 建筑定义。");
            }

            return resolvedView;
        }

        internal static LS_BuildingViewNamingConfig LoadNamingConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<LS_BuildingViewNamingConfig>(LS_BuildingViewNamingConfig.DefaultAssetPath);
            if (config == null)
            {
                throw new InvalidOperationException($"缺少固定命名配置：{LS_BuildingViewNamingConfig.DefaultAssetPath}");
            }

            return config;
        }

        internal static void ValidateSettings(LS_BuildingViewOptimizer optimizer, LS_BuildingViewNamingConfig namingConfig)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                throw new InvalidOperationException("请先退出 Play Mode 再导出建筑 View。");
            }

            if (!optimizer.gameObject.scene.IsValid())
            {
                throw new InvalidOperationException("导出组件必须挂在场景中的建筑制作根对象上。");
            }

            if (optimizer.EnableLodOptimization)
            {
                var lodGroups = optimizer.GetComponentsInChildren<LODGroup>(true);
                var isSupportedLegacyGeneration = lodGroups.Length > 0 && lodGroups.All(group => group.transform.Find(LODGenerator.LODParentGameObjectName) != null && LS_BuildingViewHierarchy.HasLegacyRendererBackup(group.gameObject));
                if ((lodGroups.Length > 0 && !isSupportedLegacyGeneration) || optimizer.transform.Find(LS_BuildingViewLodBuilder.GeneratedRootName) != null)
                {
                    throw new InvalidOperationException("源建筑包含无法识别的 LODGroup 或旧的 _LS_LODs_。请对未生成 LOD 的原始建筑执行一键优化。");
                }

                if (!(optimizer.LOD0TransitionHeight > optimizer.LOD1TransitionHeight && optimizer.LOD1TransitionHeight > optimizer.LOD2TransitionHeight))
                {
                    throw new InvalidOperationException("LOD 切换高度必须依次递减：LOD0 > LOD1 > LOD2。");
                }
            }

            var outputRoot = LS_BuildingViewAssetStore.NormalizeAssetPath(namingConfig.ViewOutputRoot);
            if (!outputRoot.StartsWith("Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("导出根目录必须位于 Unity 项目的 Assets 目录内。");
            }
        }
    }
}
#endif
