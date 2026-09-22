using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingDefence
    {
        public bool Enabled;
        public BlobArray<BuildingIntelligenceLevel> Intelligence;
        public BlobArray<BuildingBellLevel> Bells;
    }
}
