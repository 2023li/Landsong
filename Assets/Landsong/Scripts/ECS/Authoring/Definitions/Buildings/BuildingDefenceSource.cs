using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingDefenceSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("情报产出")]
        [ShowIf(nameof(Enabled))]
        public BuildingIntelligenceLevelSource[] Intelligence = Array.Empty<BuildingIntelligenceLevelSource>();
        [LabelText("警铃集结")]
        [ShowIf(nameof(Enabled))]
        public BuildingBellLevelSource[] Bells = Array.Empty<BuildingBellLevelSource>();
    }
}
