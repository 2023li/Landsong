using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentNumericEffect
    {
        public int Order;
        public int Level;
        public TalentId Target;
        public NumericEffectKind Effect;
        public float Magnitude;
    }
}
