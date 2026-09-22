using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Definitions;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Landsong.ECS.Editor
{
    public static class DynamicSpawnVerification
    {
        static StringBuilder log;
        static int assertions;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException(label);
            assertions++;
            log.AppendLine("PASS " + label);
        }

        static SpawnRegion[] Regions(EntityManager em, Entity root)
        {
            using var values = em.GetBuffer<SpawnRegion>(root).ToNativeArray(Allocator.Temp);
            return values.ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Dynamic spawn regions")]
        public static string Run()
        {
            log = new StringBuilder().AppendLine(DateTimeOffset.Now.ToString("O"));
            assertions = 0;
            try
            {
                Coverage();
                Fallback();
                Archive();
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
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/dynamic-spawn-verification.txt", log.ToString());
            }
        }

        sealed class Fixture : IDisposable
        {
            public readonly World World = new World("Dynamic spawn fixture");
            public EntityManager Em => World.EntityManager;

            public readonly Entity Root, Core;
            readonly BlobAssetReference<BuildingCatalogBlob> catalog;
            readonly BlobAssetReference<GridBlob> grid;
            public Fixture(int padding, bool divided = false)
            {
                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var blob = ref builder.ConstructRoot<BuildingCatalogBlob>();
                    var definitions = builder.Allocate(ref blob.Definitions, 1);
                    definitions[0].Footprint = new int2(1);
                    definitions[0].PlacementAndVisuals.SpawnExclusionPadding = padding;
                    catalog = builder.CreateBlobAssetReference<BuildingCatalogBlob>(Allocator.Persistent);
                }

                using (var builder = new BlobBuilder(Allocator.Temp))
                {
                    ref var blob = ref builder.ConstructRoot<GridBlob>();
                    blob.Size = new int2(40);
                    var cells = builder.Allocate(ref blob.Cells, 1600);
                    for (int y = 0; y < 40; y++)
                        for (int x = 0; x < 40; x++)
                        {
                            // Deliberately inset and irregular: the painted mask, not the map rectangle, owns fallback.
                            bool edge = x == 36 && y >= 2 && y <= 36;
                            cells[y * 40 + x] = new GridCell
                            {
                                Exists = 1,
                                Buildable = (byte)(edge ? 0 : 1),
                                EdgeZone = (byte)(edge ? 1 : 0),
                                Traversable = (byte)(divided && x == 20 ? 0 : 1)
                            };
                        }

                    grid = builder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent);
                }

                Root = Em.CreateEntity();
                Em.AddComponentData(Root, new BuildingCatalog { Value = catalog });
                Em.AddComponentData(Root, NightRules.Default);
                Em.AddComponentData(Root, new GridData { Value = grid, CellSize = 1 });
                {
                    Em.AddComponentData(Root, new Session { Phase = Phase.Day });
                    Em.AddComponentData(Root, new GameClock() { Turn = 1 });
                    Em.AddComponentData(Root, new SimulationControl() { });
                    Em.AddComponentData(Root, new PopulationState() { });
                    Em.AddComponentData(Root, new PublicOpinionState() { });
                    Em.AddComponentData(Root, new ResearchState() { });
                    Em.AddComponentData(Root, new ExpeditionPenaltyState() { });
                    Em.AddComponentData(Root, new NightRuntimeState() { });
                    Em.AddComponentData(Root, new DaySettlementState() { });
                    Em.AddComponentData(Root, new RetryState() { });
                    Em.AddComponentData(Root, new HeroSelection() { });
                    Em.AddComponentData(Root, new BellState() { });
                    Em.AddComponentData(Root, new IntelligenceModeState() { });
                    Em.AddComponentData(Root, new PersistenceGate() { });
                    Em.AddComponentData(Root, new SimulationRandomState() { State = 99 });
                    Em.AddComponentData(Root, new IdentitySequence() { });
                    Em.AddComponentData(Root, new DynastyIdentity() { });
                }

                var occupancy = Em.AddBuffer<Occupancy>(Root);
                occupancy.ResizeUninitialized(1600);
                for (int i = 0; i < occupancy.Length; i++)
                    occupancy[i] = default;
                Em.AddBuffer<SpawnRegion>(Root);
                Em.AddBuffer<IntelGeometry>(Root);
                Core = Building(new int2(10, 10), new int2(2), 1);
            }

            public Entity Building(int2 cell, int2 size, ulong id)
            {
                var entity = Em.CreateEntity();
                Em.AddComponentData(entity, new Identity { Id = id });
                Em.AddComponentData(entity, new BuildingDefinitionRef { Definition = BuildingId.FromIndex(0) });
                {
                    Em.AddComponentData(entity, new Building { Stage = LifeStage.Operational });
                    Em.AddComponentData(entity, new BuildingPlacementState() { Cell = cell, Size = size });
                    Em.AddComponentData(entity, new BuildingAppearanceState() { });
                    Em.AddComponentData(entity, new BuildingConstructionState() { });
                    Em.AddComponentData(entity, new BuildingWorkforceState() { });
                    Em.AddComponentData(entity, new BuildingHousingState() { });
                    Em.AddComponentData(entity, new BuildingProductionState() { });
                    Em.AddComponentData(entity, new BuildingFarmingState() { });
                    Em.AddComponentData(entity, new BuildingSanctumState() { });
                    Em.AddComponentData(entity, new BuildingGatheringState() { });
                    Em.AddComponentData(entity, new BuildingRecruitmentState() { });
                    Em.AddComponentData(entity, new BuildingMarketState() { });
                    Em.AddComponentData(entity, new BuildingExperienceState() { });
                    Em.AddComponentData(entity, new BuildingMaintenanceState() { });
                }

                {
                    Em.AddComponentData(entity, new BuildingHousingStats { IsCore = (byte)(id == 1 ? 1 : 0) });
                    Em.AddComponentData(entity, new BuildingWorkforceStats() { });
                    Em.AddComponentData(entity, new BuildingStorageStats() { });
                    Em.AddComponentData(entity, new BuildingGarrisonStats() { });
                    Em.AddComponentData(entity, new BuildingQuestStats() { });
                    Em.AddComponentData(entity, new BuildingIntelligenceStats() { });
                    Em.AddComponentData(entity, new BuildingSanctumStats() { });
                    Em.AddComponentData(entity, new BuildingRangeStats() { });
                    Em.AddComponentData(entity, new BuildingNavigationStats() { });
                    Em.AddComponentData(entity, new BuildingBellStats() { });
                }

                Em.AddComponentData(entity, new Health { Current = 10, Maximum = 10 });
                GridOps.Occupy(Em, Root, entity);
                return entity;
            }

            public void Dispose()
            {
                World.Dispose();
                grid.Dispose();
                catalog.Dispose();
            }
        }

        static void Coverage()
        {
            using var fixture = new Fixture(2);
            var em = fixture.Em;
            var root = fixture.Root;
            var second = fixture.Building(new int2(13, 10), new int2(2, 4), 2);
            var space = new NightSpawnOps.Space(em, root);
            var grid = space.Grid;
            int overlap = GridOps.Index(grid, new int2(12, 10));
            Check(space.Coverage[overlap] == 2, "Hidden footprints overlap by count");
            Check(!space.Legal(new int2(8, 8), false) && space.Legal(new int2(7, 8), false), "Padding boundaries use full actual footprint plus configured cells");
            GridOps.Occupy(em, root, second, true);
            em.DestroyEntity(second);
            Check(new NightSpawnOps.Space(em, root).Coverage[overlap] == 1, "Removing one building preserves other hidden coverage");
            var ruined = em.GetComponentData<Building>(fixture.Core);
            ruined.Stage = LifeStage.Ruined;
            em.SetComponentData(fixture.Core, ruined);
            Check(new NightSpawnOps.Space(em, root).Coverage[overlap] == 1, "Ruins retain hidden footprint until removed");
            ruined.Stage = LifeStage.Operational;
            em.SetComponentData(fixture.Core, ruined);
            // Detached buildings surround an uncovered courtyard; no city-component or hole filtering.
            fixture.Building(new int2(20, 10), new int2(2), 3);
            fixture.Building(new int2(10, 20), new int2(2), 4);
            fixture.Building(new int2(20, 20), new int2(2), 5);
            Check(new NightSpawnOps.Space(em, root).Legal(new int2(16, 16), false), "Uncovered interior courtyard is eligible");
            var random = new Random(123);
            space = NightSpawnOps.Generate(em, root, 4, ref random);
            var first = Regions(em, root);
            Check(first.Length >= 2 && first.Length <= 4 && first.All(r => r.EdgeOnly == 0), "Normal night chooses a bounded set of distinct regions");
            for (int i = 0; i < first.Length; i++)
                Check(NightSpatialOps.SpawnPoint(em, root, i, first[i].Center, out var point, space) && space.Legal(GridOps.Cell(grid, point), false), "Actual normal entry obeys hidden coverage and reachability");
            random = new Random(123);
            NightSpawnOps.Generate(em, root, 4, ref random);
            Check(first.SequenceEqual(Regions(em, root)), "Same seed and city layout reproduce exactly the same regions");
            random = new Random(987);
            NightSpawnOps.Generate(em, root, 4, ref random);
            Check(!first.SequenceEqual(Regions(em, root)), "New night seed can choose different regions");
            var before = Regions(em, root);
            var session = em.GetComponentData<Session>(root);
            NightSpawnOps.Repair(em, root, new NightSpawnOps.Space(em, root));
            Check(before.SequenceEqual(Regions(em, root)) && session.Equals(em.GetComponentData<Session>(root)), "Refreshing a legal night keeps its regions and random state");
            var anchor = GridOps.Cell(grid, before[0].Center);
            var added = fixture.Building(anchor, new int2(5), 6);
            space = new NightSpawnOps.Space(em, root);
            NightSpawnOps.Repair(em, root, space);
            Check(NightSpatialOps.SpawnPoint(em, root, 0, before[0].Center, out var moved, space) && space.Legal(GridOps.Cell(grid, moved), false), "Day construction repairs a blocked entry without reserving a hidden no-build zone");
            em.GetBuffer<SpawnRegion>(root).Clear();
            em.GetBuffer<SpawnRegion>(root).Add(space.Region(new int2(36, 20), true, 5));
            NightSpawnOps.Repair(em, root, space);
            Check(em.GetBuffer<SpawnRegion>(root)[0].EdgeOnly == 0, "Day repair returns to normal candidates when uncovered land is available");
            NightSpawnOps.Generate(em, root, 0, ref random);
            Check(em.GetBuffer<SpawnRegion>(root).Length == 0, "No waves create no unused regions");
        }

        static void Fallback()
        {
            using (var fixture = new Fixture(256))
            {
                var em = fixture.Em;
                var root = fixture.Root;
                var random = new Random(42);
                var space = NightSpawnOps.Generate(em, root, 4, ref random);
                var regions = Regions(em, root);
                Check(space.Normal.Count == 0 && regions.Length > 0 && regions.All(r => r.EdgeOnly == 1), "Full hidden coverage falls back to painted edge zones");
                for (int i = 0; i < regions.Length; i++)
                {
                    bool found = NightSpatialOps.SpawnPoint(em, root, i, regions[i].Center, out var point, space);
                    int at = GridOps.Index(space.Grid, GridOps.Cell(space.Grid, point));
                    Check(found && at >= 0 && space.Grid.Value.Value.Cells[at].EdgeZone == 1 && space.Coverage[at] > 0, "Fallback ignores hidden coverage only, uses inset painted edge cells");
                    Check(!GridOps.CanPlace(em, root, BuildingId.FromIndex(0), GridOps.Cell(space.Grid, point), 0), "Edge cells reject building placement");
                }

                var occupancy = em.GetBuffer<Occupancy>(root);
                foreach (var cell in space.Edge)
                    if (!math.all(cell == new int2(36, 20)))
                        occupancy[GridOps.Index(space.Grid, cell)] = new Occupancy
                        {
                            Owner = 999
                        };
                var repairRegions = em.GetBuffer<SpawnRegion>(root);
                repairRegions.Clear();
                repairRegions.Add(space.Region(new int2(10, 10), false, 5));
                repairRegions.Add(space.Region(new int2(12, 10), false, 5));
                NightSpawnOps.Repair(em, root, new NightSpawnOps.Space(em, root));
                Check(NightSpatialOps.SpawnPoint(em, root, 0, default, out var one) && NightSpatialOps.SpawnPoint(em, root, 1, default, out var two) && one.Equals(two), "Shrinking to one legal edge cell lets existing waves share it");
                foreach (var cell in space.Edge)
                    occupancy[GridOps.Index(space.Grid, cell)] = new Occupancy
                    {
                        Owner = 999
                    };
                random = new Random(42);
                NightSpawnOps.Generate(em, root, 4, ref random);
                Check(em.GetBuffer<SpawnRegion>(root).Length == 0, "Fallback never bypasses real occupied cells");
            }

            using (var fixture = new Fixture(256, true))
            {
                var random = new Random(42);
                var space = NightSpawnOps.Generate(fixture.Em, fixture.Root, 4, ref random);
                Check(space.Edge.Count == 0 && fixture.Em.GetBuffer<SpawnRegion>(fixture.Root).Length == 0, "Impassable terrain separating all targets prevents fallback spawn");
            }
        }

        static void Archive()
        {
            const string path = VerificationMap.EntityScene;
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var blobs = new BlobAssetStore(128);
            using var world = new World("Dynamic night persistence", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), blobs);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var settings = em.GetComponentData<NightSettings>(root);
                settings.FirstInvasion = 1;
                settings.InvasionChance = 1;
                settings.FirstBoss = int.MaxValue;
                em.SetComponentData(root, settings);
                GameClock sessionClock = em.GetComponentData<GameClock>(root);
                sessionClock.Turn = 5;
                {
                    em.SetComponentData(root, sessionClock);
                }

                EntityState.Set(em, root, new NightPlanState());
                NightPlanOps.Plan(em, root, false);
                Check(em.GetBuffer<NightWave>(root).Length > 0 && em.GetBuffer<SpawnRegion>(root).Length > 0, "Formal map creates combat waves without authored spawn regions");
                var regions = Regions(em, root);
                var saved = SnapshotCodec.Capture(em, root);
                string fingerprint = SnapshotCodec.ContentFingerprint(em, root);
                em.GetBuffer<SpawnRegion>(root).Clear();
                Check(SnapshotCodec.ContentFingerprint(em, root) == fingerprint, "Dynamic regions are excluded from static content signature");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, saved));
                Check(regions.SequenceEqual(Regions(em, root)) && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Save/load restores selected regions and waves exactly without reroll");
                bool rejected = false;
                try
                {
                    using var transaction = new RestoreTransaction(em, root);
                    em.GetBuffer<SpawnRegion>(transaction.Root).Clear();
                    transaction.Commit(step =>
                    {
                        if (step == "root-published")
                            throw new IOException("probe");
                    });
                }
                catch (IOException)
                {
                    rejected = true;
                }

                Check(rejected && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Publication failure rolls dynamic regions back with the rest of the night");
                var invalid = SnapshotCodec.Decode(em, root, saved);
                invalid.SpawnRegions[0].Center.x = float.NaN;
                rejected = false;
                try
                {
                    SnapshotCodec.Restore(em, root, invalid);
                }
                catch (InvalidDataException)
                {
                    rejected = true;
                }

                Check(rejected && saved.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid dynamic region rejected before live mutation");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
