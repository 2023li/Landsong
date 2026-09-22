#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Landsong.VisualSystem
{
    // Collects export measurements and formats the artist-facing report.
    internal static class LS_BuildingViewExportReport
    {
        internal static LS_BuildingViewExportStats CollectSourceStats(IEnumerable<MeshRenderer> renderers)
        {
            var stats = new LS_BuildingViewExportStats();
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                stats.SourceRenderers++;
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                    {
                        stats.SourceTriangles += (int)mesh.GetIndexCount(subMesh) / 3;
                    }
                }
            }

            return stats;
        }

        internal static void CollectDirectExportStats(Transform exportRoot, IEnumerable<LS_BuildingViewDynamicParts.Pair> dynamicParts, LS_BuildingViewExportStats stats)
        {
            var renderers = LS_BuildingViewHierarchy.CollectOriginalMeshRenderers(exportRoot);
            stats.Renderers[0] = renderers.Count;
            foreach (var renderer in renderers)
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                    {
                        stats.Triangles[0] += (int)mesh.GetIndexCount(subMesh) / 3;
                    }
                }
            }

            stats.DynamicRenderers = dynamicParts.SelectMany(pair => pair.CloneRoot.GetComponentsInChildren<Renderer>(true)).Where(renderer => renderer != null).Distinct().Count();
        }

        internal static string BuildReport(string prefabPath, LS_BuildingViewNamingConfig.ResolvedView resolvedView, LS_BuildingViewExportStats stats, LS_BuildingViewOptimizer optimizer)
        {
            var lines = new List<string>
            {
                $"Prefab：{prefabPath}",
                $"固定命名映射：{resolvedView.Entry.NormalizedAuthoringPrefix} → {resolvedView.Entry.NormalizedPrefabPrefix}",
                $"View 用途：{(resolvedView.Purpose == LS_BuildingViewPurpose.Construction ? "建造阶段" : $"运营 LV{resolvedView.Level}")}",
                resolvedView.Entry.BindLevelOneAsPlacementPreview ? "已同步 ECS 建筑对应等级/施工表现槽位；未修改玩法参数或根 Prefab 引用。" : "已导出独立视觉资产；未同步 ECS 建筑槽位。",
                $"源模型：{stats.SourceRenderers} 个 Renderer，{stats.SourceTriangles:N0} 个三角形"};
            if (optimizer.EnableLodOptimization)
            {
                lines.Add("导出模式：LOD 优化");
                lines.Add($"LOD0：{stats.Renderers[0]} 个 Renderer，{stats.Triangles[0]:N0} 个静态三角形 @ {optimizer.LOD0TransitionHeight:0.###}");
                lines.Add($"LOD1：{stats.Renderers[1]} 个 Renderer，{stats.Triangles[1]:N0} 个静态三角形 @ {optimizer.LOD1TransitionHeight:0.###}");
                lines.Add($"LOD2：{stats.Renderers[2]} 个 Renderer，{stats.Triangles[2]:N0} 个静态三角形 @ {optimizer.LOD2TransitionHeight:0.###}");
                lines.Add($"动态 Renderer：{stats.DynamicRenderers}（在全部 LOD 保持独立）");
                lines.Add($"负缩放修正：{stats.MirroredRenderers} 次子网格烘焙");
            }
            else
            {
                lines.Add("导出模式：仅导出（不减面、不合并、不生成 LOD）");
                lines.Add($"导出模型：{stats.Renderers[0]} 个 Renderer，{stats.Triangles[0]:N0} 个三角形（保留原始 Mesh 引用）");
                lines.Add($"动态 Renderer：{stats.DynamicRenderers}");
            }

            if (stats.Warnings.Count > 0)
            {
                lines.Add("警告：");
                lines.AddRange(stats.Warnings.Distinct().Select(warning => "- " + warning));
            }

            return string.Join("\n", lines);
        }
    }
}
#endif
