using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingConstructionSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("放置材料")]
        [ShowIf(nameof(Enabled))]
        public BuildingPlacementCostSource[] PlacementCosts = Array.Empty<BuildingPlacementCostSource>();
        [LabelText("施工材料")]
        [ShowIf(nameof(Enabled))]
        public BuildingConstructionCostSource[] StageCosts = Array.Empty<BuildingConstructionCostSource>();
        [LabelText("施工产物")]
        [ShowIf(nameof(Enabled))]
        public BuildingConstructionOutputSource[] StageOutputs = Array.Empty<BuildingConstructionOutputSource>();
    }
}
