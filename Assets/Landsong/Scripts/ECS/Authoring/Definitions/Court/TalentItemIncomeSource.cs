using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentItemIncomeSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("基础数量")]
        public int BaseQuantity;
        [LabelText("每级增加量")]
        public float PerLevel;
    }
}
