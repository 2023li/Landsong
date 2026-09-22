using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct KingdomNumericEffect
    {
        public int Order;
        public int Level;
        public KingdomEffectKind Effect;
        public float Magnitude;
    }
}
