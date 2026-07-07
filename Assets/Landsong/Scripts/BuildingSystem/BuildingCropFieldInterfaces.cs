using System.Collections.Generic;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    public readonly struct BuildingCropOption
    {
        public BuildingCropOption(string cropId, string displayName, int growTurns, Sprite icon = null)
        {
            CropId = string.IsNullOrWhiteSpace(cropId) ? string.Empty : cropId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? CropId : displayName.Trim();
            GrowTurns = growTurns < 1 ? 1 : growTurns;
            Icon = icon;
        }

        public string CropId { get; }
        public string DisplayName { get; }
        public int GrowTurns { get; }
        public Sprite Icon { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(CropId);
    }

    public interface IBuildingCropFieldSource
    {
        string PlantedCropId { get; }
        string PlantedCropDisplayName { get; }
        int GrowthProgressTurns { get; }
        int RequiredGrowTurns { get; }
        int RemainingGrowTurns { get; }
        bool HasCrop { get; }
        bool IsMature { get; }
        bool AutoHarvestEnabled { get; }
        IReadOnlyList<BuildingCropOption> CropOptions { get; }
        IReadOnlyList<BuildingResourceChange> LastHarvestRewards { get; }
    }

    public interface IBuildingCropFieldActions
    {
        bool CanPlant(string cropId);
        bool CanHarvest();
        bool CanClearCrop();
        bool TryPlant(string cropId);
        bool TryHarvest();
        bool TryClearCrop();
        bool TrySetAutoHarvestEnabled(bool enabled);
    }
}
