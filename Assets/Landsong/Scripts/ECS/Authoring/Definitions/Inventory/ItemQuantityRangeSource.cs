using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class ItemQuantityRangeSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("最少数量")]
        public int MinimumQuantity = 1;
        [LabelText("最多数量")]
        public int MaximumQuantity;
    }
}
