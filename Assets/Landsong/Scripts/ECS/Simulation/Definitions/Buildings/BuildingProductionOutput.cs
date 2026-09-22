using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingProductionOutput
    {
        public int Level;
        public ItemId Item;
        public int Quantity;
        public int MinimumWorkers;
        public int MaximumWorkers;
    }
}
