using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingWorkforceStats : IComponentData
    {
        public int Capacity;
    }
}
