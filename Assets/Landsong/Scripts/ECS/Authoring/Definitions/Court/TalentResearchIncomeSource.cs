using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentResearchIncomeSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("基础科研点")]
        public float BasePoints;
        [LabelText("每级增加量")]
        public float PerLevel;
        [LabelText("缩放来源")]
        public TalentEffectScalingSource Scaling = new TalentEffectScalingSource();
    }
}
