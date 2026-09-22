using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingGatheringSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("采集配置")]
        [ShowIf(nameof(Enabled))]
        public BuildingGatheringLevelSource[] Levels = Array.Empty<BuildingGatheringLevelSource>();
        [LabelText("最终采集奖励")]
        [ShowIf(nameof(Enabled))]
        public BuildingGatheringRewardSource[] Rewards = Array.Empty<BuildingGatheringRewardSource>();
    }
}
