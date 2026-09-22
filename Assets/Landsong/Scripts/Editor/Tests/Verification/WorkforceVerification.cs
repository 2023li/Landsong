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
using Unity.Transforms;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Landsong.ECS.Editor
{
    public static class WorkforceVerification
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

        [MenuItem("Landsong/ECS/Verification/Workforce")]
        public static string Run()
        {
            checks = 0;
            log = new StringBuilder();
            try
            {
                Formulas();
                AuthoringValidation();
                Sources();
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
                File.WriteAllText("Library/LandsongEcs/workforce-verification.txt", log.ToString());
            }
        }

        static void Formulas()
        {
            for (var cap = 1; cap <= 60; cap++)
            {
                var valid = true;
                foreach (var attraction in new[]
                {
                    0f,
                    .1f,
                    20f,
                    33.3333f,
                    50f,
                    90f,
                    100f
                }

                )
                    for (var target = 0; target <= cap; target++)
                    {
                        var cost = WorkforceOps.SubsidyCost(cap, attraction, target);
                        valid &= WorkforceOps.Stable(cap, attraction + cost * (100f / cap)) >= target;
                        if (cost > 0)
                            valid &= WorkforceOps.Stable(cap, attraction + (cost - 1) * (100f / cap)) < target;
                    }

                Check(valid, "Minimal integer subsidy reaches exact worker target, capacity " + cap);
            }

            Check(WorkforceOps.Stable(0, 100) == 0 && WorkforceOps.SubsidyCost(0, 0, 3) == 0 && WorkforceOps.Stable(9, 20) == 2, "No jobs and integer threshold boundaries");
        }

        static void AuthoringValidation()
        {
            var catalog = UnityEngine.ScriptableObject.CreateInstance<BuildingCatalogAsset>();
            var coin = UnityEngine.ScriptableObject.CreateInstance<ItemDefinitionAsset>();
            var building = UnityEngine.ScriptableObject.CreateInstance<BuildingDefinitionAsset>();
            try
            {
                coin.Metadata.Id = "coin";
                building.Metadata.Id = "building";
                catalog.Definitions = new[]
                {
                    building
                };
                void Use(BuildingWorkforceLevelSource row)
                {
                    building.Capabilities = new BuildingCapabilitiesSource();
                    building.Capabilities.Workforce.Enabled = true;
                    building.Capabilities.Workforce.Levels = new[]
                    {
                        row
                    };
                    building.Capabilities.Workforce.EfficiencyTiers = new[]
                    {
                        new BuildingWorkerEfficiencyTierSource
                        {
                            MinimumWorkers = 0,
                            MaximumWorkers = Math.Max(0, row.Capacity)
                        }
                    };
                }

                void Effect(BuildingSpatialEffectSource row)
                {
                    row.Type = BuildingEnvironmentKind.Beauty;
                    building.Capabilities = new BuildingCapabilitiesSource();
                    building.Capabilities.Effects.Enabled = true;
                    building.Capabilities.Effects.Spatial = new[]
                    {
                        row
                    };
                }

                void Reject(Action configure, string label)
                {
                    configure();
                    var failed = false;
                    try
                    {
                        BuildingCatalogValidation.Validate(catalog);
                    }
                    catch (InvalidOperationException)
                    {
                        failed = true;
                    }

                    Check(failed, label);
                }

                Use(new BuildingWorkforceLevelSource { Capacity = 10, InitialWorkers = 1, BaseAttraction = 55, RecruitmentCost = 10, Currency = coin });
                BuildingCatalogValidation.Validate(catalog);
                Check(true, "Authoring accepts ordinary workforce configuration");
                Reject(() => Use(new BuildingWorkforceLevelSource { Capacity = -1 }), "Authoring rejects negative job capacity");
                Reject(() => Use(new BuildingWorkforceLevelSource { Capacity = 10, InitialWorkers = 11 }), "Authoring rejects excess initial workers");
                Reject(() => Use(new BuildingWorkforceLevelSource { BaseAttraction = float.NaN }), "Authoring rejects non-finite attraction");
                Reject(() => Use(new BuildingWorkforceLevelSource { RecruitmentCost = float.PositiveInfinity }), "Authoring rejects infinite recruitment price");
                Reject(() => Use(new BuildingWorkforceLevelSource { RecruitmentCost = 1073741824f }), "Authoring rejects float-rounded price that could overflow integer payment");
                Check(typeof(BuildingWorkforceLevelSource).GetField("Currency").FieldType == typeof(ItemDefinitionAsset), "Authoring workforce payment must target an item");
                Reject(() => Effect(new BuildingSpatialEffectSource { Stacking = (BuildingEffectStacking)11 }), "Authoring rejects unknown spatial stacking mode");
                Check(typeof(BuildingSpatialEffectSource).GetField("Building").FieldType == typeof(BuildingDefinitionAsset), "Authoring spatial target must be a building");
                Reject(() => Effect(new BuildingSpatialEffectSource { Radius = float.NaN }), "Authoring rejects non-finite spatial radius");
                Effect(new BuildingSpatialEffectSource { Radius = 4, Magnitude = 10, Stacking = BuildingEffectStacking.同类最高 });
                BuildingCatalogValidation.Validate(catalog);
                Check(true, "Authoring accepts unfiltered highest spatial effect");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
                UnityEngine.Object.DestroyImmediate(coin);
                UnityEngine.Object.DestroyImmediate(building);
            }
        }

        static Entity Building(EntityManager em, int definition, ulong id, int2 cell, bool provider = false)
        {
            var e = em.CreateEntity();
            em.AddComponentData(e, new BuildingDefinitionRef { Definition = BuildingId.FromIndex(definition - 1) });
            em.AddComponentData(e, new Identity { Id = id, Name = "测试建筑" });
            {
                em.AddComponentData(e, new Building { Stage = LifeStage.Operational, Level = 1 });
                em.AddComponentData(e, new BuildingPlacementState() { Cell = cell, Size = new int2(1) });
                em.AddComponentData(e, new BuildingAppearanceState() { });
                em.AddComponentData(e, new BuildingConstructionState() { });
                em.AddComponentData(e, new BuildingWorkforceState() { });
                em.AddComponentData(e, new BuildingHousingState() { });
                em.AddComponentData(e, new BuildingProductionState() { });
                em.AddComponentData(e, new BuildingFarmingState() { Crop = CropId.None });
                em.AddComponentData(e, new BuildingSanctumState() { });
                em.AddComponentData(e, new BuildingGatheringState() { });
                em.AddComponentData(e, new BuildingRecruitmentState() { });
                em.AddComponentData(e, new BuildingMarketState() { });
                em.AddComponentData(e, new BuildingExperienceState() { });
                em.AddComponentData(e, new BuildingMaintenanceState() { Maintained = 1 });
            }

            {
                em.AddComponentData(e, new BuildingHousingStats { });
                em.AddComponentData(e, new BuildingWorkforceStats() { Capacity = 10 });
                em.AddComponentData(e, new BuildingStorageStats() { IsProvider = (byte)(provider ? 1 : 0) });
                em.AddComponentData(e, new BuildingGarrisonStats() { });
                em.AddComponentData(e, new BuildingQuestStats() { });
                em.AddComponentData(e, new BuildingIntelligenceStats() { });
                em.AddComponentData(e, new BuildingSanctumStats() { });
                em.AddComponentData(e, new BuildingRangeStats() { ActionPower = 20 });
                em.AddComponentData(e, new BuildingNavigationStats() { });
                em.AddComponentData(e, new BuildingBellStats() { });
            }

            em.AddComponentData(e, LocalTransform.FromPosition(new float3(cell.x, 0, cell.y)));
            em.AddBuffer<RepairMaterial>(e);
            em.AddBuffer<BuildingInvestment>(e);
            return e;
        }

        static void Sources()
        {
            using var world = new World("Wave five isolated source fixture");
            var em = world.EntityManager;
            var root = em.CreateEntity();
            {
                em.AddComponentData(root, new Session { Phase = Phase.Day });
                em.AddComponentData(root, new GameClock() { Turn = 1 });
                em.AddComponentData(root, new SimulationControl() { });
                em.AddComponentData(root, new PopulationState() { BasePopulation = 30 });
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
                em.AddComponentData(root, new SimulationRandomState() { State = 123 });
                em.AddComponentData(root, new IdentitySequence() { });
                em.AddComponentData(root, new DynastyIdentity() { });
            }

            {
                em.AddComponentData(root, new NightSettings { });
                em.AddComponentData(root, new CurrencySettings() { Gold = ItemId.FromIndex(0) });
                em.AddComponentData(root, new IntelligenceSettings() { });
            }

            em.AddBuffer<InventorySlot>(root);
            em.AddBuffer<PendingItem>(root);
            em.AddBuffer<NightWave>(root);
            em.AddBuffer<GameEvent>(root);
            using var fixture = new WorkforceTestFixture();
            fixture.Install(em, root);
            using var gridBuilder = new BlobBuilder(Allocator.Temp);
            ref var g = ref gridBuilder.ConstructRoot<GridBlob>();
            g.Size = new int2(8, 8);
            var cells = gridBuilder.Allocate(ref g.Cells, 64);
            for (var i = 0; i < 64; i++)
                cells[i] = new GridCell
                {
                    Exists = 1,
                    Traversable = 1,
                    Buildable = 1
                };
            using var grid = gridBuilder.CreateBlobAssetReference<GridBlob>(Allocator.Persistent);
            em.AddComponentData(root, new GridData { Value = grid, CellSize = 1 });
            em.AddBuffer<Occupancy>(root).ResizeUninitialized(64);
            var occupancy = em.GetBuffer<Occupancy>(root);
            for (var i = 0; i < 64; i++)
                occupancy[i] = default;
            var target = Building(em, 1, 1, new int2(1, 1));
            var first = Building(em, 2, 2, new int2(2, 1), true);
            var priority = Building(em, 3, 3, new int2(4, 1), true);
            var tie = Building(em, 4, 4, new int2(2, 1), true);
            void Workers(Entity entity, int count, byte maintained = 1)
            {
                BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(entity);
                BuildingMaintenanceState bMaintenance = em.GetComponentData<BuildingMaintenanceState>(entity);
                bWorkforce.Workers = count;
                bMaintenance.Maintained = maintained;
                {
                    em.SetComponentData(entity, bWorkforce);
                    em.SetComponentData(entity, bMaintenance);
                }
            }

            Workers(first, 1);
            Workers(priority, 1);
            Workers(tie, 1);
            var slots = em.GetBuffer<InventorySlot>(root);
            slots.Add(new InventorySlot { Provider = 2, Item = ItemId.FromIndex(0), Count = 100, SlotType = StorageSlotId.None });
            var q = WorkforceOps.Quote(em, root, target);
            Check(q.NaturalStable == 2 && q.CurrentStable == 2 && q.RecruitCost == 18 && q.Sources.Sum(s => s.Value) == q.Raw, "Workforce quote uses real source sum and current recruitment price");
            Check(GameRequestExecution.Execute(em, root, new SetWorkforceBudgetRequest { Building = 1, Budget = 3 }) == ResultCode.Success && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 100, "Direct subsidy budget changes without immediate payment");
            q = WorkforceOps.Quote(em, root, target);
            Check(q.SubsidyCost == 3 && q.Planned == 50 && q.Current == 20, "Budget preview separates unpaid and actual attraction");
            Workers(target, 0, 0);
            q = WorkforceOps.Quote(em, root, target);
            Check(q.Natural == 15 && q.SubsidyCost == 3, "Direct subsidy budget stays fixed when natural attraction changes");
            Workers(target, 0);
            Check(WorkforceOps.SetBudget(em, root, target, 11) == ResultCode.InvalidTarget && WorkforceOps.SetBudget(em, root, target, -1) == ResultCode.InvalidTarget, "Budget rejects negative or excessive amounts");
            var step = new SetWorkforceBudgetRequest
            {
                Building = 1,
                Budget = 1,
                Relative = 1
            };
            Check(GameRequestExecution.Execute(em, root, step) == ResultCode.Success && GameRequestExecution.Execute(em, root, step) == ResultCode.Success && WorkforceOps.Quote(em, root, target).SubsidyCost == 5, "Rapid arrow commands each apply one budget step against current state");
            Check(GameRequestExecution.Execute(em, root, new SetWorkforceTargetRequest { Building = 1, DesiredWorkers = 10 }) == ResultCode.Success && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 100, "Target command is non-paying");
            q = WorkforceOps.Quote(em, root, target);
            Check(q.PlannedStable == 10 && q.CurrentStable == 2 && q.SubsidyCost == 8, "Unpaid target cannot enlarge current stable workforce");
            Check(WorkforceSettlement.ChangeWorkers(em, root, target, 3) == ResultCode.NoCapacity && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 100, "Unpaid subsidy cannot fund recruitment eligibility");
            Check(GameRequestExecution.Execute(em, root, new RecruitWorkersRequest { Building = 1, Count = 1, ExpectedGoldCostPerWorker = 17 }) == ResultCode.Unavailable && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 100, "Stale displayed recruitment price rejected without charge");
            Check(WorkforceSettlement.ChangeWorkers(em, root, target, 1) == ResultCode.Success && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == 82, "Recruit charges exactly current quoted price");
            Check(WorkforceOps.SetTarget(em, root, target, 0) == ResultCode.Success && WorkforceOps.Quote(em, root, target).Target == 2 && em.GetComponentData<BuildingWorkforceState>(target).Workers == 1, "Low target clamps to natural stable count without layoffs");
            Workers(target, 1, 0);
            q = WorkforceOps.Quote(em, root, target);
            Check(q.Raw == 15 && q.Sources.Any(s => s.Value == -5), "Maintenance penalty included in source breakdown");
            Workers(target, 1);
            var expedition = em.CreateEntity();
            em.AddComponentData(expedition, new Identity { Id = 8 });
            em.AddComponentData(expedition, new Expedition { Site = 1, Status = ExpeditionStatus.Travelling });
            Check(WorkforceOps.SetTarget(em, root, target, 10) == ResultCode.Busy && WorkforceSettlement.ChangeWorkers(em, root, target, -1) == ResultCode.Busy, "Travelling expedition locks recruitment dismissal and target");
            em.DestroyEntity(expedition);
            var session = em.GetComponentData<Session>(root);
            session.Phase = Phase.Night;
            em.SetComponentData(root, session);
            Check(GameRequestExecution.Execute(em, root, new SetWorkforceTargetRequest { Building = 1, DesiredWorkers = 10 }) == ResultCode.WrongPhase, "Night blocks target commands");
            session.Phase = Phase.Day;
            em.SetComponentData(root, session);
            var soldier = em.CreateEntity();
            em.AddComponentData(soldier, new Soldier { PopulationCost = 30 });
            Check(WorkforceSettlement.ChangeWorkers(em, root, target, 1) == ResultCode.InsufficientPopulation, "Recruit cannot borrow population occupied by soldiers");
            em.DestroyEntity(soldier);
            var network = ResourceNetworkOps.Quote(em, root, target);
            Check(network.Selected == priority && network.Candidates.Count == 3, "Higher provider priority wins over shorter distance");
            Workers(priority, 0);
            network = ResourceNetworkOps.Quote(em, root, target);
            Check(network.Selected == first && network.Candidates.Single(c => c.Entity == priority).Reason == "缺少工人", "Inactive provider explained; equal costs use stable ID");
            Workers(first, 1, 0);
            Check(ResourceNetworkOps.Provider(em, root, target) == tie, "Unmaintained provider is ineligible");
            Workers(first, 1);
            Workers(priority, 1);
            BuildingRangeStats statsRange = em.GetComponentData<BuildingRangeStats>(target);
            statsRange.ActionPower = 0;
            {
                em.SetComponentData(target, statsRange);
            }

            Check(ResourceNetworkOps.Provider(em, root, target) == Entity.Null, "Provider outside connection budget unavailable");
            statsRange.ActionPower = 20;
            {
                em.SetComponentData(target, statsRange);
            }

            var spatial = SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Beauty);
            Check(spatial.Value == 14 && spatial.Sources.Sum(s => s.Applied) == BuildingEnvironment.Value(em, root, target, BuildingEnvironmentKind.Beauty), "Spatial actual total equals additive plus per-group max plus global max explanation");
            Check(spatial.Sources.Any(s => s.Reason.Contains("工人")) && spatial.Sources.Any(s => s.Reason.Contains("不匹配")) && spatial.Sources.Single(s => s.Source == 4).Applied == 0, "Spatial explanations include staffing, target filter and stable-ID duplicate suppression");
            Workers(priority, 1, 0);
            Check(SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Beauty).Value == 14, "Maintenance alone does not silently disable spatial effects");
            var ruined = em.GetComponentData<Building>(priority);
            BuildingMaintenanceState ruinedMaintenance = em.GetComponentData<BuildingMaintenanceState>(priority);
            ruined.Stage = LifeStage.Ruined;
            {
                em.SetComponentData(priority, ruined);
                em.SetComponentData(priority, ruinedMaintenance);
            }

            Check(SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Beauty).Value == 11, "Ruined source contributes nothing while remaining group source takes over");
            ruined.Stage = LifeStage.Operational;
            ruinedMaintenance.Maintained = 1;
            {
                em.SetComponentData(priority, ruined);
                em.SetComponentData(priority, ruinedMaintenance);
            }

            BuildingPlacementState farPlacement = em.GetComponentData<BuildingPlacementState>(tie);
            farPlacement.Cell = new int2(4, 4);
            {
                em.SetComponentData(tie, farPlacement);
            }

            Check(!SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Beauty).Sources.Any(s => s.Source == 4), "Diagonal Manhattan gap beyond radius excluded");
            farPlacement.Cell = new int2(4, 2);
            {
                em.SetComponentData(tie, farPlacement);
            }

            var large = em.GetComponentData<Building>(target);
            BuildingPlacementState largePlacement = em.GetComponentData<BuildingPlacementState>(target);
            BuildingConstructionState largeConstruction = em.GetComponentData<BuildingConstructionState>(target);
            largePlacement.Size = new int2(2);
            {
                em.SetComponentData(target, large);
                em.SetComponentData(target, largePlacement);
                em.SetComponentData(target, largeConstruction);
            }

            Check(SpatialOps.Quote(em, root, target, BuildingEnvironmentKind.Beauty).Sources.Any(s => s.Source == 4), "Spatial range measures full target footprint");
            large.Stage = LifeStage.Repairing;
            largePlacement.Size = new int2(1);
            largeConstruction.RepairDuration = 3;
            largeConstruction.Progress = 0;
            {
                em.SetComponentData(target, large);
                em.SetComponentData(target, largePlacement);
                em.SetComponentData(target, largeConstruction);
            }

            em.GetBuffer<RepairMaterial>(target).Add(new RepairMaterial { Item = ItemId.FromIndex(0), Amount = 8 });
            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 2 });
            var repair = BuildingCostOps.QuoteRepair(em, root, target);
            Check(repair.Total[0].Amount == 8 && repair.Remaining[0].Amount == 8 && repair.Payments[0].Required == 3 && repair.Payments[0].Pending == 2 && repair.Payments[0].Normal == 1 && repair.Provider == priority, "Frozen repair front-loads remainder and splits pending versus normal payment");
            var normal = InventoryOps.Count(em, root, ItemId.FromIndex(0));
            Check(BuildingCostOps.PayRepairStep(em, root, target) && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == normal - 1 && InventoryOps.PendingCount(em, root, ItemId.FromIndex(0)) == 0 && em.GetComponentData<BuildingMarketState>(priority).TurnValue == 3, "Repair charges exact split and attributes only ordinary resource value");
            largeConstruction.Progress = 1;
            {
                em.SetComponentData(target, large);
                em.SetComponentData(target, largePlacement);
                em.SetComponentData(target, largeConstruction);
            }

            repair = BuildingCostOps.QuoteRepair(em, root, target);
            Check(repair.Remaining[0].Amount == 5 && repair.Costs[0].Amount == 3, "Second installment leaves exact unpaid remainder");
            largeConstruction.Progress = 2;
            {
                em.SetComponentData(target, large);
                em.SetComponentData(target, largePlacement);
                em.SetComponentData(target, largeConstruction);
            }

            Check(BuildingCostOps.QuoteRepair(em, root, target).Costs[0].Amount == 2, "Final repair installment conserves frozen total");
            statsRange.ActionPower = 0;
            {
                em.SetComponentData(target, statsRange);
            }

            em.GetBuffer<PendingItem>(root).Add(new PendingItem { Item = ItemId.FromIndex(0), Amount = 1 });
            normal = InventoryOps.Count(em, root, ItemId.FromIndex(0));
            Check(!BuildingCostOps.PayRepairStep(em, root, target) && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == normal && InventoryOps.PendingCount(em, root, ItemId.FromIndex(0)) == 1, "Disconnected mixed repair cannot partially charge pending pool");
            var pending = em.GetBuffer<PendingItem>(root);
            var pendingGold = pending[0];
            pendingGold.Amount = 2;
            pending[0] = pendingGold;
            Check(!BuildingCostOps.QuoteRepair(em, root, target).NeedsNetwork && BuildingCostOps.PayRepairStep(em, root, target) && InventoryOps.Count(em, root, ItemId.FromIndex(0)) == normal, "Pending-only repair needs no provider and does not consume ordinary stock");
            statsRange.ActionPower = 20;
            {
                em.SetComponentData(target, statsRange);
            }

            em.GetBuffer<InventorySlot>(root).Clear();
            Check(BuildingCostOps.QuoteRepair(em, root, target).Payments[0].Missing == 2 && !BuildingCostOps.PayRepairStep(em, root, target), "Insufficient complete installment is explained and rejected");
        }

        static void Map(string path)
        {
            log.AppendLine("MAP " + path);
            var scene = EditorSceneManager.OpenPreviewScene(path);
            using var store = new BlobAssetStore(128);
            using var world = new World("Wave five isolated map", WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world, scene.GetRootGameObjects(), store);
                var em = world.EntityManager;
                var root = WorldQueries.Root(em);
                WorldInitialization.Initialize(em, root);
                using var fixture = new BuildingCatalogTestFixture(em, root);
                var definitions = fixture.Buildings.Definitions;
                int index = Array.FindIndex(definitions, d => d.Metadata.Id.Contains("伐木"));
                Check(index >= 0, "Real map includes workforce content");
                var def = BuildingId.FromIndex(index);
                var grid = em.GetComponentData<GridData>(root);
                var cell = grid.Value.Value.Min;
                var found = false;
                for (var y = 0; y < grid.Value.Value.Size.y && !found; y++)
                    for (var x = 0; x < grid.Value.Value.Size.x && !found; x++)
                    {
                        cell = grid.Value.Value.Min + new int2(x, y);
                        found = GridOps.CanPlace(em, root, def, cell, 0);
                    }

                Check(found, "Workforce fixture has legal footprint");
                var building = BuildingCreation.Create(em, root, def, cell, 0, 1, true);
                var id = em.GetComponentData<Identity>(building).Id;
                var gold = em.GetComponentData<CurrencySettings>(root).Gold;
                foreach (var item in fixture.Items.Definitions)
                    item.NaturalLossRate = 0;
                definitions[def.Index].Capabilities = new BuildingCapabilitiesSource
                {
                    Workforce = new BuildingWorkforceSource
                    {
                        Enabled = true,
                        EfficiencyTiers = new[]
                        {
                            new BuildingWorkerEfficiencyTierSource
                            {
                                MinimumWorkers = 0,
                                MaximumWorkers = 10
                            }
                        },
                        Levels = new[]
                        {
                            new BuildingWorkforceLevelSource
                            {
                                Level = 0,
                                Capacity = 10,
                                BaseAttraction = 20,
                                RecruitmentCost = 10,
                                Currency = fixture.Items.Definitions[gold.Index]
                            }
                        }
                    }
                };
                fixture.Apply();
                try
                {
                    BuildingLevelConfiguration.Apply(em, root, building, false);
                    BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(building);
                    bWorkforce.Workers = 0;
                    bWorkforce.Subsidy = 1;
                    bWorkforce.WorkerTarget = 10;
                    bWorkforce.SubsidyBudget = 8;
                    bWorkforce.PaidSubsidy = 0;
                    {
                        em.SetComponentData(building, bWorkforce);
                    }

                    GameClock sClock = em.GetComponentData<GameClock>(root);
                    PopulationState sPopulation = em.GetComponentData<PopulationState>(root);
                    DaySettlementState sSettlement = em.GetComponentData<DaySettlementState>(root);
                    PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
                    sPopulation.BasePopulation = 100;
                    sPersistence.CheckpointPending = 0;
                    sSettlement.LastSettledTurn = 0;
                    {
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sPopulation);
                        em.SetComponentData(root, sSettlement);
                        em.SetComponentData(root, sPersistence);
                    }

                    InventoryOps.Add(em, root, gold, 100);
                    var before = SnapshotCodec.Capture(em, root);
                    Check(EconomyForecastOps.Create(em, root) == ResultCode.Success && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Real map forecast rolls back paid subsidy, workers, inventory and RNG");
                    DailyEconomySettlement.Settle(em, root);
                    building = WorldQueries.Find(em, id);
                    {
                        bWorkforce = em.GetComponentData<BuildingWorkforceState>(building);
                    }

                    Check(bWorkforce.PaidSubsidy == 8 && bWorkforce.PaidSubsidyTurn == sClock.Turn && bWorkforce.StableWorkers == 10 && bWorkforce.Workers <= 1, "Real settlement pays target and changes natural workforce at most once");
                    using (var journal = em.GetBuffer<EconomyEntry>(root).ToNativeArray(Allocator.Temp))
                        Check(journal.ToArray().Any(r => r.Source == id && r.Reason == EconomyReason.Workforce && r.Delta == -8), "Paid subsidy reconciles actual per-building journal");
                    var paid = SnapshotCodec.Capture(em, root);
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, paid));
                    building = WorldQueries.Find(em, id);
                    Check(paid.SequenceEqual(SnapshotCodec.Capture(em, root)) && WorkforceOps.Quote(em, root, building).CurrentStable == 10, "Version-six snapshot preserves actual subsidy and stable workforce exactly");
                    Check(WorkforceOps.SetTarget(em, root, building, 0) == ResultCode.Success && WorkforceOps.Quote(em, root, building).CurrentStable == 10, "Changing next payment does not erase benefit already paid");
                    {
                        sClock = em.GetComponentData<GameClock>(root);
                        sPopulation = em.GetComponentData<PopulationState>(root);
                        sSettlement = em.GetComponentData<DaySettlementState>(root);
                        sPersistence = em.GetComponentData<PersistenceGate>(root);
                    }

                    sSettlement.LastSettledTurn = 0;
                    {
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sPopulation);
                        em.SetComponentData(root, sSettlement);
                        em.SetComponentData(root, sPersistence);
                    }

                    DailyEconomySettlement.Settle(em, root);
                    Check(em.GetComponentData<BuildingWorkforceState>(building).PaidSubsidy == 0 && WorkforceOps.Quote(em, root, building).CurrentStable == 2, "Next settlement replaces previously paid subsidy with current plan");
                    SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, paid));
                    building = WorldQueries.Find(em, id);
                    InventoryOps.Remove(em, root, gold, InventoryOps.Count(em, root, gold));
                    {
                        sClock = em.GetComponentData<GameClock>(root);
                        sPopulation = em.GetComponentData<PopulationState>(root);
                        sSettlement = em.GetComponentData<DaySettlementState>(root);
                        sPersistence = em.GetComponentData<PersistenceGate>(root);
                    }

                    sSettlement.LastSettledTurn = 0;
                    {
                        em.SetComponentData(root, sClock);
                        em.SetComponentData(root, sPopulation);
                        em.SetComponentData(root, sSettlement);
                        em.SetComponentData(root, sPersistence);
                    }

                    DailyEconomySettlement.Settle(em, root);
                    Check(em.GetComponentData<BuildingWorkforceState>(building).PaidSubsidy == 0 && em.GetComponentData<BuildingWorkforceState>(building).Subsidy == 1, "Unfunded payment removes bonus but retains requested future target");
                    var malformed = SnapshotCodec.Decode(em, root, paid);
                    malformed.Records.OfType<BuildingSnapshot>().Single(r => r.Identity.Id == id).BuildingWorkforce.PaidSubsidy = -1;
                    before = SnapshotCodec.Capture(em, root);
                    var rejected = false;
                    try
                    {
                        SnapshotCodec.Restore(em, root, malformed);
                    }
                    catch (InvalidDataException)
                    {
                        rejected = true;
                    }

                    Check(rejected && before.SequenceEqual(SnapshotCodec.Capture(em, root)), "Invalid persisted subsidy rejected atomically");
                }
                finally
                {
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }
    }
}
#endif
