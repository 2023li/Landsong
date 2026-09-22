using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingConstruction
    {
        public bool Enabled;
        public BlobArray<BuildingPlacementCost> PlacementCosts;
        public BlobArray<BuildingConstructionCost> StageCosts;
        public BlobArray<BuildingConstructionOutput> StageOutputs;
    }
}
