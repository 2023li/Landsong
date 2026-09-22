using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Quests/Quest")]
    public sealed class QuestDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("任务时限")]
        public int DeadlineTurns;
        [LabelText("行为标记")]
        public QuestBehaviorFlags Behavior;
        [LabelText("邀约类型")]
        public QuestOfferType OfferType;
        [LabelText("任务强度")]
        public int Intensity;
        [LabelText("抽取权重")]
        public float OfferWeight = 100;
        [LabelText("物品数量倍率")]
        public float ItemQuantityScale = 1;
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Prerequisites = new DefinitionPrerequisitesSource();
        [LabelText("任务目标")]
        public QuestObjectivesSource Objectives = new QuestObjectivesSource();
        [LabelText("完成奖励")]
        public DefinitionRewardsSource Rewards = new DefinitionRewardsSource();
        [LabelText("失败惩罚")]
        public ItemAmountSource[] FailurePenalties = Array.Empty<ItemAmountSource>();
    }
}
