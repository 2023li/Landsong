using System;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public sealed class BuildingService
    {
        private readonly Landsong.GameSystem gameSystem;

        public BuildingService(Landsong.GameSystem gameSystem)
        {
            this.gameSystem = gameSystem;
        }

        public bool TryPlace(
            BuildingDefinition definition,
            GridMapBehaviour gridMap,
            GridPosition origin,
            Transform parent,
            out BuildingBase building)
        {
            return TryPlace(definition, gridMap, origin, Quaternion.identity, parent, out building);
        }

        public bool TryPlace(
            BuildingDefinition definition,
            GridMapBehaviour gridMap,
            GridPosition origin,
            Quaternion rotation,
            Transform parent,
            out BuildingBase building)
        {
            building = null;
            if (!CanUseDefinition(definition))
            {
                return false;
            }

            if (gridMap == null)
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' without a grid map.");
                return false;
            }

            if (!gridMap.CanOccupy(origin, definition.Size, out var failureReason))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' at {origin}: {failureReason}.");
                return false;
            }

            var occupancyId = CreateGridOccupancyId(definition);
            if (!gridMap.TryOccupy(origin, definition.Size, occupancyId, out failureReason))
            {
                Debug.LogWarning($"Cannot occupy grid for building '{definition.DisplayName}' at {origin}: {failureReason}.");
                return false;
            }

            var placementPosition = gridMap.GetFootprintCenter(origin, definition.Size);
            var placedObject = UnityEngine.Object.Instantiate(definition.BuildingPrefab, placementPosition, rotation, parent);
            building = placedObject.GetComponent<BuildingBase>();
            if (building == null)
            {
                Debug.LogWarning($"Placed building prefab '{placedObject.name}' has no BuildingBase component.", placedObject);
                gridMap.ClearOccupant(occupancyId);
                UnityEngine.Object.Destroy(placedObject);
                return false;
            }

            building.SetPlacement(origin, occupancyId, gridMap);
            placedObject.SetActive(true);
            return true;
        }

        public bool TryReplace(BuildingBase sourceBuilding, BuildingDefinition replacementDefinition, out BuildingBase replacement)
        {
            replacement = null;
            if (sourceBuilding == null || !CanUseDefinition(replacementDefinition))
            {
                return false;
            }

            if (!sourceBuilding.HasPlacement || sourceBuilding.GridMap == null)
            {
                Debug.LogWarning($"Cannot replace building '{sourceBuilding.name}' because it has no grid placement.", sourceBuilding);
                return false;
            }

            var gridMap = sourceBuilding.GridMap;
            var origin = sourceBuilding.GridPosition;
            var rotation = sourceBuilding.transform.rotation;
            var parent = sourceBuilding.transform.parent;
            var ignoredOccupantId = sourceBuilding.GridOccupancyId;

            if (!gridMap.CanOccupy(origin, replacementDefinition.Size, out var failureReason, ignoredOccupantId))
            {
                Debug.LogWarning(
                    $"Cannot replace building '{sourceBuilding.name}' with '{replacementDefinition.DisplayName}' at {origin}: {failureReason}.",
                    sourceBuilding);
                return false;
            }

            sourceBuilding.ClearPlacement();
            UnityEngine.Object.Destroy(sourceBuilding.gameObject);

            return TryPlace(replacementDefinition, gridMap, origin, rotation, parent, out replacement);
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

        private static bool CanUseDefinition(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (definition.HasBuildingPrefab)
            {
                return true;
            }

            Debug.LogWarning($"Cannot use building definition '{definition.DisplayName}' because it has no building prefab.", definition);
            return false;
        }

        private static string CreateGridOccupancyId(BuildingDefinition definition)
        {
            var prefix = string.IsNullOrWhiteSpace(definition.BuildingId) ? definition.name : definition.BuildingId;
            return $"{prefix}_{Guid.NewGuid():N}";
        }
    }
}
