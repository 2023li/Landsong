using System;
using System.Collections.Generic;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public enum BuildingPlacementFailure
    {
        None = 0,
        MissingPrefab = 10,
        MissingDefinition = 20,
        MissingGridMap = 30,
        InvalidOrigin = 40,
        MissingBuildingService = 50,
        MissingInventory = 60,
        CannotAffordCosts = 70,
        CostSpendFailed = 80,
        InstantiationFailed = 90
    }

    public readonly struct BuildingPlacementRequest
    {
        public BuildingPlacementRequest(
            BuildingBase buildingPrefab,
            GridMapBehaviour gridMap,
            GridPosition origin,
            Transform parent,
            int costMultiplier = 1,
            bool spendPlacementCosts = false,
            bool registerImmediately = false,
            bool logWarnings = true)
        {
            BuildingPrefab = buildingPrefab;
            GridMap = gridMap;
            Origin = origin;
            Parent = parent;
            CostMultiplier = Mathf.Max(0, costMultiplier);
            SpendPlacementCosts = spendPlacementCosts;
            RegisterImmediately = registerImmediately;
            LogWarnings = logWarnings;
        }

        public BuildingBase BuildingPrefab { get; }
        public GridMapBehaviour GridMap { get; }
        public GridPosition Origin { get; }
        public Transform Parent { get; }
        public int CostMultiplier { get; }
        public bool SpendPlacementCosts { get; }
        public bool RegisterImmediately { get; }
        public bool LogWarnings { get; }

        public BuildingPlacementRequest WithOrigin(GridPosition origin)
        {
            return new BuildingPlacementRequest(
                BuildingPrefab,
                GridMap,
                origin,
                Parent,
                CostMultiplier,
                SpendPlacementCosts,
                RegisterImmediately,
                LogWarnings);
        }
    }

    public readonly struct BuildingPlacementResult
    {
        private BuildingPlacementResult(
            bool succeeded,
            BuildingBase building,
            BuildingPlacementFailure failure,
            GridPlacementFailureReason gridFailure,
            string message)
        {
            Succeeded = succeeded;
            Building = building;
            Failure = failure;
            GridFailure = gridFailure;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }

        public bool Succeeded { get; }
        public BuildingBase Building { get; }
        public BuildingPlacementFailure Failure { get; }
        public GridPlacementFailureReason GridFailure { get; }
        public string Message { get; }

        public static BuildingPlacementResult Success(BuildingBase building)
        {
            return new BuildingPlacementResult(true, building, BuildingPlacementFailure.None, GridPlacementFailureReason.None, string.Empty);
        }

        public static BuildingPlacementResult Fail(
            BuildingPlacementFailure failure,
            string message,
            GridPlacementFailureReason gridFailure = GridPlacementFailureReason.None)
        {
            return new BuildingPlacementResult(false, null, failure, gridFailure, message);
        }
    }

    public readonly struct BuildingBatchPlacementResult
    {
        private static readonly IReadOnlyList<BuildingBase> EmptyBuildings = Array.Empty<BuildingBase>();

        public BuildingBatchPlacementResult(
            bool succeeded,
            IReadOnlyList<BuildingBase> buildings,
            BuildingPlacementFailure failure,
            string message)
        {
            Succeeded = succeeded;
            Buildings = buildings ?? EmptyBuildings;
            Failure = failure;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message;
        }

        public bool Succeeded { get; }
        public IReadOnlyList<BuildingBase> Buildings { get; }
        public BuildingPlacementFailure Failure { get; }
        public string Message { get; }
    }
}
