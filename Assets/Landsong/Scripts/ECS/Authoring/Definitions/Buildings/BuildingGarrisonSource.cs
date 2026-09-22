using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingGarrisonSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("每回合募兵上限（0 = 驻军容量）")]
        [MinValue(0)]
        [ShowIf(nameof(Enabled))]
        public int RecruitmentLimitPerTurn;
        [LabelText("驻军容量")]
        [ShowIf(nameof(Enabled))]
        public BuildingGarrisonLevelSource[] Levels = Array.Empty<BuildingGarrisonLevelSource>();
        [LabelText("开局驻军")]
        [ShowIf(nameof(Enabled))]
        public BuildingInitialGarrisonSource[] InitialUnits = Array.Empty<BuildingInitialGarrisonSource>();
    }
}
