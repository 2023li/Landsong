using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct ItemNumericEffect
    {
        public int Order;
        public int Level;
        public ItemId Target;
        public NumericEffectKind Effect;
        public float Magnitude;
    }
}
