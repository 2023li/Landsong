using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingFarming
    {
        public bool Enabled;
        public int RequiredWorkers;
        public int FullCycleBonusWorkers;
        public int FullCycleYieldBonusPercent;
        public BlobArray<BuildingAllowedCrop> Crops;
    }
}
