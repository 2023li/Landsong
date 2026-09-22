using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingWorkforceSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("岗位配置")]
        [ShowIf(nameof(Enabled))]
        public BuildingWorkforceLevelSource[] Levels = Array.Empty<BuildingWorkforceLevelSource>();
        [LabelText("工作效率档位")]
        [ShowIf(nameof(Enabled))]
        public BuildingWorkerEfficiencyTierSource[] EfficiencyTiers = Array.Empty<BuildingWorkerEfficiencyTierSource>();
        [LabelText("附近居民吸引力")]
        [ShowIf(nameof(Enabled))]
        public BuildingAttractionSource[] Attraction = Array.Empty<BuildingAttractionSource>();
    }
}
