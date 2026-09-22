using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct ExpeditionSupply
    {
        public int Order;
        public ItemId Item;
        public int MinimumQuantity;
        public int ExtraLimit;
        public float SuccessPerExtra;
        public float RewardPerExtra;
    }
}
