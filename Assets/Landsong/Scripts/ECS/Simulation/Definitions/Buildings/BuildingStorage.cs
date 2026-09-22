using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Definitions
{
    public struct BuildingStorage
    {
        public bool Enabled;
        public BlobArray<BuildingResourceProvider> Providers;
        public BlobArray<BuildingWarehouseLevel> Warehouses;
        public BlobArray<BuildingStorageCondition> Conditions;
    }
}
