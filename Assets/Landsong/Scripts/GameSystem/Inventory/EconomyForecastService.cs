using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using UnityEngine;

namespace Landsong.InventorySystem
{
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
        {
            BuildingInstanceId = buildingInstanceId ?? string.Empty;
            BuildingDisplayName = buildingDisplayName ?? string.Empty;
            Population = Mathf.Max(0, population);
            FoodSatisfied = foodSatisfied;
            PredictedDietVariety = Mathf.Max(0, predictedDietVariety);
            PredictedDietScore = Mathf.Clamp(predictedDietScore, 0f, 100f);
            CurrentLifeQuality = Mathf.Clamp(currentLifeQuality, 0f, 100f);
        }

        public string BuildingInstanceId { get; }
        public string BuildingDisplayName { get; }
        public int Population { get; }
        public bool FoodSatisfied { get; }
        public int PredictedDietVariety { get; }
        public float PredictedDietScore { get; }
        public float CurrentLifeQuality { get; }
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
            ResidentialForecasts =
                residentialForecasts ?? Array.Empty<ResidentialQualityForecast>();
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
        private sealed class MutableLine
        {
            public int Current;
            public int Consumption;
            public int Production;
            public int Loss;
            public int Projected;
            public int Shortage;
        }

        private readonly Landsong.GameSystem gameSystem;
        private readonly List<IBuildingResourceConsumptionSource> consumptionSources =
            new List<IBuildingResourceConsumptionSource>();
        private readonly List<IBuildingResourceProductionSource> productionSources =
            new List<IBuildingResourceProductionSource>();
        private readonly List<IBuildingTaxSource> taxSources =
            new List<IBuildingTaxSource>();
        private readonly List<BM_居民运营> residentialModules =
            new List<BM_居民运营>();

        public EconomyForecastService(Landsong.GameSystem gameSystem)
        {
            this.gameSystem = gameSystem;
        }

        public EconomyForecastResult ForecastNextTurn()
        {
            var inventoryService = gameSystem?.Services.Inventory;
            var actualInventory = inventoryService?.Inventory;
            if (actualInventory == null)
            {
                return new EconomyForecastResult(
                    Array.Empty<EconomyForecastLine>(),
                    Array.Empty<InventorySlotLoss>(),
                    Array.Empty<ResidentialQualityForecast>(),
                    new[] { "库存服务未初始化，无法生成经济预测。" },
                    true);
            }

            var simulation = actualInventory.CreateSimulation();
            var mutableLines = new Dictionary<string, MutableLine>(StringComparer.Ordinal);
            var warnings = new List<string>();
            var residentialForecasts = new List<ResidentialQualityForecast>();
            CaptureCurrentQuantities(actualInventory, mutableLines);

            var buildings = gameSystem.Services.Buildings?.Buildings;
            if (buildings != null)
            {
                for (var i = 0; i < buildings.Count; i++)
                {
                    ForecastBuilding(
                        buildings[i],
                        simulation,
                        mutableLines,
                        residentialForecasts,
                        warnings);
                }
            }

            var losses = simulation.ProcessTurnLosses();
            for (var i = 0; i < losses.Count; i++)
            {
                GetLine(mutableLines, losses[i].ItemId).Loss += losses[i].AmountLost;
            }

            CaptureProjectedQuantities(simulation, mutableLines);
            var lines = BuildLines(mutableLines);
            if (warnings.Count == 0)
            {
                warnings.Add("预测按当前建筑状态计算；随机产出、施工阶段变化与回合中临时效果仍属于条件项。");
            }

            return new EconomyForecastResult(
                lines,
                losses,
                residentialForecasts,
                warnings,
                true);
        }

        private void ForecastBuilding(
            BuildingBase building,
            Inventory simulation,
            Dictionary<string, MutableLine> lines,
            List<ResidentialQualityForecast> residentialForecasts,
            List<string> warnings)
        {
            if (building == null
                || building.IsDemolishing
                || !building.IsInitialized
                || !building.IsOperational)
            {
                return;
            }

            var forecastedResidential = false;
            var canProduce = true;
            residentialModules.Clear();
            building.GetModules(residentialModules);
            for (var i = 0; i < residentialModules.Count; i++)
            {
                canProduce &= ForecastResidential(
                    building,
                    residentialModules[i],
                    simulation,
                    lines,
                    residentialForecasts,
                    warnings);
                forecastedResidential = true;
            }

            consumptionSources.Clear();
            building.GetCapabilities(consumptionSources);
            for (var i = 0; i < consumptionSources.Count; i++)
            {
                if (forecastedResidential && consumptionSources[i] is BM_居民运营)
                {
                    continue;
                }

                canProduce &= ForecastExactConsumption(
                    building,
                    consumptionSources[i].CurrentResourceConsumptions,
                    simulation,
                    lines,
                    warnings);
            }

            if (!canProduce)
            {
                residentialModules.Clear();
                consumptionSources.Clear();
                productionSources.Clear();
                taxSources.Clear();
                return;
            }

            productionSources.Clear();
            building.GetCapabilities(productionSources);
            for (var i = 0; i < productionSources.Count; i++)
            {
                ForecastProduction(
                    building,
                    productionSources[i].CurrentResourceProductions,
                    simulation,
                    lines,
                    warnings);
            }

            taxSources.Clear();
            building.GetCapabilities(taxSources);
            for (var i = 0; i < taxSources.Count; i++)
            {
                ForecastProduction(
                    building,
                    taxSources[i].CurrentTaxRewards,
                    simulation,
                    lines,
                    warnings);
            }

            residentialModules.Clear();
            consumptionSources.Clear();
            productionSources.Clear();
            taxSources.Clear();
        }

