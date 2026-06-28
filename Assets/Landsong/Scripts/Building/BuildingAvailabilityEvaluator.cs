using System;
using System.Collections.Generic;
using Landsong.InventorySystem;

namespace Landsong.BuildingSystem
{
    public static class BuildingAvailabilityEvaluator
    {
        public static BuildingAvailability Evaluate(
            BuildingDefinition definition,
            GameSystem gameSystem,
            IEnumerable<BuildingBase> existingBuildings)
        {
            if (definition == null)
            {
                return BuildingAvailability.Hidden(null, BuildingUnavailableReason.Hidden);
            }

            var builtCount = CountExistingBuildings(definition, existingBuildings);
            var visibleCondition = definition.VisibleCondition;
            var isVisible = visibleCondition == null || visibleCondition.IsMet(gameSystem);

            if (!isVisible)
            {
                return BuildingAvailability.Hidden(definition, BuildingUnavailableReason.Hidden);
            }

            var availableCondition = definition.AvailableCondition;
            var isUnlocked = availableCondition == null || availableCondition.IsMet(gameSystem);
            var hasBuildSlot = !definition.HasBuildCountLimit || builtCount < definition.MaxBuildCount;
            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            var canAfford = CanAffordPlacementCosts(definition, inventory);

            return new BuildingAvailability(
                definition,
                isVisible,
                isUnlocked,
                hasBuildSlot,
                canAfford,
                builtCount,
                definition.MaxBuildCount);
        }

        private static bool CanAffordPlacementCosts(BuildingDefinition definition, InventoryService inventory)
        {
            var placementCosts = definition.PlacementCosts;
            if (!HasAnyValidCost(placementCosts))
            {
                return true;
            }

            return inventory != null && inventory.CanAffordBuildingCosts(placementCosts);
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

        private static int CountExistingBuildings(BuildingDefinition definition, IEnumerable<BuildingBase> existingBuildings)
        {
            if (definition == null || existingBuildings == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var building in existingBuildings)
            {
                var existingDefinition = building == null ? null : building.Definition;
                if (existingDefinition == null)
                {
                    continue;
                }

                if (string.Equals(existingDefinition.BuildLimitGroupId, definition.BuildLimitGroupId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }
    }

    public readonly struct BuildingAvailability
    {
        public BuildingAvailability(
            BuildingDefinition definition,
            bool isVisible,
            bool isUnlocked,
            bool hasBuildSlot,
            bool canAfford,
            int builtCount,
            int maxBuildCount)
        {
            Definition = definition;
            IsVisible = isVisible;
            IsUnlocked = isUnlocked;
            HasBuildSlot = hasBuildSlot;
            CanAfford = canAfford;
            BuiltCount = builtCount;
            MaxBuildCount = maxBuildCount;
        }

        public BuildingDefinition Definition { get; }
        public bool IsVisible { get; }
        public bool IsUnlocked { get; }
        public bool HasBuildSlot { get; }
        public bool CanAfford { get; }
        public int BuiltCount { get; }
        public int MaxBuildCount { get; }
        public bool IsAvailable => IsVisible && IsUnlocked && HasBuildSlot;
        public bool CanBuild => IsAvailable && CanAfford;

        public BuildingUnavailableReason FirstUnavailableReason
        {
            get
            {
                if (!IsVisible)
                {
                    return BuildingUnavailableReason.Hidden;
                }

                if (!IsUnlocked)
                {
                    return BuildingUnavailableReason.Locked;
                }

                if (!HasBuildSlot)
                {
                    return BuildingUnavailableReason.BuildLimitReached;
                }

                if (!CanAfford)
                {
                    return BuildingUnavailableReason.MissingMaterials;
                }

                return BuildingUnavailableReason.None;
            }
        }

        public static BuildingAvailability Hidden(BuildingDefinition definition, BuildingUnavailableReason reason)
        {
            return new BuildingAvailability(definition, false, false, false, false, 0, 0);
        }
    }

    public enum BuildingUnavailableReason
    {
        None = 0,
        Hidden = 10,
        Locked = 20,
        MissingMaterials = 30,
        BuildLimitReached = 40
    }
}
