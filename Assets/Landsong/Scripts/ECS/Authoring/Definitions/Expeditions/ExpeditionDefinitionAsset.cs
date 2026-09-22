using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Expeditions/Expedition")]
    public sealed class ExpeditionDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("最低驻地等级")]
        public int MinimumSiteLevel = 1;
        [LabelText("最少出征人数")]
        public int MinimumCrew = 1;
        [LabelText("最多出征人数")]
        public int MaximumCrew;
        [LabelText("行程回合")]
        public int TravelTurns = 1;
        [LabelText("基础成功率")]
        public float BaseSuccessChance = 1;
        [LabelText("每人成功率加成")]
        public float SuccessChancePerCrew;
        [LabelText("成功率上限")]
        public float MaximumSuccessChance = 1;
        [LabelText("失败伤亡比例")]
        public float FailureCasualtyRatio;
        [LabelText("基础抚恤费")]
        public int BaseCompensation;
        [LabelText("每人抚恤费")]
        public int CompensationPerCrew;
        [LabelText("可重复完成")]
        public bool Repeatable;
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Prerequisites = new DefinitionPrerequisitesSource();
        [LabelText("显示前置")]
        public DefinitionPrerequisitesSource Visibility = new DefinitionPrerequisitesSource();
        [LabelText("远征补给")]
        public ExpeditionSupplySource[] Supplies = Array.Empty<ExpeditionSupplySource>();
        [LabelText("完成奖励")]
        public DefinitionRewardsSource Rewards = new DefinitionRewardsSource();
        [LabelText("失败惩罚")]
        public ItemAmountSource[] FailurePenalties = Array.Empty<ItemAmountSource>();
    }
}
