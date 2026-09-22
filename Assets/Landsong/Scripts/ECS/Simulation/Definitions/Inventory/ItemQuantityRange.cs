using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct ItemQuantityRange
    {
        public int Order;
        public ItemId Item;
        public int MinimumQuantity;
        public int MaximumQuantity;
    }
}
