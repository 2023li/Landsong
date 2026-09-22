using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentPeriodicIncomeSource
    {
        [LabelText("物品")]
        public TalentItemIncomeSource[] Items = Array.Empty<TalentItemIncomeSource>();
        [LabelText("按条件缩放的物品收益")]
        public TalentScaledItemIncomeSource[] ScaledItems = Array.Empty<TalentScaledItemIncomeSource>();
        [LabelText("科研点收益")]
        public TalentResearchIncomeSource[] ResearchPoints = Array.Empty<TalentResearchIncomeSource>();
        [LabelText("建筑蓝图")]
        public TalentBlueprintIncomeSource[] Blueprints = Array.Empty<TalentBlueprintIncomeSource>();
        [LabelText("增益")]
        public TalentBuffIncomeSource[] Buffs = Array.Empty<TalentBuffIncomeSource>();
        [LabelText("功能许可")]
        public TalentFeatureIncomeSource[] Features = Array.Empty<TalentFeatureIncomeSource>();
    }
}
