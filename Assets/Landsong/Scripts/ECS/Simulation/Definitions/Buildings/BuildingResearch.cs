using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingResearch
    {
        public bool Enabled;
        public BlobArray<BuildingResearchLevel> Levels;
    }
}
