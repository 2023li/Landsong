#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.GridSystem;
using Landsong.EditorTools;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.ECS.Editor
{
    // Daily TWC adapter. No legacy catalog, services, save format or full-content migration dependency.
    public static class EcsMapIncrementalImport
    {
        public sealed class TerrainSnapshot
        {
            public Vector2Int Min, Size;
            public Vector3 Origin;
            public float CellSize;
            public CellSource[] Cells;
        }

        public static TerrainSnapshot ReadTerrain(MapContentAuthoring content, GridMapDefinition source = null)
        {
            if (source == null)
                source = content.MapDefinition;
            if (!content.TryValidateConfiguration(source, out var error))
                throw new InvalidOperationException(error);
            var bounds = source.DeclaredCellBounds;
            var data = new TerrainSnapshot
            {
                Min = new Vector2Int(bounds.xMin, bounds.zMin),
                Size = new Vector2Int(bounds.size.x, bounds.size.z),
                Origin = new GridLayoutService(content.UnityGrid).GridToWorldPoint(0, 0),
                CellSize = source.CellSize
            };
            data.Cells = new CellSource[checked(data.Size.x * data.Size.y)];
            var seen = new HashSet<int>();
            foreach (var cell in source.Cells)
            {
                var i = (cell.Position.Z - data.Min.y) * data.Size.x + cell.Position.X - data.Min.x;
                if (i < 0 || i >= data.Cells.Length || !seen.Add(i))
                    throw new InvalidOperationException("Invalid or duplicate XZ cell.");
                var terrain = (ulong)cell.Terrain;
                data.Cells[i] = new CellSource
                {
                    Exists = true,
                    EdgeZone = cell.EdgeZone,
                    Buildable = cell.Buildable,
                    Traversable = cell.Traversable,
                    Elevation = cell.ElevationLevel,
                    Surface = cell.SurfaceLayer,
                    Height = cell.ElevationLevel * source.ElevationWorldStep,
                    Terrain = terrain
                };
            }

            return data;
        }

        public static string SourceScene(MapAsset map)
        {
            var path = AssetDatabase.GUIDToAssetPath(map.TwcSourceSceneGuid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity") || !File.Exists(path))
                throw new InvalidOperationException("MapAsset needs a valid TWC source scene GUID: " + map.MapId);
            return path;
        }

        public static string TargetScene(MapAsset map)
        {
            var path = AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                throw new InvalidOperationException("Missing ECS map SubScene reference: " + map.MapId);
            return path;
        }

        public static void Import(MapAsset map)
        {
            var path = SourceScene(map);
            var scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            var active = SceneManager.GetActiveScene();
            if (opened)
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Landsong.EditorTools.GameMapWorkflow.Bake(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapContentAuthoring>(true)).Single());
            }
            finally
            {
                if (opened)
                    EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid())
                    SceneManager.SetActiveScene(active);
            }
        }

        public static void ValidateInitialBuildings(MapAsset map, TerrainSnapshot terrain, GameContentSetAsset content = null)
        {
            content ??= AssetDatabase.LoadAssetAtPath<GameObject>(MapWorldComposition.TemplatePath).GetComponent<GameContentSetAuthoring>().Content;
            var candidate = UnityEngine.Object.Instantiate(map);
            candidate.Min = terrain.Min;
            candidate.Size = terrain.Size;
            candidate.Cells = terrain.Cells;
            candidate.Origin = terrain.Origin;
            candidate.CellSize = terrain.CellSize;
            try
            {
                using var grid = GameWorldMapAuthoring.BuildGrid(candidate);
                using var compiled = BuildingCatalogBaking.Compile(content);
                using var world = new Unity.Entities.World("Initial building placement validation");
                var em = world.EntityManager;
                var root = em.CreateEntity();
                em.AddComponentData(root, new BuildingCatalog { Value = compiled });
                em.AddComponentData(root, new GridData { Value = grid, Origin = candidate.Origin, CellSize = candidate.CellSize });
                em.AddBuffer<Occupancy>(root).Resize(candidate.Cells.Length, Unity.Collections.NativeArrayOptions.ClearMemory);
                int cores = 0;
                ulong owner = 1;
                foreach (var building in map.InitialBuildings)
                {
                    var index = new BuildingCatalogIndex(content.Buildings).Resolve(building.Definition);
                    if (!index.IsValid)
                        throw new InvalidOperationException("Unknown initial definition: " + building.Definition);
                    ref var definition = ref compiled.Value.Definitions[index.Index];
                    var cell = new Unity.Mathematics.int2(building.Cell.x, building.Cell.y);
                    if (building.Level < 1 || building.Level > definition.MaximumLevel || !GridOps.CanPlace(em, root, index, cell, building.Rotation))
                        throw new InvalidOperationException("New terrain invalidates initial building footprint/connection: " + building.Name);
                    if (definition.Capabilities.Housing.Enabled)
                        for (int i = 0; i < definition.Capabilities.Housing.Population.Length; i++)
                        {
                            var population = definition.Capabilities.Housing.Population[i];
                            if (population.IsCore && (population.Level == 0 || population.Level == building.Level))
                                cores++;
                        }

                    var size = (building.Rotation & 1) == 0 ? definition.Footprint : definition.Footprint.yx;
                    var slots = em.GetBuffer<Occupancy>(root);
                    var data = em.GetComponentData<GridData>(root);
                    for (int z = 0; z < size.y; z++)
                        for (int x = 0; x < size.x; x++)
                            slots[GridOps.Index(data, cell + new Unity.Mathematics.int2(x, z))] = new Occupancy
                            {
                                Owner = owner,
                                MovementCost = 1
                            };
                    owner++;
                }

                if (cores != 1)
                    throw new InvalidOperationException($"地图需要恰好一座王国核心（例如王宫）。当前识别到 {map.InitialBuildings.Length} 个初始建筑、{cores} 座核心。请将正式建筑预制体放在地图根的“初始建筑”下，并确认其定义在初始等级启用了“是否王国核心”。");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(candidate);
            }
        }

        public static void CloneStaticMeshes(GameObject source, Scene target)
        {
            var root = new GameObject(source.name + " · ECS Terrain");
            SceneManager.MoveGameObjectToScene(root, target);
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(false))
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || !renderer.enabled)
                    continue;
                var child = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer));
                child.transform.SetParent(root.transform);
                child.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation);
                child.transform.localScale = renderer.transform.lossyScale;
                child.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
                child.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
                child.isStatic = true;
            }
        }
    }
}
#endif
