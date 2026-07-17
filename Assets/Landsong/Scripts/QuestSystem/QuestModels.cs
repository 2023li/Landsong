using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong
{
    public enum QuestObjectiveType
    {
        BuildBuildings = 1,
        SubmitResources = 2
    }

    public enum QuestCategory
    {
        Mainline = 1,
        Random = 2
    }

    public enum QuestStatus
    {
        Active = 0,
        Completed = 1,
        Failed = 2,
        RewardClaimed = 4,
        Abandoned = 5
    }

    [Serializable]
    public sealed class GameQuestDefinition
    {
        [SerializeField, LabelText("任务ID")] private string questId;
        [SerializeField, LabelText("任务名称")] private string displayName;
        [SerializeField, LabelText("任务描述"), TextArea] private string description;
        [SerializeField, LabelText("任务机制")] private QuestCategory category = QuestCategory.Mainline;
        [SerializeField, LabelText("任务类型")] private QuestObjectiveType objectiveType = QuestObjectiveType.BuildBuildings;
        [SerializeField, LabelText("期限回合数"), Min(1)] private int turnLimit = 10;
        [SerializeField, LabelText("前置任务ID")] private string[] prerequisiteQuestIds = Array.Empty<string>();

        [SerializeField, LabelText("目标建筑")] private BuildingBase targetBuilding;
        [SerializeField, LabelText("目标建筑数量"), Min(1)] private int targetBuildingCount = 1;

        [SerializeField, LabelText("提交资源")] private ItemAmount[] requiredResources = Array.Empty<ItemAmount>();
        [SerializeField, LabelText("完成奖励")] private ItemAmount[] rewards = Array.Empty<ItemAmount>();
        [SerializeField, HideInInspector] private bool runtimeGenerated;

        public string QuestId => NormalizeQuestId(questId);
        public string DisplayName => string.IsNullOrWhiteSpace(displayName)
            ? (string.IsNullOrWhiteSpace(QuestId) ? "未命名任务" : QuestId)
            : displayName.Trim();
        public string Description => string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
        public QuestCategory Category => NormalizeQuestCategory(category);
        public QuestObjectiveType ObjectiveType => objectiveType;
        public int TurnLimit => Mathf.Max(1, turnLimit);
        public IReadOnlyList<string> PrerequisiteQuestIds => prerequisiteQuestIds ?? Array.Empty<string>();
        public BuildingBase TargetBuilding => targetBuilding;
        public string TargetBuildingId => targetBuilding == null || !targetBuilding.HasDefinition
            ? string.Empty
            : targetBuilding.FamilyId;
        public string TargetBuildingDisplayName => targetBuilding == null || !targetBuilding.HasDefinition
            ? string.Empty
            : targetBuilding.Definition.DisplayName;
        public Sprite TargetBuildingIcon => targetBuilding == null || !targetBuilding.HasDefinition
            ? null
            : targetBuilding.Definition.Icon;
        public int TargetBuildingCount => Mathf.Max(1, targetBuildingCount);
        public IReadOnlyList<ItemAmount> RequiredResources => requiredResources ?? Array.Empty<ItemAmount>();
        public IReadOnlyList<ItemAmount> Rewards => rewards ?? Array.Empty<ItemAmount>();
        public bool IsRuntimeGenerated => runtimeGenerated;
        public bool HasRewards => HasAnyReward();
        public bool IsValid => !string.IsNullOrWhiteSpace(QuestId) && HasValidObjective();

        public void Normalize()
        {
            questId = NormalizeQuestId(questId);
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            category = NormalizeQuestCategory(category);
            turnLimit = Mathf.Max(1, turnLimit);
            prerequisiteQuestIds = NormalizeQuestIds(prerequisiteQuestIds);
            targetBuildingCount = Mathf.Max(1, targetBuildingCount);

            if (requiredResources == null)
            {
                requiredResources = Array.Empty<ItemAmount>();
            }

            for (var i = 0; i < requiredResources.Length; i++)
            {
                requiredResources[i] = requiredResources[i].Normalized();
            }

            if (rewards == null)
            {
                rewards = Array.Empty<ItemAmount>();
            }

            for (var i = 0; i < rewards.Length; i++)
            {
                rewards[i] = rewards[i].Normalized();
            }
        }

        public static GameQuestDefinition CreateRuntimeSubmitResourcesQuest(
            string questId,
            string displayName,
            string description,
            QuestCategory category,
            int turnLimit,
            IEnumerable<ItemAmount> requiredResources,
            IEnumerable<ItemAmount> rewards)
        {
            var definition = new GameQuestDefinition
            {
                questId = NormalizeQuestId(questId),
                displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim(),
                description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim(),
                category = NormalizeQuestCategory(category),
                objectiveType = QuestObjectiveType.SubmitResources,
                turnLimit = Mathf.Max(1, turnLimit),
                requiredResources = CopyValidItemAmounts(requiredResources),
                rewards = CopyValidItemAmounts(rewards),
                runtimeGenerated = true
            };
            definition.Normalize();
            return definition;
        }

        public static string NormalizeQuestId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static string[] NormalizeQuestIds(IEnumerable<string> values)
        {
            if (values == null)
            {
                return Array.Empty<string>();
            }

            var result = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in values)
            {
                var questId = NormalizeQuestId(value);
                if (string.IsNullOrWhiteSpace(questId) || !seen.Add(questId))
                {
                    continue;
                }

                result.Add(questId);
            }

            return result.Count == 0 ? Array.Empty<string>() : result.ToArray();
        }

        public static QuestCategory NormalizeQuestCategory(QuestCategory value)
        {
            return value == QuestCategory.Random ? QuestCategory.Random : QuestCategory.Mainline;
        }

        private bool HasValidObjective()
        {
            return objectiveType switch
            {
                QuestObjectiveType.BuildBuildings => !string.IsNullOrWhiteSpace(TargetBuildingId) && TargetBuildingCount > 0,
                QuestObjectiveType.SubmitResources => HasAnyRequiredResource(),
                _ => false
            };
        }

        private bool HasAnyRequiredResource()
        {
            if (requiredResources == null)
            {
                return false;
            }

            for (var i = 0; i < requiredResources.Length; i++)
            {
                if (requiredResources[i].Normalized().IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnyReward()
        {
            if (rewards == null)
            {
                return false;
            }

            for (var i = 0; i < rewards.Length; i++)
            {
                if (rewards[i].Normalized().IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static ItemAmount[] CopyValidItemAmounts(IEnumerable<ItemAmount> source)
        {
            if (source == null)
            {
                return Array.Empty<ItemAmount>();
            }

            var result = new List<ItemAmount>();
            foreach (var item in source)
            {
                var normalized = item.Normalized();
                if (normalized.IsValid)
                {
                    result.Add(normalized);
                }
            }

            return result.ToArray();
        }
    }

    [Serializable]
    public sealed class GameQuestResourceProgress
    {
        public string ItemId { get; internal set; } = string.Empty;
        public ItemDefinition ItemDefinition { get; internal set; }
        public int RequiredAmount { get; internal set; }
        public int SubmittedAmount { get; internal set; }
        public int InventoryAmount { get; internal set; }
        public string DisplayName => ItemDefinition == null ? ItemId : ItemDefinition.DisplayName;
        public int RemainingAmount => Mathf.Max(0, RequiredAmount - SubmittedAmount);
        public bool IsComplete => RequiredAmount <= 0 || SubmittedAmount >= RequiredAmount;
    }

    [Serializable]
    public sealed class GameQuestState
    {
        private readonly List<GameQuestResourceProgress> resourceProgresses =
            new List<GameQuestResourceProgress>();

        public GameQuestState(GameQuestDefinition definition, int startedTurn)
            : this(definition, startedTurn, definition == null ? QuestCategory.Mainline : definition.Category)
        {
        }

        public GameQuestState(GameQuestDefinition definition, int startedTurn, QuestCategory category)
        {
            Definition = definition;
            QuestId = definition == null ? string.Empty : definition.QuestId;
            Category = GameQuestDefinition.NormalizeQuestCategory(category);
            StartedTurn = Mathf.Max(1, startedTurn);
            DeadlineTurn = StartedTurn + Mathf.Max(1, definition == null ? 1 : definition.TurnLimit) - 1;
            Status = QuestStatus.Active;
        }

        public GameQuestDefinition Definition { get; }
        public string QuestId { get; }
        public QuestCategory Category { get; internal set; }
        public QuestStatus Status { get; internal set; }
        public int StartedTurn { get; internal set; }
        public int DeadlineTurn { get; internal set; }
        public int CurrentAmount { get; internal set; }
        public int TargetAmount { get; internal set; }
        public string TargetDisplayName { get; internal set; } = string.Empty;
        public Sprite Icon { get; internal set; }
        public IReadOnlyList<GameQuestResourceProgress> ResourceProgresses => resourceProgresses;
        public bool IsActive => Status == QuestStatus.Active;
        public bool IsCompleted => Status == QuestStatus.Completed;
        public bool IsFailed => Status == QuestStatus.Failed;
        public bool IsRewardClaimed => Status == QuestStatus.RewardClaimed;
        public bool IsAbandoned => Status == QuestStatus.Abandoned;
        public bool CanClaimRewards => IsCompleted;
        public bool IsMainline => Category == QuestCategory.Mainline;
        public bool IsRandom => Category == QuestCategory.Random;
        public string CategoryDisplayName => IsRandom ? "随机" : "主线";
        public bool IsResourceSubmission => Definition != null && Definition.ObjectiveType == QuestObjectiveType.SubmitResources;
        public int TotalRequiredAmount => TargetAmount;
        public int TotalSubmittedAmount => CurrentAmount;
        public float Progress01 => TargetAmount <= 0 ? 1f : Mathf.Clamp01(CurrentAmount / (float)TargetAmount);

        public int GetRemainingTurns(int currentTurn)
        {
            if (StartedTurn <= 0 || DeadlineTurn <= 0)
            {
                return 0;
            }

            return Mathf.Max(0, DeadlineTurn - Mathf.Max(1, currentTurn) + 1);
        }

        public bool CanSubmitResources()
        {
            if (!IsActive || !IsResourceSubmission)
            {
                return false;
            }

            for (var i = 0; i < resourceProgresses.Count; i++)
            {
                var progress = resourceProgresses[i];
                if (progress != null && progress.RemainingAmount > 0 && progress.InventoryAmount > 0)
                {
                    return true;
                }
            }

            return false;
        }

        internal void ClearResourceProgresses()
        {
            resourceProgresses.Clear();
        }

        internal void AddResourceProgress(GameQuestResourceProgress progress)
        {
            if (progress != null)
            {
                resourceProgresses.Add(progress);
            }
        }
    }

    [Serializable]
    public sealed class QuestSaveData
    {
        public List<QuestStateSaveData> Quests = new List<QuestStateSaveData>();
        public int NextRandomQuestRefreshTurn = 1;
        public int GeneratedRandomQuestSerial;

        public void Validate()
        {
            Quests ??= new List<QuestStateSaveData>();
            NextRandomQuestRefreshTurn = Mathf.Max(1, NextRandomQuestRefreshTurn);
            GeneratedRandomQuestSerial = Mathf.Max(0, GeneratedRandomQuestSerial);
            var seenQuestIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = Quests.Count - 1; i >= 0; i--)
            {
                var quest = Quests[i];
                if (quest == null)
                {
                    Quests.RemoveAt(i);
                    continue;
                }

                quest.Validate();
                if (string.IsNullOrWhiteSpace(quest.QuestId) || !seenQuestIds.Add(quest.QuestId))
                {
                    Quests.RemoveAt(i);
                }
            }
        }
    }

    [Serializable]
    public sealed class QuestStateSaveData
    {
        public string QuestId = string.Empty;
        public QuestCategory Category = QuestCategory.Mainline;
        public QuestStatus Status = QuestStatus.Active;
        public int StartedTurn = 1;
        public int DeadlineTurn = 1;
        public List<QuestResourceSubmissionSaveData> SubmittedResources =
            new List<QuestResourceSubmissionSaveData>();
        public QuestGeneratedDefinitionSaveData GeneratedDefinition;

        public void Validate()
        {
            QuestId = GameQuestDefinition.NormalizeQuestId(QuestId);
            Category = GameQuestDefinition.NormalizeQuestCategory(Category);
            Status = NormalizeQuestStatus(Status);
            StartedTurn = Mathf.Max(1, StartedTurn);
            DeadlineTurn = Mathf.Max(StartedTurn, DeadlineTurn);
            SubmittedResources ??= new List<QuestResourceSubmissionSaveData>();
            GeneratedDefinition?.Validate();

            var seenItemIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = SubmittedResources.Count - 1; i >= 0; i--)
            {
                var resource = SubmittedResources[i];
                if (resource == null)
                {
                    SubmittedResources.RemoveAt(i);
                    continue;
                }

                resource.Validate();
                if (string.IsNullOrWhiteSpace(resource.ItemId) || !seenItemIds.Add(resource.ItemId))
                {
                    SubmittedResources.RemoveAt(i);
                }
            }
        }

        private static QuestStatus NormalizeQuestStatus(QuestStatus value)
        {
            return value switch
            {
                QuestStatus.Active => QuestStatus.Active,
                QuestStatus.Completed => QuestStatus.Completed,
                QuestStatus.Failed => QuestStatus.Failed,
                QuestStatus.RewardClaimed => QuestStatus.RewardClaimed,
                QuestStatus.Abandoned => QuestStatus.Abandoned,
                _ => QuestStatus.Active
            };
        }
    }

    [Serializable]
    public sealed class QuestResourceSubmissionSaveData
    {
        public string ItemId = string.Empty;
        public int SubmittedAmount;

        public void Validate()
        {
            ItemId = string.IsNullOrWhiteSpace(ItemId) ? string.Empty : ItemId.Trim();
            SubmittedAmount = Mathf.Max(0, SubmittedAmount);
        }
    }

    [Serializable]
    public sealed class QuestGeneratedDefinitionSaveData
    {
        public string QuestId = string.Empty;
        public QuestCategory Category = QuestCategory.Random;
        public string DisplayName = string.Empty;
        public string Description = string.Empty;
        public int TurnLimit = 1;
        public List<QuestItemAmountSaveData> RequiredResources = new List<QuestItemAmountSaveData>();
        public List<QuestItemAmountSaveData> Rewards = new List<QuestItemAmountSaveData>();

        public bool IsValid => !string.IsNullOrWhiteSpace(QuestId) && HasValidRequiredResource();

        public void Validate()
        {
            QuestId = GameQuestDefinition.NormalizeQuestId(QuestId);
            Category = GameQuestDefinition.NormalizeQuestCategory(Category);
            DisplayName = string.IsNullOrWhiteSpace(DisplayName) ? string.Empty : DisplayName.Trim();
            Description = string.IsNullOrWhiteSpace(Description) ? string.Empty : Description.Trim();
            TurnLimit = Mathf.Max(1, TurnLimit);
            RequiredResources = NormalizeItemAmounts(RequiredResources);
            Rewards = NormalizeItemAmounts(Rewards);
        }

        public static QuestGeneratedDefinitionSaveData FromDefinition(GameQuestDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            var data = new QuestGeneratedDefinitionSaveData
            {
                QuestId = definition.QuestId,
                Category = definition.Category,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                TurnLimit = definition.TurnLimit,
                RequiredResources = QuestItemAmountSaveData.FromItemAmounts(definition.RequiredResources),
                Rewards = QuestItemAmountSaveData.FromItemAmounts(definition.Rewards)
            };
            data.Validate();
            return data.IsValid ? data : null;
        }

        private bool HasValidRequiredResource()
        {
            if (RequiredResources == null)
            {
                return false;
            }

            for (var i = 0; i < RequiredResources.Count; i++)
            {
                if (RequiredResources[i] != null && RequiredResources[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<QuestItemAmountSaveData> NormalizeItemAmounts(
            List<QuestItemAmountSaveData> source)
        {
            var result = new List<QuestItemAmountSaveData>();
            if (source == null)
            {
                return result;
            }

            var amountsByItemId = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < source.Count; i++)
            {
                var item = source[i];
                if (item == null)
                {
                    continue;
                }

                item.Validate();
                if (!item.IsValid)
                {
                    continue;
                }

                if (!amountsByItemId.ContainsKey(item.ItemId))
                {
                    amountsByItemId.Add(item.ItemId, 0);
                }

                amountsByItemId[item.ItemId] += item.Amount;
            }

            foreach (var pair in amountsByItemId)
            {
                result.Add(new QuestItemAmountSaveData
                {
                    ItemId = pair.Key,
                    Amount = Mathf.Max(0, pair.Value)
                });
            }

            return result;
        }
    }

    [Serializable]
    public sealed class QuestItemAmountSaveData
    {
        public string ItemId = string.Empty;
        public int Amount;

        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && Amount > 0;

        public void Validate()
        {
            ItemId = string.IsNullOrWhiteSpace(ItemId) ? string.Empty : ItemId.Trim();
            Amount = Mathf.Max(0, Amount);
        }

        public static List<QuestItemAmountSaveData> FromItemAmounts(IReadOnlyList<ItemAmount> itemAmounts)
        {
            var result = new List<QuestItemAmountSaveData>();
            if (itemAmounts == null)
            {
                return result;
            }

            for (var i = 0; i < itemAmounts.Count; i++)
            {
                var item = itemAmounts[i].Normalized();
                if (!item.IsValid)
                {
                    continue;
                }

                result.Add(
                    new QuestItemAmountSaveData
                    {
                        ItemId = item.ItemId,
                        Amount = item.Amount
                    });
            }

            return result;
        }
    }

    [Serializable]
    public sealed class QuestItemAmountRange
    {
        [SerializeField, LabelText("物品")] private ItemDefinition itemDefinition;
        [SerializeField, LabelText("最小数量"), Min(1)] private int minAmount = 1;
        [SerializeField, LabelText("最大数量"), Min(1)] private int maxAmount = 1;

        public ItemDefinition ItemDefinition => itemDefinition;
        public int MinAmount => Mathf.Max(1, minAmount);
        public int MaxAmount => Mathf.Max(MinAmount, maxAmount);
        public bool IsValid => itemDefinition != null
                               && !string.IsNullOrWhiteSpace(itemDefinition.ItemId)
                               && itemDefinition.BaseValue > 0
                               && MaxAmount > 0;

        public void Normalize()
        {
            minAmount = Mathf.Max(1, minAmount);
            maxAmount = Mathf.Max(minAmount, maxAmount);
        }

        public ItemAmount Roll()
        {
            Normalize();
            if (!IsValid)
            {
                return default;
            }

            var amount = MinAmount == MaxAmount
                ? MinAmount
                : UnityEngine.Random.Range(MinAmount, MaxAmount + 1);
            return new ItemAmount(itemDefinition, amount);
        }
    }

    [Serializable]
    public sealed class RandomExchangeQuestRule
    {
        [SerializeField, LabelText("启用")] private bool enabled = true;
        [SerializeField, LabelText("任务ID前缀")] private string questIdPrefix = "random_exchange";
        [SerializeField, LabelText("请求者名称")] private string[] requesterNames = { "遥远的帝国" };
        [SerializeField, LabelText("任务名称格式")] private string displayNameFormat = "{Requester}的交换请求";
        [SerializeField, LabelText("任务描述格式"), TextArea]
        private string descriptionFormat = "{Requester}派来一些使者，希望使用{RewardList}和你交换{RequiredList}。";
        [SerializeField, LabelText("期限回合数"), Min(1)] private int turnLimit = 5;
        [SerializeField, LabelText("需求种类数"), Min(1)] private int requiredLineCount = 1;
        [SerializeField, LabelText("需求候选")] private QuestItemAmountRange[] requiredResourceCandidates =
            Array.Empty<QuestItemAmountRange>();
        [SerializeField, LabelText("奖励种类数"), Min(1)] private int rewardLineCount = 1;
        [SerializeField, LabelText("奖励物品候选")] private ItemDefinition[] rewardCandidates =
            Array.Empty<ItemDefinition>();
        [SerializeField, LabelText("最低回报倍率"), Min(0.01f)] private float minRewardValueMultiplier = 1.5f;
        [SerializeField, LabelText("最高回报倍率"), Min(0.01f)] private float maxRewardValueMultiplier = 3f;
        [SerializeField, LabelText("高倍率稀有度"), Min(1f)] private float rewardMultiplierRarity = 2.25f;

        public bool Enabled => enabled;
        public string QuestIdPrefix => string.IsNullOrWhiteSpace(questIdPrefix)
            ? "random_exchange"
            : questIdPrefix.Trim();
        public bool IsValid => enabled
                               && HasValidCandidate(requiredResourceCandidates)
                               && HasValidRewardCandidate(rewardCandidates);

        public void Normalize()
        {
            questIdPrefix = string.IsNullOrWhiteSpace(questIdPrefix) ? "random_exchange" : questIdPrefix.Trim();
            displayNameFormat = string.IsNullOrWhiteSpace(displayNameFormat)
                ? "{Requester}的交换请求"
                : displayNameFormat.Trim();
            descriptionFormat = string.IsNullOrWhiteSpace(descriptionFormat)
                ? "{Requester}派来一些使者，希望使用{RewardList}和你交换{RequiredList}。"
                : descriptionFormat.Trim();
            turnLimit = Mathf.Max(1, turnLimit);
            requiredLineCount = Mathf.Max(1, requiredLineCount);
            rewardLineCount = Mathf.Max(1, rewardLineCount);
            minRewardValueMultiplier = Mathf.Max(0.01f, minRewardValueMultiplier);
            maxRewardValueMultiplier = Mathf.Max(minRewardValueMultiplier, maxRewardValueMultiplier);
            rewardMultiplierRarity = Mathf.Max(1f, rewardMultiplierRarity);
            NormalizeCandidates(requiredResourceCandidates);

            if (requesterNames == null || requesterNames.Length == 0)
            {
                requesterNames = new[] { "遥远的帝国" };
                return;
            }

            for (var i = 0; i < requesterNames.Length; i++)
            {
                requesterNames[i] = string.IsNullOrWhiteSpace(requesterNames[i])
                    ? string.Empty
                    : requesterNames[i].Trim();
            }
        }

        public GameQuestDefinition CreateDefinition(string questId)
        {
            Normalize();
            if (!IsValid)
            {
                return null;
            }

            var requester = PickRequesterName();
            var requiredResources = RollItemAmounts(requiredResourceCandidates, requiredLineCount);
            var requiredValue = CalculateItemAmountsBaseValue(requiredResources);
            var rewards = RollRewardItemAmounts(requiredValue);
            if (requiredResources.Count == 0 || rewards.Count == 0)
            {
                return null;
            }

            var requiredList = FormatPlainItemAmountList(requiredResources);
            var rewardList = FormatPlainItemAmountList(rewards);
            var displayName = ApplyTextFormat(displayNameFormat, requester, requiredList, rewardList);
            var description = ApplyTextFormat(descriptionFormat, requester, requiredList, rewardList);

            return GameQuestDefinition.CreateRuntimeSubmitResourcesQuest(
                questId,
                displayName,
                description,
                QuestCategory.Random,
                turnLimit,
                requiredResources,
                rewards);
        }

        private string PickRequesterName()
        {
            if (requesterNames == null || requesterNames.Length == 0)
            {
                return "遥远的帝国";
            }

            var candidates = new List<string>();
            for (var i = 0; i < requesterNames.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(requesterNames[i]))
                {
                    candidates.Add(requesterNames[i].Trim());
                }
            }

            return candidates.Count == 0
                ? "遥远的帝国"
                : candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        private static List<ItemAmount> RollItemAmounts(QuestItemAmountRange[] candidates, int lineCount)
        {
            var result = new List<ItemAmount>();
            if (candidates == null || candidates.Length == 0)
            {
                return result;
            }

            var available = new List<QuestItemAmountRange>();
            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                candidate?.Normalize();
                if (candidate != null && candidate.IsValid)
                {
                    available.Add(candidate);
                }
            }

            var targetCount = Mathf.Clamp(lineCount, 1, available.Count);
            while (result.Count < targetCount && available.Count > 0)
            {
                var index = UnityEngine.Random.Range(0, available.Count);
                var itemAmount = available[index].Roll();
                available.RemoveAt(index);
                if (itemAmount.IsValid)
                {
                    result.Add(itemAmount);
                }
            }

            return result;
        }

        private static string ApplyTextFormat(
            string format,
            string requester,
            string requiredList,
            string rewardList)
        {
            return (string.IsNullOrWhiteSpace(format) ? string.Empty : format)
                .Replace("{Requester}", requester ?? string.Empty)
                .Replace("{RequiredList}", requiredList ?? string.Empty)
                .Replace("{RewardList}", rewardList ?? string.Empty);
        }

        private static string FormatPlainItemAmountList(IReadOnlyList<ItemAmount> items)
        {
            if (items == null || items.Count == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i].Normalized();
                if (!item.IsValid)
                {
                    continue;
                }

                var displayName = item.ItemDefinition == null
                    ? item.ItemId
                    : item.ItemDefinition.DisplayName;
                parts.Add($"{item.Amount}{displayName}");
            }

            return string.Join("、", parts);
        }

        private List<ItemAmount> RollRewardItemAmounts(int requiredValue)
        {
            var result = new List<ItemAmount>();
            if (requiredValue <= 0 || rewardCandidates == null || rewardCandidates.Length == 0)
            {
                return result;
            }

            var candidates = new List<ItemDefinition>();
            for (var i = 0; i < rewardCandidates.Length; i++)
            {
                var candidate = rewardCandidates[i];
                if (candidate != null
                    && !string.IsNullOrWhiteSpace(candidate.ItemId)
                    && candidate.BaseValue > 0)
                {
                    candidates.Add(candidate);
                }
            }

            var targetCount = Mathf.Clamp(rewardLineCount, 1, candidates.Count);
            if (targetCount <= 0)
            {
                return result;
            }

            var multiplier = RollRewardValueMultiplier();
            var targetRewardValue = Mathf.Max(1, Mathf.RoundToInt(requiredValue * multiplier));
            var selectedRewards = PickRewardDefinitions(candidates, targetCount);
            if (selectedRewards.Count == 0)
            {
                return result;
            }

            var remainingValue = targetRewardValue;
            for (var i = 0; i < selectedRewards.Count; i++)
            {
                var reward = selectedRewards[i];
                if (reward == null || reward.BaseValue <= 0)
                {
                    continue;
                }

                var lineValue = i == selectedRewards.Count - 1
                    ? remainingValue
                    : Mathf.Max(1, Mathf.RoundToInt(targetRewardValue / (float)selectedRewards.Count));
                var amount = Mathf.Max(1, Mathf.CeilToInt(lineValue / (float)reward.BaseValue));
                result.Add(new ItemAmount(reward, amount));
                remainingValue = Mathf.Max(1, remainingValue - amount * reward.BaseValue);
            }

            return result;
        }

        private float RollRewardValueMultiplier()
        {
            var t = Mathf.Pow(UnityEngine.Random.value, rewardMultiplierRarity);
            return Mathf.Lerp(minRewardValueMultiplier, maxRewardValueMultiplier, t);
        }

        private static List<ItemDefinition> PickRewardDefinitions(List<ItemDefinition> candidates, int count)
        {
            var result = new List<ItemDefinition>();
            if (candidates == null || candidates.Count == 0)
            {
                return result;
            }

            var available = new List<ItemDefinition>(candidates);
            var targetCount = Mathf.Clamp(count, 1, available.Count);
            while (result.Count < targetCount && available.Count > 0)
            {
                var index = UnityEngine.Random.Range(0, available.Count);
                result.Add(available[index]);
                available.RemoveAt(index);
            }

            return result;
        }

        private static int CalculateItemAmountsBaseValue(IReadOnlyList<ItemAmount> items)
        {
            if (items == null)
            {
                return 0;
            }

            var total = 0;
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i].Normalized();
                if (!item.IsValid || item.ItemDefinition == null || item.ItemDefinition.BaseValue <= 0)
                {
                    continue;
                }

                total += item.Amount * item.ItemDefinition.BaseValue;
            }

            return Mathf.Max(0, total);
        }

        private static bool HasValidCandidate(QuestItemAmountRange[] candidates)
        {
            if (candidates == null)
            {
                return false;
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                candidates[i]?.Normalize();
                if (candidates[i] != null && candidates[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasValidRewardCandidate(ItemDefinition[] candidates)
        {
            if (candidates == null)
            {
                return false;
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                var candidate = candidates[i];
                if (candidate != null
                    && !string.IsNullOrWhiteSpace(candidate.ItemId)
                    && candidate.BaseValue > 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void NormalizeCandidates(QuestItemAmountRange[] candidates)
        {
            if (candidates == null)
            {
                return;
            }

            for (var i = 0; i < candidates.Length; i++)
            {
                candidates[i]?.Normalize();
            }
        }
    }
}
