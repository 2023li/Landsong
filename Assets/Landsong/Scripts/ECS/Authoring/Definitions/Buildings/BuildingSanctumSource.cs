using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingSanctumSource
    {
        [LabelText("启用模块")]
        public bool Enabled;
        [LabelText("英雄供奉")]
        [ShowIf(nameof(Enabled))]
        public BuildingSanctumLevelSource[] Levels = Array.Empty<BuildingSanctumLevelSource>();
    }
}
