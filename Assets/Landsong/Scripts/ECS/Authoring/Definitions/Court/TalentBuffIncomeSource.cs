using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentBuffIncomeSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("增益")]
        public BuffDefinitionAsset Buff;
        [LabelText("基础等级")]
        public float BaseLevel;
        [LabelText("每级增加量")]
        public float PerLevel;
        [LabelText("缩放来源")]
        public TalentEffectScalingSource Scaling = new TalentEffectScalingSource();
    }
}
