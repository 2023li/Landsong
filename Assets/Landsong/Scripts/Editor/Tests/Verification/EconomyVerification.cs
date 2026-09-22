#if UNITY_EDITOR
using System;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class EconomyVerification
    {
        static StringBuilder log;
        static int checks;
        static void Check(bool value, string label)
        {
            if (!value)
                throw new InvalidOperationException("FAIL " + label);
            checks++;
            log.AppendLine("PASS " + label);
        }

        static T[] Rows<T>(EntityManager em, Entity root)
            where T : unmanaged, IBufferElementData
        {
            if (!em.HasBuffer<T>(root))
                return Array.Empty<T>();
            using var rows = em.GetBuffer<T>(root).ToNativeArray(Allocator.Temp);
            return rows.ToArray();
        }

        static Entity[] Entities(EntityManager em)
        {
            using var q = em.CreateEntityQuery(new EntityQueryDesc { Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab });
            using var a = q.ToEntityArray(Allocator.Temp);
            return a.ToArray().OrderBy(e => e.Index).ThenBy(e => e.Version).ToArray();
        }

        [MenuItem("Landsong/ECS/Verification/Economy")]
        public static string Run()
        {
            log = new StringBuilder();
            checks = 0;
            try
            {
                Food();
                foreach (var path in Landsong.EditorTools.GameMapPaths.BakedScenes())
                    Map(path);
                log.AppendLine("Assertions: " + checks);
                return log.ToString();
            }
            catch (Exception error)
            {
                log.AppendLine(error.ToString());
                throw;
            }
            finally
            {
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/economy-verification.txt", log.ToString());
            }
        }

        static void Food()
        {
            using var world = new World("Food matching fixture");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            {
                em.AddComponentData(root, new Session { });
                em.AddComponentData(root, new GameClock() { Turn = 1 });
                em.AddComponentData(root, new SimulationControl() { });
                em.AddComponentData(root, new PopulationState() { });
                em.AddComponentData(root, new PublicOpinionState() { });
                em.AddComponentData(root, new ResearchState() { });
                em.AddComponentData(root, new ExpeditionPenaltyState() { });
                em.AddComponentData(root, new NightRuntimeState() { });
                em.AddComponentData(root, new DaySettlementState() { });
                em.AddComponentData(root, new RetryState() { });
                em.AddComponentData(root, new HeroSelection() { });
                em.AddComponentData(root, new BellState() { });
                em.AddComponentData(root, new IntelligenceModeState() { });
                em.AddComponentData(root, new PersistenceGate() { });
                em.AddComponentData(root, new SimulationRandomState() { });
                em.AddComponentData(root, new IdentitySequence() { });
                em.AddComponentData(root, new DynastyIdentity() { });
            }

            em.AddBuffer<InventorySlot>(root);
            em.AddBuffer<PendingItem>(root);
            using var fixture = new ResidentialFoodTestFixture();
            fixture.Install(em, root);
            var house = em.CreateEntity();
            em.AddComponentData(house, new BuildingDefinitionRef { Definition = BuildingId.FromIndex(0) });
            em.AddComponentData(house, new Identity { Id = 1, Name = "测试住宅" });
            {
                em.AddComponentData(house, new Building { Level = 1 });
                em.AddComponentData(house, new BuildingPlacementState() { });
                em.AddComponentData(house, new BuildingAppearanceState() { });
                em.AddComponentData(house, new BuildingConstructionState() { });
                em.AddComponentData(house, new BuildingWorkforceState() { });
                em.AddComponentData(house, new BuildingHousingState() { Population = 2 });
                em.AddComponentData(house, new BuildingProductionState() { });
                em.AddComponentData(house, new BuildingFarmingState() { });
                em.AddComponentData(house, new BuildingSanctumState() { });
                em.AddComponentData(house, new BuildingGatheringState() { });
                em.AddComponentData(house, new BuildingRecruitmentState() { });
                em.AddComponentData(house, new BuildingMarketState() { });
                em.AddComponentData(house, new BuildingExperienceState() { });
                em.AddComponentData(house, new BuildingMaintenanceState() { });
            }

            em.AddBuffer<FoodSelection>(house);
            void Stocks(int a, int b)
            {
                var slots = em.GetBuffer<InventorySlot>(root);
                slots.Clear();
                slots.Add(new InventorySlot { Item = ItemId.FromIndex(0), Count = a, SlotType = StorageSlotId.None });
                slots.Add(new InventorySlot { Item = ItemId.FromIndex(1), Count = b, SlotType = StorageSlotId.None });
            }

            Stocks(10, 5);
            EconomyJournalOps.Begin(em, root, false);
            Check(ResidentialFoodOps.Plan(em, root, house, out var plan) && plan[0].Item == ItemId.FromIndex(1) && plan[1].Item == ItemId.FromIndex(0), "Overlapping groups find complete meal despite narrow group competing for highest stock");
            Check(ResidentialFoodOps.Pay(em, root, house) && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 8 && InventoryOps.Count(em, root, ItemId.FromIndex(1)) == 3, "Complete recipe consumes population-scaled quantities once");
            Check(Rows<FoodSelection>(em, house).Length == 2 && Rows<EconomyEntry>(em, root).Sum(r => r.Delta) == -4, "Actual food selections and actual journal agree");
            Check(ResidentialFoodOps.Pay(em, root, house) && !ResidentialFoodOps.Pay(em, root, house), "Consecutive houses compete against shared remaining stock");
            var before = Rows<InventorySlot>(em, root);
            var journal = Rows<EconomyEntry>(em, root);
            Check(!ResidentialFoodOps.Pay(em, root, house) && before.SequenceEqual(Rows<InventorySlot>(em, root)) && journal.SequenceEqual(Rows<EconomyEntry>(em, root)) && Rows<FoodSelection>(em, house).Length == 0, "Incomplete meal leaves no partial charges or stale selections");
            fixture.UseBroadRecipe(em, root);
            Stocks(10, 10);
            Check(ResidentialFoodOps.Plan(em, root, house, out plan) && plan[0].Item == ItemId.FromIndex(1), "Equal stocks use stable item ID, not catalog order");
            Stocks(11, 10);
            Check(ResidentialFoodOps.Plan(em, root, house, out plan) && plan[0].Item == ItemId.FromIndex(0), "Higher total inventory has first preference");
            Stocks(3, 100);
            before = Rows<InventorySlot>(em, root);
            journal = Rows<EconomyEntry>(em, root);
            Check(!RewardDelivery.Apply(em, root, ref fixture.Rewards.Value) && before.SequenceEqual(Rows<InventorySlot>(em, root)), "Partial reward capacity failure rolls back every item");
            Check(Rows<EconomyEntry>(em, root).Count(r => r.Delta != 0) == journal.Count(r => r.Delta != 0), "Rolled-back reward has no phantom monetary rows");
            using (var payment = new InventoryTransaction(em, root))
            {
                InventoryOps.Remove(em, root, ItemId.FromIndex(0), 2);
                payment.Reject("测试回滚");
            }

            Check(before.SequenceEqual(Rows<InventorySlot>(em, root)), "Rejected transaction restores resources and keeps diagnostic only");
            EconomyJournalOps.End(em, root);
            var length = Rows<EconomyEntry>(em, root).Length;
            InventoryOps.Remove(em, root, ItemId.FromIndex(0), 1);
            Check(Rows<EconomyEntry>(em, root).Length == length, "Manual day actions do not contaminate last-settlement journal");
        }

        static void Map(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Wave three isolated map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                var initial = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                var bytes = SnapshotCodec.Capture(em, root);
                var entities = Entities(em);
                var state = em.GetComponentData<Session>(root);
                GameClock stateClock = em.GetComponentData<GameClock>(root);
                Check(GameRequestExecution.Execute(em, root, new InventoryForecastRequest()) == ResultCode.Success, "Forecast command succeeds in day");
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)) && state.Equals(em.GetComponentData<Session>(root)), "Forecast preserves all original entities, inventory, RNG and session");
                var model = Rows<EconomyForecastEntry>(em, root);
                var modelState = em.GetComponentData<EconomyForecastState>(root);
                Check(modelState.Fingerprint.ToString() == EconomyForecastOps.Fingerprint(em, root), "Forecast is bound to captured day fingerprint");
                var failed = false;
                try
                {
                    EconomyForecastOps.Create(em, root, step => throw new IOException("Owned forecast probe"));
                }
                catch (IOException)
                {
                    failed = true;
                }

                Check(failed && bytes.SequenceEqual(SnapshotCodec.Capture(em, root)) && entities.SequenceEqual(Entities(em)) && model.SequenceEqual(Rows<EconomyForecastEntry>(em, root)), "Forecast failure restores root buffers and prior forecast, no candidate leak");
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = gold, Amount = 1 });
                Check(modelState.Fingerprint.ToString() != EconomyForecastOps.Fingerprint(em, root), "Changed day invalidates cached forecast");
                SnapshotCodec.Restore(em, root, initial);
                Check(em.GetComponentData<EconomyForecastState>(root).Fingerprint.IsEmpty, "Loading a node clears transient forecast");
                var itemCount = ItemDefinitions.Count(em, root);
                var counts = Enumerable.Range(0, itemCount).Select(i => InventoryOps.Count(em, root, ItemId.FromIndex(i)) + InventoryOps.PendingCount(em, root, ItemId.FromIndex(i))).ToArray();
                DailyEconomySettlement.Settle(em, root);
                var entries = Rows<EconomyEntry>(em, root);
                for (var i = 0; i < itemCount; i++)
                    Check(entries.Where(r => r.Item == ItemId.FromIndex(i)).Sum(r => (long)r.Delta) == (long)InventoryOps.Count(em, root, ItemId.FromIndex(i)) + InventoryOps.PendingCount(em, root, ItemId.FromIndex(i)) - counts[i], "Actual journal reconciles item " + ItemDefinitions.Get(em, root, ItemId.FromIndex(i)).Metadata.Id);
                Check(em.GetComponentData<EconomyJournalState>(root).Recording == 0 && em.GetComponentData<GameClock>(root).Turn == stateClock.Turn, "Recording closes and dusk does not increment turn");
                bytes = SnapshotCodec.Capture(em, root);
                DailyEconomySettlement.Settle(em, root);
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Settlement and journal are idempotent");
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, bytes));
                Check(entries.SequenceEqual(Rows<EconomyEntry>(em, root)), "Version-four snapshot preserves actual journal");
                var pending = em.GetBuffer<PendingItem>(root);
                pending.Add(new PendingItem { Item = gold, Amount = 17 });
                EconomyJournalOps.DiscardPending(em, root);
                Check(InventoryOps.PendingCount(em, root, gold) == 0 && Rows<EconomyEntry>(em, root).Any(r => r.Reason == EconomyReason.NightDiscard && r.Delta == -17 && r.Pending == 1), "Confirmed night discard recorded separately from ordinary spending");
                SnapshotCodec.Restore(em, root, initial);
                Vertical(em, root);
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        static void Vertical(EntityManager em, Entity root)
        {
            using var fixture = new BuildingCatalogTestFixture(em, root);
            var definitions = fixture.Buildings.Definitions;
            var core = Entity.Null;
            using (var all = WorldQueries.OrderedEntities<Building>(em))
                foreach (var e in all)
                    if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                        core = e;
            var coreDef = em.GetComponentData<BuildingDefinitionRef>(core).Definition;
            var otherDef = BuildingId.FromIndex(Array.FindIndex(definitions, d => d.Metadata.Id != definitions[coreDef.Index].Metadata.Id && d.Footprint.x > 0 && d.Footprint.y > 0));
            var grid = em.GetComponentData<GridData>(root);
            var cell = grid.Value.Value.Min;
            var found = false;
            for (var y = 0; y < grid.Value.Value.Size.y && !found; y++)
                for (var x = 0; x < grid.Value.Value.Size.x && !found; x++)
                {
                    cell = grid.Value.Value.Min + new int2(x, y);
                    found = GridOps.CanPlace(em, root, otherDef, cell, 0);
                }

            Check(found, "Vertical fixture finds a legal second footprint");
            var second = BuildingCreation.Create(em, root, otherDef, cell, 0, 1, true);
            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            var wood = ItemId.FromIndex(Enumerable.Range(0, fixture.Items.Definitions.Length).First(i => i != gold.Index));
            var coin = fixture.Items.Definitions[gold.Index];
            var material = fixture.Items.Definitions[wood.Index];
            var coreSource = definitions[coreDef.Index];
            var otherSource = definitions[otherDef.Index];
            var preserved = coreSource.Capabilities;
            coreSource.Capabilities = new BuildingCapabilitiesSource
            {
                Housing = new BuildingHousingSource
                {
                    Enabled = true,
                    Population = preserved.Housing.Population
                },
                Storage = preserved.Storage,
                Quests = preserved.Quests,
                Garrison = preserved.Garrison,
                Production = new BuildingProductionSource
                {
                    Enabled = true,
                    Cycles = new[]
                    {
                        new BuildingProductionCycleSource
                        {
                            Level = 0,
                            Interval = 1
                        }
                    },
                    Outputs = new[]
                    {
                        new BuildingProductionOutputSource
                        {
                            Level = 0,
                            Item = material,
                            Quantity = 7
                        }
                    }
                }
            };
            otherSource.Capabilities = new BuildingCapabilitiesSource
            {
                Maintenance = new BuildingMaintenanceSource
                {
                    Enabled = true,
                    Costs = new[]
                    {
                        new BuildingMaintenanceCostSource
                        {
                            Level = 0,
                            Item = material,
                            Quantity = 7
                        }
                    }
                },
                Production = new BuildingProductionSource
                {
                    Enabled = true,
                    Cycles = new[]
                    {
                        new BuildingProductionCycleSource
                        {
                            Level = 0,
                            Interval = 1
                        }
                    },
                    Outputs = new[]
                    {
                        new BuildingProductionOutputSource
                        {
                            Level = 0,
                            Item = coin,
                            Quantity = 10
                        }
                    }
                }
            };
            otherSource.ConstructionTurns = 1;
            foreach (var item in fixture.Items.Definitions)
                item.NaturalLossRate = 0;
            fixture.Apply();
            try
            {
                using (var all = WorldQueries.OrderedEntities<Building>(em))
                    foreach (var e in all)
                    {
                        var b = em.GetComponentData<Building>(e);
                        BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(e);
                        BuildingHousingState bHousing = em.GetComponentData<BuildingHousingState>(e);
                        bWorkforce.Workers = bHousing.Population = 0;
                        bWorkforce.Subsidy = 0;
                        if (e != core && e != second)
                            b.Stage = LifeStage.Ruined;
                        {
                            em.SetComponentData(e, b);
                            em.SetComponentData(e, bWorkforce);
                            em.SetComponentData(e, bHousing);
                        }

                        BuildingLevelConfiguration.Apply(em, root, e, false);
                    }

                var slots = em.GetBuffer<InventorySlot>(root);
                for (var i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    s.Item = ItemId.None;
                    s.Count = 0;
                    s.LossRemainder = 0;
                    slots[i] = s;
                }

                var day = SnapshotCodec.Decode(em, root, SnapshotCodec.Capture(em, root));
                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success, "Deterministic vertical fixture forecasts");
                var forecast = Rows<EconomyForecastEntry>(em, root).Select(r => r.Value).Where(r => r.Delta != 0).ToArray();
                DailyEconomySettlement.Settle(em, root);
                Check(em.GetComponentData<BuildingMaintenanceState>(second).Maintained == 1 && InventoryOps.Count(em, root, wood) == 0 && InventoryOps.Count(em, root, gold) == 10, "Earlier building output funds later maintenance in the same settlement");
                Check(forecast.SequenceEqual(Rows<EconomyEntry>(em, root).Where(r => r.Delta != 0)), "Deterministic forecast monetary rows match actual vertical settlement");
                var secondId = em.GetComponentData<Identity>(second).Id;
                SnapshotCodec.Restore(em, root, day);
                second = WorldQueries.Find(em, secondId);
                var construction = em.GetComponentData<Building>(second);
                BuildingConstructionState constructionConstruction = em.GetComponentData<BuildingConstructionState>(second);
                construction.Stage = LifeStage.Construction;
                constructionConstruction.Progress = 0;
                {
                    em.SetComponentData(second, construction);
                    em.SetComponentData(second, constructionConstruction);
                }

                DailyEconomySettlement.Settle(em, root);
                Check(em.GetComponentData<Building>(second).Stage == LifeStage.Operational && InventoryOps.Count(em, root, gold) == 0, "Newly completed construction cannot produce until next settlement");
                SnapshotCodec.Restore(em, root, day);
                second = WorldQueries.Find(em, secondId);
                var foodGroup = UnityEditor.AssetDatabase.LoadAssetAtPath<ItemGroupCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/ItemGroupCatalog.asset").Definitions[0];
                foreach (var item in fixture.Items.Definitions)
                {
                    item.PrimaryGroup = null;
                    item.AdditionalGroups = Array.Empty<ItemGroupDefinitionAsset>();
                }

                material.PrimaryGroup = foodGroup;
                otherSource.Capabilities = new BuildingCapabilitiesSource
                {
                    Housing = new BuildingHousingSource
                    {
                        Enabled = true,
                        Residences = new[]
                        {
                            new BuildingResidenceLevelSource
                            {
                                Level = 0,
                                Capacity = 2,
                                StarvationThreshold = 2,
                                GrowthInterval = 1
                            }
                        },
                        Food = new[]
                        {
                            new BuildingResidentFoodSource
                            {
                                Level = 0,
                                FoodGroup = foodGroup,
                                Varieties = 1,
                                AmountPerResident = 1
                            }
                        },
                        Environment = new[]
                        {
                            new BuildingEnvironmentRequirementSource
                            {
                                Level = 0,
                                Type = BuildingEnvironmentKind.Beauty,
                                RequiredValue = 100
                            }
                        },
                        Taxes = new[]
                        {
                            new BuildingResidenceTaxSource
                            {
                                Level = 0,
                                Item = coin,
                                PerResident = 1,
                                Interval = 1
                            }
                        }
                    }
                };
                coreSource.Capabilities.Production.RareOutputs = new[]
                {
                    new BuildingRareProductionSource
                    {
                        Level = 0,
                        Item = coin,
                        Quantity = 33,
                        Probability = .5f
                    }
                };
                fixture.Apply();
                BuildingHousingState residentsHousing = em.GetComponentData<BuildingHousingState>(second);
                residentsHousing.Population = 1;
                {
                    em.SetComponentData(second, residentsHousing);
                }

                BuildingLevelConfiguration.Apply(em, root, second, false);
                // Reference mode deliberately excludes rare output; environment blocks growth, not meals.
                DailyEconomySettlement.Settle(em, root, true);
                Check(InventoryOps.Count(em, root, wood) == 6 && em.GetComponentData<BuildingHousingState>(second).Population == 1, "Bad environment consumes full meal but blocks population growth");
                DaySettlementState stateSettlement = em.GetComponentData<DaySettlementState>(root);
                SimulationRandomState stateRandom = em.GetComponentData<SimulationRandomState>(root);
                stateSettlement.LastSettledTurn = 0;
                {
                    em.SetComponentData(root, stateSettlement);
                    em.SetComponentData(root, stateRandom);
                }

                {
                    residentsHousing = em.GetComponentData<BuildingHousingState>(second);
                }

                residentsHousing.Population = 2;
                {
                    em.SetComponentData(second, residentsHousing);
                }

                DailyEconomySettlement.Settle(em, root, true);
                Check(InventoryOps.Count(em, root, gold) == 2 && Rows<EconomyEntry>(em, root).Where(r => r.Reason == EconomyReason.Food).Sum(r => r.Delta) == -2, "Full house taxes after feeding even when environment is insufficient");
                {
                    stateSettlement = em.GetComponentData<DaySettlementState>(root);
                    stateRandom = em.GetComponentData<SimulationRandomState>(root);
                }

                stateSettlement.LastSettledTurn = 0;
                stateRandom.State = 111;
                {
                    em.SetComponentData(root, stateSettlement);
                    em.SetComponentData(root, stateRandom);
                }

                Check(EconomyForecastOps.Create(em, root) == ResultCode.Success, "Reference forecast supports probabilistic rules");
                var first = Rows<EconomyForecastEntry>(em, root);
                stateRandom.State = 222;
                {
                    em.SetComponentData(root, stateSettlement);
                    em.SetComponentData(root, stateRandom);
                }

                EconomyForecastOps.Create(em, root);
                Check(first.SequenceEqual(Rows<EconomyForecastEntry>(em, root)), "Reference output does not disclose the hidden next RNG roll");
                Check(first.Any(r => r.Value.Item == gold && r.Value.Delta == 0 && r.Value.Note.ToString().Contains("概率")), "Rare output is shown as probability rather than guaranteed income");
            }
            finally
            {
            }
        }
    }
}
#endif
