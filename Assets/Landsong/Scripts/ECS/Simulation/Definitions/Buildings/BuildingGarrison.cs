using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingGarrison
    {
        public bool Enabled;
        public int RecruitmentLimitPerTurn;
        public BlobArray<BuildingGarrisonLevel> Levels;
        public BlobArray<BuildingInitialGarrison> InitialUnits;
    }
}
