using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class DefinitionRewardsSource
    {
        [LabelText("物品")]
        public ItemAmountSource[] Items = Array.Empty<ItemAmountSource>();
        [LabelText("建筑蓝图")]
        public BlueprintRewardSource[] Blueprints = Array.Empty<BlueprintRewardSource>();
        [LabelText("增益")]
        public BuffRewardSource[] Buffs = Array.Empty<BuffRewardSource>();
        [LabelText("功能许可")]
        public FeatureRewardSource[] Features = Array.Empty<FeatureRewardSource>();
    }
}
