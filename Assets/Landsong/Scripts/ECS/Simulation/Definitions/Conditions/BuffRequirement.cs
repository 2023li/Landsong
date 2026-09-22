using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuffRequirement
    {
        public int Order;
        public BuffId Buff;
        public int Required;
    }
}
