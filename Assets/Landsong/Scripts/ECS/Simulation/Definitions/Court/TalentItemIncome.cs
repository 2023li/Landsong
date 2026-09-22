using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TalentItemIncome
    {
        public int Order;
        public ItemId Item;
        public int BaseQuantity;
        public float PerLevel;
    }
}
