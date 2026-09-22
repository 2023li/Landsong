using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingSpatialEffect
    {
        public int Level;
        public BuildingId Building;
        public int Magnitude;
        public BuildingEnvironmentKind Type;
        public int RequiredWorkers;
        public float Radius;
        public BuildingEffectStacking Stacking;
        public FixedString128Bytes Group;
    }
}
