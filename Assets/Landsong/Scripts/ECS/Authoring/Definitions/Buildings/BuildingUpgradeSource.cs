using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingUpgradeSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("升级材料")]
        [ShowIf(nameof(Enabled))]
        public BuildingUpgradeCostSource[] Costs = Array.Empty<BuildingUpgradeCostSource>();
        [LabelText("升级所需工人")]
        [ShowIf(nameof(Enabled))]
        public BuildingUpgradeWorkersSource[] Workers = Array.Empty<BuildingUpgradeWorkersSource>();
        [LabelText("升级所需居民")]
        [ShowIf(nameof(Enabled))]
        public BuildingUpgradePopulationSource[] Residents = Array.Empty<BuildingUpgradePopulationSource>();
        [LabelText("升级需维护")]
        [ShowIf(nameof(Enabled))]
        public BuildingUpgradeMaintenanceSource[] MaintenanceRequirements = Array.Empty<BuildingUpgradeMaintenanceSource>();
        [LabelText("经验成长")]
        [ShowIf(nameof(Enabled))]
        public BuildingExperienceSource[] Experience = Array.Empty<BuildingExperienceSource>();
    }
}
