using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingMarketSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("市场收入")]
        [ShowIf(nameof(Enabled))]
        public BuildingMarketLevelSource[] Levels = Array.Empty<BuildingMarketLevelSource>();
    }
}
