using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingEffectsSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("范围效果")]
        [ShowIf(nameof(Enabled))]
        public BuildingSpatialEffectSource[] Spatial = Array.Empty<BuildingSpatialEffectSource>();
    }
}
