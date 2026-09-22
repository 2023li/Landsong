using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct LeveledItemAmount
    {
        public int Order;
        public int Level;
        public ItemId Item;
        public int Quantity;
    }
}
