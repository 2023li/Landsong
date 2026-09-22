using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingStorageConditionSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("岗位门槛")]
        [MinValue(0)]
        public int RequiredWorkers;
        [LabelText("维护失败损耗倍率（百分比）")]
        [MinValue(0)]
        public int MaintenanceLossPercent;
        [LabelText("维护不足吸引力惩罚")]
        [MinValue(0)]
        public float AttractionPenalty;
        [LabelText("缺工损耗倍率")]
        [MinValue(0)]
        public float UnderstaffedLossMultiplier;
    }
}
