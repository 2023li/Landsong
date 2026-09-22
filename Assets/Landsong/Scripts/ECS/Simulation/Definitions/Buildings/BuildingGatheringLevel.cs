using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingGatheringLevel
    {
        public int Level;
        public ItemId Item;
        public int Uses;
        public int AmountPerUse;
    }
}
