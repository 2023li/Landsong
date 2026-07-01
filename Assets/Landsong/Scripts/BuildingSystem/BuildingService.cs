using System;
using System.Collections.Generic;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public sealed class BuildingService
    {
        private static readonly IReadOnlyList<BuildingBase> EmptyBuildings = Array.Empty<BuildingBase>();
        private readonly Landsong.GameSystem gameSystem;

        public BuildingService(Landsong.GameSystem gameSystem)
        {
            this.gameSystem = gameSystem;
        }

        public event Action<BuildingService> BuildingsChanged;

        public IReadOnlyList<BuildingBase> Buildings => gameSystem == null || gameSystem.Turn == null
            ? EmptyBuildings
            : gameSystem.Turn.Buildings;

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
            var buildings = Buildings;
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
            if (building == null || gameSystem == null || gameSystem.Turn == null)
            {
                return;
            }

            var countBefore = gameSystem.Turn.Buildings.Count;
            gameSystem.Turn.RegisterBuilding(building);
            if (gameSystem.Turn.Buildings.Count != countBefore)
            {
                NotifyBuildingsChanged();
            }
        }

        public bool UnregisterBuilding(BuildingBase building)
        {
            if (building == null || gameSystem == null || gameSystem.Turn == null)
            {
                return false;
            }

            var removed = gameSystem.Turn.UnregisterBuilding(building);
            if (removed)
            {
                NotifyBuildingsChanged();
            }

            return removed;
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
            building = null;
            if (!CanUseBuildingPrefab(buildingPrefab))
            {
                return false;
            }

            var definition = buildingPrefab.Definition;
            if (gridMap == null)
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' without a grid map.", buildingPrefab);
                return false;
            }

            if (!gridMap.CanOccupy(origin, definition.Size, definition.RequiredTerrainKeys, out var failureReason))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' at {origin}: {failureReason}.", buildingPrefab);
                return false;
            }

            var occupancyId = CreateGridOccupancyId(buildingPrefab);
            if (!gridMap.TryOccupy(origin, definition.Size, occupancyId, definition.RequiredTerrainKeys, out failureReason))
            {
                Debug.LogWarning($"Cannot occupy grid for building '{definition.DisplayName}' at {origin}: {failureReason}.", buildingPrefab);
                return false;
            }

            var placementPosition = gridMap.GetFootprintCenter(origin, definition.Size);
            building = UnityEngine.Object.Instantiate(buildingPrefab, placementPosition, rotation, parent);
            if (building == null)
            {
                Debug.LogWarning($"Placed building prefab '{buildingPrefab.name}' could not instantiate a BuildingBase.", buildingPrefab);
                gridMap.ClearOccupant(occupancyId);
                return false;
            }

            building.SetPlacement(origin, occupancyId, gridMap);
            building.gameObject.SetActive(true);
            return true;
        }

        public bool TryReplace(BuildingBase sourceBuilding, BuildingBase replacementPrefab, out BuildingBase replacement)
        {
            replacement = null;
            if (sourceBuilding == null || !CanUseBuildingPrefab(replacementPrefab))
            {
                return false;
            }

            if (!sourceBuilding.HasPlacement || sourceBuilding.GridMap == null)
            {
                Debug.LogWarning($"Cannot replace building '{sourceBuilding.name}' because it has no grid placement.", sourceBuilding);
                return false;
            }

            var replacementDefinition = replacementPrefab.Definition;
            var gridMap = sourceBuilding.GridMap;
            var origin = sourceBuilding.GridPosition;
            var rotation = sourceBuilding.transform.rotation;
            var parent = sourceBuilding.transform.parent;
            var ignoredOccupantId = sourceBuilding.GridOccupancyId;

            if (!gridMap.CanOccupy(origin, replacementDefinition.Size, replacementDefinition.RequiredTerrainKeys, out var failureReason, ignoredOccupantId))
            {
                Debug.LogWarning(
                    $"Cannot replace building '{sourceBuilding.name}' with '{replacementDefinition.DisplayName}' at {origin}: {failureReason}.",
                    sourceBuilding);
                return false;
            }

            sourceBuilding.ClearPlacement();
            UnityEngine.Object.Destroy(sourceBuilding.gameObject);

            return TryPlace(replacementPrefab, gridMap, origin, rotation, parent, out replacement);
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
            UnityEngine.Object.Destroy(building.gameObject);
        }

        private static bool CanUseBuildingPrefab(BuildingBase buildingPrefab)
        {
            if (buildingPrefab == null)
            {
                return false;
            }

            if (buildingPrefab.HasDefinition)
            {
                return true;
            }

            Debug.LogWarning($"Cannot use building prefab '{buildingPrefab.name}' because it has no valid BuildingDefinition data.", buildingPrefab);
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

        private void NotifyBuildingsChanged()
        {
            BuildingsChanged?.Invoke(this);
        }
    }
}