        private bool ForecastResidential(
            BuildingBase building,
            BM_居民运营 residential,
            Inventory simulation,
            Dictionary<string, MutableLine> lines,
            List<ResidentialQualityForecast> forecasts,
            List<string> warnings)
        {
            if (residential == null)
            {
                return true;
            }

            if (residential.CurrentPopulation > 0
                && !BuildingResourceProviderSystem.TrySelectProvider(building, out _))
            {
                warnings.Add($"{GetBuildingName(building)}：预计无法连接资源提供点。");
                forecasts.Add(new ResidentialQualityForecast(
                    building.InstanceId,
                    GetBuildingName(building),
                    residential.CurrentPopulation,
                    false,
                    0,
                    0f,
                    residential.CurrentLifeQuality));
                return false;
            }

            var succeeded = residential.TryForecastFoodConsumption(simulation, out var receipt);
            if (receipt != null)
            {
                for (var i = 0; i < receipt.Lines.Count; i++)
                {
                    GetLine(lines, receipt.Lines[i].ItemId).Consumption += receipt.Lines[i].Amount;
                }
            }

            var expected = residential.CurrentPopulation;
            var consumed = receipt == null ? 0 : receipt.TotalConsumed;
            if (!succeeded && expected > 0)
            {
                warnings.Add($"{GetBuildingName(building)}：预计食物不足 {Mathf.Max(0, expected - consumed)}。");
            }

            forecasts.Add(new ResidentialQualityForecast(
                building.InstanceId,
                GetBuildingName(building),
                expected,
                succeeded,
                receipt == null ? 0 : receipt.DistinctItemCount,
                succeeded
                    ? residential.CalculateDietScore(
                        receipt,
                        gameSystem.Services.Inventory.ItemCatalog,
                        expected)
                    : 0f,
                residential.CurrentLifeQuality));
            return succeeded;
        }

        private static bool ForecastExactConsumption(
            BuildingBase building,
            IReadOnlyList<BuildingResourceChange> resources,
            Inventory simulation,
            Dictionary<string, MutableLine> lines,
            List<string> warnings)
        {
            if (resources == null)
            {
                return true;
            }

            var succeeded = true;
            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.IsValid || simulation.Catalog == null
                    || !simulation.Catalog.Contains(resource.ItemId))
                {
                    continue;
                }

                var available = simulation.GetQuantity(resource.ItemId);
                var line = GetLine(lines, resource.ItemId);
                if (available >= resource.Amount)
                {
                    simulation.Remove(resource.ItemId, resource.Amount);
                    line.Consumption += resource.Amount;
                    continue;
                }

                var shortage = resource.Amount - available;
                line.Shortage += shortage;
                succeeded = false;
                warnings.Add(
                    $"{GetBuildingName(building)}：{resource.ItemId} 预计缺少 {shortage}（当前可用 {available}）。");
            }

            return succeeded;
        }

        private static void ForecastProduction(
            BuildingBase building,
            IReadOnlyList<BuildingResourceChange> resources,
            Inventory simulation,
            Dictionary<string, MutableLine> lines,
            List<string> warnings)
        {
            if (resources == null)
            {
                return;
            }

            for (var i = 0; i < resources.Count; i++)
            {
                var resource = resources[i];
                if (!resource.IsValid)
                {
                    continue;
                }

                var added = simulation.Add(resource.ItemId, resource.Amount);
                GetLine(lines, resource.ItemId).Production += added;
                if (added < resource.Amount)
                {
                    warnings.Add(
                        $"{GetBuildingName(building)}：{resource.ItemId} 预计有 {resource.Amount - added} 无法入库。");
                }
            }
        }

        private static void CaptureCurrentQuantities(
            Inventory inventory,
            Dictionary<string, MutableLine> lines)
        {
            for (var i = 0; i < inventory.Slots.Count; i++)
            {
                var slot = inventory.Slots[i];
                if (!slot.IsEmpty)
                {
                    GetLine(lines, slot.ItemId).Current += slot.Quantity;
                }
            }
        }

        private static void CaptureProjectedQuantities(
            Inventory inventory,
            Dictionary<string, MutableLine> lines)
        {
            for (var i = 0; i < inventory.Slots.Count; i++)
            {
                var slot = inventory.Slots[i];
                if (!slot.IsEmpty)
                {
                    GetLine(lines, slot.ItemId).Projected += slot.Quantity;
                }
            }
        }

        private static IReadOnlyList<EconomyForecastLine> BuildLines(
            Dictionary<string, MutableLine> mutableLines)
        {
            var itemIds = new List<string>(mutableLines.Keys);
            itemIds.Sort(StringComparer.Ordinal);
            var result = new List<EconomyForecastLine>(itemIds.Count);
            for (var i = 0; i < itemIds.Count; i++)
            {
                var mutable = mutableLines[itemIds[i]];
                result.Add(new EconomyForecastLine(
                    itemIds[i],
                    mutable.Current,
                    mutable.Consumption,
                    mutable.Production,
                    mutable.Loss,
                    mutable.Projected,
                    mutable.Shortage));
            }

            return result;
        }

        private static MutableLine GetLine(
            Dictionary<string, MutableLine> lines,
            string itemId)
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            if (!lines.TryGetValue(itemId, out var line))
            {
                line = new MutableLine();
                lines.Add(itemId, line);
            }

            return line;
        }

        private static string GetBuildingName(BuildingBase building)
        {
            return building == null || building.Definition == null
                ? "建筑"
                : building.Definition.DisplayName;
        }
    }
}
