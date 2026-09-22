using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingFarmingSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("可种植作物")]
        [ShowIf(nameof(Enabled))]
        public BuildingAllowedCropSource[] Crops = Array.Empty<BuildingAllowedCropSource>();
    }
}
