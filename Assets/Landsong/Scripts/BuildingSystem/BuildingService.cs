using System;
using System.Collections.Generic;
using Landsong.GridSystem;
using Landsong.InventorySystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public sealed class BuildingService
    {
        private static readonly IReadOnlyList<BuildingBase> EmptyBuildings = Array.Empty<BuildingBase>();

        private readonly Landsong.GameSystem gameSystem;
        private readonly List<BuildingBase> buildings = new List<BuildingBase>();
        private readonly HashSet<BuildingBase> registeredBuildings = new HashSet<BuildingBase>();

        public BuildingService(Landsong.GameSystem gameSystem)
        {
            this.gameSystem = gameSystem;
        }

        public event Action<BuildingService> BuildingsChanged;

        public IReadOnlyList<BuildingBase> Buildings => buildings.Count == 0 ? EmptyBuildings : buildings;

        public BuildingAvailability EvaluateAvailability(BuildingBase buildingPrefab)
        {
            return BuildingAvailabilityEvaluator.Evaluate(buildingPrefab, gameSystem, CountExistingBuildings(buildingPrefab));
        }

        public int CountExistingBuildings(BuildingBase buildingPrefab)
        {
            if (buildingPrefab == null || !buildingPrefab.HasDefinition)
            {
                return 0;
            }

            var count = 0;
            var targetGroupId = buildingPrefab.Definition.BuildLimitGroupId;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (!ShouldCountBuilding(building) || !building.HasDefinition)
                {
                    continue;
                }

                if (string.Equals(building.Definition.BuildLimitGroupId, targetGroupId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        public void RegisterBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            if (!registeredBuildings.Add(building))
            {
                return;
            }

            buildings.Add(building);
            NotifyBuildingsChanged();
        }

        public bool UnregisterBuilding(BuildingBase building)
        {
            if (building == null || !registeredBuildings.Remove(building))
            {
                return false;
            }

            buildings.Remove(building);
            NotifyBuildingsChanged();
            return true;
        }

        public void ClearBuildings()
        {
            buildings.Clear();
            registeredBuildings.Clear();
            NotifyBuildingsChanged();
        }

        public bool CanAffordPlacementCosts(BuildingDefinition definition, int multiplier = 1)
        {
            multiplier = Mathf.Max(0, multiplier);
            if (multiplier <= 0 || definition == null || !HasAnyValidCost(definition.PlacementCosts))
            {
                return true;
            }

            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            if (inventory == null)
            {
                return false;
            }

            return inventory.CanAffordBuildingCosts(RepeatPlacementCosts(definition.PlacementCosts, multiplier));
        }

        public bool TrySpendPlacementCosts(BuildingDefinition definition, int multiplier = 1)
        {
            multiplier = Mathf.Max(0, multiplier);
            if (multiplier <= 0 || definition == null || !HasAnyValidCost(definition.PlacementCosts))
            {
                return true;
            }

            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            if (inventory == null)
            {
                return false;
            }

            return inventory.TrySpendBuildingCosts(RepeatPlacementCosts(definition.PlacementCosts, multiplier));
        }

        public bool TryPlace(
            BuildingBase buildingPrefab,
            GridMapBehaviour gridMap,
            GridPosition origin,
            Transform parent,
            out BuildingBase building)
        {
            return TryPlace(buildingPrefab, gridMap, origin, Quaternion.identity, parent, out building);
        }

        public bool TryPlace(
            BuildingBase buildingPrefab,
            GridMapBehaviour gridMap,
            GridPosition origin,
            Quaternion rotation,
            Transform parent,
            out BuildingBase building)
        {
            var request = new BuildingPlacementRequest(
                buildingPrefab,
                gridMap,
                origin,
                rotation,
                parent);
            var result = TryPlace(request, out building);
            return result.Succeeded;
        }

        public BuildingPlacementResult TryPlace(BuildingPlacementRequest request, out BuildingBase building)
        {
            building = null;
            if (!CanUseBuildingPrefab(request.BuildingPrefab, out var definition, request.LogWarnings))
            {
                return BuildingPlacementResult.Fail(
                    request.BuildingPrefab == null ? BuildingPlacementFailure.MissingPrefab : BuildingPlacementFailure.MissingDefinition,
                    "建筑预制体缺失或缺少有效定义。");
            }

            if (request.GridMap == null)
            {
                LogWarning(request.LogWarnings, $"Cannot place building '{definition.DisplayName}' without a grid map.", request.BuildingPrefab);
                return BuildingPlacementResult.Fail(BuildingPlacementFailure.MissingGridMap, "缺少 GridMap。");
            }

            if (!request.GridMap.CanOccupy(request.Origin, definition.Size, definition.RequiredTerrainKeys, out var failureReason))
            {
                LogWarning(request.LogWarnings, $"Cannot place building '{definition.DisplayName}' at {request.Origin}: {failureReason}.", request.BuildingPrefab);
                return BuildingPlacementResult.Fail(BuildingPlacementFailure.InvalidOrigin, "目标格子不可放置。", failureReason);
            }

            if (request.SpendPlacementCosts && !CanAffordPlacementCosts(definition, request.CostMultiplier))
            {
                LogWarning(request.LogWarnings, $"Cannot place building '{definition.DisplayName}' because placement costs are missing.", request.BuildingPrefab);
                return BuildingPlacementResult.Fail(BuildingPlacementFailure.CannotAffordCosts, "建造材料不足。");
            }

            var occupancyId = CreateGridOccupancyId(request.BuildingPrefab);
            if (!request.GridMap.TryOccupy(
                    request.Origin,
                    definition.Size,
                    occupancyId,
                    definition.RequiredTerrainKeys,
                    definition.MovementResistance,
                    out failureReason))
            {
                LogWarning(request.LogWarnings, $"Cannot occupy grid for building '{definition.DisplayName}' at {request.Origin}: {failureReason}.", request.BuildingPrefab);
                return BuildingPlacementResult.Fail(BuildingPlacementFailure.InvalidOrigin, "占用格子失败。", failureReason);
            }

            var placementPosition = request.GridMap.GetFootprintCenter(request.Origin, definition.Size);
            building = UnityEngine.Object.Instantiate(request.BuildingPrefab, placementPosition, request.Rotation, request.Parent);
            if (building == null)
            {
                LogWarning(request.LogWarnings, $"Placed building prefab '{request.BuildingPrefab.name}' could not instantiate a BuildingBase.", request.BuildingPrefab);
                request.GridMap.ClearOccupant(occupancyId);
                return BuildingPlacementResult.Fail(BuildingPlacementFailure.InstantiationFailed, "建筑实例化失败。");
            }

            building.SetPlacement(request.Origin, occupancyId, request.GridMap);
            building.gameObject.SetActive(true);

            if (request.SpendPlacementCosts && !TrySpendPlacementCosts(definition, request.CostMultiplier))
            {
                LogWarning(request.LogWarnings, $"Cannot place building '{definition.DisplayName}' because placement costs could not be spent.", request.BuildingPrefab);
                Remove(building);
                building = null;
                return BuildingPlacementResult.Fail(BuildingPlacementFailure.CostSpendFailed, "扣除建造材料失败。");
            }

            if (request.RegisterImmediately)
            {
                gameSystem?.RegisterBuilding(building);
            }

            return BuildingPlacementResult.Success(building);
        }

        public BuildingBatchPlacementResult TryPlaceBatch(
            BuildingBase buildingPrefab,
            GridMapBehaviour gridMap,
            IReadOnlyList<GridPosition> origins,
            Transform parent,
            out List<BuildingBase> placedBuildings,
            bool spendPlacementCosts = false,
            bool registerImmediately = false)
        {
            placedBuildings = new List<BuildingBase>();
            if (!CanUseBuildingPrefab(buildingPrefab, out var definition, true))
            {
                return new BuildingBatchPlacementResult(false, placedBuildings, BuildingPlacementFailure.MissingDefinition, "建筑预制体缺失或缺少有效定义。");
            }

            if (origins == null || origins.Count == 0)
            {
                return new BuildingBatchPlacementResult(false, placedBuildings, BuildingPlacementFailure.InvalidOrigin, "没有可放置的建筑格子。");
            }

            if (spendPlacementCosts && !CanAffordPlacementCosts(definition, origins.Count))
            {
                Debug.LogWarning($"Cannot place building batch '{definition.DisplayName}' because placement costs are missing.", buildingPrefab);
                return new BuildingBatchPlacementResult(false, placedBuildings, BuildingPlacementFailure.CannotAffordCosts, "建造材料不足。");
            }

            for (var i = 0; i < origins.Count; i++)
            {
                var request = new BuildingPlacementRequest(
                    buildingPrefab,
                    gridMap,
                    origins[i],
                    Quaternion.identity,
                    parent,
                    1,
                    false,
                    registerImmediately);
                var result = TryPlace(request, out var placedBuilding);
                if (!result.Succeeded)
                {
                    RollbackPlacedBuildings(placedBuildings);
                    placedBuildings.Clear();
                    return new BuildingBatchPlacementResult(false, placedBuildings, result.Failure, result.Message);
                }

                placedBuildings.Add(placedBuilding);
            }

            if (spendPlacementCosts && !TrySpendPlacementCosts(definition, origins.Count))
            {
                Debug.LogWarning($"Cannot place building batch '{definition.DisplayName}' because placement costs could not be spent.", buildingPrefab);
                RollbackPlacedBuildings(placedBuildings);
                placedBuildings.Clear();
                return new BuildingBatchPlacementResult(false, placedBuildings, BuildingPlacementFailure.CostSpendFailed, "扣除建造材料失败。");
            }

            return new BuildingBatchPlacementResult(true, placedBuildings, BuildingPlacementFailure.None, string.Empty);
        }

        public bool TryReplace(BuildingBase sourceBuilding, BuildingBase replacementPrefab, out BuildingBase replacement)
        {
            replacement = null;
            if (!CanReplace(sourceBuilding, replacementPrefab)
                || sourceBuilding.Definition == null
                || replacementPrefab.Definition == null)
            {
                return false;
            }

            var gridMap = sourceBuilding.GridMap;
            var origin = sourceBuilding.GridPosition;
            var rotation = sourceBuilding.transform.rotation;
            var parent = sourceBuilding.transform.parent;
            var sourceDefinition = sourceBuilding.Definition;
            var replacementDefinition = replacementPrefab.Definition;
            var sourceOccupancyId = sourceBuilding.GridOccupancyId;
            var replacementOccupancyId = CreateGridOccupancyId(replacementPrefab);
            var sourceWasRegistered = sourceBuilding.IsRegistered || registeredBuildings.Contains(sourceBuilding);
            var occupancyTransferred = false;
            var sourceDetached = false;

            try
            {
                var placementPosition = gridMap.GetFootprintCenter(origin, replacementDefinition.Size);
                replacement = UnityEngine.Object.Instantiate(
                    replacementPrefab,
                    placementPosition,
                    rotation,
                    parent);
                if (replacement == null)
                {
                    Debug.LogWarning(
                        $"Cannot replace building '{sourceBuilding.name}' because replacement instantiation failed.",
                        sourceBuilding);
                    return false;
                }

                // Start 尚未执行，先禁用实例，避免替换提交前进入运行时注册流程。
                replacement.gameObject.SetActive(false);
                replacement.ReceiveReplacementStateFrom(sourceBuilding);

                if (!gridMap.TryReplaceOccupant(
                        sourceOccupancyId,
                        origin,
                        replacementDefinition.Size,
                        replacementOccupancyId,
                        replacementDefinition.RequiredTerrainKeys,
                        replacementDefinition.MovementResistance,
                        out var failureReason))
                {
                    Debug.LogWarning(
                        $"Cannot replace building '{sourceBuilding.name}' with '{replacementDefinition.DisplayName}' at {origin}: {failureReason}.",
                        sourceBuilding);
                    UnityEngine.Object.Destroy(replacement.gameObject);
                    replacement = null;
                    return false;
                }

                occupancyTransferred = true;
                sourceBuilding.DetachPlacementAfterOccupancyTransfer();
                sourceDetached = true;
                replacement.SetPlacement(origin, replacementOccupancyId, gridMap);
                replacement.gameObject.SetActive(true);

                if (sourceWasRegistered)
                {
                    gameSystem?.UnregisterBuilding(sourceBuilding);
                    gameSystem?.RegisterBuilding(replacement);
                }

                UnityEngine.Object.Destroy(sourceBuilding.gameObject);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, sourceBuilding);

                if (replacement != null)
                {
                    gameSystem?.UnregisterBuilding(replacement);
                }

                if (occupancyTransferred)
                {
                    if (gridMap.TryReplaceOccupant(
                            replacementOccupancyId,
                            origin,
                            sourceDefinition.Size,
                            sourceOccupancyId,
                            sourceDefinition.RequiredTerrainKeys,
                            sourceDefinition.MovementResistance,
                            out var rollbackFailure))
                    {
                        if (sourceDetached)
                        {
                            sourceBuilding.SetPlacement(origin, sourceOccupancyId, gridMap);
                        }
                    }
                    else
                    {
                        Debug.LogError(
                            $"Building replacement rollback failed for '{sourceBuilding.name}' at {origin}: {rollbackFailure}.",
                            sourceBuilding);
                    }
                }

                if (sourceWasRegistered)
                {
                    gameSystem?.RegisterBuilding(sourceBuilding);
                }

                if (replacement != null)
                {
                    UnityEngine.Object.Destroy(replacement.gameObject);
                    replacement = null;
                }

                return false;
            }
        }

        public bool CanReplace(BuildingBase sourceBuilding, BuildingBase replacementPrefab, bool logWarning = true)
        {
            if (sourceBuilding == null || !CanUseBuildingPrefab(replacementPrefab, out var replacementDefinition, logWarning))
            {
                return false;
            }

            if (!sourceBuilding.HasPlacement || sourceBuilding.GridMap == null)
            {
                if (logWarning)
                {
                    Debug.LogWarning($"Cannot replace building '{sourceBuilding.name}' because it has no grid placement.", sourceBuilding);
                }

                return false;
            }

            var gridMap = sourceBuilding.GridMap;
            var origin = sourceBuilding.GridPosition;
            var ignoredOccupantId = sourceBuilding.GridOccupancyId;

            if (!gridMap.CanOccupy(origin, replacementDefinition.Size, replacementDefinition.RequiredTerrainKeys, out var failureReason, ignoredOccupantId))
            {
                if (logWarning)
                {
                    Debug.LogWarning(
                        $"Cannot replace building '{sourceBuilding.name}' with '{replacementDefinition.DisplayName}' at {origin}: {failureReason}.",
                        sourceBuilding);
                }

                return false;
            }

            return true;
        }

        public void Demolish(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            building.RunDemolition();
        }

        public void Remove(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            building.ClearPlacement();
            gameSystem?.UnregisterBuilding(building);
            UnityEngine.Object.Destroy(building.gameObject);
        }

        private void RollbackPlacedBuildings(IReadOnlyList<BuildingBase> placedBuildings)
        {
            if (placedBuildings == null)
            {
                return;
            }

            for (var i = placedBuildings.Count - 1; i >= 0; i--)
            {
                Remove(placedBuildings[i]);
            }
        }

        private static bool CanUseBuildingPrefab(BuildingBase buildingPrefab, out BuildingDefinition definition, bool logWarning)
        {
            definition = buildingPrefab == null ? null : buildingPrefab.Definition;
            if (buildingPrefab != null && buildingPrefab.HasDefinition)
            {
                return true;
            }

            if (logWarning && buildingPrefab != null)
            {
                Debug.LogWarning($"Cannot use building prefab '{buildingPrefab.name}' because it has no valid BuildingDefinition data.", buildingPrefab);
            }

            return false;
        }

        private static string CreateGridOccupancyId(BuildingBase buildingPrefab)
        {
            var definition = buildingPrefab.Definition;
            var prefix = definition == null || string.IsNullOrWhiteSpace(definition.BuildingId)
                ? buildingPrefab.name
                : definition.BuildingId;
            return $"{prefix}_{Guid.NewGuid():N}";
        }

        private static bool ShouldCountBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private static bool HasAnyValidCost(IReadOnlyList<BuildingCost> costs)
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

        private static IEnumerable<BuildingCost> RepeatPlacementCosts(IReadOnlyList<BuildingCost> costs, int multiplier)
        {
            if (costs == null || multiplier <= 0)
            {
                yield break;
            }

            for (var repeatIndex = 0; repeatIndex < multiplier; repeatIndex++)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    yield return costs[i];
                }
            }
        }

        private static void LogWarning(bool enabled, string message, UnityEngine.Object context)
        {
            if (enabled)
            {
                Debug.LogWarning(message, context);
            }
        }

        private void NotifyBuildingsChanged()
        {
            BuildingsChanged?.Invoke(this);
        }
    }
}
