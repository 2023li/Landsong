using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingResidenceLevel
    {
        public int Level;
        public int Capacity;
        public int InitialResidents;
        public int StarvationThreshold;
        public int GrowthInterval;
    }
}
