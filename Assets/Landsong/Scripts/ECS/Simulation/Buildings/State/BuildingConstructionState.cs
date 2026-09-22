using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingConstructionState : IComponentData
    {
        public int Progress;
        public int RepairDuration;
        public int RepairCompletedTurn;
    }
}
