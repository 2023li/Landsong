using Landsong.ECS.Definitions;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Landsong.ECS.Presentation
{
    /// <summary>
    /// Owns the persistent world-space geometry for one selected building's range.
    /// Geometry is rebuilt on selection/style changes and then rendered by MeshRenderer;
    /// no per-cell draw commands are submitted from UI update loops.
    /// </summary>
    internal sealed class BuildingRangeOverlayView
    {
        sealed class Geometry
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public void Add(float3 position, Vector3 size)
            {
                var center = (Vector3)position;
                center.y += .07f + size.y * .5f;
                float x = size.x * .5f;
                float z = size.z * .5f;
                int first = Vertices.Count;
                Vertices.Add(center + new Vector3(-x, 0, -z));
                Vertices.Add(center + new Vector3(-x, 0, z));
                Vertices.Add(center + new Vector3(x, 0, z));
                Vertices.Add(center + new Vector3(x, 0, -z));
                Normals.Add(Vector3.up);
                Normals.Add(Vector3.up);
                Normals.Add(Vector3.up);
                Normals.Add(Vector3.up);
                Triangles.Add(first);
                Triangles.Add(first + 1);
                Triangles.Add(first + 2);
                Triangles.Add(first);
                Triangles.Add(first + 2);
                Triangles.Add(first + 3);
            }
        }

        readonly List<Mesh> meshes = new List<Mesh>();
        GameObject root;

        public ulong SourceId { get; private set; }
        public bool HighContrast { get; private set; }

        public int Rebuild(EntityManager em, Entity sessionRoot, Entity building, Transform parent, Material material, bool highContrast)
        {
            if (building == Entity.Null || !em.Exists(building) || !em.HasComponent<Building>(building) || !em.HasComponent<Identity>(building))
            {
                Clear();
                return 0;
            }

            var placement = em.GetComponentData<BuildingPlacementState>(building);
            var definition = em.GetComponentData<BuildingDefinitionRef>(building).Definition;
            var level = em.GetComponentData<Building>(building).Level;
            using var distances = BuildingRangeOps.Reach(em, sessionRoot, building, Allocator.Temp);
            var provider = ResourceNetworkOps.Provider(em, sessionRoot, building, distances);
            return RebuildGeometry(em, sessionRoot, placement, definition, level, distances, provider,
                em.GetComponentData<Identity>(building).Id, parent, material, highContrast);
        }

        public int RebuildPreview(EntityManager em, Entity sessionRoot, BuildingId definition, BuildingPlacementState placement,
            Transform parent, Material material, bool highContrast)
        {
            using var distances = BuildingRangeOps.Reach(em, sessionRoot, placement,
                BuildingRangeOps.ActionPower(em, sessionRoot, definition), Allocator.Temp);
            var provider = ResourceNetworkOps.Provider(em, sessionRoot, distances);
            return RebuildGeometry(em, sessionRoot, placement, definition, 1, distances, provider,
                0, parent, material, highContrast);
        }

        int RebuildGeometry(EntityManager em, Entity sessionRoot, BuildingPlacementState placement, BuildingId definition, int level,
            NativeArray<float> distances, Entity provider, ulong sourceId, Transform parent, Material material, bool highContrast)
        {
            Clear();

            var geometry = new Dictionary<Color32, Geometry>();
            void Add(float3 position, Vector3 size, Color color)
            {
                if (highContrast)
                {
                    color.a = Mathf.Max(.8f, color.a);
                    size.x = Mathf.Max(.16f, size.x);
                    size.z = Mathf.Max(.16f, size.z);
                }

                var key = (Color32)color;
                if (!geometry.TryGetValue(key, out var group))
                {
                    group = new Geometry();
                    geometry.Add(key, group);
                }

                group.Add(position, size);
            }

            var grid = em.GetComponentData<GridData>(sessionRoot);
            for (var i = 0; i < distances.Length; i++)
                if (math.isfinite(distances[i]))
                {
                    var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                    Add(GridOps.Position(grid, cell, new int2(1)), new Vector3(grid.CellSize, .035f, grid.CellSize), new Color(.1f, .65f, 1, .22f));
                }

            ref var data = ref BuildingDefinitions.Get(em, sessionRoot, definition);
            for (var i = 0; i < data.Capabilities.Effects.Spatial.Length; i++)
            {
                var effect = data.Capabilities.Effects.Spatial[i];
                if (effect.Level != 0 && effect.Level != level)
                    continue;
                var radius = (int)math.ceil(effect.Radius);
                for (var y = -radius; y < placement.Size.y + radius; y++)
                    for (var x = -radius; x < placement.Size.x + radius; x++)
                    {
                        var gap = math.max(0, -x) + math.max(0, x - placement.Size.x + 1) + math.max(0, -y) + math.max(0, y - placement.Size.y + 1);
                        var cell = placement.Cell + new int2(x, y);
                        if (gap > effect.Radius || GridOps.Index(grid, cell) < 0)
                            continue;
                        Add(GridOps.Position(grid, cell, new int2(1)), new Vector3(grid.CellSize * .7f, .05f, grid.CellSize * .7f), new Color(.8f, .3f, 1, .35f));
                    }
            }

            var path = BuildingRangeOps.ProviderPath(em, sessionRoot, provider, distances);
            foreach (var cell in path)
                Add(GridOps.Position(grid, cell, new int2(1)), new Vector3(grid.CellSize * .28f, .085f, grid.CellSize * .28f), new Color(.15f, 1, .3f, .9f));
            if (provider != Entity.Null)
            {
                var providerPlacement = em.GetComponentData<BuildingPlacementState>(provider);
                Add(EntityState.Position(em, provider), new Vector3(providerPlacement.Size.x * grid.CellSize, .1f, providerPlacement.Size.y * grid.CellSize), new Color(.2f, 1, .3f, .65f));
            }

            root = new GameObject("Building range overlay");
            root.transform.SetParent(parent, false);
            int order = 0;
            foreach (var pair in geometry)
            {
                var child = new GameObject("Range color " + order);
                child.transform.SetParent(root.transform, false);
                var filter = child.AddComponent<MeshFilter>();
                var renderer = child.AddComponent<MeshRenderer>();
                var mesh = new Mesh
                {
                    name = "Building range " + order,
                    indexFormat = IndexFormat.UInt32
                };
                mesh.SetVertices(pair.Value.Vertices);
                mesh.SetNormals(pair.Value.Normals);
                mesh.SetTriangles(pair.Value.Triangles, 0, true);
                mesh.UploadMeshData(true);
                meshes.Add(mesh);
                filter.sharedMesh = mesh;
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.sortingOrder = order++;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", (Color)pair.Key);
                renderer.SetPropertyBlock(properties);
            }

            SourceId = sourceId;
            HighContrast = highContrast;
            return path.Count;
        }

        public void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }

        public void Clear()
        {
            if (root != null)
                root.SetActive(false);
            foreach (var mesh in meshes)
                DestroyObject(mesh);
            meshes.Clear();
            if (root != null)
                DestroyObject(root);
            root = null;
            SourceId = 0;
            HighContrast = false;
        }

        static void DestroyObject(Object value)
        {
            if (value == null)
                return;
            if (Application.isPlaying)
                Object.Destroy(value);
            else
                Object.DestroyImmediate(value);
        }
    }
}
