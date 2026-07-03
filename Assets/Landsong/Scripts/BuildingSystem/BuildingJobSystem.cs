using System;
using System.Collections.Generic;
using System.Text;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public interface IBuildingPopulationSource
    {
        int CurrentPopulation { get; }
    }

    public interface IBuildingJobSource
    {
        int CurrentWorkers { get; }
        int MaxWorkers { get; }
        int StableWorkers { get; }
        float RawJobAttraction { get; }
        float JobAttraction { get; }
    }

    public readonly struct BuildingWorkforceAttractionFactor
    {
        public BuildingWorkforceAttractionFactor(string label, float value)
        {
            Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
            Value = value;
        }

        public string Label { get; }
        public float Value { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Label) || !Mathf.Approximately(Value, 0f);
    }

    public interface IBuildingWorkforceFundingSource : IBuildingJobSource
    {
        bool AutoFullWorkerSubsidyEnabled { get; }
        int TargetStableWorkers { get; }
        int TargetSubsidyGoldPerTurn { get; }
        int PaidSubsidyGoldThisTurn { get; }
        int MissingWorkersToFull { get; }
        int RecruitToFullWorkerCount { get; }
        int RecruitToFullCost { get; }
        float JobAttractionWithoutSubsidy { get; }
        float SubsidyAttractionPerGold { get; }
        float SubsidyAttractionBonus { get; }
        float TargetSubsidyAttractionBonus { get; }
        float PreviewJobAttractionWithTargetSubsidy { get; }
        float FullWorkerRequiredAttraction { get; }
        float JobAttractionGapToFullWorkers { get; }
        IReadOnlyList<BuildingWorkforceAttractionFactor> WorkforceAttractionFactors { get; }

        void SetAutoFullWorkerSubsidyEnabled(bool enabled);
        void SetTargetStableWorkers(int targetStableWorkers);
        bool TryRecruitToFull();
    }

    public readonly struct BuildingJobAttractionModifier
    {
        public BuildingJobAttractionModifier(
            string id,
            string displayName,
            float value,
            string sourceType = null,
            string description = null)
        {
            Id = NormalizeText(id);
            DisplayName = NormalizeText(displayName);
            Value = value;
            SourceType = NormalizeText(sourceType);
            Description = NormalizeText(description);
        }

        public string Id { get; }
        public string DisplayName { get; }
        public float Value { get; }
        public string SourceType { get; }
        public string Description { get; }
        public string DisplayText => string.IsNullOrWhiteSpace(DisplayName) ? Id : DisplayName;
        public bool IsValid => !Mathf.Approximately(Value, 0f) || !string.IsNullOrWhiteSpace(DisplayText);

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }
    }

    public interface IBuildingJobAttractionModifierProvider
    {
        IReadOnlyList<BuildingJobAttractionModifier> GetJobAttractionModifiers(BuildingBase building);
    }

    public readonly struct BuildingJobCalculationInput
    {
        public BuildingJobCalculationInput(
            int maxWorkers,
            int currentWorkers,
            float baseAttraction,
            int nearbyPopulation,
            int populationCellCount,
            float attractionPerNearbyPopulation,
            IReadOnlyList<BuildingJobAttractionModifier> globalAttractionModifiers,
            float subsidyAttractionBonus,
            int singleRecruitCost)
        {
            var normalizedMaxWorkers = Mathf.Max(0, maxWorkers);

            MaxWorkers = normalizedMaxWorkers;
            CurrentWorkers = Mathf.Clamp(currentWorkers, 0, normalizedMaxWorkers);
            BaseAttraction = Mathf.Max(0f, baseAttraction);
            NearbyPopulation = Mathf.Max(0, nearbyPopulation);
            PopulationCellCount = Mathf.Max(0, populationCellCount);
            AttractionPerNearbyPopulation = Mathf.Max(0f, attractionPerNearbyPopulation);
            GlobalAttractionModifiers = NormalizeModifiers(globalAttractionModifiers);
            SubsidyAttractionBonus = Mathf.Max(0f, subsidyAttractionBonus);
            SingleRecruitCost = Mathf.Max(0, singleRecruitCost);
        }

        public int MaxWorkers { get; }
        public int CurrentWorkers { get; }
        public float BaseAttraction { get; }
        public int NearbyPopulation { get; }
        public int PopulationCellCount { get; }
        public float AttractionPerNearbyPopulation { get; }
        public IReadOnlyList<BuildingJobAttractionModifier> GlobalAttractionModifiers { get; }
        public float SubsidyAttractionBonus { get; }
        public int SingleRecruitCost { get; }

        private static IReadOnlyList<BuildingJobAttractionModifier> NormalizeModifiers(
            IReadOnlyList<BuildingJobAttractionModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return Array.Empty<BuildingJobAttractionModifier>();
            }

            List<BuildingJobAttractionModifier> normalized = null;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (!modifier.IsValid)
                {
                    continue;
                }

                normalized ??= new List<BuildingJobAttractionModifier>(modifiers.Count);
                normalized.Add(modifier);
            }

            return normalized == null ? Array.Empty<BuildingJobAttractionModifier>() : normalized.ToArray();
        }
    }

    public readonly struct BuildingJobCalculation
    {
        public BuildingJobCalculation(BuildingJobCalculationInput input)
        {
            var maxWorkers = input.MaxWorkers;
            var currentWorkers = input.CurrentWorkers;
            var baseAttraction = input.BaseAttraction;
            var nearbyPopulation = input.NearbyPopulation;
            var populationCellCount = input.PopulationCellCount;
            var attractionPerNearbyPopulation = input.AttractionPerNearbyPopulation;
            var globalAttractionModifiers = input.GlobalAttractionModifiers;
            var subsidyAttractionBonus = input.SubsidyAttractionBonus;
            var singleRecruitCost = input.SingleRecruitCost;
            var populationDensity = populationCellCount <= 0 ? 0f : nearbyPopulation / (float)populationCellCount;
            var populationAttractionBonus = nearbyPopulation * attractionPerNearbyPopulation;
            var globalAttractionModifierTotal =
                BuildingJobSystem.CalculateGlobalAttractionModifierTotal(globalAttractionModifiers);
            var rawAttraction = baseAttraction
                                + populationAttractionBonus
                                + globalAttractionModifierTotal
                                + subsidyAttractionBonus;
            var attraction = Mathf.Clamp(rawAttraction, 0f, 100f);
            var stableWorkers = maxWorkers <= 0
                ? 0
                : BuildingJobSystem.CalculateStableWorkers(maxWorkers, attraction);
            var missingStableWorkers = Mathf.Max(0, stableWorkers - currentWorkers);
            var missingMaxWorkers = Mathf.Max(0, maxWorkers - currentWorkers);
            var workerShortageRatio = maxWorkers <= 0 ? 0f : (stableWorkers - currentWorkers) / (float)maxWorkers;
            var excessWorkerRatio = maxWorkers <= 0 ? 0f : Mathf.Max(0, currentWorkers - stableWorkers) / (float)maxWorkers;
            var recruitChancePercent = maxWorkers <= 0
                ? 0f
                : Mathf.Clamp(40f + attraction * 0.4f + workerShortageRatio * 30f, 20f, 95f);
            var resignChancePercent = maxWorkers <= 0
                ? 0f
                : Mathf.Clamp(excessWorkerRatio * 60f + (100f - attraction) * 0.2f, 5f, 70f);

            MaxWorkers = maxWorkers;
            CurrentWorkers = currentWorkers;
            BaseAttraction = baseAttraction;
            NearbyPopulation = nearbyPopulation;
            PopulationCellCount = populationCellCount;
            AttractionPerNearbyPopulation = attractionPerNearbyPopulation;
            GlobalAttractionModifiers = globalAttractionModifiers ?? Array.Empty<BuildingJobAttractionModifier>();
            GlobalAttractionModifierTotal = globalAttractionModifierTotal;
            SubsidyAttractionBonus = subsidyAttractionBonus;
            SingleRecruitCost = singleRecruitCost;
            PopulationDensity = populationDensity;
            PopulationAttractionBonus = populationAttractionBonus;
            RawAttraction = rawAttraction;
            Attraction = attraction;
            StableWorkers = stableWorkers;
            MissingStableWorkers = missingStableWorkers;
            MissingMaxWorkers = missingMaxWorkers;
            WorkerShortageRatio = workerShortageRatio;
            ExcessWorkerRatio = excessWorkerRatio;
            RecruitChancePercent = recruitChancePercent;
            ResignChancePercent = resignChancePercent;
            ImmediateRecruitCost = Mathf.CeilToInt(
                missingMaxWorkers * singleRecruitCost * (1f + (100f - attraction) / 100f));
        }

        public int MaxWorkers { get; }
        public int CurrentWorkers { get; }
        public float BaseAttraction { get; }
        public int NearbyPopulation { get; }
        public int PopulationCellCount { get; }
        public float PopulationDensity { get; }
        public float AttractionPerNearbyPopulation { get; }
        public float PopulationAttractionBonus { get; }
        public IReadOnlyList<BuildingJobAttractionModifier> GlobalAttractionModifiers { get; }
        public float GlobalAttractionModifierTotal { get; }
        public float SubsidyAttractionBonus { get; }
        public float RawAttraction { get; }
        public float Attraction { get; }
        public int StableWorkers { get; }
        public int MissingStableWorkers { get; }
        public int MissingMaxWorkers { get; }
        public float WorkerShortageRatio { get; }
        public float ExcessWorkerRatio { get; }
        public float RecruitChancePercent { get; }
        public float ResignChancePercent { get; }
        public int SingleRecruitCost { get; }
        public int ImmediateRecruitCost { get; }
    }

    public static class BuildingJobSystem
    {
        public const float DefaultAttractionPerNearbyPopulation = 25f / 15f;

        private static readonly IReadOnlyList<BuildingJobAttractionModifier> EmptyAttractionModifiers =
            Array.Empty<BuildingJobAttractionModifier>();

        public static BuildingJobCalculation Calculate(BuildingJobCalculationInput input)
        {
            return new BuildingJobCalculation(input);
        }

        public static int CalculateStableWorkers(int maxWorkers, float attraction)
        {
            maxWorkers = Mathf.Max(0, maxWorkers);
            if (maxWorkers <= 0)
            {
                return 0;
            }

            return Mathf.Clamp(Mathf.FloorToInt(Mathf.Clamp(attraction, 0f, 100f) * (maxWorkers + 1) / 100f), 0, maxWorkers);
        }

        public static float CalculateRequiredAttractionForStableWorkers(int maxWorkers, int targetStableWorkers)
        {
            maxWorkers = Mathf.Max(0, maxWorkers);
            if (maxWorkers <= 0)
            {
                return 0f;
            }

            targetStableWorkers = Mathf.Clamp(targetStableWorkers, 0, maxWorkers);
            return targetStableWorkers <= 0 ? 0f : targetStableWorkers * 100f / (maxWorkers + 1);
        }

        public static float CalculateFullWorkerRequiredAttraction(int maxWorkers)
        {
            return CalculateRequiredAttractionForStableWorkers(maxWorkers, Mathf.Max(0, maxWorkers));
        }

        public static float CalculateSubsidyAttractionPerGold(int maxWorkers)
        {
            maxWorkers = Mathf.Max(0, maxWorkers);
            return maxWorkers <= 0 ? 0f : 100f / maxWorkers;
        }

        public static int CalculateRequiredSubsidyGoldForTargetStableWorkers(
            int maxWorkers,
            float baseAttractionWithoutSubsidy,
            int targetStableWorkers)
        {
            var subsidyAttractionPerGold = CalculateSubsidyAttractionPerGold(maxWorkers);
            if (subsidyAttractionPerGold <= 0f)
            {
                return 0;
            }

            var requiredAttraction = CalculateRequiredAttractionForStableWorkers(maxWorkers, targetStableWorkers);
            var attractionGap = Mathf.Max(0f, requiredAttraction - Mathf.Clamp(baseAttractionWithoutSubsidy, 0f, 100f));
            return Mathf.CeilToInt(attractionGap / subsidyAttractionPerGold);
        }

        public static float CalculateAttractionWithSubsidy(
            float baseAttractionWithoutSubsidy,
            int paidSubsidyGold,
            int maxWorkers)
        {
            var subsidyAttraction = Mathf.Max(0, paidSubsidyGold) * CalculateSubsidyAttractionPerGold(maxWorkers);
            return Mathf.Clamp(baseAttractionWithoutSubsidy + subsidyAttraction, 0f, 100f);
        }

        public static IReadOnlyList<BuildingJobAttractionModifier> ResolveGlobalAttractionModifiers(
            BuildingBase source,
            IBuildingJobAttractionModifierProvider modifierProvider)
        {
            if (source == null || modifierProvider == null)
            {
                return EmptyAttractionModifiers;
            }

            return modifierProvider.GetJobAttractionModifiers(source) ?? EmptyAttractionModifiers;
        }

        public static float CalculateGlobalAttractionModifierTotal(
            IReadOnlyList<BuildingJobAttractionModifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0)
            {
                return 0f;
            }

            var total = 0f;
            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (modifier.IsValid)
                {
                    total += modifier.Value;
                }
            }

            return total;
        }

        public static int CountPopulationCells(BuildingBase source, int radius)
        {
            if (source == null || !source.HasPlacement)
            {
                return 0;
            }

            radius = Mathf.Max(0, radius);
            var cells = new HashSet<GridPosition>();
            foreach (var footprintPosition in source.Footprint.Positions())
            {
                for (var offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    var remainingRadius = radius - Mathf.Abs(offsetX);
                    for (var offsetY = -remainingRadius; offsetY <= remainingRadius; offsetY++)
                    {
                        var position = new GridPosition(
                            footprintPosition.X + offsetX,
                            footprintPosition.Y + offsetY);

                        if (source.GridMap != null && !source.GridMap.HasBaseTileAt(position))
                        {
                            continue;
                        }

                        cells.Add(position);
                    }
                }
            }

            return cells.Count;
        }

        public static int CountNearbyPopulation(BuildingBase source, IReadOnlyList<BuildingBase> buildings, int radius)
        {
            if (source == null || buildings == null || !source.HasPlacement)
            {
                return 0;
            }

            radius = Mathf.Max(0, radius);
            var population = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (!CanUseNeighbor(source, building) || building is not IBuildingPopulationSource populationSource)
                {
                    continue;
                }

                if (GetFootprintManhattanDistance(source, building) > radius)
                {
                    continue;
                }

                population += Mathf.Max(0, populationSource.CurrentPopulation);
            }

            return population;
        }

        public static int CountCurrentWorkers(IReadOnlyList<BuildingBase> buildings)
        {
            if (buildings == null)
            {
                return 0;
            }

            var workerCount = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null || !building.isActiveAndEnabled || building.IsDemolishing)
                {
                    continue;
                }

                if (building is IBuildingJobSource jobSource)
                {
                    workerCount += Mathf.Max(0, jobSource.CurrentWorkers);
                }
            }

            return workerCount;
        }

        public static int GetAvailablePopulation(Landsong.GameSystem gameSystem, IReadOnlyList<BuildingBase> buildings)
        {
            var population = gameSystem == null || gameSystem.Dynasty == null ? 0 : gameSystem.Dynasty.Population;
            return Mathf.Max(0, population - CountCurrentWorkers(buildings));
        }

        public static int GetFootprintManhattanDistance(BuildingBase source, BuildingBase target)
        {
            if (source == null || target == null || !source.HasPlacement || !target.HasPlacement)
            {
                return int.MaxValue;
            }

            if (source.GridMap != null && target.GridMap != null && source.GridMap != target.GridMap)
            {
                return int.MaxValue;
            }

            var bestDistance = int.MaxValue;
            foreach (var sourcePosition in source.Footprint.Positions())
            {
                foreach (var targetPosition in target.Footprint.Positions())
                {
                    var distance = Mathf.Abs(sourcePosition.X - targetPosition.X)
                                   + Mathf.Abs(sourcePosition.Y - targetPosition.Y);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                    }
                }
            }

            return bestDistance;
        }

        public static string FormatDebugText(BuildingJobCalculation calculation)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"最大岗位数: {calculation.MaxWorkers}");
            builder.AppendLine($"当前工人: {calculation.CurrentWorkers}");
            builder.AppendLine($"稳定支撑工人: {calculation.StableWorkers}");
            builder.AppendLine($"基础吸引力: {calculation.BaseAttraction:0.##}");
            builder.AppendLine($"附近人口: {calculation.NearbyPopulation}");
            builder.AppendLine($"人口密度格子数: {calculation.PopulationCellCount}");
            builder.AppendLine($"人口密度: {calculation.PopulationDensity:0.####}");
            builder.AppendLine($"附近每人口就业吸引力: {calculation.AttractionPerNearbyPopulation:0.##}");
            builder.AppendLine($"附近人口增益: {calculation.PopulationAttractionBonus:0.##}");
            builder.AppendLine($"全局修正合计: {calculation.GlobalAttractionModifierTotal:+0.##;-0.##;0}");
            AppendGlobalModifierDebugText(builder, calculation.GlobalAttractionModifiers);
            builder.AppendLine($"补贴就业吸引力: {calculation.SubsidyAttractionBonus:0.##}");
            builder.AppendLine($"原始岗位吸引力: {calculation.RawAttraction:0.##}");
            builder.AppendLine($"岗位吸引力: {calculation.Attraction:0.##}");
            builder.AppendLine($"缺工比例: {calculation.WorkerShortageRatio:0.##}");
            builder.AppendLine($"每回合招工概率: {calculation.RecruitChancePercent:0.##}%");
            builder.AppendLine($"超员比例: {calculation.ExcessWorkerRatio:0.##}");
            builder.AppendLine($"每回合离职概率: {calculation.ResignChancePercent:0.##}%");
            builder.AppendLine($"单人招工费用: {calculation.SingleRecruitCost}");
            builder.Append($"立即招工费用: {calculation.ImmediateRecruitCost}");
            return builder.ToString();
        }

        private static void AppendGlobalModifierDebugText(
            StringBuilder builder,
            IReadOnlyList<BuildingJobAttractionModifier> modifiers)
        {
            if (builder == null || modifiers == null || modifiers.Count == 0)
            {
                return;
            }

            for (var i = 0; i < modifiers.Count; i++)
            {
                var modifier = modifiers[i];
                if (!modifier.IsValid)
                {
                    continue;
                }

                var displayName = string.IsNullOrWhiteSpace(modifier.DisplayText)
                    ? "未命名修正"
                    : modifier.DisplayText;
                builder.AppendLine($"  {displayName}: {modifier.Value:+0.##;-0.##;0}");
            }
        }

        private static bool CanUseNeighbor(BuildingBase source, BuildingBase building)
        {
            return building != null
                   && building != source
                   && building.isActiveAndEnabled
                   && !building.IsDemolishing
                   && building.HasPlacement
                   && (source.GridMap == null || building.GridMap == null || building.GridMap == source.GridMap);
        }
    }
}
