using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentSoldierJobEffect
    {
        public int Order;
        public SoldierId Recipient;
        public NumericEffectKind Effect;
        public float BaseMagnitude;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
