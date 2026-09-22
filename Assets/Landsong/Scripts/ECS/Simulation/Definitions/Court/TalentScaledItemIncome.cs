using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentScaledItemIncome
    {
        public int Order;
        public ItemId Item;
        public float BaseQuantity;
        public float PerLevel;
        public TalentEffectScaling Scaling;
    }
}
