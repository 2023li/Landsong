using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentItemJobEffect
    {
        public int Order;
        public ItemId Recipient;
        public NumericEffectKind Effect;
        public float BaseMagnitude;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
