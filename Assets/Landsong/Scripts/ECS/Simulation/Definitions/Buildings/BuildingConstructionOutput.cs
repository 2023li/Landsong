using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingConstructionOutput
    {
        public int Stage;
        public ItemId Item;
        public int Quantity;
    }
}
