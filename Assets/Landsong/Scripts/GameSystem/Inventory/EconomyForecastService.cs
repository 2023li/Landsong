using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.BuildingSystem;
using Landsong.Localization;
using UnityEngine;

namespace Landsong.InventorySystem
{
    public enum EconomyForecastEventKind
    {
        Construction,
        Production,
        Processing,
        Harvest,
        Tax,
        Market,
        Population,
        Warning
    }

    public enum EconomyForecastCertainty
    {
        Exact,
        Range,
        Conditional,
        Manual
    }

    public readonly struct EconomyForecastEvent
    {
        public EconomyForecastEvent(
            int turnOffset,
            string buildingInstanceId,
            string buildingDisplayName,
            EconomyForecastEventKind kind,
            EconomyForecastCertainty certainty,
            string description,
            bool isBlocked = false)
        {
            TurnOffset = Mathf.Max(1, turnOffset);
            BuildingInstanceId = buildingInstanceId ?? string.Empty;
            BuildingDisplayName = string.IsNullOrWhiteSpace(buildingDisplayName)
                ? L10n.Gameplay("gameplay.common.building", "建筑")
                : buildingDisplayName.Trim();
            Kind = kind;
            Certainty = certainty;
            Description = description ?? string.Empty;
            IsBlocked = isBlocked;
        }

        public int TurnOffset { get; }
        public string BuildingInstanceId { get; }
        public string BuildingDisplayName { get; }
        public EconomyForecastEventKind Kind { get; }
        public EconomyForecastCertainty Certainty { get; }
        public string Description { get; }
        public bool IsBlocked { get; }
    }

    public readonly struct EconomyForecastResourceTurn
    {
        public EconomyForecastResourceTurn(
            int turnOffset,
            int startingQuantity,
            int expectedConsumption,
            int minimumProduction,
            int maximumProduction,
            int expectedLoss,
            int minimumProjectedQuantity,
            int maximumProjectedQuantity,
            int shortage,
            int overflow)
        {
            TurnOffset = Mathf.Max(1, turnOffset);
            StartingQuantity = Mathf.Max(0, startingQuantity);
            ExpectedConsumption = Mathf.Max(0, expectedConsumption);
            MinimumProduction = Mathf.Max(0, minimumProduction);
            MaximumProduction = Mathf.Max(MinimumProduction, maximumProduction);
            ExpectedLoss = Mathf.Max(0, expectedLoss);
            MinimumProjectedQuantity = Mathf.Max(0, minimumProjectedQuantity);
            MaximumProjectedQuantity = Mathf.Max(
                MinimumProjectedQuantity,
                maximumProjectedQuantity);
            Shortage = Mathf.Max(0, shortage);
            Overflow = Mathf.Max(0, overflow);
        }

        public int TurnOffset { get; }
        public int StartingQuantity { get; }
        public int ExpectedConsumption { get; }
        public int MinimumProduction { get; }
        public int MaximumProduction { get; }
        public int ExpectedLoss { get; }
        public int MinimumProjectedQuantity { get; }
        public int MaximumProjectedQuantity { get; }
        public int Shortage { get; }
        public int Overflow { get; }
        public bool HasRisk => Shortage > 0 || Overflow > 0;
        public bool HasRange => MinimumProduction != MaximumProduction
                                || MinimumProjectedQuantity != MaximumProjectedQuantity;
        public int MinimumNetChange => MinimumProjectedQuantity - StartingQuantity;
    }

    public sealed class EconomyForecastResourceTimeline
    {
        public EconomyForecastResourceTimeline(
            string itemId,
            int currentQuantity,
            IReadOnlyList<EconomyForecastResourceTurn> turns)
        {
            ItemId = itemId ?? string.Empty;
            CurrentQuantity = Mathf.Max(0, currentQuantity);
            Turns = turns ?? Array.Empty<EconomyForecastResourceTurn>();
        }

        public string ItemId { get; }
        public int CurrentQuantity { get; }
        public IReadOnlyList<EconomyForecastResourceTurn> Turns { get; }
        public bool HasRisk => Turns.Any(turn => turn.HasRisk);
        public int LowestProjectedQuantity => Turns.Count == 0
            ? CurrentQuantity
            : Turns.Min(turn => turn.MinimumProjectedQuantity);
    }

    public readonly struct EconomyForecastLine
    {
        public EconomyForecastLine(
            string itemId,
            int currentQuantity,
            int expectedConsumption,
            int expectedProduction,
            int expectedLoss,
            int projectedQuantity,
            int shortage)
        {
            ItemId = itemId ?? string.Empty;
            CurrentQuantity = Mathf.Max(0, currentQuantity);
            ExpectedConsumption = Mathf.Max(0, expectedConsumption);
            ExpectedProduction = Mathf.Max(0, expectedProduction);
            ExpectedLoss = Mathf.Max(0, expectedLoss);
            ProjectedQuantity = Mathf.Max(0, projectedQuantity);
            Shortage = Mathf.Max(0, shortage);
        }

        public string ItemId { get; }
        public int CurrentQuantity { get; }
        public int ExpectedConsumption { get; }
        public int ExpectedProduction { get; }
        public int ExpectedLoss { get; }
        public int ProjectedQuantity { get; }
        public int Shortage { get; }
        public int NetChange => ProjectedQuantity - CurrentQuantity;
        public bool HasWarning => Shortage > 0;
    }

    public readonly struct ResidentialQualityForecast
    {
        public ResidentialQualityForecast(
            string buildingInstanceId,
            string buildingDisplayName,
            int population,
            bool foodSatisfied,
            int predictedDietVariety,
            float predictedDietScore,
            float currentLifeQuality)
            : this(
                1,
                buildingInstanceId,
                buildingDisplayName,
                population,
                population,
                foodSatisfied,
                predictedDietVariety,
                predictedDietScore,
                currentLifeQuality,
                currentLifeQuality)
        {
        }

        public ResidentialQualityForecast(
            int turnOffset,
            string buildingInstanceId,
            string buildingDisplayName,
            int population,
            int predictedPopulation,
            bool foodSatisfied,
            int predictedDietVariety,
            float predictedDietScore,
            float currentLifeQuality,
            float predictedLifeQuality)
        {
            TurnOffset = Mathf.Max(1, turnOffset);
            BuildingInstanceId = buildingInstanceId ?? string.Empty;
            BuildingDisplayName = buildingDisplayName ?? string.Empty;
            Population = Mathf.Max(0, population);
            PredictedPopulation = Mathf.Max(0, predictedPopulation);
            FoodSatisfied = foodSatisfied;
            PredictedDietVariety = Mathf.Max(0, predictedDietVariety);
            PredictedDietScore = Mathf.Clamp(predictedDietScore, 0f, 100f);
            CurrentLifeQuality = Mathf.Clamp(currentLifeQuality, 0f, 100f);
            PredictedLifeQuality = Mathf.Clamp(predictedLifeQuality, 0f, 100f);
        }

        public int TurnOffset { get; }
        public string BuildingInstanceId { get; }
        public string BuildingDisplayName { get; }
        public int Population { get; }
        public int PredictedPopulation { get; }
        public bool FoodSatisfied { get; }
        public int PredictedDietVariety { get; }
        public float PredictedDietScore { get; }
        public float CurrentLifeQuality { get; }
        public float PredictedLifeQuality { get; }
    }

