using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingWorkforceLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("补贴和招聘货币（空 = 默认金币）")]
        public ItemDefinitionAsset Currency;
        [LabelText("岗位上限")]
        [MinValue(0)]
        public int Capacity;
        [LabelText("初始工人")]
        [MinValue(0)]
        public int InitialWorkers;
        [LabelText("默认启用补贴")]
        public bool InitialSubsidy;
        [LabelText("基础吸引力")]
        [MinValue(0)]
        public float BaseAttraction;
        [LabelText("单人招聘费用")]
        [MinValue(0)]
        public float RecruitmentCost;
    }
}
