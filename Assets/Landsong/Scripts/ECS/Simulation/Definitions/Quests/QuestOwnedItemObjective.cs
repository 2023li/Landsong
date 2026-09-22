using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct QuestOwnedItemObjective
    {
        public int Order;
        public FixedString64Bytes Key;
        public ItemId Item;
        public int Quantity;
    }
}
