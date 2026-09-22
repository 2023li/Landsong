using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingSanctum
    {
        public bool Enabled;
        public BlobArray<BuildingSanctumLevel> Levels;
    }
}
