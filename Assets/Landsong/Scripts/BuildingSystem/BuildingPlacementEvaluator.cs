using System;
using System.Collections.Generic;
using Landsong.GridSystem;

namespace Landsong.BuildingSystem
{
    public sealed class BuildingPlacementEvaluation
    {
        public BuildingPlacementEvaluation(
            BuildingBase buildingPrefab,
            GridFootprint footprint,
            GridPlacementFailureReason gridFailure,
            BuildingConnectionQueryResult resourceConnection,
            IReadOnlyList<BuildingSpatialEffectPreview> spatialEffects)
        {
            BuildingPrefab = buildingPrefab;
            Footprint = footprint;
            GridFailure = gridFailure;
            ResourceConnection = resourceConnection;
            SpatialEffects = spatialEffects ?? Array.Empty<BuildingSpatialEffectPreview>();
        }

        public BuildingBase BuildingPrefab { get; }
        public GridFootprint Footprint { get; }
        public GridPlacementFailureReason GridFailure { get; }
        public BuildingConnectionQueryResult ResourceConnection { get; }
        public IReadOnlyList<BuildingSpatialEffectPreview> SpatialEffects { get; }
        public bool IsSpatiallyLegal => GridFailure == GridPlacementFailureReason.None;
        public bool CanConfirm => IsSpatiallyLegal;
        public bool RequiresResourceConnection => ResourceConnection != null;
        public bool ResourceProviderFound => ResourceConnection?.HasSelectedProvider == true;
    }

    public static class BuildingPlacementEvaluator
    {
        public static BuildingPlacementEvaluation Evaluate(
            BuildingBase buildingPrefab,
            GridMapBehaviour gridMap,
            GridPosition origin,
            IReadOnlyList<BuildingBase> runtimeBuildings,
            string ignoredOccupantId = null)
        {
            if (buildingPrefab == null || !buildingPrefab.HasDefinition || gridMap == null)
            {
                return new BuildingPlacementEvaluation(
                    buildingPrefab,
                    new GridFootprint(origin, buildingPrefab?.Definition?.Size ?? UnityEngine.Vector2Int.one),
                    GridPlacementFailureReason.InvalidSize,
                    null,
                    null);
            }

            var definition = buildingPrefab.Definition;
            var footprint = definition.CreateFootprint(origin);
            gridMap.CanOccupy(
                origin,
                definition.Size,
                definition.RequiredTerrainKeys,
                out var gridFailure,
                ignoredOccupantId);

            BuildingConnectionQueryResult resourceConnection = null;
            if (buildingPrefab.RequiresConnectionType(BuildingConnectionTypes.Resource))
            {
                var probe = new ResourceConsumerProbe(
                    gridMap,
                    footprint,
                    buildingPrefab.BuildingActionPower,
                    BuildingConnectionTypes.Resource,
                    ignoredOccupantId);
                BuildingResourceProviderSystem.TryQuery(probe, runtimeBuildings, out resourceConnection);
            }

            var spatialEffects = BuildingSpatialEffectService.BuildPlacementPreviews(
                buildingPrefab,
                gridMap,
                footprint);
            return new BuildingPlacementEvaluation(
                buildingPrefab,
                footprint,
                gridFailure,
                resourceConnection,
                spatialEffects);
        }
    }
}
