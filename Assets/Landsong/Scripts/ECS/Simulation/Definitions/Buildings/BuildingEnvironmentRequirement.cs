using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingEnvironmentRequirement
    {
        public int Level;
        public int RequiredValue;
        public BuildingEnvironmentKind Type;
    }
}
