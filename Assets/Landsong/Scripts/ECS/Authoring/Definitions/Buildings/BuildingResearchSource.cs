using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingResearchSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("科研产出"), ShowIf(nameof(Enabled))]
        public BuildingResearchLevelSource[] Levels = Array.Empty<BuildingResearchLevelSource>();
    }
}
