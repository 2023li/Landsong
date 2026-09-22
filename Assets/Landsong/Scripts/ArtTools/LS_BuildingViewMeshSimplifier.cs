#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityMeshSimplifier;
using Object = UnityEngine.Object;

namespace Landsong.VisualSystem
{
    // Applies mesh reduction attempts and chooses the best result while preserving the existing fallback policy.
    internal static class LS_BuildingViewMeshSimplifier
    {
        internal static Mesh SimplifyMesh(Mesh source, float quality, LS_BuildingViewOptimizer optimizer, string meshName, int level, LS_BuildingViewExportStats stats)
        {
            var attempts = new[]
            {
                CreateSimplificationOptions(optimizer.PreserveBorderEdges, optimizer.PreserveUVSeamEdges, optimizer.PreserveUVFoldoverEdges, optimizer.PreserveSurfaceCurvature),
                CreateSimplificationOptions(true, false, false, optimizer.PreserveSurfaceCurvature),
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
                stats.Warnings.Add($"LOD{level} 的材质组“{meshName}”因 UV 保护阻止简化，已自动放宽 UV 接缝/翻折保护；边界保护仍保留。");
            }

            return best;
        }

        internal static Mesh SimplifyWithOptions(Mesh source, float quality, SimplificationOptions options, string meshName)
        {
            var simplifier = new MeshSimplifier
            {
                SimplificationOptions = options
            };
            simplifier.Initialize(source);
            simplifier.SimplifyMesh(Mathf.Clamp01(quality));
            var result = simplifier.ToMesh();
            result.name = meshName;
            result.RecalculateBounds();
            return result;
        }

        internal static SimplificationOptions CreateSimplificationOptions(bool preserveBorder, bool preserveUvSeam, bool preserveUvFoldover, bool preserveCurvature)
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

        internal static bool SimplificationOptionsEqual(SimplificationOptions left, SimplificationOptions right)
        {
            return left.PreserveBorderEdges == right.PreserveBorderEdges && left.PreserveUVSeamEdges == right.PreserveUVSeamEdges && left.PreserveUVFoldoverEdges == right.PreserveUVFoldoverEdges && left.PreserveSurfaceCurvature == right.PreserveSurfaceCurvature;
        }

        internal static int GetTriangleCount(Mesh mesh)
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
    }
}
#endif
