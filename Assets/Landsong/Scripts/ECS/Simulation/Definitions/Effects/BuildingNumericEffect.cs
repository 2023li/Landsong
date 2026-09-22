using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingNumericEffect
    {
        public int Order;
        public int Level;
        public BuildingId Target;
        public NumericEffectKind Effect;
        public float Magnitude;
    }
}
