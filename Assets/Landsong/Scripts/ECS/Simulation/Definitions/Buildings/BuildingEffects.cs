using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingEffects
    {
        public bool Enabled;
        public BlobArray<BuildingSpatialEffect> Spatial;
    }
}
