using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingGatheringState : IComponentData
    {
        public int RemainingUses;
    }
}
