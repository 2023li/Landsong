using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingUpgradeCost
    {
        public int TargetLevel;
        public ItemId Item;
        public int Quantity;
    }
}
