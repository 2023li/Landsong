using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class ItemLossOverrideSource
    {
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("倍率")]
        public float Multiplier;
    }
}
