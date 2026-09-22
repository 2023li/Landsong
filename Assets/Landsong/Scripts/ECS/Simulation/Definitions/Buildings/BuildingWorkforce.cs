using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingWorkforce
    {
        public bool Enabled;
        public BlobArray<BuildingWorkforceLevel> Levels;
        public BlobArray<BuildingWorkerEfficiencyTier> EfficiencyTiers;
        public BlobArray<BuildingAttraction> Attraction;
    }
}
