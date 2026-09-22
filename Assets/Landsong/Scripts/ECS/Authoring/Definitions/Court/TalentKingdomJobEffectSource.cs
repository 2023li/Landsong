using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentKingdomJobEffectSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("效果种类")]
        public KingdomEffectKind Effect;
        [LabelText("基础效果")]
        public float BaseMagnitude;
        [LabelText("每级增加量")]
        public float PerLevel;
        [LabelText("缩放来源")]
        public TalentEffectScalingSource Scaling = new TalentEffectScalingSource();
    }
}
