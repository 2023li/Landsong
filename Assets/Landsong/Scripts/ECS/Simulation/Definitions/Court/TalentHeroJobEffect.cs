using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentHeroJobEffect
    {
        public int Order;
        public HeroId Recipient;
        public NumericEffectKind Effect;
        public float BaseMagnitude;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
