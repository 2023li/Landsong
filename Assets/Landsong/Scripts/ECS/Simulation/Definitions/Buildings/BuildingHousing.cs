using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingHousing
    {
        public bool Enabled;
        public BlobArray<BuildingBasePopulation> Population;
        public BlobArray<BuildingResidenceLevel> Residences;
        public BlobArray<BuildingResidentFood> Food;
        public BlobArray<BuildingResidenceTax> Taxes;
        public BlobArray<BuildingEnvironmentRequirement> Environment;
    }
}
