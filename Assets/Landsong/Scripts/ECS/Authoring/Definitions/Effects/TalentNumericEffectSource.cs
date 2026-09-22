using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentNumericEffectSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("适用等级")]
        public int Level;
        [LabelText("作用对象")]
        public TalentDefinitionAsset Target;
        [LabelText("效果种类")]
        public NumericEffectKind Effect;
        [LabelText("效果数值")]
        public float Magnitude;
    }
}
