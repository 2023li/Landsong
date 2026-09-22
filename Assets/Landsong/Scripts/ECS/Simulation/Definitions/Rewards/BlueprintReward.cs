using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BlueprintReward
    {
        public int Order;
        public BuildingId Building;
        public int GrantedLevel;
    }
}
