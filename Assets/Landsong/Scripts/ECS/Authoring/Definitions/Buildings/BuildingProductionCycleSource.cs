using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingProductionCycleSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("生产间隔")]
        [MinValue(0)]
        public int Interval = 1;
        [LabelText("所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
    }
}
