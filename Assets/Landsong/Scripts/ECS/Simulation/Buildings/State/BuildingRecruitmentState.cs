using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingRecruitmentState : IComponentData
    {
        public int Turn;
        public int Count;
    }
}
