using System;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static partial class TerrainConnectionVerification
    {
        static void Integration()
        {
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            BuildingCatalogAsset catalog = null;
            MapAsset map = null;
            BuildingDefinitionAsset bridgeDefinition = null;
            using var copies = new CatalogFixture.Scope();
            using var store = new BlobAssetStore(128);
            using var world = new World("Formal building connection integration", WorldFlags.Game);
            try
            {
                var authoring = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GameWorldMapAuthoring>(true)).Single();
                copies.CloneCatalogsOn(authoring.gameObject);
                catalog = authoring.GetComponent<GameContentSetAuthoring>().Content.Buildings;
                bridgeDefinition = UnityEngine.Object.Instantiate(catalog.Definitions.First(d => (d.PlacementAndVisuals.Category & BuildingCategory.Road) != 0));
                bridgeDefinition.Metadata.Id = "verification.connection.bridge";
                bridgeDefinition.Metadata.Name = "验证桥梁";
                bridgeDefinition.Footprint = new Vector2Int(3, 9);
                bridgeDefinition.MaximumLevel = 1;
                bridgeDefinition.MaximumCount = 0;
                bridgeDefinition.ConstructionTurns = 1;
                bridgeDefinition.MovementCost = 1;
                bridgeDefinition.LimitGroup = null;
                bridgeDefinition.Capabilities = new BuildingCapabilitiesSource
                {
                    Connection = new BuildingTerrainConnectionSource
                    {
                        Enabled = true
                    }
                };
                catalog.Definitions = catalog.Definitions.Concat(new[] { bridgeDefinition }).ToArray();
                map = UnityEngine.Object.Instantiate(authoring.Map);
                copies.RemapReferences(map);
                map.MapId = "verification.connections";
                map.Min = Vector2Int.zero;
                map.Size = new Vector2Int(40, 40);
                map.Origin = Vector3.zero;
                map.CellSize = map.ElevationStep = 1;
                map.NavigationSurfaces = Array.Empty<NavigationSurface>();
                map.Connections = Array.Empty<AuthoredConnection>();
                map.Cells = new CellSource[1600];
                for (int z = 0; z < 40; z++)
                    for (int x = 0; x < 40; x++)
                    {
                        int height = x >= 2 && x <= 6 && (z >= 2 && z <= 4 || z >= 12 && z <= 14) ? 3 : 0;
                        map.Cells[z * 40 + x] = new CellSource
                        {
                            Exists = true,
                            Buildable = true,
                            Traversable = true,
                            Height = height,
                            Elevation = height,
                            Terrain = GridOps.TerrainBit("land")
                        };
                    }

                var home = authoring.Map.InitialBuildings.First(b => b.Definition.Capabilities.Housing.Enabled && b.Definition.Capabilities.Housing.Population.Any(row => row.IsCore));
                var originalHome = authoring.Map.Cells[(home.Cell.y - authoring.Map.Min.y) * authoring.Map.Size.x + home.Cell.x - authoring.Map.Min.x];
                for (int i = 0; i < map.Cells.Length; i++)
                    map.Cells[i].Terrain = originalHome.Terrain;
                report.AppendLine("DETAIL formal home size=" + home.Definition.Footprint + "; terrain=" + originalHome.Terrain);
                home.Definition = catalog.Definitions.First(asset => asset.Metadata.Id == home.Definition.Metadata.Id);
                home.Cell = new Vector2Int(25, 25);
                map.InitialBuildings = new[]
                {
                    home
                };
                authoring.Map = map;
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                InvitationExpeditionVerification.FixturePermissions(em, root);
                var definition = BuildingDefinitions.Find(em, root, bridgeDefinition.Metadata.Id);
                BuildingBlueprints.Grant(em, root, definition, 1);
                var command = new BuildRequest
                {
                    Definition = definition,
                    Position = new float3(3.1f, 3, 4.1f)
                };
                Check(BuildingPlacementCommands.CheckBuild(em, root, definition, new int2(3, 4), 0).Allowed, "Formal command preview accepts valid bridge supports");
                Check(BuildingCommandHandler.Execute(em, root, command) == ResultCode.Success, "Formal Build command creates bridge construction");
                Entity Bridge()
                {
                    using var entities = WorldQueries.Entities<Building>(em);
                    foreach (var e in entities)
                        if (em.GetComponentData<BuildingDefinitionRef>(e).Definition == definition)
                            return e;
                    throw new Exception("Bridge missing");
                }

                using (var q = new QueryScope(em, root, .2f))
                    Check(!q.Path(BridgeA, BridgeB), "Formal construction remains disconnected before settlement");
                DailyEconomySettlement.Settle(em, root);
                Check(em.GetComponentData<Building>(Bridge()).Stage == LifeStage.Operational, "Real economy settlement completes bridge construction");
                using (var q = new QueryScope(em, root, .2f))
                    Check(q.Path(BridgeA, BridgeB), "Real construction completion activates bridge navigation");
                var saved = SnapshotCodec.Capture(em, root);
                using (var q = new QueryScope(em, root, .2f))
                    Check(q.Path(BridgeB, BridgeA), "Runtime graph supports reverse route before save");
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Derived navigation graph does not pollute saved authoritative state");
                Check(BuildingPlacementCommands.Move(em, root, Bridge(), new int2(3, 4), 2) == ResultCode.Success, "Formal rotation replaces bridge connection while retaining building identity");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
                using (var q = new QueryScope(em, root, .2f))
                    Check(q.Path(BridgeA, BridgeB), "Save restoration reconstructs bridge connection from building data");
                Check(saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Bridge save/load restores exact authoritative bytes");
                Check(BuildingLifecycle.Demolish(em, root, Bridge()) == ResultCode.Success, "Formal demolition succeeds in daytime");
                using (var q = new QueryScope(em, root, .2f))
                    Check(!q.Path(BridgeA, BridgeB), "Formal demolition removes bridge navigation without ghost links");
                Check(GridOps.CanPlace(em, root, definition, new int2(3, 4), 0), "Demolition releases the full construction footprint");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                if (map != null)
                    UnityEngine.Object.DestroyImmediate(map);
                if (bridgeDefinition != null)
                    UnityEngine.Object.DestroyImmediate(bridgeDefinition);
            }
        }
    }
}
