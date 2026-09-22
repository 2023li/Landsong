using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingGathering
    {
        public bool Enabled;
        public BlobArray<BuildingGatheringLevel> Levels;
        public BlobArray<BuildingGatheringReward> Rewards;
    }
}
