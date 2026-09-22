#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Landsong.VisualSystem
{
    internal readonly struct LS_BuildingViewMeshContribution
    {
        public LS_BuildingViewMeshContribution(MeshFilter filter, MeshRenderer renderer, int subMeshIndex)
        {
            Filter = filter;
            Renderer = renderer;
            SubMeshIndex = subMeshIndex;
        }

        public MeshFilter Filter { get; }
        public MeshRenderer Renderer { get; }
        public int SubMeshIndex { get; }
    }

    internal sealed class LS_BuildingViewMeshAccumulator
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
        public LS_BuildingViewMeshAccumulator(Transform exportRoot)
        {
            this.exportRoot = exportRoot;
            for (var channel = 0; channel < uvs.Length; channel++)
            {
                uvs[channel] = new List<Vector4>();
            }
        }

        public int TriangleCount => triangles.Count / 3;

        public void Append(LS_BuildingViewMeshContribution contribution, LS_BuildingViewExportStats stats)
        {
            var sourceMesh = contribution.Filter.sharedMesh;
            if (sourceMesh == null)
            {
                return;
            }

            if (sourceMesh.GetTopology(contribution.SubMeshIndex) != MeshTopology.Triangles)
            {
                stats.Warnings.Add($"跳过非三角形子网格：{LS_BuildingViewHierarchy.GetTransformPath(contribution.Renderer.transform)} / submesh {contribution.SubMeshIndex}");
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
                throw new InvalidOperationException($"无法读取网格 '{sourceMesh.name}'。请在模型 Import Settings 中开启 Read/Write Enabled。", exception);
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
                var index0 = GetOrAppendVertex(sourceIndices[triangle], sourceVertices, sourceNormals, sourceTangents, sourceColors, sourceUvs, matrix, normalMatrix, mirrored, remap);
                var index1 = GetOrAppendVertex(sourceIndices[triangle + 1], sourceVertices, sourceNormals, sourceTangents, sourceColors, sourceUvs, matrix, normalMatrix, mirrored, remap);
                var index2 = GetOrAppendVertex(sourceIndices[triangle + 2], sourceVertices, sourceNormals, sourceTangents, sourceColors, sourceUvs, matrix, normalMatrix, mirrored, remap);
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
            var result = new Mesh
            {
                name = meshName
            };
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

        private int GetOrAppendVertex(int sourceIndex, IReadOnlyList<Vector3> sourceVertices, IReadOnlyList<Vector3> sourceNormals, IReadOnlyList<Vector4> sourceTangents, IReadOnlyList<Color32> sourceColors, IReadOnlyList<Vector4>[] sourceUvs, Matrix4x4 matrix, Matrix4x4 normalMatrix, bool mirrored, IDictionary<int, int> remap)
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
                var transformed = matrix.MultiplyVector(new Vector3(sourceTangent.x, sourceTangent.y, sourceTangent.z)).normalized;
                tangents.Add(new Vector4(transformed.x, transformed.y, transformed.z, mirrored ? -sourceTangent.w : sourceTangent.w));
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
}
#endif
