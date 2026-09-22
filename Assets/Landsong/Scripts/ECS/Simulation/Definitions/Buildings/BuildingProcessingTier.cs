using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingProcessingTier
    {
        public int Level;
        public int Interval;
        public int RequiredWorkers;
    }
}
