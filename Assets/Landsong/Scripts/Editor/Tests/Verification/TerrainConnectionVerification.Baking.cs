using System;
using System.Linq;
using GiantGrey.TileWorldCreator;
using Landsong.ECS.Authoring;
using Landsong.GridSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static partial class TerrainConnectionVerification
    {
        static void Baking()
        {
            AssetDatabase.ImportAsset(Landsong.EditorTools.TwcFlowVerification.ConfigPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            var config = AssetDatabase.LoadAssetAtPath<Configuration>(Landsong.EditorTools.TwcFlowVerification.ConfigPath);
            var blueprint = config.blueprintLayerFolders.SelectMany(f => f.blueprintLayers).Single(b => b.layerName == "land_坡顶");
            // TWC can skip its runtime cache during compilation, before Configuration repairs layer references.
            // This fixture deliberately restores the freshly imported, persisted mask rather than editing it.
            blueprint.OnAfterDeserialize();
            Check(blueprint.allPositions.Count > 0, "Persisted TWC navigation fixture restores a nonempty source mask");
            var go = new GameObject("TWC navigation baking verification");
            var map = ScriptableObject.CreateInstance<MapAsset>();
            try
            {
                var content = go.AddComponent<MapContentAuthoring>();
                content.TwcConfiguration = config;
                var navigation = go.AddComponent<MapNavigationAuthoring>();
                navigation.Surfaces = new[]
                {
                    new TwcNavigationLayer
                    {
                        BlueprintGuid = blueprint.guid,
                        SurfaceId = 100,
                        Elevation = 2
                    }
                };
                navigation.Connections = new[]
                {
                    new AuthoredConnection
                    {
                        Id = 1,
                        Cell = new int2(10, 6),
                        Size = new int2(4, 3),
                        EntrySurface = 0,
                        ExitSurface = 100,
                        EntryElevation = 0,
                        Rise = 2,
                        Bidirectional = true
                    }
                };
                map.Size = new Vector2Int(32, 24);
                map.Cells = new CellSource[32 * 24];
                for (int i = 0; i < map.Cells.Length; i++)
                    map.Cells[i] = new CellSource
                    {
                        Exists = true,
                        Traversable = true,
                        Buildable = true
                    };
                Landsong.EditorTools.MapNavigationBaker.Bake(content, map);
                Check(map.NavigationSurfaces.Length == blueprint.allPositions.Count && map.NavigationSurfaces.All(s => s.Elevation == 2 && s.Surface == 100), "Actual TWC Blueprint mask bakes stable surface identity and integer height");
                string first = JsonUtility.ToJson(map);
                Landsong.EditorTools.MapNavigationBaker.Bake(content, map);
                Check(first == JsonUtility.ToJson(map), "Repeated TWC navigation baking is deterministic");
                using var grid = GameWorldMapAuthoring.BuildGrid(map);
                using var world = new World("TWC baked surface graph");
                var em = world.EntityManager;
                var root = em.CreateEntity();
                em.AddComponentData(root, new GridData { Value = grid, CellSize = 1 });
                em.AddBuffer<Occupancy>(root).Resize(map.Cells.Length, NativeArrayOptions.ClearMemory);
                using (var q = new QueryScope(em, root, .2f))
                {
                    Check(q.Path(new float3(11.5f, .5f, 5.5f), new float3(11.5f, 2.5f, 10.5f)), "Baked TWC surface and explicit slope reach the upper plane");
                    Check(q.Value.Locate(new float3(12.5f, .5f, 12.5f)) >= 0 && q.Value.Locate(new float3(12.5f, 2.5f, 12.5f)) >= 0, "Baked static surfaces coexist at identical XZ");
                }

                map.Connections[0].ExitSurface = 999;
                bool rejected = false;
                try
                {
                    using var invalid = GameWorldMapAuthoring.BuildGrid(map);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "Baking rejects a connection bound to the wrong surface identity");
                map.Connections[0].ExitSurface = 100;
                map.Connections[0].Bidirectional = false;
                using (var directed = GameWorldMapAuthoring.BuildGrid(map))
                using (var directedWorld = new World("Directed connection"))
                {
                    var manager = directedWorld.EntityManager;
                    var simulation = manager.CreateEntity();
                    manager.AddComponentData(simulation, new GridData { Value = directed, CellSize = 1 });
                    manager.AddBuffer<Occupancy>(simulation).Resize(map.Cells.Length, NativeArrayOptions.ClearMemory);
                    using var q = new QueryScope(manager, simulation, .2f);
                    Check(q.Path(new float3(11.5f, .5f, 5.5f), new float3(11.5f, 2.5f, 10.5f)) && !q.Path(new float3(11.5f, 2.5f, 10.5f), new float3(11.5f, .5f, 5.5f)), "One-way authored passage rejects reverse travel");
                }

                map.Cells[7 * 32 + 10].Elevation = 2;
                map.Cells[7 * 32 + 10].Height = 2;
                rejected = false;
                try
                {
                    using var invalid = GameWorldMapAuthoring.BuildGrid(map);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "Baking rejects a passage crossing solid terrain");
                map.Cells[7 * 32 + 10].Elevation = 0;
                map.Cells[7 * 32 + 10].Height = 0;
                map.Connections = Array.Empty<AuthoredConnection>();
                map.Cells[0].Height = 2.781f;
                rejected = false;
                try
                {
                    using var invalid = GameWorldMapAuthoring.BuildGrid(map);
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, "Baking rejects non-unitized 2.781 terrain height");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(map);
            }
        }
    }
}
