using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingProductionSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("生产投入材料")]
        [ShowIf(nameof(Enabled))]
        public BuildingInputSource[] Inputs = Array.Empty<BuildingInputSource>();
        [LabelText("基础生产周期")]
        [ShowIf(nameof(Enabled))]
        public BuildingProductionCycleSource[] Cycles = Array.Empty<BuildingProductionCycleSource>();
        [LabelText("工人加工周期")]
        [ShowIf(nameof(Enabled))]
        public BuildingProcessingTierSource[] ProcessingTiers = Array.Empty<BuildingProcessingTierSource>();
        [LabelText("生产产出档位")]
        [ShowIf(nameof(Enabled))]
        public BuildingProductionOutputSource[] Outputs = Array.Empty<BuildingProductionOutputSource>();
        [LabelText("随机产出")]
        [ShowIf(nameof(Enabled))]
        public BuildingRareProductionSource[] RareOutputs = Array.Empty<BuildingRareProductionSource>();
    }
}
