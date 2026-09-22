using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct ItemLossOverride
    {
        public ItemId Item;
        public float Multiplier;
    }
}
