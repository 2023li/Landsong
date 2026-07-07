using System;
using System.Collections.Generic;
using Landsong.InventorySystem;

namespace Landsong.BuildingSystem
{
    public static class BuildingAvailabilityEvaluator
    {
        public static BuildingAvailability Evaluate(
            BuildingBase buildingPrefab,
            GameSystem gameSystem,
            int builtCount)
        {
            if (buildingPrefab == null || !buildingPrefab.HasDefinition)
            {
                return BuildingAvailability.Hidden(buildingPrefab, BuildingUnavailableReason.Hidden);
            }

            var definition = buildingPrefab.Definition;
            builtCount = Math.Max(0, builtCount);
            var visibleCondition = definition.VisibleCondition;
            var isVisible = visibleCondition == null || visibleCondition.IsMet(gameSystem);

            if (!isVisible)
            {
                return BuildingAvailability.Hidden(buildingPrefab, BuildingUnavailableReason.Hidden);
            }

            var availableCondition = definition.AvailableCondition;
            var isDevelopmentCompleted = definition.IsDevelopmentCompleted;
            var isUnlocked = availableCondition == null || availableCondition.IsMet(gameSystem);
            var isBlueprintUnlocked = IsBlueprintUnlocked(definition, gameSystem);
            var hasBuildSlot = !definition.HasBuildCountLimit || builtCount < definition.MaxBuildCount;
            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            var canAfford = CanAffordPlacementCosts(definition, inventory);

            return new BuildingAvailability(
                buildingPrefab,
                isVisible,
                isDevelopmentCompleted,
                isUnlocked,
                isBlueprintUnlocked,
                hasBuildSlot,
                canAfford,
                builtCount,
                definition.MaxBuildCount);
        }

        private static bool IsBlueprintUnlocked(BuildingDefinition definition, GameSystem gameSystem)
        {
            if (!RequiresBlueprint(definition))
            {
                return true;
            }

            return gameSystem != null && gameSystem.IsBuildingBlueprintUnlocked(definition.BuildingId);
        }

        private static bool RequiresBlueprint(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            return (definition.Category & BuildingCategory.奇迹) == BuildingCategory.奇迹;
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
    }

    public readonly struct BuildingAvailability
    {
        public BuildingAvailability(
            BuildingBase buildingPrefab,
            bool isVisible,
            bool isDevelopmentCompleted,
            bool isUnlocked,
            bool isBlueprintUnlocked,
            bool hasBuildSlot,
            bool canAfford,
            int builtCount,
            int maxBuildCount)
        {
            BuildingPrefab = buildingPrefab;
            IsVisible = isVisible;
            IsDevelopmentCompleted = isDevelopmentCompleted;
            IsUnlocked = isUnlocked;
            IsBlueprintUnlocked = isBlueprintUnlocked;
            HasBuildSlot = hasBuildSlot;
            CanAfford = canAfford;
            BuiltCount = builtCount;
            MaxBuildCount = maxBuildCount;
        }

        public BuildingBase BuildingPrefab { get; }
        public BuildingDefinition Definition => BuildingPrefab == null ? null : BuildingPrefab.Definition;
        public bool IsVisible { get; }
        public bool IsDevelopmentCompleted { get; }
        public bool IsUnlocked { get; }
        public bool IsBlueprintUnlocked { get; }
        public bool HasBuildSlot { get; }
        public bool CanAfford { get; }
        public int BuiltCount { get; }
        public int MaxBuildCount { get; }
        public bool IsAvailable => IsVisible && IsDevelopmentCompleted && IsUnlocked && IsBlueprintUnlocked && HasBuildSlot;
        public bool CanBuild => IsAvailable && CanAfford;

        public BuildingUnavailableReason FirstUnavailableReason
        {
            get
            {
                if (!IsVisible)
                {
                    return BuildingUnavailableReason.Hidden;
                }

                if (!IsDevelopmentCompleted)
                {
                    return BuildingUnavailableReason.DevelopmentIncomplete;
                }

                if (!IsUnlocked)
                {
                    return BuildingUnavailableReason.Locked;
                }

                if (!IsBlueprintUnlocked)
                {
                    return BuildingUnavailableReason.BlueprintLocked;
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

        public static BuildingAvailability Hidden(BuildingBase buildingPrefab, BuildingUnavailableReason reason)
        {
            return new BuildingAvailability(buildingPrefab, false, false, false, false, false, false, 0, 0);
        }
    }

    public enum BuildingUnavailableReason
    {
        None = 0,
        Hidden = 10,
        DevelopmentIncomplete = 15,
        Locked = 20,
        BlueprintLocked = 25,
        MissingMaterials = 30,
        BuildLimitReached = 40
    }
}
