using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingFarming
    {
        public bool Enabled;
        public BlobArray<BuildingAllowedCrop> Crops;
    }
}
