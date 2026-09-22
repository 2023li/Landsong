using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingResidenceLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("居民容量")]
        [MinValue(0)]
        public int Capacity;
        [LabelText("完工居民数")]
        [MinValue(0)]
        public int InitialResidents;
        [LabelText("连续缺粮衰退阈值")]
        [MinValue(0)]
        public int StarvationThreshold;
        [LabelText("人口增长间隔（回合）")]
        [MinValue(1)]
        public int GrowthInterval = 1;
    }
}
