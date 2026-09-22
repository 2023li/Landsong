using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingStorageSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("资源提供点")]
        [ShowIf(nameof(Enabled))]
        public BuildingResourceProviderSource[] Providers = Array.Empty<BuildingResourceProviderSource>();
        [LabelText("库存容量")]
        [ShowIf(nameof(Enabled))]
        public BuildingWarehouseLevelSource[] Warehouses = Array.Empty<BuildingWarehouseLevelSource>();
        [LabelText("仓储运行条件")]
        [ShowIf(nameof(Enabled))]
        public BuildingStorageConditionSource[] Conditions = Array.Empty<BuildingStorageConditionSource>();
    }
}
