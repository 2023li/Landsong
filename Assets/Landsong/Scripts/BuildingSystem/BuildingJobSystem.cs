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
        int SubsidyGoldPerTurn { get; }
        float RawJobAttraction { get; }
        float JobAttraction { get; }
    }

    public readonly struct BuildingJobCalculationInput
    {
        public BuildingJobCalculationInput(
            int maxWorkers,
            int currentWorkers,
            float baseAttraction,
            int nearbyPopulation,
            int populationCellCount,
            int subsidyGoldPerTurn,
            float competitionPenalty,
            int singleRecruitCost)
        {
            var normalizedMaxWorkers = Mathf.Max(0, maxWorkers);

            MaxWorkers = normalizedMaxWorkers;
            CurrentWorkers = Mathf.Clamp(currentWorkers, 0, normalizedMaxWorkers);
            BaseAttraction = Mathf.Max(0f, baseAttraction);
            NearbyPopulation = Mathf.Max(0, nearbyPopulation);
            PopulationCellCount = Mathf.Max(0, populationCellCount);
            SubsidyGoldPerTurn = Mathf.Max(0, subsidyGoldPerTurn);
            CompetitionPenalty = Mathf.Max(0f, competitionPenalty);
            SingleRecruitCost = Mathf.Max(0, singleRecruitCost);
        }

        public int MaxWorkers { get; }
        public int CurrentWorkers { get; }
        public float BaseAttraction { get; }
        public int NearbyPopulation { get; }
        public int PopulationCellCount { get; }
        public int SubsidyGoldPerTurn { get; }
        public float CompetitionPenalty { get; }
        public int SingleRecruitCost { get; }
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
            var subsidyGoldPerTurn = input.SubsidyGoldPerTurn;
            var competitionPenalty = input.CompetitionPenalty;
            var singleRecruitCost = input.SingleRecruitCost;
            var populationDensity = populationCellCount <= 0 ? 0f : nearbyPopulation / (float)populationCellCount;
            var perWorkerSubsidy = maxWorkers <= 0 ? 0f : subsidyGoldPerTurn / (float)maxWorkers;
            var subsidyBonus = Mathf.Clamp(perWorkerSubsidy * 5f, 0f, 100f);
            var rawAttraction = baseAttraction + populationDensity * 30f + subsidyBonus - competitionPenalty;
            var attraction = Mathf.Clamp(rawAttraction, 0f, 100f);
            var stableWorkers = maxWorkers <= 0
                ? 0
                : Mathf.Clamp(Mathf.FloorToInt(attraction * (maxWorkers + 1) / 100f), 0, maxWorkers);
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
            SubsidyGoldPerTurn = subsidyGoldPerTurn;
            CompetitionPenalty = competitionPenalty;
            SingleRecruitCost = singleRecruitCost;
            PopulationDensity = populationDensity;
            PerWorkerSubsidy = perWorkerSubsidy;
            SubsidyBonus = subsidyBonus;
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
        public int SubsidyGoldPerTurn { get; }
        public float PerWorkerSubsidy { get; }
        public float SubsidyBonus { get; }
        public float CompetitionPenalty { get; }
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
        public static BuildingJobCalculation Calculate(BuildingJobCalculationInput input)
        {
            return new BuildingJobCalculation(input);
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

        public static int CountNearbyCompetingJobs(
            BuildingBase source,
            IReadOnlyList<BuildingBase> buildings,
            int radius)
        {
            if (source == null || buildings == null || !source.HasPlacement)
            {
                return 0;
            }

            radius = Mathf.Max(0, radius);
            var jobCount = 0;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (!CanUseNeighbor(source, building) || building is not IBuildingJobSource jobSource)
                {
                    continue;
                }

                if (GetFootprintManhattanDistance(source, building) > radius)
                {
                    continue;
                }

                jobCount += Mathf.Max(0, jobSource.MaxWorkers);
            }

            return jobCount;
        }

        public static float CalculateCompetitionPenalty(
            int competingJobs,
            float penaltyPerCompetingJob,
            float maxCompetitionPenalty)
        {
            return Mathf.Clamp(
                Mathf.Max(0, competingJobs) * Mathf.Max(0f, penaltyPerCompetingJob),
                0f,
                Mathf.Max(0f, maxCompetitionPenalty));
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
            builder.AppendLine($"每回合补贴: {calculation.SubsidyGoldPerTurn}");
            builder.AppendLine($"人均补贴: {calculation.PerWorkerSubsidy:0.##}");
            builder.AppendLine($"补贴加成: {calculation.SubsidyBonus:0.##}");
            builder.AppendLine($"竞争惩罚: {calculation.CompetitionPenalty:0.##}");
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
