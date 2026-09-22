using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentKingdomJobEffect
    {
        public int Order;
        public KingdomEffectKind Effect;
        public float BaseMagnitude;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
