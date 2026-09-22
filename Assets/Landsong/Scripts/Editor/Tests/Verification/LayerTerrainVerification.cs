using Landsong.ECS;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GiantGrey.TileWorldCreator;
using Landsong.EditorTools;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.GridSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class LayerTerrainVerification
    {
        sealed class Fixture : IDisposable
        {
            public readonly List<UnityEngine.Object> Owned = new List<UnityEngine.Object>();
            public Configuration Config;
            public MapTerrainRules Rules;
            public TilePreset Land, Water, Swamp;
            public BlueprintLayer Lower, Wet, Marsh, Upper;
            public TilesBuildLayer LandView;
            public BlueprintLayerFolder G0, G3;
            public T Asset<T>()
                where T : ScriptableObject
            {
                var a = ScriptableObject.CreateInstance<T>();
                Owned.Add(a);
                return a;
            }

            public Fixture()
            {
                Config = Asset<Configuration>();
                Config.width = Config.height = 8;
                Config.cellSize = 1;
                Config.blueprintLayerFolders.Clear();
                Config.buildLayerFolders.Clear();
                Rules = Asset<MapTerrainRules>();
                Rules.overlapRules = Asset<TerrainOverlapRules>();
                Rules.useLayerBlueprintRules = true;
                Land = Asset<TilePreset>();
                Water = Asset<TilePreset>();
                Swamp = Asset<TilePreset>();
                Rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = "陆地", Terrain = TerrainType.陆地 });
                Rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = "水域", Terrain = TerrainType.水域, Buildable = false, Traversable = false });
                Rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = "沼泽", Terrain = TerrainType.沼泽 });
                G0 = new BlueprintLayerFolder("Layer0");
                G3 = new BlueprintLayerFolder("Layer3");
                Config.blueprintLayerFolders.Add(G0);
                Config.blueprintLayerFolders.Add(G3);
                Config.buildLayerFolders.Add(new BuildLayerFolder("Views"));
                Lower = Blueprint(G0, "陆地", Land);
                LandView = (TilesBuildLayer)Config.buildLayerFolders[0].buildLayers.Last();
                Wet = Blueprint(G0, "水域", Water);
                Marsh = Blueprint(G0, "沼泽", Swamp);
                Upper = Blueprint(G3, "陆地", Land);
                for (int z = 1; z <= 5; z++)
                    for (int x = 1; x <= 3; x++)
                    {
                        Lower.allPositions.Add(new Vector2(x, z));
                        Wet.allPositions.Add(new Vector2(x, z));
                    }

                Marsh.allPositions.Add(new Vector2(2, 1));
                Upper.allPositions.Add(new Vector2(1, 1));
                Upper.allPositions.Add(new Vector2(1, 5));
                Upper.allPositions.Add(new Vector2(2, 1));
                Upper.allPositions.Add(new Vector2(2, 5));
                Upper.allPositions.Add(new Vector2(3, 1));
                Upper.allPositions.Add(new Vector2(3, 5));
            }

            public BlueprintLayer Blueprint(BlueprintLayerFolder folder, string name, TilePreset preset)
            {
                var b = Asset<BlueprintLayer>();
                b.defaultLayerHeight = int.Parse(folder.folderName.Substring(5));
                b.layerName = "L" + ((int)b.defaultLayerHeight) + "_" + name;
                folder.blueprintLayers.Add(b);
                var view = Asset<TilesBuildLayer>();
                view.assignedBlueprintLayerGuid = b.guid;
                view.SetNewTilePreset(preset);
                Config.buildLayerFolders[0].buildLayers.Add(view);
                return b;
            }

            public LayerTerrainCompiler.Result Compile() => LayerTerrainCompiler.Compile(Config, Rules);
            public MapAsset Map(LayerTerrainCompiler.Result r)
            {
                var map = Asset<MapAsset>();
                map.Size = new Vector2Int(8, 8);
                map.CellSize = 1;
                map.ElevationStep = 1;
                map.Cells = new CellSource[64];
                foreach (var c in r.Primary)
                    map.Cells[c.Position.Z * 8 + c.Position.X] = new CellSource
                    {
                        Exists = true,
                        Buildable = c.Buildable,
                        Traversable = c.Traversable,
                        Elevation = c.ElevationLevel,
                        Height = c.ElevationLevel,
                        Surface = c.SurfaceLayer,
                        Terrain = (ulong)c.Terrain
                    };
                map.NavigationSurfaces = r.Additional.ToArray();
                return map;
            }

            public void Dispose()
            {
                foreach (var a in Owned.AsEnumerable().Reverse())
                    UnityEngine.Object.DestroyImmediate(a);
            }
        }

        [MenuItem("Landsong/ECS/Verification/Layer terrain rules")]
        public static string Run()
        {
            var log = new StringBuilder();
            int count = 0;
            void Check(bool ok, string message)
            {
                if (!ok)
                    throw new InvalidOperationException(message);
                count++;
                log.AppendLine("PASS " + message);
            }

            void Reject(Action action, string message)
            {
                bool rejected = false;
                try
                {
                    action();
                }
                catch (InvalidOperationException)
                {
                    rejected = true;
                }

                Check(rejected, message);
            }

            try
            {
                using var f = new Fixture();
                var r = f.Compile();
                Check(r.Primary.Count == 15 && r.Primary.Single(c => c.Position.X == 2 && c.Position.Z == 1).Terrain == TerrainType.沼泽, "SO priority selects swamp over land and water");
                Check(r.Primary.All(c => TerrainTypes.Single(c.Terrain)), "Overridden terrain tags do not survive");
                Check(r.Additional.Count == 6 && r.Additional.All(s => s.Elevation == 0), "Lower walkable surfaces survive under the upper layer");
                Check(r.Primary.Where(c => c.SurfaceLayer == 4).All(c => c.ElevationLevel == 3), "Layer is the sole logical height source");
                string Snapshot(LayerTerrainCompiler.Result result) => string.Join(";", result.Primary.Select(c => c.Position + ":" + c.Terrain + ":" + c.ElevationLevel + ":" + c.SurfaceLayer));
                string before = Snapshot(r);
                string lowerName = f.Lower.layerName;
                f.Lower.layerName = "L3_陆地";
                Reject(() => f.Compile(), "Blueprint prefix must match its folder height");
                f.Lower.layerName = lowerName;
                f.Lower.layerName = "L03_陆地";
                Reject(() => f.Compile(), "Noncanonical Layer prefixes are rejected");
                f.Lower.layerName = lowerName;
                f.Lower.layerName = "L0_陆地额外";
                Reject(() => f.Compile(), "Terrain keys use exact matching, never substring matching");
                f.Lower.layerName = lowerName;
                Check(f.Compile().Primary.Any(c => c.ElevationLevel == 3 && c.Terrain == TerrainType.陆地) && f.Rules.blueprintRules.Count == 3, "Layer0 and Layer3 share one land rule");
                var g12 = new BlueprintLayerFolder("Layer12");
                f.Config.blueprintLayerFolders.Add(g12);
                var land12 = f.Blueprint(g12, "陆地", f.Land);
                land12.allPositions.Add(new Vector2(6, 6));
                Check(f.Compile().Primary.Single(c => c.Position.Equals(new GridPosition(6, 6))).ElevationLevel == 12 && f.Rules.blueprintRules.Count == 3, "A new Layer12 needs no additional terrain rule");
                f.Config.blueprintLayerFolders.Remove(g12);
                f.Config.buildLayerFolders[0].buildLayers.RemoveAt(f.Config.buildLayerFolders[0].buildLayers.Count - 1);
                var collapsed = BlueprintRuleNormalization.Collapse(new[] { new BlueprintTerrainRule { BlueprintLayerName = "L0_陆地" }, new BlueprintTerrainRule { BlueprintLayerName = "L3_陆地" } });
                Check(collapsed.Count == 1 && collapsed[0].BlueprintLayerName == "陆地", "Migration merges equivalent Layer rules");
                Reject(() => BlueprintRuleNormalization.Collapse(new[] { new BlueprintTerrainRule { BlueprintLayerName = "L0_陆地" }, new BlueprintTerrainRule { BlueprintLayerName = "L3_陆地", Buildable = false } }), "Migration rejects conflicting per-Layer permissions");
                f.Rules.blueprintRules[0].BlueprintLayerName = "L0_陆地";
                Reject(() => f.Compile(), "Shared rule assets reject numbered rule keys");
                f.Rules.blueprintRules[0].BlueprintLayerName = "陆地";
                f.Config.blueprintLayerFolders.Reverse();
                f.Config.buildLayerFolders[0].buildLayers.Reverse();
                Check(Snapshot(f.Compile()) == before, "Folder and view reordering does not change logical results");
                f.LandView.isEnabled = false;
                f.LandView.useDualGrid = false;
                f.LandView.scaleOffset = new Vector3(100, 37, 5);
                f.LandView.layerYOffset = 90;
                f.Land.fillTileYRotationOffset = 270;
                Check(Snapshot(f.Compile()) == before, "Build visibility, Dual Grid, visual offsets and rotations do not affect logic");
                f.LandView.tilePresetsTop.Add(new TilesBuildLayer.TilePresetSelection { preset = f.Water, weight = 1 });
                Check(Snapshot(f.Compile()) == before, "Mixed visual presets do not change Blueprint terrain");
                f.LandView.tilePresetsTop.RemoveAt(1);
                f.LandView.tileLayers.Add(new TilesBuildLayer.TileLayers { layerOverrides = new List<TilesBuildLayer.TilePresetOverride> { new TilesBuildLayer.TilePresetOverride { preset = f.Water } } });
                Check(Snapshot(f.Compile()) == before, "Visual preset overrides do not change Blueprint terrain");
                f.LandView.tileLayers.Clear();
                var savedViews = f.Config.buildLayerFolders.ToArray();
                f.Config.buildLayerFolders.Clear();
                Check(Snapshot(f.Compile()) == before, "Blueprint terrain works without any Build Layer or TilePreset");
                f.Config.buildLayerFolders.AddRange(savedViews);
                f.Lower.layerName = "未定义地形";
                Reject(() => f.Compile(), "Unmapped Blueprint terrain is rejected");
                f.Lower.layerName = "陆地";
                f.Rules.blueprintRules.Add(new BlueprintTerrainRule { BlueprintLayerName = "陆地", Terrain = TerrainType.沼泽 });
                Reject(() => f.Compile(), "One Blueprint name cannot declare two terrain definitions");
                f.Rules.blueprintRules.RemoveAt(3);
                f.Rules.overlapRules.HighToLow.Add(TerrainType.陆地);
                Reject(() => f.Compile(), "Duplicate terrain ordering is rejected");
                f.Rules.overlapRules.HighToLow.RemoveAt(f.Rules.overlapRules.HighToLow.Count - 1);
                f.Upper.defaultLayerHeight = 2;
                Reject(() => f.Compile(), "Child height inconsistent with Layer is rejected");
                f.Upper.defaultLayerHeight = 3;
                f.Lower.allPositions.Add(new Vector2(.5f, 2));
                Reject(() => f.Compile(), "Dual Grid half coordinates cannot enter logical Blueprint cells");
                f.Lower.allPositions.Remove(new Vector2(.5f, 2));
                f.Marsh.isEnabled = false;
                Check(f.Compile().Primary.Single(c => c.Position.X == 2 && c.Position.Z == 1).Terrain == TerrainType.陆地, "Blueprint disabling removes its logical terrain");
                f.Marsh.isEnabled = true;
                var duplicate = new BlueprintLayerFolder("Layer0");
                f.Config.blueprintLayerFolders.Add(duplicate);
                Reject(() => f.Compile(), "Duplicate Layer indices are rejected");
                f.Config.blueprintLayerFolders.Remove(duplicate);
                r = f.Compile();
                var map = f.Map(r);
                var author = new GameObject("Layer authoring verification");
                try
                {
                    var manager = author.AddComponent<TileWorldCreatorManager>();
                    manager.configuration = f.Config;
                    var content = author.AddComponent<MapContentAuthoring>();
                    content.TwcConfiguration = f.Config;
                    content.TerrainRules = f.Rules;
                    content.EdgeWidth = 0;
                    var navigation = author.AddComponent<MapNavigationAuthoring>();
                    var validation = TileWorldCreatorMapBaker.CreateValidationGrid(content);
                    try
                    {
                        Check(validation.CellSize == 1 && validation.ElevationWorldStep == 1, "Actual map baker produces Layer heights with unit step");
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(validation);
                    }

                    MapNavigationBaker.Bake(content, map);
                    Check(map.NavigationSurfaces.Length == 2 && map.NavigationSurfaces.All(s => s.Surface == 1), "Actual navigation baker preserves lower surfaces automatically");
                    navigation.Surfaces = new[]
                    {
                        new TwcNavigationLayer
                        {
                            Elevation = 99,
                            SurfaceId = 10
                        }
                    };
                    Reject(() => MapNavigationBaker.Bake(content, map), "Layer mode rejects an independent manually authored navigation height");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(author);
                }

                var bridge = new LayerTerrainConnection
                {
                    Id = 1,
                    EntryLayerGuid = f.G3.guid,
                    ExitLayerGuid = f.G3.guid,
                    EntryCell = new Vector2Int(1, 1),
                    ExitCell = new Vector2Int(1, 5),
                    Width = 3,
                    Bidirectional = true
                };
                var baked = LayerTerrainCompiler.Connection(r, bridge);
                Check(baked.EntryElevation == 3 && baked.Rise == 0 && baked.EntrySurface == 4, "Connection endpoint heights and identities derive from Layer references");
                foreach (var pair in new[]
                {
                    (new Vector2Int(1, 1), new Vector2Int(1, 3)),
                    (new Vector2Int(1, 3), new Vector2Int(3, 3)),
                    (new Vector2Int(3, 3), new Vector2Int(3, 1)),
                    (new Vector2Int(3, 1), new Vector2Int(1, 1))
                }

                )
                {
                    var connection = LayerTerrainCompiler.Connection(r, new LayerTerrainConnection { Id = 7, EntryLayerGuid = f.G0.guid, ExitLayerGuid = f.G0.guid, EntryCell = pair.Item1, ExitCell = pair.Item2, Width = 3 });
                    var first = TerrainConnectionOps.Port(connection.Cell, connection.Size, connection.Rotation, 0, 0);
                    var last = TerrainConnectionOps.Port(connection.Cell, connection.Size, connection.Rotation, 0, connection.Size.y - 1);
                    Check(first.Equals(new int2(pair.Item1.x, pair.Item1.y)) && last.Equals(new int2(pair.Item2.x, pair.Item2.y)), "Clicked endpoint coordinates survive wide-connection rotation " + connection.Rotation);
                }

                map.Connections = new[]
                {
                    baked
                };
                using (var grid = GameWorldMapAuthoring.BuildGrid(map))
                using (var world = new World("Layer terrain test"))
                {
                    var em = world.EntityManager;
                    var root = em.CreateEntity();
                    em.AddComponentData(root, new GridData { Value = grid, CellSize = 1 });
                    em.AddBuffer<Occupancy>(root).Resize(64, NativeArrayOptions.ClearMemory);
                    SurfaceNavigationGraph.Ensure(em, root);
                    var nodes = em.GetBuffer<SurfaceNavNode>(root);
                    using (var copy = nodes.ToNativeArray(Allocator.Temp))
                        Check(copy.Count(n => n.Cell.Equals(new int2(1, 3)) && n.Open != 0) == 2, "Actual navigation retains the bridge corridor and walkable ground below");
                    var occupied = em.GetBuffer<Occupancy>(root);
                    occupied[9] = new Occupancy
                    {
                        Owner = 42,
                        MovementCost = 0
                    };
                    SurfaceNavigationGraph.Ensure(em, root);
                    nodes = em.GetBuffer<SurfaceNavNode>(root);
                    using (var copy = nodes.ToNativeArray(Allocator.Temp))
                        Check(copy.Any(n => n.Cell.Equals(new int2(1, 1)) && n.Surface == 1 && n.Open != 0), "Building reservation at upper XZ does not erase lower navigation");
                }

                bridge.ExitCell = new Vector2Int(2, 5);
                Reject(() => LayerTerrainCompiler.Connection(r, bridge), "Diagonal connections are rejected");
                bridge.ExitCell = new Vector2Int(1, 5);
                bridge.ExitLayerGuid = "missing";
                Reject(() => LayerTerrainCompiler.Connection(r, bridge), "Missing Layer references cannot retain stale heights");
                bridge.ExitLayerGuid = f.G3.guid;
                bridge.Width = 2;
                Reject(() => LayerTerrainCompiler.Connection(r, bridge), "Flat bridges require at least three lanes");
                bridge.Width = 4;
                Reject(() => LayerTerrainCompiler.Connection(r, bridge), "All lanes require valid endpoint surfaces");
                bridge.Width = 3;
                map.Connections = new[]
                {
                    baked,
                    baked
                };
                Reject(() =>
                {
                    using var invalid = GameWorldMapAuthoring.BuildGrid(map);
                }, "Duplicate stable connection IDs are rejected");
                map.Connections = Array.Empty<AuthoredConnection>();
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var content = ref builder.ConstructRoot<BuildingCatalogBlob>();
                    var definitions = builder.Allocate(ref content.Definitions, 1);
                    definitions[0].Metadata.Id = "swamp-only";
                    definitions[0].Footprint = new int2(1);
                    definitions[0].Capabilities.Placement.AllowedTerrains = TerrainType.沼泽;
                    using var catalog = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
                    using var grid = GameWorldMapAuthoring.BuildGrid(map);
                    using var world = new World("Swamp placement test");
                    var em = world.EntityManager;
                    var root = em.CreateEntity();
                    em.AddComponentData(root, new BuildingCatalog { Value = catalog });
                    em.AddComponentData(root, new GridData { Value = grid, CellSize = 1 });
                    em.AddBuffer<Occupancy>(root).Resize(64, NativeArrayOptions.ClearMemory);
                    Check(GridOps.CanPlace(em, root, BuildingId.FromIndex(0), new int2(2, 1), 0, 0) && !GridOps.CanPlace(em, root, BuildingId.FromIndex(0), new int2(3, 1), 0, 0), "Actual building placement accepts swamp and rejects ordinary land");
                    var occupancy = em.GetBuffer<Occupancy>(root);
                    occupancy[10] = new Occupancy
                    {
                        Owner = 12
                    };
                    Check(!GridOps.CanPlace(em, root, BuildingId.FromIndex(0), new int2(2, 1), 0, 0), "Buildings still share a single XZ occupancy reservation");
                }

                var profile = f.Asset<TileWorldCreatorMapBakeProfile>();
                f.Rules.elevationWorldStep = .1f;
                profile.ApplyRules(f.Rules);
                Check(profile.UseLayerBlueprintRules && profile.ElevationWorldStep == 1, "Layer mode ignores obsolete authoring height-step values");
                log.AppendLine("Assertions: " + count);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine("FAIL " + error);
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/layer-terrain-verification.txt", log.ToString());
            }
        }
    }
}
