using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct IntelligenceEffect
    {
        public int Order;
        public int Level;
        public TechnologyId RequiredTechnology;
        public int Points;
    }
}
