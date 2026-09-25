using System;
using System.Collections.Generic;
using System.Linq;
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
        [SerializeReference, LabelText("任务要求"), ListDrawerSettings(ShowIndexLabels = true)]
        public List<QuestObjectiveSource> Requirements = new List<QuestObjectiveSource>();
        [SerializeReference, LabelText("任务奖励"), ListDrawerSettings(ShowIndexLabels = true)]
        public List<QuestRewardSource> RewardEntries = new List<QuestRewardSource>();
        [LabelText("后续任务")]
        public QuestDefinitionAsset NextQuest;
        [LabelText("随机任务刷新条件"), ShowIf(nameof(IsRandomQuest))]
        public DefinitionPrerequisitesSource RefreshPrerequisites = new DefinitionPrerequisitesSource();
        [LabelText("最少刷新间隔（回合）"), ShowIf(nameof(IsRandomQuest))]
        public int MinimumRefreshTurns;
        [LabelText("最多刷新间隔（回合）"), ShowIf(nameof(IsRandomQuest))]
        public int MaximumRefreshTurns;
        [LabelText("失败惩罚")]
        public ItemAmountSource[] FailurePenalties = Array.Empty<ItemAmountSource>();

        bool IsRandomQuest => (Behavior & QuestBehaviorFlags.Mainline) == 0;
        [HideInInspector]
        public QuestObjectivesSource Objectives => new QuestObjectivesSource { Requirements = Requirements };
        [HideInInspector]
        public DefinitionRewardsSource Rewards => new DefinitionRewardsSource
        {
            Items = RewardEntries?.OfType<QuestItemRewardSource>().Select(row => new ItemAmountSource { Order = row.Order, Item = row.Item, Quantity = row.Quantity }).ToArray(),
            Blueprints = RewardEntries?.OfType<QuestBlueprintRewardSource>().Select(row => new BlueprintRewardSource { Order = row.Order, Building = row.Building, GrantedLevel = row.GrantedLevel }).ToArray(),
            Buffs = RewardEntries?.OfType<QuestBuffRewardSource>().Select(row => new BuffRewardSource { Order = row.Order, Buff = row.Buff, GrantedLevel = row.GrantedLevel }).ToArray(),
            Features = RewardEntries?.OfType<QuestFeatureRewardSource>().Select(row => new FeatureRewardSource { Order = row.Order, Feature = row.Feature, GrantedLevel = row.GrantedLevel }).ToArray()
        };

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Requirements == null)
                return;
            foreach (var requirement in Requirements)
                if (requirement != null && string.IsNullOrWhiteSpace(requirement.Key))
                    requirement.Key = Guid.NewGuid().ToString("N");
        }
#endif
    }

    [Serializable]
    public abstract class QuestRewardSource
    {
        [LabelText("执行顺序")]
        public int Order;
    }

    [Serializable]
    public sealed class QuestItemRewardSource : QuestRewardSource
    {
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        public int Quantity = 1;
    }

    [Serializable]
    public sealed class QuestBlueprintRewardSource : QuestRewardSource
    {
        [LabelText("建筑蓝图")]
        public BuildingDefinitionAsset Building;
        [LabelText("授予等级")]
        public int GrantedLevel = 1;
    }

    [Serializable]
    public sealed class QuestBuffRewardSource : QuestRewardSource
    {
        [LabelText("增益")]
        public BuffDefinitionAsset Buff;
        [LabelText("授予等级")]
        public int GrantedLevel = 1;
    }

    [Serializable]
    public sealed class QuestFeatureRewardSource : QuestRewardSource
    {
        [LabelText("功能许可")]
        public FeatureDefinitionAsset Feature;
        [LabelText("授予等级")]
        public int GrantedLevel = 1;
    }
}
