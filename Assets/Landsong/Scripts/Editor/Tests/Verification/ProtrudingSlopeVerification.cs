using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.ECS;
using Landsong.ECS.Authoring;
using Landsong.ECS.Definitions;
using Landsong.GridSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class ProtrudingSlopeVerification
    {
        [MenuItem("Landsong/ECS/Verification/Protruding slopes")]
        static void Menu() => Debug.Log(Run());
        public static string Run()
        {
            var log = new StringBuilder();
            int assertions = 0;
            void Check(bool ok, string message)
            {
                if (!ok)
                    throw new InvalidOperationException(message);
                assertions++;
                log.AppendLine("PASS " + message);
            }

            void Reject(Action action, string message)
            {
                bool fail = false;
                try
                {
                    action();
                }
                catch (InvalidOperationException)
                {
                    fail = true;
                }

                Check(fail, message);
            }

            var owned = new List<UnityEngine.Object>();
            T Asset<T>()
                where T : ScriptableObject
            {
                var obj = ScriptableObject.CreateInstance<T>();
                owned.Add(obj);
                return obj;
            }

            var config = Asset<Configuration>();
            config.width = config.height = 12;
            config.cellSize = 1;
            config.blueprintLayerFolders.Clear();
            config.buildLayerFolders.Clear();
            var rules = Asset<MapTerrainRules>();
            rules.overlapRules = Asset<TerrainOverlapRules>();
            rules.useLayerBlueprintRules = true;
            var lower = new BlueprintLayerFolder("Layer0");
            var upper = new BlueprintLayerFolder("Layer1");
            config.blueprintLayerFolders.Add(lower);
            config.blueprintLayerFolders.Add(upper);
            BlueprintLayer Add(BlueprintLayerFolder f, string name, BlueprintLogicKind kind)
            {
                var b = Asset<BlueprintLayer>();
                b.defaultLayerHeight = f == lower ? 0 : 1;
                b.layerName = "L" + ((int)b.defaultLayerHeight) + "_" + name;
                f.blueprintLayers.Add(b);
                rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = name, Terrain = TerrainType.陆地, Kind = kind });
                return b;
            }

            var land = Add(lower, "低地", BlueprintLogicKind.Terrain);
            var hill = Add(upper, "高地", BlueprintLogicKind.Terrain);
            var ramp = Add(upper, "斜坡", BlueprintLogicKind.ProtrudingSlope);
            for (int z = 0; z < 12; z++)
                for (int x = 0; x < 12; x++)
                    land.allPositions.Add(new Vector2(x, z));
            for (int z = 4; z <= 8; z++)
                for (int x = 2; x <= 9; x++)
                    hill.allPositions.Add(new Vector2(x, z));
            for (int x = 4; x <= 6; x++)
                ramp.allPositions.Add(new Vector2(x, 3));
            LayerTerrainCompiler.Result Compile() => LayerTerrainCompiler.Compile(config, rules);
            try
            {
                var result = Compile();
                Check(result.Slopes.Count == 1, "Three painted exterior cells compile to one slope");
                Check(result.Cells.Values.Count(c => c.Group.Height == 1) == 40, "Platform keeps all original terrain cells");
                var strip = result.Slopes[0];
                Check(strip.Cells.Length == 3 && strip.Connection.EntryElevation == 0 && strip.Connection.ExitSurface == 2 && strip.Connection.ProtrudingSlope, "Endpoints derive from adjacent Layer n and n-1");
                lower.folderName = "Layer2";
                upper.folderName = "Layer3";
                land.defaultLayerHeight = 2;
                hill.defaultLayerHeight = ramp.defaultLayerHeight = 3;
                land.layerName = "L2_低地";
                hill.layerName = "L3_高地";
                ramp.layerName = "L3_斜坡";
                var upperStrip = Compile().Slopes.Single();
                Check(upperStrip.Connection.EntryElevation == 2 && upperStrip.Connection.Rise == 1 && upperStrip.Connection.ExitSurface == 4 && rules.blueprintRules.Count == 3, "Layer3 slope reuses the shared rule and connects Layer3 to Layer2");
                lower.folderName = "Layer0";
                upper.folderName = "Layer1";
                land.defaultLayerHeight = 0;
                hill.defaultLayerHeight = ramp.defaultLayerHeight = 1;
                land.layerName = "L0_低地";
                hill.layerName = "L1_高地";
                ramp.layerName = "L1_斜坡";
                ramp.allPositions.Remove(new Vector2(6, 3));
                Reject(() => Compile(), "Two-cell slopes rejected");
                ramp.allPositions.Add(new Vector2(6, 3));
                ramp.allPositions.Add(new Vector2(4, 4));
                Reject(() => Compile(), "Slope cannot occupy a platform cell");
                ramp.allPositions.Remove(new Vector2(4, 4));
                ramp.allPositions.Clear();
                for (int x = 2; x <= 4; x++)
                    ramp.allPositions.Add(new Vector2(x, 3));
                Reject(() => Compile(), "Corner cells rejected");
                ramp.allPositions.Clear();
                for (int x = 4; x <= 6; x++)
                    ramp.allPositions.Add(new Vector2(x, 3));
                var removed = hill.allPositions.Where(p => p.x < 3 || p.x > 7).ToArray();
                foreach (var p in removed)
                    hill.allPositions.Remove(p);
                Reject(() => Compile(), "Five-cell edge rejected even with three slope cells");
                foreach (var p in removed)
                    hill.allPositions.Add(p);
                land.allPositions.Remove(new Vector2(5, 2));
                Reject(() => Compile(), "Missing lower mouth rejected");
                land.allPositions.Add(new Vector2(5, 2));
                config.cellSize = 2;
                Reject(() => Compile(), "45-degree slope rejects incompatible grid size");
                config.cellSize = 1;
                var map = Asset<MapAsset>();
                map.Size = new Vector2Int(12, 12);
                map.CellSize = 1;
                map.ElevationStep = 1;
                void Fill(LayerTerrainCompiler.Result r)
                {
                    map.Cells = new CellSource[144];
                    foreach (var c in r.Primary)
                        map.Cells[c.Position.Z * 12 + c.Position.X] = new CellSource
                        {
                            Exists = true,
                            Buildable = c.Buildable,
                            Traversable = c.Traversable,
                            Elevation = c.ElevationLevel,
                            Height = c.ElevationLevel,
                            Surface = c.SurfaceLayer,
                            Terrain = GridOps.TerrainBit("陆地")
                        };
                    map.NavigationSurfaces = r.Additional.ToArray();
                    map.Connections = r.Slopes.Select(s => s.Connection).ToArray();
                }

                for (int direction = 0; direction < 4; direction++)
                {
                    result = Compile();
                    Fill(result);
                    strip = result.Slopes[0];
                    Check(strip.Rotation == direction, "Blueprint orientation " + direction + " inferred without model data");
                    using var blob = GameWorldMapAuthoring.BuildGrid(map);
                    using var world = new World("Slope verification");
                    var em = world.EntityManager;
                    var root = em.CreateEntity();
                    var grid = new GridData
                    {
                        Value = blob,
                        CellSize = 1
                    };
                    em.AddComponentData(root, grid);
                    em.AddBuffer<Occupancy>(root).Resize(144, NativeArrayOptions.ClearMemory);
                    using var builder = new BlobBuilder(Allocator.Temp);
                    ref var catalog = ref builder.ConstructRoot<BuildingCatalogBlob>();
                    var defs = builder.Allocate(ref catalog.Definitions, 3);
                    defs[0] = new BuildingDefinition
                    {
                        Footprint = new int2(1),
                        PlacementAndVisuals = new BuildingPlacementAndVisuals
                        {
                            Category = BuildingCategory.Road,
                            CanRotate = true
                        }
                    };
                    defs[1] = new BuildingDefinition
                    {
                        Footprint = new int2(1)
                    };
                    defs[2] = new BuildingDefinition
                    {
                        Footprint = new int2(2),
                        PlacementAndVisuals = new BuildingPlacementAndVisuals
                        {
                            Category = BuildingCategory.Road
                        }
                    };
                    for (int i = 0; i < defs.Length; i++)
                        defs[i].Capabilities.Placement.AllowedTerrains = TerrainType.All;
                    using var content = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
                    em.AddComponentData(root, new BuildingCatalog { Value = content });
                    var cell = new int2(strip.Cells[1].x, strip.Cells[1].y);
                    Check(GridOps.CanPlace(em, root, BuildingId.FromIndex(0), cell, 0) && !GridOps.CanPlace(em, root, BuildingId.FromIndex(1), cell, 0) && !GridOps.CanPlace(em, root, BuildingId.FromIndex(2), cell, 0), "Only 1x1 roads can occupy slope, direction " + direction);
                    Check(math.abs(GridOps.Position(grid, cell, new int2(1)).y - .5f) < .0001f, "Road anchor lies at slope midpoint, direction " + direction);
                    var anchor = GridOps.Position(grid, cell, new int2(1));
                    Check(GridOps.RaycastSurface(grid, anchor + new float3(0, 10, 0), new float3(0, -1, 0), out var hit) && math.distance(hit, anchor) < .0001f, "Logical pointer picking hits slope height, direction " + direction);
                    var reserved = em.GetBuffer<Occupancy>(root);
                    reserved[GridOps.Index(grid, cell)] = new Occupancy
                    {
                        Owner = 10,
                        MovementCost = 1.7f
                    };
                    Check(!GridOps.CanPlace(em, root, BuildingId.FromIndex(0), cell, 0), "Slope road still reserves XZ occupancy, direction " + direction);
                    SurfaceNavigationGraph.Ensure(em, root);
                    using var nodes = em.GetBuffer<SurfaceNavNode>(root).ToNativeArray(Allocator.Temp);
                    using var edges = em.GetBuffer<SurfaceNavEdge>(root).ToNativeArray(Allocator.Temp);
                    using var cells = new NativeParallelMultiHashMap<int2, int>(nodes.Length, Allocator.Temp);
                    for (int i = 0; i < nodes.Length; i++)
                        cells.Add(nodes[i].Cell, i);
                    var query = new SurfacePathQuery
                    {
                        Grid = grid,
                        Nodes = nodes,
                        Edges = edges,
                        Cells = cells,
                        Radius = .1f
                    };
                    var delta = new int2(strip.Direction.x, strip.Direction.y);
                    var from = GridOps.Position(grid, cell - delta, new int2(1)) + new float3(0, .5f, 0);
                    var to = GridOps.Position(grid, cell + delta, new int2(1)) + new float3(0, .5f, 0);
                    var pathEntity = em.CreateEntity();
                    var path = em.AddBuffer<Waypoint>(pathEntity);
                    Check(query.Find(from, to, path), "Road preserves uphill navigation, direction " + direction);
                    Check(query.Find(to, from, path), "Road preserves downhill navigation, direction " + direction);
                    var point = from;
                    for (int step = 1; step <= 20; step++)
                    {
                        var candidate = point + new float3(delta.x * .1f, 0, delta.y * .1f);
                        Check(query.Shift(point, candidate, out point), "Continuous slope step " + direction + "/" + step);
                    }

                    Check(math.abs(point.y - to.y) < .0002f, "Footprint profile reaches upper flat surface without a height jump, direction " + direction);
                    Check(nodes.Any(n => math.all(n.Cell == cell) && n.Corridor != 0 && math.abs(n.Cost - 1.7f) < .001f), "Road movement cost applies on slope, direction " + direction);
                    foreach (var bp in new[]
                    {
                        land,
                        hill,
                        ramp
                    }

                    )
                    {
                        var positions = bp.allPositions.ToArray();
                        bp.allPositions.Clear();
                        foreach (var p in positions)
                            bp.allPositions.Add(new Vector2(p.y, 11 - p.x));
                    }
                }

                var preset = AssetDatabase.LoadAssetAtPath<ProtrudingSlopeTilePreset>(ProtrudingSlopeSetup.PresetPath);
                Check(preset != null && preset.Left != null && preset.Middle != null && preset.Right != null, "My斜坡 references all three actual prefabs");
                var owner = new GameObject("Slope TWC verification");
                owned.Add(owner);
                var manager = owner.AddComponent<TileWorldCreatorManager>();
                manager.configuration = config;
                var layer = Asset<ProtrudingSlopeBuildLayer>();
                layer.rules = rules;
                layer.tileSet = preset;
                layer.assignedBlueprintLayerGuid = ramp.guid;
                layer.ExecuteLayer(config, owner, manager);
                Check(layer.LayerObject.transform.childCount == 3, "Actual TWC build layer instantiates left, middle, right");
                layer.ExecuteLayer(config, owner, manager);
                Check(layer.LayerObject.transform.childCount == 3, "Repeated slope builds do not duplicate geometry");
                Check(layer.LayerObject.transform.GetChild(1).localPosition.z == 3, "Slope visual occupies exterior cell row, platform starts at row four");
                var logicalBefore = Compile();
                layer.layerYOffset = -1;
                layer.ExecuteLayer(config, owner, manager);
                Check(layer.LayerObject.transform.GetChild(1).localPosition.y == 0, "Slope Global Offset translates generated instances");
                var logicalAfter = Compile();
                Check(logicalAfter.Slopes[0].Height == logicalBefore.Slopes[0].Height && logicalAfter.Primary.Select(c => (c.Position, c.ElevationLevel, c.SurfaceLayer, c.Terrain, c.Buildable, c.Traversable)).SequenceEqual(logicalBefore.Primary.Select(c => (c.Position, c.ElevationLevel, c.SurfaceLayer, c.Terrain, c.Buildable, c.Traversable))), "Visual offset never changes logical terrain or layer height");
                layer.layerYOffset = -.5f;
                layer.ExecuteLayer(config, owner, manager);
                Check(layer.LayerObject.transform.GetChild(1).localPosition.y == .5f, "Changing visual offset replaces geometry without accumulating translation");
                layer.layerYOffset = float.NaN;
                Reject(() => layer.ExecuteLayer(config, owner, manager), "Nonfinite visual offset is rejected before rebuilding");
                layer.layerYOffset = 0;
                var testMesh = new Mesh();
                owned.Add(testMesh);
                testMesh.vertices = new[]
                {
                    new Vector3(4, 1.5f, 3),
                    new Vector3(5, 1.5f, 3),
                    new Vector3(4, 1.5f, 3.5f),
                    new Vector3(5, 1.5f, 3.5f),
                    new Vector3(4, 1.4f, 3),
                    new Vector3(5, 1.4f, 3),
                    new Vector3(4, 1.4f, 3.5f),
                    new Vector3(5, 1.4f, 3.5f)
                };
                testMesh.triangles = new[]
                {
                    0, 2, 1, 1, 2, 3,
                    4, 5, 6, 5, 7, 6,
                    4, 0, 5, 5, 0, 1,
                    6, 7, 2, 7, 3, 2,
                    4, 6, 0, 6, 2, 0,
                    5, 1, 7, 7, 1, 3
                };
                testMesh.RecalculateNormals();
                var originalTestVertices = testMesh.vertices.ToArray();
                var mouth = new GameObject("Generated cliff", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
                mouth.transform.SetParent(owner.transform, false);
                var filter = mouth.GetComponent<MeshFilter>();
                var collider = mouth.GetComponent<MeshCollider>();
                filter.sharedMesh = collider.sharedMesh = testMesh;
                result = Compile();
                SlopeVisualCut.Apply(owner.transform, result.Slopes);
                Check(filter.sharedMesh != testMesh && testMesh.vertices.SequenceEqual(originalTestVertices), "Visual mouth uses a copy and preserves source mesh");
                var modified = filter.sharedMesh.vertices;
                SlopeVisualCut.Apply(owner.transform, result.Slopes);
                Check(filter.sharedMesh.vertices.SequenceEqual(modified), "Repeated mouth builds do not accumulate deformation");
                var offsets = new Dictionary<string, float>
                {
                    {
                        ramp.guid,
                        -1
                    }
                };
                mouth.transform.localPosition = Vector3.down;
                SlopeVisualCut.Apply(owner.transform, result.Slopes, visualOffsets: offsets);
                Check(filter.sharedMesh.vertices.Zip(modified, (a, b) => (a - b).sqrMagnitude < .000001f).All(v => v), "Cliff trimming follows the same offset as the slope");
                var grass = AssetDatabase.LoadAssetAtPath<ProtrudingSlopeTilePreset>(ProtrudingSlopeSetup.PresetPath).Middle.GetComponentInChildren<MeshRenderer>().sharedMaterial;
                mouth.GetComponent<MeshRenderer>().sharedMaterial = grass;
                SlopeVisualCut.Apply(owner.transform, result.Slopes, grass, visualOffsets: offsets);
                Check(filter.sharedMesh.vertices.All(v => Mathf.Abs(mouth.transform.TransformPoint(v).y - .5f) < .0001f), "Grass landing height follows negative slope offset");
                var collisionVertices = collider.sharedMesh.vertices;
                var collisionTriangles = collider.sharedMesh.triangles;
                Check(collider.sharedMesh != filter.sharedMesh && collisionTriangles.Length > 0 && Enumerable.Range(0, collisionTriangles.Length / 3).All(i =>
                {
                    var a = collisionVertices[collisionTriangles[i * 3]];
                    var b = collisionVertices[collisionTriangles[i * 3 + 1]];
                    var c = collisionVertices[collisionTriangles[i * 3 + 2]];
                    return Vector3.Cross(b - a, c - a).sqrMagnitude > .000000000001f;
                }), "Collision mouth removes degenerate triangles or keeps the authored collision fallback");
                mouth.transform.localPosition = Vector3.zero;
                offsets[ramp.guid] = 0;
                SlopeVisualCut.Apply(owner.transform, result.Slopes, grass, visualOffsets: offsets);
                Check(filter.sharedMesh.vertices.All(v => Mathf.Abs(mouth.transform.TransformPoint(v).y - 1.5f) < .0001f), "Changing offset restores the landing from original mesh coordinates");
                SlopeVisualCut.Apply(owner.transform, Array.Empty<ProtrudingSlopeCompiler.Strip>());
                Check(filter.sharedMesh == testMesh && collider.sharedMesh == testMesh, "Removing slope restores original render and collision meshes");
                log.AppendLine("Assertions: " + assertions);
                return log.ToString();
            }
            catch (Exception e)
            {
                log.AppendLine("FAIL " + e);
                throw;
            }
            finally
            {
                File.WriteAllText("Library/LandsongEcs/protruding-slope-verification.txt", log.ToString());
                foreach (var o in owned.AsEnumerable().Reverse())
                    if (o != null)
                        UnityEngine.Object.DestroyImmediate(o);
            }
        }
    }
}
