using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class ExpeditionSupplySource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("最少数量")]
        public int MinimumQuantity;
        [LabelText("额外数量上限")]
        public int ExtraLimit;
        [LabelText("每份补给成功率加成")]
        public float SuccessPerExtra;
        [LabelText("每份补给奖励加成")]
        public float RewardPerExtra;
    }
}
