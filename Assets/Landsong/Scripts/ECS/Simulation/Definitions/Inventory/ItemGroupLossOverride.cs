using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct ItemGroupLossOverride
    {
        public ItemGroupId Group;
        public float Multiplier;
    }
}
