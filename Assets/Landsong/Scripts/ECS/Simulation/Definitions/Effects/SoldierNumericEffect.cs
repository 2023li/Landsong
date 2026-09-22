using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct SoldierNumericEffect
    {
        public int Order;
        public int Level;
        public SoldierId Target;
        public NumericEffectKind Effect;
        public float Magnitude;
    }
}
