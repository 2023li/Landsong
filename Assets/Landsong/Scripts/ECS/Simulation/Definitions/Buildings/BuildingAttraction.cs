using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingAttraction
    {
        public int Level;
        public float Radius;
        public float PerResidentBonus;
    }
}
