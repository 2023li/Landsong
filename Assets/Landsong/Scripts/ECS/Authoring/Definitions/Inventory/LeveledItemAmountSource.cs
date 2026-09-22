using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class LeveledItemAmountSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("适用等级")]
        public int Level;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        public int Quantity = 1;
    }
}
