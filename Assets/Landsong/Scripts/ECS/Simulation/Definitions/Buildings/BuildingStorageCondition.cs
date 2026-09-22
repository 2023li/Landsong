using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingStorageCondition
    {
        public int Level;
        public int RequiredWorkers;
        public int MaintenanceLossPercent;
        public float AttractionPenalty;
        public float UnderstaffedLossMultiplier;
    }
}
