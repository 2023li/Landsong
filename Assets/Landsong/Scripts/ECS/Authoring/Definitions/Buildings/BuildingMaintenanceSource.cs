using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingMaintenanceSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("默认修复回合数（0 = 跟随建筑施工时长）")]
        [MinValue(0)]
        [ShowIf(nameof(Enabled))]
        public int RepairTurns;
        [LabelText("每回合维护材料")]
        [ShowIf(nameof(Enabled))]
        public BuildingMaintenanceCostSource[] Costs = Array.Empty<BuildingMaintenanceCostSource>();
        [LabelText("修复材料")]
        [ShowIf(nameof(Enabled))]
        public BuildingRepairMaterialSource[] Repairs = Array.Empty<BuildingRepairMaterialSource>();
    }
}
