#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class BuildingModuleVerification
    {
        static GameContentSetAsset Content => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/ECSContent/World/GameWorldTemplate.prefab").GetComponent<GameContentSetAuthoring>().Content;

        static BlobAssetReference<BuildingCatalogBlob> Compile(BuildingCatalogAsset catalog)
        {
            var source = Content;
            return BuildingCatalogCompiler.Build(catalog, new BuildingLimitGroupCatalogIndex(source.BuildingLimitGroups), new CropCatalogIndex(source.Crops), new HeroCatalogIndex(source.Heroes), new ItemCatalogIndex(source.Items), new ItemGroupCatalogIndex(source.ItemGroups), new SoldierCatalogIndex(source.Soldiers), new StorageSlotCatalogIndex(source.StorageSlots), new TechnologyCatalogIndex(source.Technologies));
        }

        public static string Run()
        {
            var report = new StringBuilder();
            int checks = 0;
            void Check(bool value, string text)
            {
                if (!value)
                    throw new InvalidOperationException(text);
                checks++;
                report.AppendLine("PASS " + text);
            }

            var catalog = Content.Buildings;
            try
            {
                using (var compiled = Compile(catalog))
                    Check(compiled.Value.Definitions.Length == catalog.Definitions.Length, "Every current building definition compiles");
                foreach (var source in catalog.Definitions)
                {
                    ContentCompilationVerification.VerifySerialization(source);
                    Check(true, "Current building module values and references survive serialization: " + source.Metadata.Id);
                }

                var palace = UnityEngine.Object.Instantiate(catalog.Definitions.First(asset => asset.Metadata.Id == "b王宫"));
                try
                {
                    var data = palace;
                    var modules = data.Capabilities;
                    var original = data.Capabilities.Garrison.InitialUnits[0].Count;
                    void Reject(Action mutation, Action restore, string message)
                    {
                        mutation();
                        bool failed = false;
                        try
                        {
                            BuildingCatalogValidation.Validate(data);
                        }
                        catch (InvalidOperationException)
                        {
                            failed = true;
                        }
                        finally
                        {
                            restore();
                        }

                        Check(failed, message);
                    }

                    Reject(() => data.Capabilities.Garrison.InitialUnits[0].Count = modules.Garrison.Levels.Max(row => row.Capacity) + 1, () => data.Capabilities.Garrison.InitialUnits[0].Count = original, "Initial garrison cannot exceed capacity");
                    var soldier = data.Capabilities.Garrison.InitialUnits[0].Soldier;
                    Check(typeof(BuildingInitialGarrisonSource).GetField("Soldier").FieldType == typeof(SoldierDefinitionAsset), "Wrong content domain is excluded by the typed source field");
                    Reject(() => data.Capabilities = null, () => data.Capabilities = modules, "Missing typed building configuration is rejected");
                    var level = data.Capabilities.Garrison.Levels[0].Level;
                    Reject(() => data.Capabilities.Garrison.Levels[0].Level = data.MaximumLevel + 1, () => data.Capabilities.Garrison.Levels[0].Level = level, "Module level beyond authored maximum rejected");
                    var housing = modules.Housing;
                    modules.Housing = new BuildingHousingSource
                    {
                        Enabled = true,
                        Residences = new[]
                        {
                            new BuildingResidenceLevelSource
                            {
                                Capacity = 1,
                                GrowthInterval = 1
                            }
                        }
                    };
                    BuildingCatalogValidation.Validate(data);
                    Reject(() => modules.Housing.Residences[0].GrowthInterval = 0, () => modules.Housing.Residences[0].GrowthInterval = 1, "Odin positive integer minimum remains enforced outside the inspector");
                    modules.Housing = housing;
                    var units = data.Capabilities.Garrison.InitialUnits;
                    data.Capabilities.Garrison.InitialUnits = new[]
                    {
                        new BuildingInitialGarrisonSource
                        {
                            Level = level,
                            Soldier = soldier,
                            Count = 1
                        },
                        new BuildingInitialGarrisonSource
                        {
                            Level = level,
                            Soldier = soldier,
                            Count = 1
                        }
                    };
                    BuildingCatalogValidation.Validate(data);
                    Check(data.Capabilities.Garrison.InitialUnits.Length == 2, "Multiple initial unit groups compile independently");
                    data.Capabilities.Garrison.InitialUnits = units;
                    data.Capabilities.Garrison.Enabled = false;
                    var isolated = CatalogFixture.Clone(catalog);
                    try
                    {
                        var at = Array.FindIndex(isolated.Definitions, asset => asset.Metadata.Id == data.Metadata.Id);
                        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(data), isolated.Definitions[at]);
                        using var disabled = Compile(isolated);
                        Check(!disabled.Value.Definitions[at].Capabilities.Garrison.Enabled && disabled.Value.Definitions[at].Capabilities.Garrison.InitialUnits.Length == 0 && disabled.Value.Definitions[at].Capabilities.Garrison.Levels.Length == 0, "Disabling garrison removes both capacity and starting units");
                    }
                    finally
                    {
                        CatalogFixture.Destroy(isolated);
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(palace);
                }

                VerifyWorkerTierConfiguration(catalog, Check);
                VerifyLevelExecution(catalog, Check);
                report.AppendLine("Assertions: " + checks);
                return report.ToString();
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/building-modules-verification.txt", report.ToString());
            }
        }

        static void VerifyWorkerTierConfiguration(BuildingCatalogAsset catalog, Action<bool, string> check)
        {
            var farm = UnityEngine.Object.Instantiate(catalog.Definitions.First(asset => asset.Metadata.Id == "b农田"));
            try
            {
                var workforce = farm.Capabilities.Workforce;
                var farming = farm.Capabilities.Farming;
                check(farming.RequiredWorkers == 2 && farming.FullCycleBonusWorkers == 3 && farming.FullCycleYieldBonusPercent == 50, "Farm owns the shared crop workforce and full-cycle yield rules");
                farming.FullCycleBonusWorkers = workforce.Levels[0].Capacity + 1;
                bool rejectedFarming = false;
                try
                {
                    BuildingCatalogValidation.Validate(farm);
                }
                catch (InvalidOperationException)
                {
                    rejectedFarming = true;
                }
                finally
                {
                    farming.FullCycleBonusWorkers = 3;
                }
                check(rejectedFarming, "Farm yield threshold cannot exceed its workforce capacity");
                var original = workforce.EfficiencyTiers;
                var attraction = workforce.Levels[0].BaseAttraction;
                bool rejectedAttraction = false;
                try
                {
                    workforce.Levels[0].BaseAttraction = -1;
                    BuildingCatalogValidation.Validate(farm);
                }
                catch (InvalidOperationException)
                {
                    rejectedAttraction = true;
                }
                finally
                {
                    workforce.Levels[0].BaseAttraction = attraction;
                }

                check(rejectedAttraction, "Odin float minimum remains enforced outside the inspector");
                void Reject(BuildingWorkerEfficiencyTierSource[] tiers, string message)
                {
                    workforce.EfficiencyTiers = tiers;
                    bool failed = false;
                    try
                    {
                        BuildingCatalogValidation.Validate(farm);
                    }
                    catch (InvalidOperationException)
                    {
                        failed = true;
                    }
                    finally
                    {
                        workforce.EfficiencyTiers = original;
                    }

                    check(failed, message);
                }

                BuildingWorkerEfficiencyTierSource Tier(int min, int max) => new BuildingWorkerEfficiencyTierSource
                {
                    Level = 1,
                    MinimumWorkers = min,
                    MaximumWorkers = max
                };
                Reject(Array.Empty<BuildingWorkerEfficiencyTierSource>(), "Missing worker tiers are rejected instead of inferred");
                Reject(new[] { Tier(0, 1), Tier(1, 3) }, "Overlapping authored worker ranges rejected");
                Reject(new[] { Tier(0, 0), Tier(2, 3) }, "Gaps in authored worker ranges rejected");
                Reject(new[] { Tier(0, 4) }, "Worker range exceeding capacity rejected");
                Reject(new[] { Tier(0, 2), Tier(3, 3) }, "A range crossing an actual crop worker threshold is rejected");
                workforce.EfficiencyTiers = new[]
                {
                    Tier(3, 3),
                    Tier(0, 1),
                    Tier(2, 2)
                };
                BuildingCatalogValidation.Validate(farm);
                check(workforce.EfficiencyTiers.Length == 3, "Explicit equal-effect 0-1 range accepted without generating per-worker rows");
                var copy = UnityEngine.Object.Instantiate(farm);
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(farm), copy);
                    check(copy.Capabilities.Workforce.EfficiencyTiers.Select(t => t.Level + ":" + t.MinimumWorkers + ":" + t.MaximumWorkers).SequenceEqual(workforce.EfficiencyTiers.Select(t => t.Level + ":" + t.MinimumWorkers + ":" + t.MaximumWorkers)), "Authored worker tier order and bounds survive serialization");
                    check(copy.Capabilities.Farming.RequiredWorkers == farming.RequiredWorkers && copy.Capabilities.Farming.FullCycleBonusWorkers == farming.FullCycleBonusWorkers && copy.Capabilities.Farming.FullCycleYieldBonusPercent == farming.FullCycleYieldBonusPercent, "Farm workforce rules survive authoring serialization");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(copy);
                }

                using var compiled = Compile(catalog);
                check(compiled.Value.Definitions.Length > 0 && farm.Capabilities.Workforce.EfficiencyTiers.Length == 3 && farm.Capabilities.Workforce.EfficiencyTiers[1].MaximumWorkers == 1, "One-way compilation preserves authored tier metadata and order");
                var farmId = new BuildingCatalogIndex(catalog).Resolve(catalog.Definitions.First(asset => asset.Metadata.Id == "b农田"));
                ref var bakedFarming = ref compiled.Value.Definitions[farmId.Index].Capabilities.Farming;
                check(bakedFarming.RequiredWorkers == 2 && bakedFarming.FullCycleBonusWorkers == 3 && bakedFarming.FullCycleYieldBonusPercent == 50, "Compiled farm carries shared crop rules");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(farm);
            }
        }

        static void VerifyLevelExecution(BuildingCatalogAsset catalog, Action<bool, string> check)
        {
            var modified = CatalogFixture.Clone(catalog);
            var farm = modified.Definitions.First(asset => asset.Metadata.Id == "b农田");
            var scene = EditorSceneManager.OpenPreviewScene(VerificationMap.EntityScene);
            using var store = new BlobAssetStore(128);
            using var world = new World("Building module level execution", WorldFlags.Game);
            try
            {
                var crop = farm.Capabilities.Farming.Crops[0].Crop;
                farm.MaximumLevel = 2;
                farm.Capabilities.Farming.Crops = new[]
                {
                    new BuildingAllowedCropSource
                    {
                        Level = 2,
                        Crop = crop
                    }
                };
                var levelOneWorkforce = farm.Capabilities.Workforce.Levels[0];
                farm.Capabilities.Workforce.Levels = new[]
                {
                    levelOneWorkforce,
                    new BuildingWorkforceLevelSource
                    {
                        Level = 2,
                        Currency = levelOneWorkforce.Currency,
                        Capacity = levelOneWorkforce.Capacity,
                        InitialWorkers = levelOneWorkforce.InitialWorkers,
                        InitialSubsidy = levelOneWorkforce.InitialSubsidy,
                        BaseAttraction = levelOneWorkforce.BaseAttraction,
                        RecruitmentCost = levelOneWorkforce.RecruitmentCost
                    }
                };
                var levelTwoTiers = farm.Capabilities.Workforce.EfficiencyTiers.Select(tier => new BuildingWorkerEfficiencyTierSource
                {
                    Level = 2,
                    MinimumWorkers = tier.MinimumWorkers,
                    MaximumWorkers = tier.MaximumWorkers
                }).ToArray();
                farm.Capabilities.Workforce.EfficiencyTiers = farm.Capabilities.Workforce.EfficiencyTiers.Concat(levelTwoTiers).ToArray();
                var gold = Content.Items.Definitions.First(d => d.Metadata.Id == "金币");
                farm.Capabilities.Gathering.Enabled = true;
                farm.Capabilities.Gathering.Levels = new[]
                {
                    new BuildingGatheringLevelSource
                    {
                        Level = 0,
                        Uses = 1
                    }
                };
                farm.Capabilities.Gathering.Rewards = new[]
                {
                    new BuildingGatheringRewardSource
                    {
                        Level = 0,
                        Item = gold,
                        Quantity = 1
                    },
                    new BuildingGatheringRewardSource
                    {
                        Level = 1,
                        Item = gold,
                        Quantity = 2
                    },
                    new BuildingGatheringRewardSource
                    {
                        Level = 2,
                        Item = gold,
                        Quantity = 5
                    }
                };
                using var compiled = Compile(modified);
                farm.Capabilities.Workforce.EfficiencyTiers = new[]
                {
                    new BuildingWorkerEfficiencyTierSource
                    {
                        Level = 1,
                        MinimumWorkers = 3,
                        MaximumWorkers = 3
                    },
                    new BuildingWorkerEfficiencyTierSource
                    {
                        Level = 1,
                        MinimumWorkers = 0,
                        MaximumWorkers = 1
                    },
                    new BuildingWorkerEfficiencyTierSource
                    {
                        Level = 1,
                        MinimumWorkers = 2,
                        MaximumWorkers = 2
                    }
                }.Concat(levelTwoTiers).ToArray();
                using var groupedTiers = Compile(modified);
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var original = em.GetComponentData<BuildingCatalog>(root);
                var replacement = original;
                replacement.Value = compiled;
                em.SetComponentData(root, replacement);
                try
                {
                    PersistenceGate statePersistence = em.GetComponentData<PersistenceGate>(root);
                    statePersistence.CheckpointPending = 0;
                    {
                        em.SetComponentData(root, statePersistence);
                    }

                    var farmId = new BuildingCatalogIndex(modified).Resolve(farm);
                    var goldId = new ItemCatalogIndex(Content.Items).Resolve(gold);
                    var cropId = new CropCatalogIndex(Content.Crops).Resolve(crop);
                    InventoryOps.Remove(em, root, goldId, InventoryOps.Count(em, root, goldId));
                    Entity Gathering(int level)
                    {
                        var e = em.CreateEntity();
                        em.AddComponentData(e, new Identity { Id = (ulong)(ulong.MaxValue - 10 - (ulong)level), Name = "采集级别验证" });
                        em.AddComponentData(e, new BuildingDefinitionRef { Definition = farmId });
                        em.AddComponentData(e, new SimulationOwner { Root = root });
                        em.AddComponentData(e, new Building { Stage = LifeStage.Operational, Level = level });
                        em.AddComponentData(e, new BuildingFarmingState());
                        em.AddComponentData(e, new BuildingGatheringState { RemainingUses = 1 });
                        em.AddComponentData(e, new BuildingPlacementState());
                        em.AddComponentData(e, new BuildingNavigationStats());
                        return e;
                    }

                    var harvest1 = Gathering(1);
                    var id1 = em.GetComponentData<Identity>(harvest1).Id;
                    check(BuildingCommandHandler.Execute(em, root, new HarvestBuildingRequest { Building = id1 }) == ResultCode.Success && InventoryOps.Count(em, root, goldId) == 3, "Level 1 gathering rewards combine common and matching level only");
                    InventoryOps.Remove(em, root, goldId, 3);
                    var harvest2 = Gathering(2);
                    var id2 = em.GetComponentData<Identity>(harvest2).Id;
                    check(BuildingCommandHandler.Execute(em, root, new HarvestBuildingRequest { Building = id2 }) == ResultCode.Success && InventoryOps.Count(em, root, goldId) == 6, "Level 2 gathering rewards exclude previous level");
                    var field = em.CreateEntity();
                    ulong id = ulong.MaxValue - 1;
                    em.AddComponentData(field, new Identity { Id = id, Name = "等级种植验证" });
                    em.AddComponentData(field, new BuildingDefinitionRef { Definition = farmId });
                    em.AddComponentData(field, new SimulationOwner { Root = root });
                    {
                        em.AddComponentData(field, new Building { Level = 1, Stage = LifeStage.Operational });
                        em.AddComponentData(field, new BuildingPlacementState() { });
                        em.AddComponentData(field, new BuildingAppearanceState() { });
                        em.AddComponentData(field, new BuildingConstructionState() { });
                        em.AddComponentData(field, new BuildingWorkforceState() { });
                        em.AddComponentData(field, new BuildingHousingState() { });
                        em.AddComponentData(field, new BuildingProductionState() { });
                        em.AddComponentData(field, new BuildingFarmingState() { Crop = CropId.None });
                        em.AddComponentData(field, new BuildingSanctumState() { });
                        em.AddComponentData(field, new BuildingGatheringState() { });
                        em.AddComponentData(field, new BuildingRecruitmentState() { });
                        em.AddComponentData(field, new BuildingMarketState() { });
                        em.AddComponentData(field, new BuildingExperienceState() { });
                        em.AddComponentData(field, new BuildingMaintenanceState() { Maintained = 1 });
                    }

                    {
                        em.AddComponentData(field, new BuildingHousingStats());
                        em.AddComponentData(field, new BuildingWorkforceStats() { });
                        em.AddComponentData(field, new BuildingStorageStats() { });
                        em.AddComponentData(field, new BuildingGarrisonStats() { });
                        em.AddComponentData(field, new BuildingQuestStats() { });
                        em.AddComponentData(field, new BuildingIntelligenceStats() { });
                        em.AddComponentData(field, new BuildingSanctumStats() { });
                        em.AddComponentData(field, new BuildingRangeStats() { });
                        em.AddComponentData(field, new BuildingNavigationStats() { });
                        em.AddComponentData(field, new BuildingBellStats() { });
                    }

                    var beforeTierEdit = Landsong.ECS.Persistence.SnapshotCodec.Capture(em, root);
                    replacement.Value = groupedTiers;
                    em.SetComponentData(root, replacement);
                    var tiers = WorkerEfficiencyOps.Tiers(em, root, farmId, 1);
                    check(tiers.Count == 3 && tiers[0].MinimumWorkers == 0 && tiers[0].MaximumWorkers == 1 && tiers[2].MinimumWorkers == 3, "Shared worker query reads and sorts explicit baked ranges without reverse inference");
                    check(WorkerEfficiencyOps.Tiers(em, root, farmId, 2).Count == levelTwoTiers.Length, "Second-level farm retains explicit worker tiers");
                    check(beforeTierEdit.SequenceEqual(Landsong.ECS.Persistence.SnapshotCodec.Capture(em, root)), "Changing only display tier grouping preserves gameplay content signature and snapshot bytes");
                    replacement.Value = compiled;
                    em.SetComponentData(root, replacement);
                    ref var plantingCosts = ref CropDefinitions.Get(em, root, cropId).PlantingCosts;
                    for (int i = 0; i < plantingCosts.Length; i++)
                        InventoryOps.Add(em, root, plantingCosts[i].Item, plantingCosts[i].Quantity);
                    var plant = new PlantCropRequest
                    {
                        Building = id,
                        Crop = cropId
                    };
                    check(GameRequestExecution.Execute(em, root, plant) == ResultCode.Unavailable && !em.GetComponentData<BuildingFarmingState>(field).Crop.IsValid, "Level 1 cannot plant a crop configured only for level 2");
                    var building = em.GetComponentData<Building>(field);
                    building.Level = 2;
                    em.SetComponentData(field, building);
                    check(GameRequestExecution.Execute(em, root, plant) == ResultCode.Success && em.GetComponentData<BuildingFarmingState>(field).Crop == cropId, "Matching building level accepts the configured crop");
                }
                finally
                {
                    em.SetComponentData(root, original);
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                CatalogFixture.Destroy(modified);
            }
        }
    }
}
#endif
