using Landsong.ECS.Definitions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public struct BuildingPlacementState : IComponentData
    {
        public int2 Cell;
        public int2 Size;
        public int Rotation;
        public int Elevation;
        public int Surface;
    }
}
