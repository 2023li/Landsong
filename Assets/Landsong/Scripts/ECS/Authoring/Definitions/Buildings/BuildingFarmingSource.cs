using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingFarmingSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("种植所需工人"), ShowIf(nameof(Enabled))]
        public int RequiredWorkers;
        [LabelText("全生长期奖励所需工人"), ShowIf(nameof(Enabled))]
        public int FullCycleBonusWorkers;
        [LabelText("全生长期收获加成百分比"), ShowIf(nameof(Enabled))]
        public int FullCycleYieldBonusPercent;
        [LabelText("可种植作物")]
        [ShowIf(nameof(Enabled))]
        public BuildingAllowedCropSource[] Crops = Array.Empty<BuildingAllowedCropSource>();
    }
}
