using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingSanctumLevel
    {
        public int Level;
        public HeroId Hero;
        public int RequiredWorkers;
    }
}
