using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuffReward
    {
        public int Order;
        public BuffId Buff;
        public int GrantedLevel;
    }
}
