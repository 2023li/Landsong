using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingWorkerEfficiencyTier
    {
        public int Level;
        public int MinimumWorkers;
        public int MaximumWorkers;
    }
}
