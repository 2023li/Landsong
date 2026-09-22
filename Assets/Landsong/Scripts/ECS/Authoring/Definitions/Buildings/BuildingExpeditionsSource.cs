using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingExpeditionsSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("远征所")]
        [ShowIf(nameof(Enabled))]
        public BuildingExpeditionSiteLevelSource[] Levels = Array.Empty<BuildingExpeditionSiteLevelSource>();
    }
}