    public sealed class EconomyForecastTimelineResult
    {
        public EconomyForecastTimelineResult(
            int forecastTurns,
            IReadOnlyList<EconomyForecastResourceTimeline> resourceTimelines,
            IReadOnlyList<EconomyForecastEvent> events,
            IReadOnlyList<InventorySlotLoss> nextTurnSlotLosses,
            IReadOnlyList<ResidentialQualityForecast> residentialForecasts,
            IReadOnlyList<string> warnings,
            int totalSlots,
            int occupiedSlots,
            int projectedOccupiedSlots,
            bool isApproximate)
        {
            ForecastTurns = Mathf.Max(1, forecastTurns);
            ResourceTimelines = resourceTimelines ?? Array.Empty<EconomyForecastResourceTimeline>();
            Events = events ?? Array.Empty<EconomyForecastEvent>();
            NextTurnSlotLosses = nextTurnSlotLosses ?? Array.Empty<InventorySlotLoss>();
            ResidentialForecasts = residentialForecasts ?? Array.Empty<ResidentialQualityForecast>();
            Warnings = warnings ?? Array.Empty<string>();
            TotalSlots = Mathf.Max(0, totalSlots);
            OccupiedSlots = Mathf.Clamp(occupiedSlots, 0, TotalSlots);
            ProjectedOccupiedSlots = Mathf.Clamp(projectedOccupiedSlots, 0, TotalSlots);
            IsApproximate = isApproximate;
        }

        public int ForecastTurns { get; }
        public IReadOnlyList<EconomyForecastResourceTimeline> ResourceTimelines { get; }
        public IReadOnlyList<EconomyForecastEvent> Events { get; }
        public IReadOnlyList<InventorySlotLoss> NextTurnSlotLosses { get; }
        public IReadOnlyList<ResidentialQualityForecast> ResidentialForecasts { get; }
        public IReadOnlyList<string> Warnings { get; }
        public int TotalSlots { get; }
        public int OccupiedSlots { get; }
        public int ProjectedOccupiedSlots { get; }
        public bool IsApproximate { get; }
    }

    public sealed class EconomyForecastResult
    {
        public EconomyForecastResult(
            IReadOnlyList<EconomyForecastLine> itemLines,
            IReadOnlyList<InventorySlotLoss> slotLosses,
            IReadOnlyList<ResidentialQualityForecast> residentialForecasts,
            IReadOnlyList<string> warnings,
            bool isApproximate)
        {
            ItemLines = itemLines ?? Array.Empty<EconomyForecastLine>();
            SlotLosses = slotLosses ?? Array.Empty<InventorySlotLoss>();
            ResidentialForecasts = residentialForecasts ?? Array.Empty<ResidentialQualityForecast>();
            Warnings = warnings ?? Array.Empty<string>();
            IsApproximate = isApproximate;
        }

        public IReadOnlyList<EconomyForecastLine> ItemLines { get; }
        public IReadOnlyList<InventorySlotLoss> SlotLosses { get; }
        public IReadOnlyList<ResidentialQualityForecast> ResidentialForecasts { get; }
        public IReadOnlyList<string> Warnings { get; }
        public bool IsApproximate { get; }
    }

    public sealed class EconomyForecastService
    {
        private const int MaximumForecastTurns = 10;

        private sealed class MutableTurnLine
        {
            public int Consumption;
            public int MinimumProduction;
            public int MaximumProduction;
            public int Loss;
            public int Shortage;
            public int Overflow;
        }

        private sealed class MutableTimeline
        {
            public int Current;
            public readonly List<EconomyForecastResourceTurn> Turns =
                new List<EconomyForecastResourceTurn>();
        }

        private sealed class CropPlanState
        {
            public bool HasCrop;
            public int Progress;
            public int RequiredTurns;
            public bool AutoHarvest;
            public bool ManualEventEmitted;
        }

        private sealed class ResidentialPlanState
        {
            public int Population;
            public int GrowthProgress;
            public int TaxProgress;
            public int ConsecutiveFailures;
            public bool IsAbandoned;
            public float LifeQuality;
        }

        private sealed class BuildingPlanState
        {
            public BuildingPlanState(BuildingBase building)
            {
                StartedUnderConstruction = building != null && building.IsUnderConstruction;
                ConstructionProgress = building?.ConstructionProgress ?? 0;
            }

            public bool StartedUnderConstruction;
            public int ConstructionProgress;
            public bool ConstructionCompleted;
            public bool CompletionNoticeAdded;
            public readonly Dictionary<BuildingModuleBase, int> ModuleProgress =
                new Dictionary<BuildingModuleBase, int>();
            public readonly Dictionary<BuildingCropGrowthModule, CropPlanState> Crops =
                new Dictionary<BuildingCropGrowthModule, CropPlanState>();
            public readonly Dictionary<BM_居民运营, ResidentialPlanState> Residential =
                new Dictionary<BM_居民运营, ResidentialPlanState>();
        }

        private readonly Landsong.GameSystem gameSystem;

        public EconomyForecastService(Landsong.GameSystem gameSystem)
        {
            this.gameSystem = gameSystem;
        }

        public EconomyForecastResult ForecastNextTurn()
        {
            var timeline = ForecastTurns(1);
            var lines = new List<EconomyForecastLine>(timeline.ResourceTimelines.Count);
            for (var i = 0; i < timeline.ResourceTimelines.Count; i++)
            {
                var resource = timeline.ResourceTimelines[i];
                var turn = resource.Turns.Count == 0 ? default : resource.Turns[0];
                lines.Add(new EconomyForecastLine(
                    resource.ItemId,
                    resource.CurrentQuantity,
                    turn.ExpectedConsumption,
                    turn.MinimumProduction,
                    turn.ExpectedLoss,
                    turn.MinimumProjectedQuantity,
                    turn.Shortage));
            }

            return new EconomyForecastResult(
                lines,
                timeline.NextTurnSlotLosses,
                timeline.ResidentialForecasts.Where(value => value.TurnOffset == 1).ToArray(),
                timeline.Warnings,
                timeline.IsApproximate);
        }

