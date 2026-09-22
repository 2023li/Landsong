using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingWorkerEfficiencyTierSource
    {
        [LabelText("建筑等级（0 = 通用）")]
        [MinValue(0)]
        public int Level;
        [LabelText("最少工人")]
        [MinValue(0)]
        public int MinimumWorkers;
        [LabelText("最多工人（含）")]
        [MinValue(0)]
        public int MaximumWorkers;
    }
}
