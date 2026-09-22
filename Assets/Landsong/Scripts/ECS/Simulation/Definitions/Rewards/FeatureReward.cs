using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct FeatureReward
    {
        public int Order;
        public FeatureId Feature;
        public int GrantedLevel;
    }
}
