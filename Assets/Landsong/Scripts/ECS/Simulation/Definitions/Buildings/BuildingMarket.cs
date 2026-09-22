using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingMarket
    {
        public bool Enabled;
        public BlobArray<BuildingMarketLevel> Levels;
    }
}
