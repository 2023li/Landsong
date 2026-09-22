using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingSanctumState : IComponentData
    {
        public byte Offering;
        public int PaidOfferingTurn;
        public int WokenTurn;
    }
}
