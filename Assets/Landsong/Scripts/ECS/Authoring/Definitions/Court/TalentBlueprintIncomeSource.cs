using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentBlueprintIncomeSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("基础等级")]
        public float BaseLevel;
        [LabelText("每级增加量")]
        public float PerLevel;
        [LabelText("缩放来源")]
        public TalentEffectScalingSource Scaling = new TalentEffectScalingSource();
    }
}
