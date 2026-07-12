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
            var isBlueprintUnlocked = IsBlueprintUnlocked(definition, gameSystem);
            var visibleCondition = definition.VisibleCondition;
            var isVisible = (visibleCondition == null || visibleCondition.IsMet(gameSystem))
                            && (!definition.HideWhenBlueprintLocked || isBlueprintUnlocked);

            if (!isVisible)
            {
                return BuildingAvailability.Hidden(buildingPrefab, BuildingUnavailableReason.Hidden);
            }

            var isDevelopmentCompleted = definition.IsDevelopmentCompleted;
            var hasBuildSlot = !definition.HasBuildCountLimit || builtCount < definition.MaxBuildCount;
            var inventory = gameSystem == null ? null : gameSystem.Services.Inventory;
            var canAfford = CanAffordPlacementCosts(definition, inventory);

            return new BuildingAvailability(
                buildingPrefab,
                isVisible,
                isDevelopmentCompleted,
                isBlueprintUnlocked,
                hasBuildSlot,
                canAfford,
                builtCount,
                definition.MaxBuildCount);
        }

        private static bool IsBlueprintUnlocked(BuildingDefinition definition, GameSystem gameSystem)
        {
            return definition != null
                   && gameSystem != null
                   && gameSystem.Services.BuildingBlueprints.IsUnlocked(definition.BuildingId);
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
            bool isBlueprintUnlocked,
            bool hasBuildSlot,
            bool canAfford,
            int builtCount,
            int maxBuildCount)
        {
            BuildingPrefab = buildingPrefab;
            IsVisible = isVisible;
            IsDevelopmentCompleted = isDevelopmentCompleted;
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
        public bool IsBlueprintUnlocked { get; }
        public bool HasBuildSlot { get; }
        public bool CanAfford { get; }
        public int BuiltCount { get; }
        public int MaxBuildCount { get; }
        public bool IsAvailable => IsVisible && IsDevelopmentCompleted && IsBlueprintUnlocked && HasBuildSlot;
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
            return new BuildingAvailability(buildingPrefab, false, false, false, false, false, 0, 0);
        }
    }

    public enum BuildingUnavailableReason
    {
        None = 0,
        Hidden = 10,
        DevelopmentIncomplete = 15,
        BlueprintLocked = 25,
        MissingMaterials = 30,
        BuildLimitReached = 40
    }
}
