using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingMarketState : IComponentData
    {
        public long TurnValue;
        public long LifetimeValue;
    }
}
