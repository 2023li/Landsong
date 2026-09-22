using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingTerrainConnection
    {
        public bool Enabled;
        public int Rise;
        public bool Bidirectional;
        public float Clearance;
        public float DamagedCost;
    }
}
