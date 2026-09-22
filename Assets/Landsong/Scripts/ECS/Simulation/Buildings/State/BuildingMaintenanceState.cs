using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingMaintenanceState : IComponentData
    {
        public byte Maintained;
    }
}
