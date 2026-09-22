using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct TechnologyRequirement
    {
        public int Order;
        public TechnologyId Technology;
        public int Required;
    }
}
