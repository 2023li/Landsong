using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingInitialGarrison
    {
        public int Level;
        public SoldierId Soldier;
        public int Count;
    }
}
