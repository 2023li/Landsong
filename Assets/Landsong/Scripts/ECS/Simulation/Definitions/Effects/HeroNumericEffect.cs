using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct HeroNumericEffect
    {
        public int Order;
        public int Level;
        public HeroId Target;
        public NumericEffectKind Effect;
        public float Magnitude;
    }
}
