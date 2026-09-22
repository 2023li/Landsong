using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestSubmittedItemObjectiveSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("目标稳定标识")]
        public string Key = "";
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        public int Quantity;
    }
}
