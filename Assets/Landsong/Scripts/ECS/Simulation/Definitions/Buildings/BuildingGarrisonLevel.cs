using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingGarrisonLevel
    {
        public int Level;
        public int Capacity;
        public int DeploymentBatchSize;
    }
}
