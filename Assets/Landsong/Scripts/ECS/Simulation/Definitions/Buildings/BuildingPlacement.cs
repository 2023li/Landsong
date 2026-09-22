using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingPlacement
    {
        public TerrainType AllowedTerrains;
        public TerrainType ExcludedTerrains;
    }
}
