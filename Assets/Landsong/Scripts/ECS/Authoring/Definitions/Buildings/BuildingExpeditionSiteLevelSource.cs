using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingExpeditionSiteLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("最少派遣人数")]
        [MinValue(0)]
        public int MinimumCrew;
        [LabelText("最多派遣人数")]
        [MinValue(0)]
        public int MaximumCrew;
        [LabelText("满员奖励加成")]
        [MinValue(0)]
        public float FullCrewRewardBonus;
    }
}
