using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingRequirement
    {
        public int Order;
        public BuildingId Building;
        public int Required;
    }
}
