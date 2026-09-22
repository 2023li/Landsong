using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentSocialTaskSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        public int Quantity;
        [LabelText("好感奖励")]
        public int AffectionReward;
    }
}
