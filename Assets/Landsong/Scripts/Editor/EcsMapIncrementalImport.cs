#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
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
            if (source == null) source = content.MapDefinition;
            if (!content.TryValidateConfiguration(source, out var error)) throw new InvalidOperationException(error);
            var bounds = source.BakedCellBounds;
            var data = new TerrainSnapshot { Min = new Vector2Int(bounds.xMin, bounds.zMin), Size = new Vector2Int(bounds.size.x, bounds.size.z),
                Origin = new GridLayoutService(content.UnityGrid).GridToWorldPoint(0, 0), CellSize = source.CellSize };
            data.Cells = new CellSource[checked(data.Size.x * data.Size.y)];
            var seen = new HashSet<int>(); var keys = new Dictionary<ulong, string>();
            ulong Bit(string key)
            {
                var bit = GridOps.TerrainBit(new FixedString64Bytes(key));
                if (keys.TryGetValue(bit, out var other) && other != key) throw new InvalidOperationException("Terrain hash collision: " + key + "/" + other);
                keys[bit] = key; return bit;
            }
            foreach (var cell in source.Cells)
            {
                var i = (cell.Position.Z - data.Min.y) * data.Size.x + cell.Position.X - data.Min.x;
                if (i < 0 || i >= data.Cells.Length || !seen.Add(i)) throw new InvalidOperationException("Invalid or duplicate XZ cell.");
                var terrain = Bit(cell.PrimaryTerrainKey); foreach (var key in cell.OverlayTerrainKeys) terrain |= Bit(key);
                data.Cells[i] = new CellSource { Exists = true, Buildable = cell.Buildable, Traversable = cell.Traversable, Elevation = cell.ElevationLevel,
                    Surface = cell.SurfaceLayer, Height = cell.ElevationLevel * source.ElevationWorldStep, Terrain = terrain };
            }
            return data;
        }
        public static string SourceScene(MapAsset map)
        {
            var path = AssetDatabase.GUIDToAssetPath(map.TwcSourceSceneGuid);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".unity") || !File.Exists(path)) throw new InvalidOperationException("MapAsset needs a valid TWC source scene GUID: " + map.MapId);
            return path;
        }
        public static string TargetScene(MapAsset map)
        {
            var path = AssetDatabase.GUIDToAssetPath(map.EntitySceneGuid);
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) throw new InvalidOperationException("Missing ECS map SubScene reference: " + map.MapId);
            return path;
        }
        public static void Import(MapAsset map)
        {
            var path = SourceScene(map);
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            var active = SceneManager.GetActiveScene();
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { Landsong.EditorTools.GameMapWorkflow.Bake(scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<MapContentAuthoring>(true)).Single()); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); if (active.IsValid()) SceneManager.SetActiveScene(active); }
        }
        public static void ValidateInitialBuildings(MapAsset map, TerrainSnapshot terrain, GameCatalogAsset catalog = null)
        {
            catalog ??= AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            var definitions=catalog.Content.ToDictionary(d=>d.Id);var compiled=new ContentCompilation(catalog);
            var occupied = new HashSet<Vector2Int>(); var cores = 0;
            foreach (var building in map.InitialBuildings)
            {
                if (!definitions.TryGetValue(building.Definition, out var definition)) throw new InvalidOperationException("Unknown initial definition.");
                if (definition.Kind != ContentKind.Building || definition.Size.x <= 0 || definition.Size.y <= 0 || building.Level < 1 || building.Level > definition.Level || building.Rotation < 0 || building.Rotation > 3)
                    throw new InvalidOperationException("Invalid initial building definition/level/rotation: " + building.Name);
                if (compiled.For(definition).Any(r => r.Kind == RuleKind.Population && r.B != 0 && (r.Level == 0 || r.Level == building.Level))) cores++;
                var size = (building.Rotation & 1) == 0 ? definition.Size : new Vector2Int(definition.Size.y, definition.Size.x);
                var elevation = int.MinValue; var surface = int.MinValue; ulong terrainUnion = 0;
                for (var z = 0; z < size.y; z++) for (var x = 0; x < size.x; x++)
                {
                    var cell = building.Cell + new Vector2Int(x, z); var local = cell - terrain.Min;
                    if (local.x < 0 || local.y < 0 || local.x >= terrain.Size.x || local.y >= terrain.Size.y || !occupied.Add(cell))
                        throw new InvalidOperationException("New terrain excludes or overlaps initial building: " + building.Name);
                    var value = terrain.Cells[local.y * terrain.Size.x + local.x];
                    if (!value.Exists || !value.Buildable || (elevation != int.MinValue && (elevation != value.Elevation || surface != value.Surface)))
                        throw new InvalidOperationException("New terrain invalidates initial building footprint: " + building.Name);
                    foreach (var rule in compiled.For(definition).Where(r => r.Kind == RuleKind.RequiredTerrain))
                        if ((value.Terrain & GridOps.TerrainBit(rule.Key)) == 0) throw new InvalidOperationException("Initial building required terrain removed: " + building.Name);
                    elevation = value.Elevation; surface = value.Surface; terrainUnion |= value.Terrain;
                }
                var any = compiled.For(definition).Where(r => r.Kind == RuleKind.AnyTerrain).ToArray();
                if (any.Length > 0 && !any.Any(r => (terrainUnion & GridOps.TerrainBit(r.Key)) != 0))
                    throw new InvalidOperationException("Initial building required adjacent terrain removed: " + building.Name);
            }
            if (cores != 1) throw new InvalidOperationException("An ECS map needs exactly one PlayerHome core.");
        }
        public static void CloneStaticMeshes(GameObject source, Scene target)
        {
            var root = new GameObject(source.name + " · ECS Terrain"); SceneManager.MoveGameObjectToScene(root, target);
            foreach (var renderer in source.GetComponentsInChildren<MeshRenderer>(false))
            {
                var filter = renderer.GetComponent<MeshFilter>(); if (filter == null || filter.sharedMesh == null || !renderer.enabled) continue;
                var child = new GameObject(renderer.name, typeof(MeshFilter), typeof(MeshRenderer)); child.transform.SetParent(root.transform);
                child.transform.SetPositionAndRotation(renderer.transform.position, renderer.transform.rotation); child.transform.localScale = renderer.transform.lossyScale;
                child.GetComponent<MeshFilter>().sharedMesh = filter.sharedMesh; child.GetComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials; child.isStatic = true;
            }
        }
    }
}
#endif
