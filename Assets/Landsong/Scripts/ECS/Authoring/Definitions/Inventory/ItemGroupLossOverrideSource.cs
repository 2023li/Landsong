using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class ItemGroupLossOverrideSource
    {
        [LabelText("分组")]
        public ItemGroupDefinitionAsset Group;
        [LabelText("倍率")]
        public float Multiplier;
    }
}
