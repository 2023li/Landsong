using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingExpeditions
    {
        public bool Enabled;
        public BlobArray<BuildingExpeditionSiteLevel> Levels;
    }
}
