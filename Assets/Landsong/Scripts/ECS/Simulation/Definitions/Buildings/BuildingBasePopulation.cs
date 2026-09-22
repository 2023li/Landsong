using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingBasePopulation
    {
        public int Level;
        public int Population;
        public bool IsCore;
    }
}
