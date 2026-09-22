using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingProduction
    {
        public bool Enabled;
        public BlobArray<BuildingInput> Inputs;
        public BlobArray<BuildingProductionCycle> Cycles;
        public BlobArray<BuildingProcessingTier> ProcessingTiers;
        public BlobArray<BuildingProductionOutput> Outputs;
        public BlobArray<BuildingRareProduction> RareOutputs;
    }
}
