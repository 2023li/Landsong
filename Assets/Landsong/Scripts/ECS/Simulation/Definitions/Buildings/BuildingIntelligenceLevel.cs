using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingIntelligenceLevel
    {
        public int Level;
        public TechnologyId Technology;
        public int Points;
        public int RequiredWorkers;
    }
}