        public EconomyForecastTimelineResult ForecastTurns(int turns = 5)
        {
            turns = Mathf.Clamp(turns, 1, MaximumForecastTurns);
            var actualInventory = gameSystem?.Services.Inventory?.Inventory;
            if (actualInventory == null)
            {
                return EmptyTimeline(turns, L10n.Gameplay("gameplay.economy.service.inventory_missing", "库存服务未初始化，无法生成经济预测。"));
            }

            var simulation = actualInventory.CreateSimulation();
            var timelines = new Dictionary<string, MutableTimeline>(StringComparer.Ordinal);
            var buildingStates = new Dictionary<BuildingBase, BuildingPlanState>();
            var potentialExtras = new Dictionary<string, int>(StringComparer.Ordinal);
            var events = new List<EconomyForecastEvent>();
            var warnings = new List<string>();
            var residentialForecasts = new List<ResidentialQualityForecast>();
            IReadOnlyList<InventorySlotLoss> nextTurnLosses = Array.Empty<InventorySlotLoss>();

            var initialQuantities = CaptureQuantities(actualInventory);
            foreach (var pair in initialQuantities)
            {
                GetTimeline(timelines, pair.Key).Current = pair.Value;
            }

            AddWarning(
                warnings,
                L10n.Gameplay(
                    "gameplay.economy.service.assumptions",
                    "预测假设未来工人数、连接关系和科技状态保持不变；随机产出以范围显示，并以最小产出进行库存可行性判断。"));

            var buildings = gameSystem.Services.Buildings?.Buildings;
            for (var turnOffset = 1; turnOffset <= turns; turnOffset++)
            {
                var startingQuantities = CaptureQuantities(simulation);
                var turnLines = new Dictionary<string, MutableTurnLine>(StringComparer.Ordinal);
                var marketProvidedValues = new Dictionary<BuildingBase, long>();

                if (buildings != null)
                {
                    for (var buildingIndex = 0; buildingIndex < buildings.Count; buildingIndex++)
                    {
                        var building = buildings[buildingIndex];
                        if (building == null || building.IsDemolishing || !building.IsInitialized)
                        {
                            continue;
                        }

                        if (!buildingStates.TryGetValue(building, out var state))
                        {
                            state = new BuildingPlanState(building);
                            buildingStates.Add(building, state);
                        }

                        ForecastBuildingTurn(
                            building,
                            state,
                            turnOffset,
                            simulation,
                            turnLines,
                            potentialExtras,
                            marketProvidedValues,
                            events,
                            residentialForecasts,
                            warnings);
                    }
                }

                ForecastMarketSettlement(
                    buildings,
                    marketProvidedValues,
                    turnOffset,
                    simulation,
                    turnLines,
                    events,
                    warnings);

                var losses = simulation.ProcessTurnLosses();
                if (turnOffset == 1)
                {
                    nextTurnLosses = losses;
                }

                for (var i = 0; i < losses.Count; i++)
                {
                    GetTurnLine(turnLines, losses[i].ItemId).Loss += losses[i].AmountLost;
                }

                var projectedQuantities = CaptureQuantities(simulation);
                var itemIds = new HashSet<string>(timelines.Keys, StringComparer.Ordinal);
                itemIds.UnionWith(startingQuantities.Keys);
                itemIds.UnionWith(projectedQuantities.Keys);
                itemIds.UnionWith(turnLines.Keys);
                itemIds.UnionWith(potentialExtras.Keys);

                foreach (var itemId in itemIds)
                {
                    startingQuantities.TryGetValue(itemId, out var starting);
                    projectedQuantities.TryGetValue(itemId, out var projected);
                    potentialExtras.TryGetValue(itemId, out var projectedExtra);
                    turnLines.TryGetValue(itemId, out var mutable);
                    mutable ??= new MutableTurnLine();
                    GetTimeline(timelines, itemId).Turns.Add(new EconomyForecastResourceTurn(
                        turnOffset,
                        starting,
                        mutable.Consumption,
                        mutable.MinimumProduction,
                        mutable.MaximumProduction,
                        mutable.Loss,
                        projected,
                        projected + projectedExtra,
                        mutable.Shortage,
                        mutable.Overflow));
                }
            }

            var resourceTimelines = timelines
                .Select(pair => new EconomyForecastResourceTimeline(
                    pair.Key,
                    pair.Value.Current,
                    pair.Value.Turns))
                .OrderByDescending(value => value.HasRisk)
                .ThenBy(value => value.LowestProjectedQuantity)
                .ThenBy(value => value.ItemId, StringComparer.Ordinal)
                .ToArray();
            events.Sort(CompareEvents);

            return new EconomyForecastTimelineResult(
                turns,
                resourceTimelines,
                events,
                nextTurnLosses,
                residentialForecasts,
                warnings,
                actualInventory.Slots.Count,
                CountOccupiedSlots(actualInventory),
                CountOccupiedSlots(simulation),
                true);
        }

        private void ForecastBuildingTurn(
            BuildingBase building,
            BuildingPlanState state,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<string, int> potentialExtras,
            Dictionary<BuildingBase, long> marketProvidedValues,
            List<EconomyForecastEvent> events,
            List<ResidentialQualityForecast> residentialForecasts,
            List<string> warnings)
        {
            if (state.StartedUnderConstruction && !state.ConstructionCompleted)
            {
                ForecastConstruction(
                    building,
                    state,
                    turnOffset,
                    simulation,
                    turnLines,
                    marketProvidedValues,
                    events,
                    warnings);
                return;
            }

            if (state.StartedUnderConstruction)
            {
                if (!state.CompletionNoticeAdded)
                {
                    AddWarning(
                        warnings,
                        L10n.Gameplay(
                            "gameplay.economy.service.construction_completed_note",
                            "{0}：预测期内施工完成；竣工后的新运营能力从下一次预测开始计算。",
                            GetBuildingName(building)));
                    state.CompletionNoticeAdded = true;
                }

                return;
            }

            if (!building.IsOperational)
            {
                return;
            }

            var producedStandardResource = false;
            var modules = building.BuildingModules;
            for (var moduleIndex = 0; moduleIndex < modules.Count; moduleIndex++)
            {
                var module = modules[moduleIndex];
                if (module == null || !module.IsEnabled)
                {
                    continue;
                }

                bool succeeded;
                switch (module)
                {
                    case BM_居民运营 residential:
                        succeeded = ForecastResidential(
                            building,
                            residential,
                            state,
                            turnOffset,
                            simulation,
                            turnLines,
                            marketProvidedValues,
                            events,
                            residentialForecasts,
                            warnings);
                        break;
                    case BM_资源产出 production:
                        succeeded = ForecastProductionCycle(
                            building,
                            production,
                            state,
                            turnOffset,
                            simulation,
                            turnLines,
                            events,
                            warnings,
                            out producedStandardResource);
                        break;
                    case BM_资源加工 processing:
                        succeeded = ForecastProcessingCycle(
                            building,
                            processing,
                            state,
                            turnOffset,
                            simulation,
                            turnLines,
                            marketProvidedValues,
                            events,
                            warnings);
                        break;
                    case BuildingCropGrowthModule crop:
                        succeeded = ForecastCrop(
                            building,
                            crop,
                            state,
                            turnOffset,
                            simulation,
                            turnLines,
                            potentialExtras,
                            events,
                            warnings);
                        break;
                    case BM_稀有产出 rare:
                        succeeded = ForecastRareProduction(
                            building,
                            rare,
                            producedStandardResource,
                            turnOffset,
                            simulation,
                            turnLines,
                            potentialExtras,
                            events,
                            warnings);
                        break;
                    case BM_市场资源结算:
                        succeeded = true;
                        break;
                    default:
                        succeeded = ForecastGenericResourceModule(
                            building,
                            module,
                            turnOffset,
                            simulation,
                            turnLines,
                            events,
                            warnings);
                        break;
                }

                if (!succeeded)
                {
                    return;
                }
            }
        }

        private static void ForecastConstruction(
            BuildingBase building,
            BuildingPlanState state,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<BuildingBase, long> marketProvidedValues,
            List<EconomyForecastEvent> events,
            List<string> warnings)
        {
            var construction = building.FamilyDefinition?.Construction;
            if (construction == null)
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Construction,
                    L10n.Gameplay("gameplay.economy.service.construction_config_missing", "施工配置缺失"));
                return;
            }

