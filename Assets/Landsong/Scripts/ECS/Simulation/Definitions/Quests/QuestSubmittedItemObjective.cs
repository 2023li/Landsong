using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct QuestSubmittedItemObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public ItemId Item;
        public int Quantity;
    }
}
