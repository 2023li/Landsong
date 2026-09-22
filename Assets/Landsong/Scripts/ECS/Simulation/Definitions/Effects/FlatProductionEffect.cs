using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct FlatProductionEffect
    {
        public int Order;
        public int Level;
        public ItemId Item;
        public BuildingId Building;
        public int Quantity;
    }
}
