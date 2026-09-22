using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingConstructionCost
    {
        public int Stage;
        public ItemId Item;
        public int Quantity;
    }
}
