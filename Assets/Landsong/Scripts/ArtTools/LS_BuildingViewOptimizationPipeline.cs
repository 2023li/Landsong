#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityMeshSimplifier;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    /// <summary>
    /// 将场景中的建筑制作对象转换成纯表现 View Prefab。
    /// 这里不使用 UnityMeshSimplifier 的 MeshCombiner，因为它不会修正负缩放网格的三角面绕序。
    /// </summary>
    public static class LS_BuildingViewOptimizationPipeline
    {
        private const string GeneratedRootName = "_LS_LODs_";

        private sealed class ExportStats
        {
            public int SourceRenderers;
            public int SourceTriangles;
            public readonly int[] Renderers = new int[3];
            public readonly int[] Triangles = new int[3];
            public int DynamicRenderers;
            public int MirroredRenderers;
            public readonly List<string> Warnings = new List<string>();
        }

        private sealed class DynamicPartPair
        {
            public LS_BuildingViewOptimizer.DynamicPartSettings Settings;
            public Transform SourceRoot;
            public Transform CloneRoot;
        }

        private sealed class MaterialGroup
        {
            public Material Material;
            public readonly List<MeshContribution> Contributions = new List<MeshContribution>();
        }

        private readonly struct MeshContribution
        {
            public MeshContribution(MeshFilter filter, MeshRenderer renderer, int subMeshIndex)
            {
                Filter = filter;
                Renderer = renderer;
                SubMeshIndex = subMeshIndex;
            }

            public MeshFilter Filter { get; }
            public MeshRenderer Renderer { get; }
            public int SubMeshIndex { get; }
        }

        private sealed class MeshAccumulator
        {
            private readonly Transform exportRoot;
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector4> tangents = new List<Vector4>();
            private readonly List<Color32> colors = new List<Color32>();
            private readonly List<int> triangles = new List<int>();
            private readonly List<Vector4>[] uvs = new List<Vector4>[8];
            private bool hasMissingNormal;
            private bool hasAnyTangent;
            private bool hasMissingTangent;
            private bool hasAnyColor;
            private readonly bool[] hasAnyUv = new bool[8];

            public MeshAccumulator(Transform exportRoot)
            {
                this.exportRoot = exportRoot;
                for (var channel = 0; channel < uvs.Length; channel++)
                {
                    uvs[channel] = new List<Vector4>();
                }
            }

            public int TriangleCount => triangles.Count / 3;

            public void Append(MeshContribution contribution, ExportStats stats)
            {
                var sourceMesh = contribution.Filter.sharedMesh;
                if (sourceMesh == null)
                {
                    return;
                }

                if (sourceMesh.GetTopology(contribution.SubMeshIndex) != MeshTopology.Triangles)
                {
                    stats.Warnings.Add($"跳过非三角形子网格：{GetTransformPath(contribution.Renderer.transform)} / submesh {contribution.SubMeshIndex}");
                    return;
                }

                Vector3[] sourceVertices;
                int[] sourceIndices;
                try
                {
                    sourceVertices = sourceMesh.vertices;
                    sourceIndices = sourceMesh.GetIndices(contribution.SubMeshIndex, false);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"无法读取网格 '{sourceMesh.name}'。请在模型 Import Settings 中开启 Read/Write Enabled。", exception);
                }

                var sourceNormals = sourceMesh.normals;
                var sourceTangents = sourceMesh.tangents;
                var sourceColors = sourceMesh.colors32;
                var sourceUvs = new List<Vector4>[8];
                for (var channel = 0; channel < sourceUvs.Length; channel++)
                {
                    sourceUvs[channel] = new List<Vector4>(sourceVertices.Length);
                    sourceMesh.GetUVs(channel, sourceUvs[channel]);
                }

                var matrix = exportRoot.worldToLocalMatrix * contribution.Filter.transform.localToWorldMatrix;
                var normalMatrix = matrix.inverse.transpose;
                var mirrored = matrix.determinant < 0f;
                if (mirrored)
                {
                    stats.MirroredRenderers++;
                }

                var remap = new Dictionary<int, int>();
                for (var triangle = 0; triangle + 2 < sourceIndices.Length; triangle += 3)
                {
                    var index0 = GetOrAppendVertex(sourceIndices[triangle], sourceVertices, sourceNormals,
                        sourceTangents, sourceColors, sourceUvs, matrix, normalMatrix, mirrored, remap);
                    var index1 = GetOrAppendVertex(sourceIndices[triangle + 1], sourceVertices, sourceNormals,
                        sourceTangents, sourceColors, sourceUvs, matrix, normalMatrix, mirrored, remap);
                    var index2 = GetOrAppendVertex(sourceIndices[triangle + 2], sourceVertices, sourceNormals,
                        sourceTangents, sourceColors, sourceUvs, matrix, normalMatrix, mirrored, remap);

                    triangles.Add(index0);
                    if (mirrored)
                    {
                        triangles.Add(index2);
                        triangles.Add(index1);
                    }
                    else
                    {
                        triangles.Add(index1);
                        triangles.Add(index2);
                    }
                }
            }

            public Mesh ToMesh(string meshName)
            {
                var result = new Mesh { name = meshName };
                if (vertices.Count > ushort.MaxValue)
                {
                    result.indexFormat = IndexFormat.UInt32;
                }

                result.SetVertices(vertices);
                result.SetTriangles(triangles, 0, true);

                if (hasMissingNormal)
                {
                    result.RecalculateNormals();
                }
                else
                {
                    result.SetNormals(normals);
                }

                if (hasAnyColor)
                {
                    result.SetColors(colors);
                }

                for (var channel = 0; channel < uvs.Length; channel++)
                {
                    if (hasAnyUv[channel])
                    {
                        result.SetUVs(channel, uvs[channel]);
                    }
                }

                if (hasAnyTangent && !hasMissingTangent)
                {
                    result.SetTangents(tangents);
                }
                else if (hasAnyUv[0])
                {
                    result.RecalculateTangents();
                }

                result.RecalculateBounds();
                return result;
            }

            private int GetOrAppendVertex(
                int sourceIndex,
                IReadOnlyList<Vector3> sourceVertices,
                IReadOnlyList<Vector3> sourceNormals,
                IReadOnlyList<Vector4> sourceTangents,
                IReadOnlyList<Color32> sourceColors,
                IReadOnlyList<Vector4>[] sourceUvs,
                Matrix4x4 matrix,
                Matrix4x4 normalMatrix,
                bool mirrored,
                IDictionary<int, int> remap)
            {
                if (remap.TryGetValue(sourceIndex, out var existing))
                {
                    return existing;
                }

                var resultIndex = vertices.Count;
                remap.Add(sourceIndex, resultIndex);
                vertices.Add(matrix.MultiplyPoint3x4(sourceVertices[sourceIndex]));

                if (sourceNormals != null && sourceNormals.Count == sourceVertices.Count)
                {
                    normals.Add(normalMatrix.MultiplyVector(sourceNormals[sourceIndex]).normalized);
                }
                else
                {
                    normals.Add(Vector3.up);
                    hasMissingNormal = true;
                }

                if (sourceTangents != null && sourceTangents.Count == sourceVertices.Count)
                {
                    var sourceTangent = sourceTangents[sourceIndex];
                    var transformed = matrix.MultiplyVector(
                        new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z)).normalized;
                    tangents.Add(new Vector4(
                        transformed.x,
                        transformed.y,
                        transformed.z,
                        mirrored ? -sourceTangent.w : sourceTangent.w));
                    hasAnyTangent = true;
                }
                else
                {
                    tangents.Add(new Vector4(1f, 0f, 0f, 1f));
                    hasMissingTangent = true;
                }

                if (sourceColors != null && sourceColors.Count == sourceVertices.Count)
                {
                    colors.Add(sourceColors[sourceIndex]);
                    hasAnyColor = true;
                }
                else
                {
                    colors.Add(new Color32(255, 255, 255, 255));
                }

                for (var channel = 0; channel < sourceUvs.Length; channel++)
                {
                    if (sourceUvs[channel] != null && sourceUvs[channel].Count == sourceVertices.Count)
                    {
                        uvs[channel].Add(sourceUvs[channel][sourceIndex]);
                        hasAnyUv[channel] = true;
                    }
                    else
                    {
                        uvs[channel].Add(Vector4.zero);
                    }
                }

                return resultIndex;
            }
        }

        [MenuItem("Tools/Landsong/建筑/优化并导出选中建筑 ECS 视觉")]
        private static void ExportSelected()
        {
            var selected = Selection.activeGameObject;
            var optimizer = selected == null
                ? null
                : selected.GetComponentInParent<LS_BuildingViewOptimizer>();
            if (optimizer == null)
            {
                EditorUtility.DisplayDialog(
                    "未找到建筑优化组件",
                    "请选择挂有 LS_BuildingViewOptimizer 的场景建筑对象。",
                    "确定");
                return;
            }

            Export(optimizer, true);
        }

        [MenuItem("Tools/Landsong/建筑/优化并导出选中建筑 ECS 视觉", true)]
        private static bool ValidateExportSelected()
        {
            return Selection.activeGameObject != null
                   && Selection.activeGameObject.GetComponentInParent<LS_BuildingViewOptimizer>() != null;
        }

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
                var namingConfig = LoadNamingConfig();
                if (!namingConfig.TryValidate(out var configError))
                {
                    throw new InvalidOperationException($"建筑 View 命名配置无效：{configError}");
                }

                if (!namingConfig.TryResolveAuthoringName(
                        optimizer.gameObject.name,
                        out var resolvedView,
                        out var resolveError))
                {
                    throw new InvalidOperationException(resolveError);
                }

                if (resolvedView.Entry.Definition == null || resolvedView.Entry.Definition.Data.Kind != Landsong.ECS.ContentKind.Building)
                {
                    throw new InvalidOperationException(
                        $"固定命名映射“{resolvedView.Entry.NormalizedAuthoringPrefix}”未绑定有效 ECS 建筑定义。");
                }

                ValidateSettings(optimizer, namingConfig);
                var output = ResolveOutputPaths(namingConfig, resolvedView);
                EnsureAssetFolder(output.BuildingFolder);
                if (optimizer.EnableLodOptimization)
                {
                    EnsureAssetFolder(output.MeshRoot);
                }

                var sourceRoot = optimizer.transform;
                var dynamicSourceRoots = ValidateAndCollectDynamicRoots(optimizer, sourceRoot);
                var sourceRenderers = optimizer.EnableLodOptimization
                    ? CollectStaticRenderers(sourceRoot, dynamicSourceRoots)
                    : CollectOriginalMeshRenderers(sourceRoot);
                if (sourceRenderers.Count == 0)
                {
                    throw new InvalidOperationException("没有找到可导出的 MeshRenderer / MeshFilter。");
                }

                var stats = CollectSourceStats(sourceRenderers);
                temporaryRoot = Object.Instantiate(optimizer.gameObject);
                temporaryRoot.name = Path.GetFileNameWithoutExtension(output.PrefabPath);
                ResetRootTransform(temporaryRoot.transform);
                temporaryRoot.SetActive(true);
                UnpackAllPrefabInstances(temporaryRoot);

                var dynamicPairs = ResolveDynamicPairs(optimizer, sourceRoot, temporaryRoot.transform);
                if (optimizer.EnableLodOptimization)
                {
                    StripLegacyGeneratedHierarchy(temporaryRoot.transform);
                }

                StripExportOnlyComponents(temporaryRoot, optimizer.EnableLodOptimization);
                StripPhysics(temporaryRoot);
                ConfigureDynamicParts(dynamicPairs, stats);
                if (optimizer.EnableLodOptimization)
                {
                    StripStaticRenderers(
                        temporaryRoot.transform,
                        dynamicPairs.Select(pair => pair.CloneRoot).ToList());
                    PruneEmptyBranches(
                        temporaryRoot.transform,
                        dynamicPairs.Select(pair => pair.CloneRoot).ToList());

                    var generatedRoot = new GameObject(GeneratedRootName);
                    generatedRoot.transform.SetParent(temporaryRoot.transform, false);
                    var dynamicRenderers = CollectDynamicRenderers(dynamicPairs);
                    stats.DynamicRenderers = dynamicRenderers.Length;

                    var meshFolder = output.MeshRoot;
                    var lodRenderers = new Renderer[3][];
                    AssetDatabase.StartAssetEditing();
                    isAssetEditing = true;
                    lodRenderers[0] = BuildLod0(
                        sourceRenderers,
                        sourceRoot,
                        generatedRoot.transform,
                        meshFolder,
                        optimizer,
                        stats,
                        createdMeshes,
                        dynamicRenderers);
                    lodRenderers[1] = BuildCombinedLod(
                        1,
                        optimizer.LOD1Quality,
                        sourceRenderers,
                        sourceRoot,
                        generatedRoot.transform,
                        meshFolder,
                        optimizer,
                        stats,
                        createdMeshes,
                        dynamicRenderers);
                    lodRenderers[2] = BuildCombinedLod(
                        2,
                        optimizer.LOD2Quality,
                        sourceRenderers,
                        sourceRoot,
                        generatedRoot.transform,
                        meshFolder,
                        optimizer,
                        stats,
                        createdMeshes,
                        dynamicRenderers);

                    AssetDatabase.StopAssetEditing();
                    isAssetEditing = false;

                    var lodGroup = temporaryRoot.GetComponent<LODGroup>();
                    if (lodGroup == null)
                    {
                        lodGroup = temporaryRoot.AddComponent<LODGroup>();
                    }

                    lodGroup.fadeMode = LODFadeMode.None;
                    lodGroup.animateCrossFading = false;
                    lodGroup.SetLODs(new[]
                    {
                        new LOD(optimizer.LOD0TransitionHeight, lodRenderers[0]),
                        new LOD(optimizer.LOD1TransitionHeight, lodRenderers[1]),
                        new LOD(optimizer.LOD2TransitionHeight, lodRenderers[2])
                    });
                    lodGroup.RecalculateBounds();
                }
                else
                {
                    CollectDirectExportStats(temporaryRoot.transform, dynamicPairs, stats);
                }

                foreach (var renderer in temporaryRoot.GetComponentsInChildren<MeshRenderer>(true))
                    (renderer.GetComponent<EntityVisualAuthoring>() ?? renderer.gameObject.AddComponent<EntityVisualAuthoring>()).Owner = temporaryRoot;
                var exportedPrefab = SaveOrReplacePrefab(temporaryRoot, output.PrefabPath);
                if (exportedPrefab == null)
                {
                    throw new InvalidOperationException($"Prefab 保存失败：{output.PrefabPath}");
                }

                var runtimePrefab = exportedPrefab; // Keep every level/stage as a reusable art asset.
                var runtimePath = AssetDatabase.GetAssetPath(runtimePrefab);
                if (resolvedView.Entry.BindLevelOneAsPlacementPreview && resolvedView.Entry.Definition != null)
                {
                    LS_BuildingVisualSlotBinding.BindExport(resolvedView.Entry.Definition, exportedPrefab,
                        resolvedView.Purpose == LS_BuildingViewPurpose.Operational ? BuildingVisualPurpose.Operational : BuildingVisualPurpose.Construction, resolvedView.Level);
                }

                optimizer.SetLastExportedPrefabPath(runtimePath);
                EditorUtility.SetDirty(optimizer);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Selection.activeObject = runtimePrefab;
                EditorGUIUtility.PingObject(runtimePrefab);

                var report = BuildReport(runtimePath, resolvedView, stats, optimizer);
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

        private static LS_BuildingViewNamingConfig LoadNamingConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<LS_BuildingViewNamingConfig>(
                LS_BuildingViewNamingConfig.DefaultAssetPath);
            if (config == null)
            {
                throw new InvalidOperationException(
                    $"缺少固定命名配置：{LS_BuildingViewNamingConfig.DefaultAssetPath}");
            }

            return config;
        }

        private static void ValidateSettings(
            LS_BuildingViewOptimizer optimizer,
            LS_BuildingViewNamingConfig namingConfig)
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
                var isSupportedLegacyGeneration = lodGroups.Length > 0
                                                  && lodGroups.All(group =>
                                                      group.transform.Find(LODGenerator.LODParentGameObjectName) != null
                                                      && HasLegacyRendererBackup(group.gameObject));
                if ((lodGroups.Length > 0 && !isSupportedLegacyGeneration)
                    || optimizer.transform.Find(GeneratedRootName) != null)
                {
                    throw new InvalidOperationException(
                        "源建筑包含无法识别的 LODGroup 或旧的 _LS_LODs_。请对未生成 LOD 的原始建筑执行一键优化。");
                }

                if (!(optimizer.LOD0TransitionHeight > optimizer.LOD1TransitionHeight
                      && optimizer.LOD1TransitionHeight > optimizer.LOD2TransitionHeight))
                {
                    throw new InvalidOperationException("LOD 切换高度必须依次递减：LOD0 > LOD1 > LOD2。");
                }
            }

            var outputRoot = NormalizeAssetPath(namingConfig.ViewOutputRoot);
            if (!outputRoot.StartsWith("Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("导出根目录必须位于 Unity 项目的 Assets 目录内。");
            }
        }

        private static (string BuildingFolder, string MeshRoot, string PrefabPath) ResolveOutputPaths(
            LS_BuildingViewNamingConfig namingConfig,
            LS_BuildingViewNamingConfig.ResolvedView resolvedView)
        {
            var buildingName = SanitizeFileName(resolvedView.BuildingFolderName);
            var prefabName = SanitizeFileName(resolvedView.PrefabBaseName);
            var buildingFolder =
                $"{NormalizeAssetPath(namingConfig.ViewOutputRoot).TrimEnd('/')}/{buildingName}";
            var meshRoot = $"{buildingFolder}/OptimizedMeshes/{prefabName}";
            return (buildingFolder, meshRoot, $"{buildingFolder}/{prefabName}.prefab");
        }

        private static List<Transform> ValidateAndCollectDynamicRoots(
            LS_BuildingViewOptimizer optimizer,
            Transform sourceRoot)
        {
            var result = new List<Transform>();
            foreach (var settings in optimizer.DynamicParts)
            {
                if (settings?.Root == null)
                {
                    continue;
                }

                if (settings.Root == sourceRoot || !settings.Root.IsChildOf(sourceRoot))
                {
                    throw new InvalidOperationException(
                        $"动态部件“{settings.Root.name}”必须是建筑根对象的子节点，不能是建筑根对象本身。");
                }

                if (result.Any(existing => settings.Root.IsChildOf(existing) || existing.IsChildOf(settings.Root)))
                {
                    throw new InvalidOperationException(
                        $"动态部件“{settings.Root.name}”与另一个动态部件存在父子嵌套，请只配置最外层旋转根节点。");
                }

                result.Add(settings.Root);
            }

            return result;
        }

        private static List<MeshRenderer> CollectStaticRenderers(
            Transform sourceRoot,
            IReadOnlyList<Transform> dynamicRoots)
        {
            var backedUpRenderers = GetLegacyOriginalRenderers(sourceRoot.gameObject);
            if (backedUpRenderers.Count > 0)
            {
                return backedUpRenderers
                    .OfType<MeshRenderer>()
                    .Where(renderer => renderer != null
                                       && renderer.GetComponent<MeshFilter>()?.sharedMesh != null
                                       && !IsUnderAny(renderer.transform, dynamicRoots))
                    .Distinct()
                    .ToList();
            }

            return sourceRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer.enabled
                                   && IsActiveWithinSource(renderer.transform, sourceRoot)
                                   && renderer.GetComponent<MeshFilter>()?.sharedMesh != null
                                   && !IsUnderAny(renderer.transform, dynamicRoots))
                .ToList();
        }

        private static List<MeshRenderer> CollectOriginalMeshRenderers(Transform sourceRoot)
        {
            return sourceRoot.GetComponentsInChildren<MeshRenderer>(true)
                .Where(renderer => renderer != null
                                   && renderer.GetComponent<MeshFilter>()?.sharedMesh != null)
                .ToList();
        }

        private static bool HasLegacyRendererBackup(GameObject root)
        {
            return GetLegacyOriginalRenderers(root).Count > 0;
        }

        private static List<Renderer> GetLegacyOriginalRenderers(GameObject root)
        {
            var result = new List<Renderer>();
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null
                    || !string.Equals(
                        component.GetType().FullName,
                        "UnityMeshSimplifier.LODBackupComponent",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var property = component.GetType().GetProperty(
                    "OriginalRenderers",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property?.GetValue(component) is Renderer[] renderers)
                {
                    result.AddRange(renderers.Where(renderer => renderer != null));
                }
            }

            return result;
        }

        private static ExportStats CollectSourceStats(IEnumerable<MeshRenderer> renderers)
        {
            var stats = new ExportStats();
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

        private static void CollectDirectExportStats(
            Transform exportRoot,
            IEnumerable<DynamicPartPair> dynamicParts,
            ExportStats stats)
        {
            var renderers = CollectOriginalMeshRenderers(exportRoot);
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

            stats.DynamicRenderers = dynamicParts
                .SelectMany(pair => pair.CloneRoot.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer != null)
                .Distinct()
                .Count();
        }

        private static List<DynamicPartPair> ResolveDynamicPairs(
            LS_BuildingViewOptimizer optimizer,
            Transform sourceRoot,
            Transform cloneRoot)
        {
            var result = new List<DynamicPartPair>();
            foreach (var settings in optimizer.DynamicParts)
            {
                if (settings?.Root == null)
                {
                    continue;
                }

                var cloneTransform = ResolveBySiblingPath(
                    cloneRoot,
                    GetSiblingPath(sourceRoot, settings.Root));
                if (cloneTransform == null)
                {
                    throw new InvalidOperationException($"无法在导出副本中找到动态部件“{settings.Root.name}”。");
                }

                result.Add(new DynamicPartPair
                {
                    Settings = settings,
                    SourceRoot = settings.Root,
                    CloneRoot = cloneTransform
                });
            }

            return result;
        }

        private static void UnpackAllPrefabInstances(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true)
                .OrderBy(transform => GetDepth(transform))
                .ToArray();
            foreach (var transform in transforms)
            {
                if (transform == null
                    || !PrefabUtility.IsAnyPrefabInstanceRoot(transform.gameObject))
                {
                    continue;
                }

                PrefabUtility.UnpackPrefabInstance(
                    transform.gameObject,
                    PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
            }
        }

        private static void StripExportOnlyComponents(GameObject root, bool removeLodGroups)
        {
            foreach (var nativeHelper in root.GetComponentsInChildren<LODGeneratorHelper>(true))
            {
                if (nativeHelper != null)
                {
                    Object.DestroyImmediate(nativeHelper);
                }
            }

            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform)
                {
                    continue;
                }

                var fullName = component.GetType().FullName;
                if (component is LS_BuildingViewOptimizer
                    || component is EntityVisualAuthoring
                    || (removeLodGroups && component is LODGroup)
                    || string.Equals(fullName, "UnityMeshSimplifier.LODBackupComponent", StringComparison.Ordinal))
                {
                    Object.DestroyImmediate(component);
                }
            }
        }

        private static void PruneEmptyBranches(
            Transform exportRoot,
            IReadOnlyList<Transform> protectedRoots)
        {
            var protectedTransforms = new HashSet<Transform>();
            foreach (var protectedRoot in protectedRoots)
            {
                var current = protectedRoot;
                while (current != null)
                {
                    protectedTransforms.Add(current);
                    if (current == exportRoot)
                    {
                        break;
                    }

                    current = current.parent;
                }
            }

            var transforms = exportRoot.GetComponentsInChildren<Transform>(true)
                .OrderByDescending(GetDepth)
                .ToArray();
            foreach (var transform in transforms)
            {
                if (transform == null
                    || transform == exportRoot
                    || protectedTransforms.Contains(transform)
                    || transform.childCount > 0
                    || transform.GetComponents<Component>().Any(component =>
                        component != null && !(component is Transform)))
                {
                    continue;
                }

                Object.DestroyImmediate(transform.gameObject);
            }
        }

        private static void StripLegacyGeneratedHierarchy(Transform cloneRoot)
        {
            var legacyRoots = cloneRoot.GetComponentsInChildren<Transform>(true)
                .Where(transform => transform != cloneRoot
                                    && transform.name == LODGenerator.LODParentGameObjectName)
                .OrderByDescending(GetDepth)
                .ToArray();
            foreach (var legacyRoot in legacyRoots)
            {
                if (legacyRoot != null)
                {
                    Object.DestroyImmediate(legacyRoot.gameObject);
                }
            }
        }

        private static void StripStaticRenderers(Transform cloneRoot, IReadOnlyList<Transform> dynamicRoots)
        {
            foreach (var renderer in cloneRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || IsUnderAny(renderer.transform, dynamicRoots))
                {
                    continue;
                }

                if (renderer is MeshRenderer)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    Object.DestroyImmediate(renderer);
                    if (filter != null)
                    {
                        Object.DestroyImmediate(filter);
                    }
                }
                else
                {
                    Debug.LogWarning(
                        $"[建筑一键优化] 静态层级中的 {renderer.GetType().Name} 不支持合并，已从导出 View 移除：{GetTransformPath(renderer.transform)}");
                    Object.DestroyImmediate(renderer);
                }
            }
        }

        private static void StripPhysics(GameObject root)
        {
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var collider in root.GetComponentsInChildren<Collider2D>(true))
            {
                Object.DestroyImmediate(collider);
            }

            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.DestroyImmediate(rigidbody);
            }

            foreach (var rigidbody in root.GetComponentsInChildren<Rigidbody2D>(true))
            {
                Object.DestroyImmediate(rigidbody);
            }
        }

        private static void ConfigureDynamicParts(IEnumerable<DynamicPartPair> dynamicParts, ExportStats stats)
        {
            foreach (var pair in dynamicParts)
            {
                if (pair.Settings.AddLocalAxisRotation)
                {
                    var rotator = pair.CloneRoot.GetComponent<LS_LocalAxisRotator>()
                                  ?? pair.CloneRoot.gameObject.AddComponent<LS_LocalAxisRotator>();
                    rotator.Configure(
                        pair.Settings.LocalAxis,
                        pair.Settings.DegreesPerSecond,
                        pair.Settings.UseUnscaledTime);
                }

                if (pair.CloneRoot.GetComponentsInChildren<Renderer>(true).Length == 0)
                {
                    stats.Warnings.Add($"动态部件没有 Renderer：{GetTransformPath(pair.SourceRoot)}");
                }
            }
        }

        private static Renderer[] CollectDynamicRenderers(IEnumerable<DynamicPartPair> dynamicParts)
        {
            var renderers = dynamicParts
                .SelectMany(pair => pair.CloneRoot.GetComponentsInChildren<Renderer>(true))
                .Where(renderer => renderer != null)
                .Distinct()
                .ToArray();
            foreach (var renderer in renderers)
            {
                renderer.enabled = true;
            }

            return renderers;
        }

        private static Renderer[] BuildLod0(
            IReadOnlyList<MeshRenderer> sources,
            Transform sourceRoot,
            Transform generatedRoot,
            string meshFolder,
            LS_BuildingViewOptimizer optimizer,
            ExportStats stats,
            ICollection<Mesh> createdMeshes,
            IReadOnlyCollection<Renderer> dynamicRenderers)
        {
            var levelRoot = CreateLevelRoot(generatedRoot, 0);
            var result = new List<Renderer>();
            for (var index = 0; index < sources.Count; index++)
            {
                var sourceRenderer = sources[index];
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                var accumulator = new MeshAccumulator(sourceRoot);
                for (var subMesh = 0; subMesh < sourceFilter.sharedMesh.subMeshCount; subMesh++)
                {
                    accumulator.Append(new MeshContribution(sourceFilter, sourceRenderer, subMesh), stats);
                }

                if (accumulator.TriangleCount == 0)
                {
                    continue;
                }

                var meshName = $"LOD0_{index:000}_{SanitizeFileName(sourceRenderer.name)}";
                var mesh = accumulator.ToMesh(meshName);
                createdMeshes.Add(mesh);
                mesh = SaveMeshAsset(mesh, $"{meshFolder}/{meshName}.asset");
                var renderer = CreateMeshRenderer(
                    levelRoot,
                    meshName,
                    mesh,
                    NormalizeMaterials(sourceRenderer.sharedMaterials, mesh.subMeshCount),
                    optimizer);
                result.Add(renderer);
                stats.Triangles[0] += GetTriangleCount(mesh);
            }

            result.AddRange(dynamicRenderers);
            stats.Renderers[0] = result.Count;
            return result.ToArray();
        }

        private static Renderer[] BuildCombinedLod(
            int level,
            float quality,
            IReadOnlyList<MeshRenderer> sources,
            Transform sourceRoot,
            Transform generatedRoot,
            string meshFolder,
            LS_BuildingViewOptimizer optimizer,
            ExportStats stats,
            ICollection<Mesh> createdMeshes,
            IReadOnlyCollection<Renderer> dynamicRenderers)
        {
            var groups = GroupByMaterial(sources, stats);
            var levelRoot = CreateLevelRoot(generatedRoot, level);
            var result = new List<Renderer>();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                var accumulator = new MeshAccumulator(sourceRoot);
                foreach (var contribution in group.Contributions)
                {
                    accumulator.Append(contribution, stats);
                }

                if (accumulator.TriangleCount == 0)
                {
                    continue;
                }

                var materialName = group.Material == null ? "MissingMaterial" : group.Material.name;
                var meshName = $"LOD{level}_{groupIndex:000}_{SanitizeFileName(materialName)}";
                var combined = accumulator.ToMesh(meshName + "_Combined");
                var simplified = SimplifyMesh(combined, quality, optimizer, meshName, level, stats);
                Object.DestroyImmediate(combined);
                createdMeshes.Add(simplified);
                simplified = SaveMeshAsset(simplified, $"{meshFolder}/{meshName}.asset");
                var renderer = CreateMeshRenderer(
                    levelRoot,
                    meshName,
                    simplified,
                    new[] { group.Material },
                    optimizer);
                result.Add(renderer);
                stats.Triangles[level] += GetTriangleCount(simplified);
            }

            result.AddRange(dynamicRenderers);
            stats.Renderers[level] = result.Count;
            return result.ToArray();
        }

        private static List<MaterialGroup> GroupByMaterial(
            IEnumerable<MeshRenderer> renderers,
            ExportStats stats)
        {
            var result = new List<MaterialGroup>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter.sharedMesh;
                var materials = renderer.sharedMaterials;
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    var material = materials.Length == 0
                        ? null
                        : materials[Mathf.Min(subMesh, materials.Length - 1)];
                    var group = result.FirstOrDefault(candidate => candidate.Material == material);
                    if (group == null)
                    {
                        group = new MaterialGroup { Material = material };
                        result.Add(group);
                    }

                    group.Contributions.Add(new MeshContribution(filter, renderer, subMesh));
                    if (material == null)
                    {
                        stats.Warnings.Add($"材质为空：{GetTransformPath(renderer.transform)} / submesh {subMesh}");
                    }
                }
            }

            return result;
        }

        private static Mesh SimplifyMesh(
            Mesh source,
            float quality,
            LS_BuildingViewOptimizer optimizer,
            string meshName,
            int level,
            ExportStats stats)
        {
            var attempts = new[]
            {
                CreateSimplificationOptions(
                    optimizer.PreserveBorderEdges,
                    optimizer.PreserveUVSeamEdges,
                    optimizer.PreserveUVFoldoverEdges,
                    optimizer.PreserveSurfaceCurvature),
                CreateSimplificationOptions(
                    true,
                    false,
                    false,
                    optimizer.PreserveSurfaceCurvature),
                CreateSimplificationOptions(true, false, false, false)
            };

            var sourceTriangles = GetTriangleCount(source);
            var acceptableTriangles = Mathf.CeilToInt(sourceTriangles * Mathf.Clamp01(quality) * 1.1f);
            Mesh best = null;
            var bestTriangles = int.MaxValue;
            var usedAttempt = 0;
            for (var attempt = 0; attempt < attempts.Length; attempt++)
            {
                if (attempt > 0 && SimplificationOptionsEqual(attempts[attempt], attempts[attempt - 1]))
                {
                    continue;
                }

                var candidate = SimplifyWithOptions(source, quality, attempts[attempt], meshName);
                var candidateTriangles = GetTriangleCount(candidate);
                if (candidateTriangles < bestTriangles)
                {
                    if (best != null)
                    {
                        Object.DestroyImmediate(best);
                    }

                    best = candidate;
                    bestTriangles = candidateTriangles;
                    usedAttempt = attempt;
                }
                else
                {
                    Object.DestroyImmediate(candidate);
                }

                if (bestTriangles <= acceptableTriangles)
                {
                    break;
                }
            }

            if (usedAttempt > 0)
            {
                stats.Warnings.Add(
                    $"LOD{level} 的材质组“{meshName}”因 UV 保护阻止简化，已自动放宽 UV 接缝/翻折保护；边界保护仍保留。");
            }

            return best;
        }

        private static Mesh SimplifyWithOptions(
            Mesh source,
            float quality,
            SimplificationOptions options,
            string meshName)
        {
            var simplifier = new MeshSimplifier { SimplificationOptions = options };
            simplifier.Initialize(source);
            simplifier.SimplifyMesh(Mathf.Clamp01(quality));
            var result = simplifier.ToMesh();
            result.name = meshName;
            result.RecalculateBounds();
            return result;
        }

        private static SimplificationOptions CreateSimplificationOptions(
            bool preserveBorder,
            bool preserveUvSeam,
            bool preserveUvFoldover,
            bool preserveCurvature)
        {
            return new SimplificationOptions
            {
                PreserveBorderEdges = preserveBorder,
                PreserveUVSeamEdges = preserveUvSeam,
                PreserveUVFoldoverEdges = preserveUvFoldover,
                PreserveSurfaceCurvature = preserveCurvature,
                EnableSmartLink = true,
                VertexLinkDistance = double.Epsilon,
                MaxIterationCount = 100,
                Agressiveness = 7d,
                ManualUVComponentCount = false,
                UVComponentCount = 2
            };
        }

        private static bool SimplificationOptionsEqual(
            SimplificationOptions left,
            SimplificationOptions right)
        {
            return left.PreserveBorderEdges == right.PreserveBorderEdges
                   && left.PreserveUVSeamEdges == right.PreserveUVSeamEdges
                   && left.PreserveUVFoldoverEdges == right.PreserveUVFoldoverEdges
                   && left.PreserveSurfaceCurvature == right.PreserveSurfaceCurvature;
        }

        private static MeshRenderer CreateMeshRenderer(
            Transform parent,
            string name,
            Mesh mesh,
            Material[] materials,
            LS_BuildingViewOptimizer optimizer)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = optimizer.CastShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            renderer.receiveShadows = optimizer.ReceiveShadows;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return renderer;
        }

        private static Transform CreateLevelRoot(Transform generatedRoot, int level)
        {
            var levelRoot = new GameObject($"LOD{level}");
            levelRoot.transform.SetParent(generatedRoot, false);
            return levelRoot.transform;
        }

        private static Mesh SaveMeshAsset(Mesh mesh, string assetPath)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }

            AssetDatabase.CreateAsset(mesh, assetPath);
            return mesh;
        }

        private static GameObject SaveOrReplacePrefab(GameObject root, string prefabPath)
        {
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out var success);
            return success ? AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) : null;
        }

        private static string BuildReport(
            string prefabPath,
            LS_BuildingViewNamingConfig.ResolvedView resolvedView,
            ExportStats stats,
            LS_BuildingViewOptimizer optimizer)
        {
            var lines = new List<string>
            {
                $"Prefab：{prefabPath}",
                $"固定命名映射：{resolvedView.Entry.NormalizedAuthoringPrefix} → {resolvedView.Entry.NormalizedPrefabPrefix}",
                $"View 用途：{(resolvedView.Purpose == LS_BuildingViewPurpose.Construction ? "建造阶段" : $"运营 LV{resolvedView.Level}")}",
                resolvedView.Entry.BindLevelOneAsPlacementPreview
                    ? "已同步 ECS 建筑对应等级/施工表现槽位；未修改玩法参数或根 Prefab 引用。"
                    : "已导出独立视觉资产；未同步 ECS 建筑槽位。",
                $"源模型：{stats.SourceRenderers} 个 Renderer，{stats.SourceTriangles:N0} 个三角形"
            };
            if (optimizer.EnableLodOptimization)
            {
                lines.Add("导出模式：LOD 优化");
                lines.Add(
                    $"LOD0：{stats.Renderers[0]} 个 Renderer，{stats.Triangles[0]:N0} 个静态三角形 @ {optimizer.LOD0TransitionHeight:0.###}");
                lines.Add(
                    $"LOD1：{stats.Renderers[1]} 个 Renderer，{stats.Triangles[1]:N0} 个静态三角形 @ {optimizer.LOD1TransitionHeight:0.###}");
                lines.Add(
                    $"LOD2：{stats.Renderers[2]} 个 Renderer，{stats.Triangles[2]:N0} 个静态三角形 @ {optimizer.LOD2TransitionHeight:0.###}");
                lines.Add($"动态 Renderer：{stats.DynamicRenderers}（在全部 LOD 保持独立）");
                lines.Add($"负缩放修正：{stats.MirroredRenderers} 次子网格烘焙");
            }
            else
            {
                lines.Add("导出模式：仅导出（不减面、不合并、不生成 LOD）");
                lines.Add(
                    $"导出模型：{stats.Renderers[0]} 个 Renderer，{stats.Triangles[0]:N0} 个三角形（保留原始 Mesh 引用）");
                lines.Add($"动态 Renderer：{stats.DynamicRenderers}");
            }

            if (stats.Warnings.Count > 0)
            {
                lines.Add("警告：");
                lines.AddRange(stats.Warnings.Distinct().Select(warning => "- " + warning));
            }

            return string.Join("\n", lines);
        }

        private static void ResetRootTransform(Transform root)
        {
            root.SetParent(null, false);
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one;
        }

        private static int[] GetSiblingPath(Transform root, Transform target)
        {
            var reversed = new List<int>();
            var current = target;
            while (current != null && current != root)
            {
                reversed.Add(current.GetSiblingIndex());
                current = current.parent;
            }

            if (current != root)
            {
                throw new InvalidOperationException($"{target.name} 不属于建筑根对象 {root.name}。");
            }

            reversed.Reverse();
            return reversed.ToArray();
        }

        private static Transform ResolveBySiblingPath(Transform root, IReadOnlyList<int> siblingPath)
        {
            var current = root;
            foreach (var siblingIndex in siblingPath)
            {
                if (siblingIndex < 0 || siblingIndex >= current.childCount)
                {
                    return null;
                }

                current = current.GetChild(siblingIndex);
            }

            return current;
        }

        private static bool IsUnderAny(Transform transform, IReadOnlyList<Transform> roots)
        {
            for (var i = 0; i < roots.Count; i++)
            {
                if (roots[i] != null && (transform == roots[i] || transform.IsChildOf(roots[i])))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsActiveWithinSource(Transform transform, Transform sourceRoot)
        {
            var current = transform;
            while (current != null && current != sourceRoot)
            {
                if (!current.gameObject.activeSelf)
                {
                    return false;
                }

                current = current.parent;
            }

            return current == sourceRoot;
        }

        private static int GetDepth(Transform transform)
        {
            var depth = 0;
            while (transform.parent != null)
            {
                depth++;
                transform = transform.parent;
            }

            return depth;
        }

        private static string GetTransformPath(Transform transform)
        {
            var names = new Stack<string>();
            var current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names);
        }

        private static Material[] NormalizeMaterials(Material[] source, int subMeshCount)
        {
            var result = new Material[subMeshCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = source != null && source.Length > 0
                    ? source[Mathf.Min(i, source.Length - 1)]
                    : null;
            }

            return result;
        }

        private static int GetTriangleCount(Mesh mesh)
        {
            var triangles = 0;
            for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
            {
                if (mesh.GetTopology(subMesh) == MeshTopology.Triangles)
                {
                    triangles += (int)mesh.GetIndexCount(subMesh) / 3;
                }
            }

            return triangles;
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException($"无效的资源目录：{folder}");
            }

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? string.Empty).Replace('\\', '/').Trim();
        }

        private static string SanitizeFileName(string value)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = (value ?? string.Empty)
                .Select(character => invalid.Contains(character) || character == '/' || character == '\\'
                    ? '_'
                    : character)
                .ToArray();
            var result = new string(chars).Trim().Trim('.');
            return string.IsNullOrWhiteSpace(result) ? "Building" : result;
        }
    }
}
#endif