            var turnIndex = Mathf.Clamp(state.ConstructionProgress, 0, construction.RequiredTurns - 1);
            var costs = construction.GetCosts(turnIndex);
            var rewards = construction.GetRewards(turnIndex);
            ResourceProviderSelection providerSelection = default;
            if (HasValidCosts(costs)
                && !BuildingResourceProviderSystem.TrySelectProvider(building, out providerSelection))
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Construction,
                    L10n.Gameplay("gameplay.economy.service.construction_provider_missing", "施工无法连接资源提供点"));
                return;
            }

            var inputs = ToItemAmounts(costs);
            var outputs = ToItemAmounts(rewards);
            if (!simulation.TryExchangeItems(inputs, outputs))
            {
                var shortage = RecordShortages(simulation, costs, turnLines);
                var reason = shortage > 0
                    ? L10n.Gameplay(
                        "gameplay.economy.service.construction_resources_missing",
                        "施工第 {0}/{1} 回合资源不足",
                        turnIndex + 1,
                        construction.RequiredTurns)
                    : L10n.Gameplay(
                        "gameplay.economy.service.construction_storage_blocked",
                        "施工第 {0}/{1} 回合产出无法入库",
                        turnIndex + 1,
                        construction.RequiredTurns);
                if (shortage <= 0)
                {
                    RecordOverflow(rewards, turnLines);
                }

                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Construction,
                    reason);
                return;
            }

            RecordConsumption(costs, turnLines);
            RecordProduction(rewards, turnLines);
            RecordMarketValue(
                providerSelection,
                costs,
                marketProvidedValues,
                simulation.Catalog);
            state.ConstructionProgress++;
            state.ConstructionCompleted = state.ConstructionProgress >= construction.RequiredTurns;
            var description = L10n.Gameplay(
                                  "gameplay.economy.service.construction_progress",
                                  "施工 {0}/{1}",
                                  turnIndex + 1,
                                  construction.RequiredTurns)
                              + FormatResourceChanges(costs, rewards)
                              + (state.ConstructionCompleted
                                  ? L10n.Gameplay("gameplay.economy.service.expected_completion", "，预计竣工")
                                  : string.Empty);
            events.Add(CreateEvent(
                turnOffset,
                building,
                EconomyForecastEventKind.Construction,
                EconomyForecastCertainty.Exact,
                description));
        }

        private bool ForecastResidential(
            BuildingBase building,
            BM_居民运营 residential,
            BuildingPlanState buildingState,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<BuildingBase, long> marketProvidedValues,
            List<EconomyForecastEvent> events,
            List<ResidentialQualityForecast> forecasts,
            List<string> warnings)
        {
            if (!buildingState.Residential.TryGetValue(residential, out var state))
            {
                state = new ResidentialPlanState
                {
                    Population = residential.CurrentPopulation,
                    GrowthProgress = residential.GrowthProgress,
                    TaxProgress = residential.TaxProgress,
                    ConsecutiveFailures = residential.ConsecutiveFailures,
                    IsAbandoned = residential.IsAbandoned,
                    LifeQuality = residential.CurrentLifeQuality
                };
                buildingState.Residential.Add(residential, state);
            }

            if (state.IsAbandoned || state.Population <= 0)
            {
                return false;
            }

            var populationBefore = state.Population;
            var qualityBefore = state.LifeQuality;
            ItemConsumptionReceipt receipt = null;
            var connected = BuildingResourceProviderSystem.TrySelectProvider(
                building,
                out var providerSelection);
            var foodSatisfied = connected
                                && residential.TryForecastFoodConsumption(
                                    simulation,
                                    state.Population,
                                    out receipt);
            if (receipt != null)
            {
                for (var i = 0; i < receipt.Lines.Count; i++)
                {
                    var line = receipt.Lines[i];
                    GetTurnLine(turnLines, line.ItemId).Consumption += line.Amount;
                }

                if (foodSatisfied)
                {
                    RecordMarketValue(
                        providerSelection,
                        receipt.Lines,
                        marketProvidedValues,
                        simulation.Catalog);
                }
            }

            var dietScore = foodSatisfied
                ? residential.CalculateDietScore(
                    receipt,
                    gameSystem.Services.Inventory.ItemCatalog,
                    populationBefore)
                : 0f;
            state.LifeQuality = Mathf.MoveTowards(
                state.LifeQuality,
                dietScore,
                residential.MaxLifeQualityChangePerTurn);

            if (!foodSatisfied)
            {
                state.GrowthProgress = 0;
                state.TaxProgress = 0;
                state.ConsecutiveFailures++;
                if (state.ConsecutiveFailures >= residential.FailureDecayThreshold)
                {
                    state.ConsecutiveFailures = 0;
                    state.Population = Mathf.Max(0, state.Population - 1);
                    state.IsAbandoned = state.Population <= 0;
                }

                var shortage = receipt == null
                    ? populationBefore
                    : Mathf.Max(0, populationBefore - receipt.TotalConsumed);
                if (shortage > 0
                    && receipt != null
                    && residential.FoodItemGroup == null
                    && residential.FoodItemDefinition != null)
                {
                    GetTurnLine(turnLines, residential.FoodItemDefinition.ItemId).Shortage += shortage;
                }

                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Warning,
                    connected
                        ? L10n.Gameplay("gameplay.economy.service.food_shortage", "预计食物不足 {0}", shortage)
                        : L10n.Gameplay("gameplay.economy.service.provider_unreachable", "预计无法连接资源提供点"));
            }
            else
            {
                state.ConsecutiveFailures = 0;
                if (state.Population >= residential.MaximumPopulation)
                {
                    state.GrowthProgress = 0;
                    state.TaxProgress++;
                    if (state.TaxProgress >= residential.TaxIntervalTurns)
                    {
                        var tax = new BuildingResourceChange(
                            residential.TaxItemDefinition?.ItemId,
                            state.Population);
                        if (!TryAddExact(
                                simulation,
                                tax,
                                turnLines,
                                out var overflow))
                        {
                            AddBlockedEvent(
                                events,
                                warnings,
                                turnOffset,
                                building,
                                EconomyForecastEventKind.Tax,
                                L10n.Gameplay("gameplay.economy.service.tax_storage_blocked", "预计税收无法入库 {0}", overflow));
                            return false;
                        }

                        state.TaxProgress = 0;
                        events.Add(CreateEvent(
                            turnOffset,
                            building,
                            EconomyForecastEventKind.Tax,
                            EconomyForecastCertainty.Exact,
                            L10n.Gameplay("gameplay.economy.service.tax_income", "预计税收 +{0} {1}", tax.Amount, tax.ItemId)));
                    }
                }
                else
                {
                    state.TaxProgress = 0;
                    state.GrowthProgress += state.LifeQuality >= residential.HighQualityGrowthThreshold
                        ? 2
                        : 1;
                    if (state.GrowthProgress >= residential.GrowthIntervalTurns)
                    {
                        state.GrowthProgress = 0;
                        state.Population = Mathf.Min(
                            residential.MaximumPopulation,
                            state.Population + 1);
                        events.Add(CreateEvent(
                            turnOffset,
                            building,
                            EconomyForecastEventKind.Population,
                            EconomyForecastCertainty.Conditional,
                            L10n.Gameplay("gameplay.economy.service.population_growth", "预计人口增长至 {0}", state.Population)));
                    }
                }
            }

            forecasts.Add(new ResidentialQualityForecast(
                turnOffset,
                building.InstanceId,
                GetBuildingName(building),
                populationBefore,
                state.Population,
                foodSatisfied,
                receipt?.DistinctItemCount ?? 0,
                dietScore,
                qualityBefore,
                state.LifeQuality));
            return foodSatisfied;
        }

        private static bool ForecastProductionCycle(
            BuildingBase building,
            BM_资源产出 production,
            BuildingPlanState state,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            List<EconomyForecastEvent> events,
            List<string> warnings,
            out bool producedResources)
        {
            producedResources = false;
            if (!BuildingWorkforceUtility.TryGetSource(building, out var workforce))
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Production,
                    L10n.Gameplay("gameplay.economy.service.production_workforce_missing", "生产缺少岗位配置"));
                return false;
            }

            var outputs = production.GetForecastResourceProductions(
                building,
                workforce.CurrentWorkers,
                workforce.MaxWorkers);
            if (outputs == null || outputs.Count == 0)
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Production,
                    L10n.Gameplay(
                        "gameplay.economy.service.workers_missing",
                        "工人不足（{0}/{1}）",
                        workforce.CurrentWorkers,
                        production.GetMinimumWorkersForProduction(workforce.MaxWorkers)));
                return false;
            }

            var progress = GetModuleProgress(state, production, production.ProductionProgress);
            progress = Mathf.Min(production.ProductionIntervalTurns, progress + 1);
            state.ModuleProgress[production] = progress;
            if (progress < production.ProductionIntervalTurns)
            {
                if (turnOffset == 1)
                {
                    events.Add(CreateEvent(
                        turnOffset,
                        building,
                        EconomyForecastEventKind.Production,
                        EconomyForecastCertainty.Conditional,
                        L10n.Gameplay("gameplay.economy.service.production_progress", "生产进度 {0}/{1}", progress, production.ProductionIntervalTurns)));
                }

                return true;
            }

            for (var i = 0; i < outputs.Count; i++)
            {
                if (TryAddExact(simulation, outputs[i], turnLines, out _))
                {
                    continue;
                }

                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Production,
                    L10n.Gameplay("gameplay.economy.service.item_storage_blocked", "{0} 预计无法入库", outputs[i].ItemId));
                return false;
            }

            state.ModuleProgress[production] = 0;
            producedResources = true;
            events.Add(CreateEvent(
                turnOffset,
                building,
                EconomyForecastEventKind.Production,
                EconomyForecastCertainty.Exact,
                L10n.Gameplay("gameplay.economy.service.production_cycle", "周期生产") + FormatResourceChanges(null, outputs)));
            return true;
        }

        private static bool ForecastProcessingCycle(
            BuildingBase building,
            BM_资源加工 processing,
            BuildingPlanState state,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<BuildingBase, long> marketProvidedValues,
            List<EconomyForecastEvent> events,
            List<string> warnings)
        {
            if (!processing.HasForecastRecipe)
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Processing,
                    L10n.Gameplay("gameplay.economy.service.processing_recipe_invalid", "加工配方无效"));
                return false;
            }

            if (BuildingWorkforceUtility.TryGetSource(building, out var workforce)
                && workforce.CurrentWorkers < Mathf.Min(processing.MinimumWorkers, workforce.MaxWorkers))
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Processing,
                    L10n.Gameplay("gameplay.economy.service.processing_workers_missing", "加工工人不足（{0}/{1}）", workforce.CurrentWorkers, processing.MinimumWorkers));
                return false;
            }

            var progress = GetModuleProgress(state, processing, processing.ProductionProgress);
            progress = Mathf.Min(processing.ProductionIntervalTurns, progress + 1);
            state.ModuleProgress[processing] = progress;
            if (progress < processing.ProductionIntervalTurns)
            {
                if (turnOffset == 1)
                {
                    events.Add(CreateEvent(
                        turnOffset,
                        building,
                        EconomyForecastEventKind.Processing,
                        EconomyForecastCertainty.Conditional,
                        L10n.Gameplay("gameplay.economy.service.processing_progress", "加工进度 {0}/{1}", progress, processing.ProductionIntervalTurns)));
                }

                return true;
            }

            if (!BuildingResourceProviderSystem.TrySelectProvider(
                    building,
                    out var providerSelection))
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Processing,
                    L10n.Gameplay("gameplay.economy.service.processing_provider_missing", "加工无法连接资源提供点"));
                return false;
            }

            var inputs = ToItemAmounts(processing.CurrentResourceConsumptions, simulation.Catalog);
            var outputs = ToItemAmounts(processing.CurrentResourceProductions, simulation.Catalog);
            if (!simulation.TryExchangeItems(inputs, outputs))
            {
                var shortage = RecordShortages(
                    simulation,
                    processing.CurrentResourceConsumptions,
                    turnLines);
                if (shortage <= 0)
                {
                    RecordOverflow(processing.CurrentResourceProductions, turnLines);
                }

                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Processing,
                    shortage > 0
                        ? L10n.Gameplay("gameplay.economy.service.processing_inputs_missing", "加工原料不足")
                        : L10n.Gameplay("gameplay.economy.service.processing_storage_blocked", "加工产出无法入库"));
                return false;
            }

            RecordConsumption(processing.CurrentResourceConsumptions, turnLines);
            RecordProduction(processing.CurrentResourceProductions, turnLines);
            RecordMarketValue(
                providerSelection,
                processing.CurrentResourceConsumptions,
                marketProvidedValues,
                simulation.Catalog);
            state.ModuleProgress[processing] = 0;
            events.Add(CreateEvent(
                turnOffset,
                building,
                EconomyForecastEventKind.Processing,
                EconomyForecastCertainty.Exact,
                L10n.Gameplay("gameplay.economy.service.processing_complete", "加工完成") + FormatResourceChanges(
                    processing.CurrentResourceConsumptions,
                    processing.CurrentResourceProductions)));
            return true;
        }

        private static bool ForecastCrop(
            BuildingBase building,
            BuildingCropGrowthModule crop,
            BuildingPlanState state,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<string, int> potentialExtras,
            List<EconomyForecastEvent> events,
            List<string> warnings)
        {
            if (!state.Crops.TryGetValue(crop, out var cropState))
            {
                cropState = new CropPlanState
                {
                    HasCrop = crop.HasCrop,
                    Progress = crop.GrowthProgressTurns,
                    RequiredTurns = crop.RequiredGrowTurns,
                    AutoHarvest = crop.AutoHarvestEnabled
                };
                state.Crops.Add(crop, cropState);
            }

            if (!cropState.HasCrop)
            {
                return true;
            }

            if (!BuildingWorkforceUtility.TryGetSource(building, out var workforce)
                || workforce.CurrentWorkers < workforce.MaxWorkers)
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Harvest,
                    L10n.Gameplay("gameplay.economy.service.crop_workers_missing", "农田工人不足，作物暂停生长"));
                return false;
            }

            if (cropState.Progress < cropState.RequiredTurns)
            {
                cropState.Progress++;
            }

            if (cropState.Progress < cropState.RequiredTurns)
            {
                if (turnOffset == 1)
                {
                    events.Add(CreateEvent(
                        turnOffset,
                        building,
                        EconomyForecastEventKind.Harvest,
                        EconomyForecastCertainty.Conditional,
                        L10n.Gameplay(
                            "gameplay.economy.service.crop_turns_remaining",
                            "{0} 成熟剩余 {1} 回合",
                            crop.PlantedCropDisplayName,
                            cropState.RequiredTurns - cropState.Progress)));
                }

                return true;
            }

            if (!cropState.AutoHarvest)
            {
                if (!cropState.ManualEventEmitted)
                {
                    events.Add(CreateEvent(
                        turnOffset,
                        building,
                        EconomyForecastEventKind.Harvest,
                        EconomyForecastCertainty.Manual,
                        L10n.Gameplay(
                            "gameplay.economy.service.crop_manual_harvest",
                            "{0} 可手动收获（尚未计入库存）",
                            crop.PlantedCropDisplayName)));
                    cropState.ManualEventEmitted = true;
                }

                return true;
            }

            var costs = crop.GetAutomaticHarvestCostForecast();
            var rewards = crop.GetHarvestRewardForecast();
            var minimumRewards = ToMinimumItemAmounts(rewards, simulation.Catalog);
            if (!simulation.CanAddItems(minimumRewards))
            {
                RecordOverflow(rewards, turnLines);
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Harvest,
                    L10n.Gameplay("gameplay.economy.service.auto_harvest_storage_blocked", "自动收获产出预计无法入库"));
                return false;
            }

            if (!TryConsumeChanges(simulation, costs, turnLines, out var shortage))
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Harvest,
                    L10n.Gameplay("gameplay.economy.service.auto_harvest_cost_missing", "自动收获费用不足 {0}", shortage));
                return false;
            }

            for (var i = 0; i < rewards.Count; i++)
            {
                AddProductionRange(
                    simulation,
                    rewards[i],
                    turnLines,
                    potentialExtras);
            }

            cropState.HasCrop = false;
            cropState.Progress = 0;
            var certainty = rewards.Any(value => value.IsRange)
                ? EconomyForecastCertainty.Range
                : EconomyForecastCertainty.Exact;
            events.Add(CreateEvent(
                turnOffset,
                building,
                EconomyForecastEventKind.Harvest,
                certainty,
                L10n.Gameplay("gameplay.economy.service.auto_harvest", "自动收获 {0}", crop.PlantedCropDisplayName) + FormatResourceRanges(costs, rewards)));
            return true;
        }

        private static bool ForecastRareProduction(
            BuildingBase building,
            BM_稀有产出 rare,
            bool standardProductionSucceeded,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<string, int> potentialExtras,
            List<EconomyForecastEvent> events,
            List<string> warnings)
        {
            if (!rare.RareProductionEnabled || rare.Amount <= 0 || !standardProductionSucceeded)
            {
                return true;
            }

            if (!BuildingWorkforceUtility.TryGetSource(building, out var workforce)
                || workforce.CurrentWorkers < rare.MinimumWorkers)
            {
                return true;
            }

            var minimum = rare.ChancePercent >= 100f ? rare.Amount : 0;
            var range = new BuildingResourceRange(rare.ForecastItemId, minimum, rare.Amount);
            if (!range.IsValid)
            {
                return true;
            }

            AddProductionRange(simulation, range, turnLines, potentialExtras);
            events.Add(CreateEvent(
                turnOffset,
                building,
                EconomyForecastEventKind.Production,
                range.IsRange ? EconomyForecastCertainty.Range : EconomyForecastCertainty.Exact,
                range.IsRange
                    ? L10n.Gameplay(
                        "gameplay.economy.service.rare_production_range",
                        "稀有产出概率 {0:0.##}%：{1} 0~{2}",
                        rare.ChancePercent,
                        range.ItemId,
                        range.MaximumAmount)
                    : L10n.Gameplay(
                        "gameplay.economy.service.rare_production",
                        "稀有产出 +{0} {1}",
                        range.MaximumAmount,
                        range.ItemId)));
            return true;
        }

        private static bool ForecastGenericResourceModule(
            BuildingBase building,
            BuildingModuleBase module,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            List<EconomyForecastEvent> events,
            List<string> warnings)
        {
            if (module is IBuildingResourceConsumptionSource consumption
                && !TryConsumeChanges(
                    simulation,
                    consumption.CurrentResourceConsumptions,
                    turnLines,
                    out var shortage))
            {
                AddBlockedEvent(
                    events,
                    warnings,
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Warning,
                    L10n.Gameplay("gameplay.economy.service.resource_shortage", "预计资源不足 {0}", shortage));
                return false;
            }

            if (module is IBuildingResourceProductionSource production)
            {
                var outputs = production.CurrentResourceProductions;
                for (var i = 0; i < outputs.Count; i++)
                {
                    if (!TryAddExact(simulation, outputs[i], turnLines, out _))
                    {
                        AddBlockedEvent(
                            events,
                            warnings,
                            turnOffset,
                            building,
                            EconomyForecastEventKind.Production,
                            L10n.Gameplay("gameplay.economy.service.item_storage_blocked", "{0} 预计无法入库", outputs[i].ItemId));
                        return false;
                    }
                }
            }

            if (module is IBuildingTaxSource tax)
            {
                var rewards = tax.CurrentTaxRewards;
                for (var i = 0; i < rewards.Count; i++)
                {
                    if (!TryAddExact(simulation, rewards[i], turnLines, out _))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static void ForecastMarketSettlement(
            IReadOnlyList<BuildingBase> buildings,
            Dictionary<BuildingBase, long> marketProvidedValues,
            int turnOffset,
            Inventory simulation,
            Dictionary<string, MutableTurnLine> turnLines,
            List<EconomyForecastEvent> events,
            List<string> warnings)
        {
            if (buildings == null || marketProvidedValues == null || marketProvidedValues.Count == 0)
            {
                return;
            }

            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
                    || !marketProvidedValues.TryGetValue(building, out var providedValue)
                    || providedValue <= 0
                    || !building.TryGetCapability<BM_市场资源结算>(out var market))
                {
                    continue;
                }

                var income = (int)Math.Min(
                    int.MaxValue,
                    Math.Floor(providedValue * market.IncomeRatio));
                if (income <= 0)
                {
                    continue;
                }

                var reward = new BuildingResourceChange(
                    market.GoldItemDefinition?.ItemId,
                    income);
                if (!TryAddExact(simulation, reward, turnLines, out var overflow))
                {
                    AddBlockedEvent(
                        events,
                        warnings,
                        turnOffset,
                        building,
                        EconomyForecastEventKind.Market,
                        L10n.Gameplay("gameplay.economy.service.market_storage_blocked", "市场收入无法入库 {0}", overflow));
                    continue;
                }

                events.Add(CreateEvent(
                    turnOffset,
                    building,
                    EconomyForecastEventKind.Market,
                    EconomyForecastCertainty.Conditional,
                    L10n.Gameplay(
                        "gameplay.economy.service.market_income",
                        "经手价值 {0}，预计金币 +{1}",
                        providedValue,
                        income)));
            }
        }

        private static bool TryConsumeChanges(
            Inventory simulation,
            IReadOnlyList<BuildingResourceChange> resources,
            Dictionary<string, MutableTurnLine> turnLines,
            out int totalShortage)
        {
            totalShortage = 0;
            if (resources == null || resources.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.IsValid)
                {
                    continue;
                }

                var available = simulation.GetQuantity(resource.ItemId);
                if (available >= resource.Amount)
                {
                    continue;
                }

                var shortage = resource.Amount - available;
                GetTurnLine(turnLines, resource.ItemId).Shortage += shortage;
                totalShortage += shortage;
            }

            if (totalShortage > 0)
            {
                return false;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.IsValid)
                {
                    continue;
                }

                simulation.Remove(resource.ItemId, resource.Amount);
                GetTurnLine(turnLines, resource.ItemId).Consumption += resource.Amount;
            }

            return true;
        }

        private static bool TryAddExact(
            Inventory simulation,
            BuildingResourceChange resource,
            Dictionary<string, MutableTurnLine> turnLines,
            out int overflow)
        {
            overflow = 0;
            if (!resource.IsValid)
            {
                return true;
            }

            if (simulation.TryAdd(resource.ItemId, resource.Amount))
            {
                var line = GetTurnLine(turnLines, resource.ItemId);
                line.MinimumProduction += resource.Amount;
                line.MaximumProduction += resource.Amount;
                return true;
            }

            overflow = resource.Amount;
            GetTurnLine(turnLines, resource.ItemId).Overflow += overflow;
            return false;
        }

        private static void AddProductionRange(
            Inventory simulation,
            BuildingResourceRange resource,
            Dictionary<string, MutableTurnLine> turnLines,
            Dictionary<string, int> potentialExtras)
        {
            if (!resource.IsValid)
            {
                return;
            }

            var line = GetTurnLine(turnLines, resource.ItemId);
            var added = resource.MinimumAmount <= 0
                ? 0
                : simulation.Add(resource.ItemId, resource.MinimumAmount);
            line.MinimumProduction += added;
            line.MaximumProduction += Mathf.Max(added, resource.MaximumAmount);
            if (added < resource.MinimumAmount)
            {
                line.Overflow += resource.MinimumAmount - added;
            }

            var extra = Mathf.Max(0, resource.MaximumAmount - added);
            potentialExtras.TryGetValue(resource.ItemId, out var current);
            potentialExtras[resource.ItemId] = current + extra;
        }

        private static void RecordMarketValue(
            ResourceProviderSelection selection,
            IReadOnlyList<BuildingCost> resources,
            Dictionary<BuildingBase, long> marketProvidedValues,
            ItemCatalog catalog)
        {
            if (!CanRecordMarketValue(selection, marketProvidedValues, catalog) || resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                if (resources[i].IsValid)
                {
                    AddMarketValue(
                        selection.Provider,
                        resources[i].ItemId,
                        resources[i].Amount,
                        marketProvidedValues,
                        catalog);
                }
            }
        }

        private static void RecordMarketValue(
            ResourceProviderSelection selection,
            IReadOnlyList<BuildingResourceChange> resources,
            Dictionary<BuildingBase, long> marketProvidedValues,
            ItemCatalog catalog)
        {
            if (!CanRecordMarketValue(selection, marketProvidedValues, catalog) || resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                if (resources[i].IsValid)
                {
                    AddMarketValue(
                        selection.Provider,
                        resources[i].ItemId,
                        resources[i].Amount,
                        marketProvidedValues,
                        catalog);
                }
            }
        }

        private static void RecordMarketValue(
            ResourceProviderSelection selection,
            IReadOnlyList<ItemConsumptionLine> resources,
            Dictionary<BuildingBase, long> marketProvidedValues,
            ItemCatalog catalog)
        {
            if (!CanRecordMarketValue(selection, marketProvidedValues, catalog) || resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                if (resources[i].IsValid)
                {
                    AddMarketValue(
                        selection.Provider,
                        resources[i].ItemId,
                        resources[i].Amount,
                        marketProvidedValues,
                        catalog);
                }
            }
        }

        private static bool CanRecordMarketValue(
            ResourceProviderSelection selection,
            Dictionary<BuildingBase, long> marketProvidedValues,
            ItemCatalog catalog) =>
            selection.IsValid
            && marketProvidedValues != null
            && catalog != null
            && selection.Provider.TryGetCapability<BM_市场资源结算>(out _);

        private static void AddMarketValue(
            BuildingBase marketBuilding,
            string itemId,
            int amount,
            Dictionary<BuildingBase, long> marketProvidedValues,
            ItemCatalog catalog)
        {
            if (marketBuilding == null
                || amount <= 0
                || !catalog.TryGetDefinition(itemId, out var definition)
                || definition == null)
            {
                return;
            }

            var value = (long)Mathf.Max(0, definition.BaseValue) * amount;
            marketProvidedValues.TryGetValue(marketBuilding, out var current);
            marketProvidedValues[marketBuilding] = value > long.MaxValue - current
                ? long.MaxValue
                : current + value;
        }

        private static bool HasValidCosts(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null)
            {
                return false;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static int RecordShortages(
            Inventory simulation,
            IReadOnlyList<BuildingCost> costs,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            var shortage = 0;
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (!cost.IsValid)
                {
                    continue;
                }

                var missing = Mathf.Max(0, cost.Amount - simulation.GetQuantity(cost.ItemId));
                GetTurnLine(turnLines, cost.ItemId).Shortage += missing;
                shortage += missing;
            }

            return shortage;
        }

        private static int RecordShortages(
            Inventory simulation,
            IReadOnlyList<BuildingResourceChange> costs,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            var shortage = 0;
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (!cost.IsValid)
                {
                    continue;
                }

                var missing = Mathf.Max(0, cost.Amount - simulation.GetQuantity(cost.ItemId));
                GetTurnLine(turnLines, cost.ItemId).Shortage += missing;
                shortage += missing;
            }

            return shortage;
        }

        private static void RecordConsumption(
            IReadOnlyList<BuildingCost> costs,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    GetTurnLine(turnLines, costs[i].ItemId).Consumption += costs[i].Amount;
                }
            }
        }

        private static void RecordConsumption(
            IReadOnlyList<BuildingResourceChange> costs,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    GetTurnLine(turnLines, costs[i].ItemId).Consumption += costs[i].Amount;
                }
            }
        }

        private static void RecordProduction(
            IReadOnlyList<BuildingCost> rewards,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < rewards.Count; i++)
            {
                if (!rewards[i].IsValid)
                {
                    continue;
                }

                var line = GetTurnLine(turnLines, rewards[i].ItemId);
                line.MinimumProduction += rewards[i].Amount;
                line.MaximumProduction += rewards[i].Amount;
            }
        }

        private static void RecordProduction(
            IReadOnlyList<BuildingResourceChange> rewards,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < rewards.Count; i++)
            {
                if (!rewards[i].IsValid)
                {
                    continue;
                }

                var line = GetTurnLine(turnLines, rewards[i].ItemId);
                line.MinimumProduction += rewards[i].Amount;
                line.MaximumProduction += rewards[i].Amount;
            }
        }

        private static void RecordOverflow(
            IReadOnlyList<BuildingCost> rewards,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].IsValid)
                {
                    GetTurnLine(turnLines, rewards[i].ItemId).Overflow += rewards[i].Amount;
                }
            }
        }

        private static void RecordOverflow(
            IReadOnlyList<BuildingResourceChange> rewards,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].IsValid)
                {
                    GetTurnLine(turnLines, rewards[i].ItemId).Overflow += rewards[i].Amount;
                }
            }
        }

        private static void RecordOverflow(
            IReadOnlyList<BuildingResourceRange> rewards,
            Dictionary<string, MutableTurnLine> turnLines)
        {
            for (var i = 0; i < rewards.Count; i++)
            {
                if (rewards[i].IsValid)
                {
                    GetTurnLine(turnLines, rewards[i].ItemId).Overflow += rewards[i].MinimumAmount;
                }
            }
        }

        private static Dictionary<string, int> CaptureQuantities(Inventory inventory)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (inventory == null)
            {
                return result;
            }

            for (var i = 0; i < inventory.Slots.Count; i++)
            {
                var slot = inventory.Slots[i];
                if (slot.IsEmpty)
                {
                    continue;
                }

                result.TryGetValue(slot.ItemId, out var current);
                result[slot.ItemId] = current + slot.Quantity;
            }

            return result;
        }

        private static int CountOccupiedSlots(Inventory inventory)
        {
            var count = 0;
            if (inventory == null)
            {
                return count;
            }

            for (var i = 0; i < inventory.Slots.Count; i++)
            {
                if (!inventory.Slots[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private static MutableTimeline GetTimeline(
            Dictionary<string, MutableTimeline> timelines,
            string itemId)
        {
            itemId = NormalizeItemId(itemId);
            if (!timelines.TryGetValue(itemId, out var timeline))
            {
                timeline = new MutableTimeline();
                timelines.Add(itemId, timeline);
            }

            return timeline;
        }

        private static MutableTurnLine GetTurnLine(
            Dictionary<string, MutableTurnLine> lines,
            string itemId)
        {
            itemId = NormalizeItemId(itemId);
            if (!lines.TryGetValue(itemId, out var line))
            {
                line = new MutableTurnLine();
                lines.Add(itemId, line);
            }

            return line;
        }

        private static int GetModuleProgress(
            BuildingPlanState state,
            BuildingModuleBase module,
            int initialProgress)
        {
            if (!state.ModuleProgress.TryGetValue(module, out var progress))
            {
                progress = Mathf.Max(0, initialProgress);
                state.ModuleProgress.Add(module, progress);
            }

            return progress;
        }

        private static ItemAmount[] ToItemAmounts(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null || costs.Count == 0)
            {
                return Array.Empty<ItemAmount>();
            }

            var result = new List<ItemAmount>(costs.Count);
            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    result.Add(new ItemAmount(costs[i].ItemDefinition, costs[i].Amount));
                }
            }

            return result.ToArray();
        }

        private static ItemAmount[] ToItemAmounts(
            IReadOnlyList<BuildingResourceChange> changes,
            ItemCatalog catalog)
        {
            if (changes == null || changes.Count == 0 || catalog == null)
            {
                return Array.Empty<ItemAmount>();
            }

            var result = new List<ItemAmount>(changes.Count);
            for (var i = 0; i < changes.Count; i++)
            {
                var change = changes[i];
                if (change.IsValid && catalog.TryGetDefinition(change.ItemId, out var definition))
                {
                    result.Add(new ItemAmount(definition, change.Amount));
                }
            }

            return result.ToArray();
        }

        private static ItemAmount[] ToMinimumItemAmounts(
            IReadOnlyList<BuildingResourceRange> ranges,
            ItemCatalog catalog)
        {
            if (ranges == null || ranges.Count == 0 || catalog == null)
            {
                return Array.Empty<ItemAmount>();
            }

            var result = new List<ItemAmount>(ranges.Count);
            for (var i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                if (range.MinimumAmount > 0
                    && catalog.TryGetDefinition(range.ItemId, out var definition))
                {
                    result.Add(new ItemAmount(definition, range.MinimumAmount));
                }
            }

            return result.ToArray();
        }

        private static EconomyForecastEvent CreateEvent(
            int turnOffset,
            BuildingBase building,
            EconomyForecastEventKind kind,
            EconomyForecastCertainty certainty,
            string description,
            bool blocked = false) =>
            new EconomyForecastEvent(
                turnOffset,
                building?.InstanceId,
                GetBuildingName(building),
                kind,
                certainty,
                description,
                blocked);

        private static void AddBlockedEvent(
            List<EconomyForecastEvent> events,
            List<string> warnings,
            int turnOffset,
            BuildingBase building,
            EconomyForecastEventKind kind,
            string reason)
        {
            events.Add(CreateEvent(
                turnOffset,
                building,
                kind,
                EconomyForecastCertainty.Conditional,
                reason,
                true));
            AddWarning(warnings, L10n.Gameplay(
                "gameplay.economy.service.blocked_warning",
                "T+{0} {1}：{2}。",
                turnOffset,
                GetBuildingName(building),
                reason));
        }

        private static void AddWarning(List<string> warnings, string warning)
        {
            if (!string.IsNullOrWhiteSpace(warning) && !warnings.Contains(warning))
            {
                warnings.Add(warning);
            }
        }

        private static string FormatResourceChanges(
            IReadOnlyList<BuildingCost> costs,
            IReadOnlyList<BuildingCost> rewards)
        {
            var parts = new List<string>();
            if (costs != null)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    if (costs[i].IsValid)
                    {
                        parts.Add($"-{costs[i].Amount} {costs[i].ItemId}");
                    }
                }
            }

            if (rewards != null)
            {
                for (var i = 0; i < rewards.Count; i++)
                {
                    if (rewards[i].IsValid)
                    {
                        parts.Add($"+{rewards[i].Amount} {rewards[i].ItemId}");
                    }
                }
            }

            return parts.Count == 0
                ? string.Empty
                : L10n.Gameplay("gameplay.economy.service.resource_changes", "：{0}", string.Join(L10n.Gameplay("gameplay.common.comma", "，"), parts));
        }

        private static string FormatResourceChanges(
            IReadOnlyList<BuildingResourceChange> costs,
            IReadOnlyList<BuildingResourceChange> rewards)
        {
            var parts = new List<string>();
            if (costs != null)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    if (costs[i].IsValid)
                    {
                        parts.Add($"-{costs[i].Amount} {costs[i].ItemId}");
                    }
                }
            }

            if (rewards != null)
            {
                for (var i = 0; i < rewards.Count; i++)
                {
                    if (rewards[i].IsValid)
                    {
                        parts.Add($"+{rewards[i].Amount} {rewards[i].ItemId}");
                    }
                }
            }

            return parts.Count == 0
                ? string.Empty
                : L10n.Gameplay("gameplay.economy.service.resource_changes", "：{0}", string.Join(L10n.Gameplay("gameplay.common.comma", "，"), parts));
        }

        private static string FormatResourceRanges(
            IReadOnlyList<BuildingResourceChange> costs,
            IReadOnlyList<BuildingResourceRange> rewards)
        {
            var parts = new List<string>();
            if (costs != null)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    if (costs[i].IsValid)
                    {
                        parts.Add($"-{costs[i].Amount} {costs[i].ItemId}");
                    }
                }
            }

            if (rewards != null)
            {
                for (var i = 0; i < rewards.Count; i++)
                {
                    var reward = rewards[i];
                    if (!reward.IsValid)
                    {
                        continue;
                    }

                    parts.Add(reward.IsRange
                        ? $"+{reward.MinimumAmount}~{reward.MaximumAmount} {reward.ItemId}"
                        : $"+{reward.MaximumAmount} {reward.ItemId}");
                }
            }

            return parts.Count == 0
                ? string.Empty
                : L10n.Gameplay("gameplay.economy.service.resource_changes", "：{0}", string.Join(L10n.Gameplay("gameplay.common.comma", "，"), parts));
        }

        private static int CompareEvents(EconomyForecastEvent left, EconomyForecastEvent right)
        {
            var byTurn = left.TurnOffset.CompareTo(right.TurnOffset);
            if (byTurn != 0)
            {
                return byTurn;
            }

            if (left.IsBlocked != right.IsBlocked)
            {
                return left.IsBlocked ? -1 : 1;
            }

            return string.Compare(
                left.BuildingDisplayName,
                right.BuildingDisplayName,
                StringComparison.Ordinal);
        }

        private static EconomyForecastTimelineResult EmptyTimeline(int turns, string warning) =>
            new EconomyForecastTimelineResult(
                turns,
                Array.Empty<EconomyForecastResourceTimeline>(),
                Array.Empty<EconomyForecastEvent>(),
                Array.Empty<InventorySlotLoss>(),
                Array.Empty<ResidentialQualityForecast>(),
                new[] { warning },
                0,
                0,
                0,
                true);

        private static string NormalizeItemId(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string GetBuildingName(BuildingBase building) =>
            building == null || building.Definition == null
                ? L10n.Gameplay("gameplay.common.building", "建筑")
                : building.Definition.DisplayName;
    }
}
