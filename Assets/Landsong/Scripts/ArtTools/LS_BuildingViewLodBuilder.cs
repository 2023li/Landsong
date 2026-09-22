#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Builds renderer hierarchies for the three authored LOD levels.
    internal static class LS_BuildingViewLodBuilder
    {
        internal const string GeneratedRootName = "_LS_LODs_";
        private sealed class MaterialGroup
        {
            public Material Material;
            public readonly List<LS_BuildingViewMeshContribution> Contributions = new List<LS_BuildingViewMeshContribution>();
        }

        internal static Renderer[][] BuildLevels(Transform exportRoot, Transform sourceRoot, IReadOnlyList<MeshRenderer> sourceRenderers, string meshFolder, LS_BuildingViewOptimizer optimizer, LS_BuildingViewExportStats stats, ICollection<Mesh> createdMeshes, IReadOnlyCollection<Renderer> dynamicRenderers)
        {
            var generatedRoot = new GameObject(GeneratedRootName);
            generatedRoot.transform.SetParent(exportRoot, false);
            return new[]
            {
                BuildLod0(sourceRenderers, sourceRoot, generatedRoot.transform, meshFolder, optimizer, stats, createdMeshes, dynamicRenderers),
                BuildCombinedLod(1, optimizer.LOD1Quality, sourceRenderers, sourceRoot, generatedRoot.transform, meshFolder, optimizer, stats, createdMeshes, dynamicRenderers),
                BuildCombinedLod(2, optimizer.LOD2Quality, sourceRenderers, sourceRoot, generatedRoot.transform, meshFolder, optimizer, stats, createdMeshes, dynamicRenderers)
            };
        }

        internal static void AssignLodGroup(GameObject exportRoot, Renderer[][] lodRenderers, LS_BuildingViewOptimizer optimizer)
        {
            var lodGroup = exportRoot.GetComponent<LODGroup>();
            if (lodGroup == null)
            {
                lodGroup = exportRoot.AddComponent<LODGroup>();
            }

            lodGroup.fadeMode = LODFadeMode.None;
            lodGroup.animateCrossFading = false;
            lodGroup.SetLODs(new[] { new LOD(optimizer.LOD0TransitionHeight, lodRenderers[0]), new LOD(optimizer.LOD1TransitionHeight, lodRenderers[1]), new LOD(optimizer.LOD2TransitionHeight, lodRenderers[2]) });
            lodGroup.RecalculateBounds();
        }

        internal static Renderer[] BuildLod0(IReadOnlyList<MeshRenderer> sources, Transform sourceRoot, Transform generatedRoot, string meshFolder, LS_BuildingViewOptimizer optimizer, LS_BuildingViewExportStats stats, ICollection<Mesh> createdMeshes, IReadOnlyCollection<Renderer> dynamicRenderers)
        {
            var levelRoot = CreateLevelRoot(generatedRoot, 0);
            var result = new List<Renderer>();
            for (var index = 0; index < sources.Count; index++)
            {
                var sourceRenderer = sources[index];
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                var accumulator = new LS_BuildingViewMeshAccumulator(sourceRoot);
                for (var subMesh = 0; subMesh < sourceFilter.sharedMesh.subMeshCount; subMesh++)
                {
                    accumulator.Append(new LS_BuildingViewMeshContribution(sourceFilter, sourceRenderer, subMesh), stats);
                }

                if (accumulator.TriangleCount == 0)
                {
                    continue;
                }

                var meshName = $"LOD0_{index:000}_{LS_BuildingViewAssetStore.SanitizeFileName(sourceRenderer.name)}";
                var mesh = accumulator.ToMesh(meshName);
                createdMeshes.Add(mesh);
                mesh = LS_BuildingViewAssetStore.SaveMeshAsset(mesh, $"{meshFolder}/{meshName}.asset");
                var renderer = CreateMeshRenderer(levelRoot, meshName, mesh, NormalizeMaterials(sourceRenderer.sharedMaterials, mesh.subMeshCount), optimizer);
                result.Add(renderer);
                stats.Triangles[0] += LS_BuildingViewMeshSimplifier.GetTriangleCount(mesh);
            }

            result.AddRange(dynamicRenderers);
            stats.Renderers[0] = result.Count;
            return result.ToArray();
        }

        internal static Renderer[] BuildCombinedLod(int level, float quality, IReadOnlyList<MeshRenderer> sources, Transform sourceRoot, Transform generatedRoot, string meshFolder, LS_BuildingViewOptimizer optimizer, LS_BuildingViewExportStats stats, ICollection<Mesh> createdMeshes, IReadOnlyCollection<Renderer> dynamicRenderers)
        {
            var groups = GroupByMaterial(sources, stats);
            var levelRoot = CreateLevelRoot(generatedRoot, level);
            var result = new List<Renderer>();
            for (var groupIndex = 0; groupIndex < groups.Count; groupIndex++)
            {
                var group = groups[groupIndex];
                var accumulator = new LS_BuildingViewMeshAccumulator(sourceRoot);
                foreach (var contribution in group.Contributions)
                {
                    accumulator.Append(contribution, stats);
                }

                if (accumulator.TriangleCount == 0)
                {
                    continue;
                }

                var materialName = group.Material == null ? "MissingMaterial" : group.Material.name;
                var meshName = $"LOD{level}_{groupIndex:000}_{LS_BuildingViewAssetStore.SanitizeFileName(materialName)}";
                var combined = accumulator.ToMesh(meshName + "_Combined");
                var simplified = LS_BuildingViewMeshSimplifier.SimplifyMesh(combined, quality, optimizer, meshName, level, stats);
                Object.DestroyImmediate(combined);
                createdMeshes.Add(simplified);
                simplified = LS_BuildingViewAssetStore.SaveMeshAsset(simplified, $"{meshFolder}/{meshName}.asset");
                var renderer = CreateMeshRenderer(levelRoot, meshName, simplified, new[] { group.Material }, optimizer);
                result.Add(renderer);
                stats.Triangles[level] += LS_BuildingViewMeshSimplifier.GetTriangleCount(simplified);
            }

            result.AddRange(dynamicRenderers);
            stats.Renderers[level] = result.Count;
            return result.ToArray();
        }

        private static List<MaterialGroup> GroupByMaterial(IEnumerable<MeshRenderer> renderers, LS_BuildingViewExportStats stats)
        {
            var result = new List<MaterialGroup>();
            foreach (var renderer in renderers)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                var mesh = filter.sharedMesh;
                var materials = renderer.sharedMaterials;
                for (var subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    var material = materials.Length == 0 ? null : materials[Mathf.Min(subMesh, materials.Length - 1)];
                    var group = result.FirstOrDefault(candidate => candidate.Material == material);
                    if (group == null)
                    {
                        group = new MaterialGroup
                        {
                            Material = material
                        };
                        result.Add(group);
                    }

                    group.Contributions.Add(new LS_BuildingViewMeshContribution(filter, renderer, subMesh));
                    if (material == null)
                    {
                        stats.Warnings.Add($"材质为空：{LS_BuildingViewHierarchy.GetTransformPath(renderer.transform)} / submesh {subMesh}");
                    }
                }
            }

            return result;
        }

        internal static MeshRenderer CreateMeshRenderer(Transform parent, string name, Mesh mesh, Material[] materials, LS_BuildingViewOptimizer optimizer)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            var filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = optimizer.CastShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = optimizer.ReceiveShadows;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            return renderer;
        }

        internal static Transform CreateLevelRoot(Transform generatedRoot, int level)
        {
            var levelRoot = new GameObject($"LOD{level}");
            levelRoot.transform.SetParent(generatedRoot, false);
            return levelRoot.transform;
        }

        internal static Material[] NormalizeMaterials(Material[] source, int subMeshCount)
        {
            var result = new Material[subMeshCount];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = source != null && source.Length > 0 ? source[Mathf.Min(i, source.Length - 1)] : null;
            }

            return result;
        }
    }
}
#endif
