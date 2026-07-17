using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.CameraSystem;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Landsong.TechnologySystem;
using Landsong.TurnSystem;
using UnityEngine;

namespace Landsong
{
    /// <summary>
    /// 任务领域服务。拥有任务运行时状态、完成判定、随机任务、订阅和存档逻辑。
    /// GameSystem 只保留序列化配置和服务组装。
    /// </summary>
    public sealed class QuestService : IDisposable
    {
        private const string GameplayDebugGoldItemId = "金币";

        private readonly GameSystem context;
        private readonly List<GameQuestState> quests = new List<GameQuestState>();
        private readonly HashSet<BuildingBase> subscribedQuestBuildingStates = new HashSet<BuildingBase>();

        private GameQuestDefinition[] startingQuests = Array.Empty<GameQuestDefinition>();
        private GameQuestDefinition[] randomQuestPool = Array.Empty<GameQuestDefinition>();
        private RandomExchangeQuestRule[] runtimeExchangeQuestRules = Array.Empty<RandomExchangeQuestRule>();
        private int startingRandomQuestCount;
        private int maxActiveRandomQuests;
        private int randomQuestRefreshIntervalTurns = 1;

        private InventoryService subscribedQuestInventory;
        private BuildingService subscribedQuestBuildings;
        private TurnService subscribedQuestTurn;
        private TechnologyService subscribedQuestTechnology;
        private bool subscribedQuestCameraInput;
        private bool questsInitialized;
        private bool suppressQuestInventoryRefresh;
        private bool suppressQuestRuntimeMessages;
        private int nextRandomQuestRefreshTurn = 1;
        private int generatedRandomQuestSerial;

        internal QuestService(GameSystem context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public event Action<QuestService> StateChanged;
        public event Action<QuestService, GameQuestState> QuestRequested;

        public IReadOnlyList<GameQuestState> Quests => quests;
        private InventoryService Inventory => context.Services.Inventory;
        private BuildingService Buildings => context.Services.Buildings;
        private TurnService Turn => context.Services.Turn;
        private TechnologyService Technology => context.Services.Technology;
        private GameFeatureUnlockService Features => context.Services.Features;
        private GameEventService Events => context.Services.Events;
        private ItemCatalog itemCatalog => context.QuestItemCatalog;
        private int CurrentTurn => Turn == null ? 1 : Turn.CurrentTurn;

        internal void Configure(
            QuestCatalog catalog,
            IReadOnlyList<string> startingQuestIds,
            int newStartingRandomQuestCount,
            int newMaxActiveRandomQuests,
            int newRandomQuestRefreshIntervalTurns)
        {
            startingQuests = catalog == null
                ? Array.Empty<GameQuestDefinition>()
                : catalog.Resolve(startingQuestIds);
            randomQuestPool = catalog == null
                ? Array.Empty<GameQuestDefinition>()
                : catalog.GetDefinitions(QuestCategory.Random);
            runtimeExchangeQuestRules = catalog == null
                ? Array.Empty<RandomExchangeQuestRule>()
                : catalog.CopyRuntimeExchangeQuestRules();
            startingRandomQuestCount = Mathf.Max(0, newStartingRandomQuestCount);
            maxActiveRandomQuests = Mathf.Max(0, newMaxActiveRandomQuests);
            randomQuestRefreshIntervalTurns = Mathf.Max(1, newRandomQuestRefreshIntervalTurns);
        }

        public void Dispose()
        {
            UnsubscribeRuntimeServices();
            questsInitialized = false;
        }

        public bool TryAddDebugRandomQuest(out GameQuestState quest)
        {
            quest = null;
            if (!questsInitialized)
            {
                Initialize();
            }

            if (maxActiveRandomQuests <= 0
                || CountActiveRandomQuests() >= maxActiveRandomQuests)
            {
                return false;
            }

            var usedQuestIds = BuildCurrentQuestIdSet();
            var definition = PickRandomQuestDefinition(usedQuestIds)
                             ?? CreateGameplayDebugRandomQuestDefinition(usedQuestIds);
            if (definition == null
                || !TryAddQuestDefinition(
                    definition,
                    QuestCategory.Random,
                    null,
                    usedQuestIds,
                    true,
                    out quest))
            {
                return false;
            }

            NotifyChanged();
            return quest != null;
        }

        private GameQuestDefinition CreateGameplayDebugRandomQuestDefinition(HashSet<string> usedQuestIds)
        {
            var catalog = Inventory == null ? itemCatalog : Inventory.ItemCatalog;
            var definitions = catalog == null ? null : catalog.Definitions;
            if (definitions == null || definitions.Count == 0)
            {
                return null;
            }

            var candidates = new List<ItemDefinition>();
            for (var i = 0; i < definitions.Count; i++)
            {
                var item = definitions[i];
                if (item != null && !string.IsNullOrWhiteSpace(item.ItemId))
                {
                    candidates.Add(item);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            var requiredItem = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var rewardItem = FindItemDefinition(GameplayDebugGoldItemId)
                             ?? candidates[UnityEngine.Random.Range(0, candidates.Count)];
            var requiredAmount = UnityEngine.Random.Range(10, 101);
            var rewardAmount = Mathf.Max(1, requiredAmount * 2);
            var questId = CreateRuntimeRandomQuestId("debug_random", usedQuestIds);
            return GameQuestDefinition.CreateRuntimeSubmitResourcesQuest(
                questId,
                $"调试随机委托：交付{requiredItem.DisplayName}",
                $"交付 {requiredItem.DisplayName} x{requiredAmount}，获得 {rewardItem.DisplayName} x{rewardAmount}。",
                QuestCategory.Random,
                8,
                new[] { new ItemAmount(requiredItem, requiredAmount) },
                new[] { new ItemAmount(rewardItem, rewardAmount) },
                "debug_delivery",
                string.Empty,
                string.Empty,
                "调试随机委托：交付{RequiredList}",
                "交付 {RequiredList}，获得 {RewardList}。");
        }

        public QuestSaveData CaptureSaveData()
        {
            var saveData = new QuestSaveData
            {
                Quests = new List<QuestStateSaveData>(),
                NextRandomQuestRefreshTurn = Mathf.Max(1, nextRandomQuestRefreshTurn),
                GeneratedRandomQuestSerial = Mathf.Max(0, generatedRandomQuestSerial)
            };

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    continue;
                }

                var questData = new QuestStateSaveData
                {
                    QuestId = quest.QuestId,
                    Category = quest.Category,
                    Status = quest.Status,
                    StartedTurn = quest.StartedTurn,
                    DeadlineTurn = quest.IsTimed
                        ? Mathf.Max(quest.StartedTurn, quest.DeadlineTurn)
                        : 0,
                    SubmittedResources = new List<QuestResourceSubmissionSaveData>(),
                    GeneratedDefinition = quest.Definition.IsRuntimeGenerated
                        ? QuestGeneratedDefinitionSaveData.FromDefinition(quest.Definition)
                        : null
                };

                var resourceProgresses = quest.ResourceProgresses;
                for (var j = 0; j < resourceProgresses.Count; j++)
                {
                    var progress = resourceProgresses[j];
                    if (progress == null
                        || string.IsNullOrWhiteSpace(progress.ItemId)
                        || progress.SubmittedAmount <= 0)
                    {
                        continue;
                    }

                    questData.SubmittedResources.Add(
                        new QuestResourceSubmissionSaveData
                        {
                            ItemId = progress.ItemId,
                            SubmittedAmount = Mathf.Max(0, progress.SubmittedAmount)
                        });
                }

                saveData.Quests.Add(questData);
            }

            saveData.Validate();
            return saveData;
        }

        public void RestoreSaveData(QuestSaveData questData)
        {
            questData?.Validate();
            Initialize(questData, false);
        }

        public void BeginRuntimeRestore()
        {
            suppressQuestRuntimeMessages = true;
        }

        public void EndRuntimeRestore()
        {
            suppressQuestRuntimeMessages = false;
            RefreshAllQuestProgress(false);
        }

        public bool TrySubmitResources(string questId)
        {
            var quest = Find(questId);
            return TrySubmitResources(quest);
        }

        public bool TrySubmitResources(GameQuestState quest)
        {
            if (quest == null || !quest.IsActive || !quest.IsResourceSubmission)
            {
                return false;
            }

            if (Inventory == null)
            {
                context.EnsureInventoryServiceForQuest();
            }

            if (Inventory == null)
            {
                return false;
            }

            var totalSubmitted = 0;
            suppressQuestInventoryRefresh = true;
            try
            {
                var resourceProgresses = quest.ResourceProgresses;
                for (var i = 0; i < resourceProgresses.Count; i++)
                {
                    var progress = resourceProgresses[i];
                    if (progress == null || progress.RemainingAmount <= 0)
                    {
                        continue;
                    }

                    var available = Inventory.GetQuantity(progress.ItemId);
                    var submitAmount = Mathf.Min(progress.RemainingAmount, available);
                    if (submitAmount <= 0)
                    {
                        continue;
                    }

                    var removed = Inventory.RemoveItem(progress.ItemId, submitAmount);
                    if (removed <= 0)
                    {
                        continue;
                    }

                    progress.SubmittedAmount = Mathf.Min(progress.RequiredAmount, progress.SubmittedAmount + removed);
                    totalSubmitted += removed;
                }
            }
            finally
            {
                suppressQuestInventoryRefresh = false;
            }

            if (totalSubmitted <= 0)
            {
                RefreshQuestProgress(quest, false);
                NotifyChanged();
                return false;
            }

            RefreshQuestProgress(quest, true);
            NotifyChanged();
            return true;
        }

        public bool TryAbandon(string questId)
        {
            return TryAbandon(Find(questId));
        }

        public bool TryAbandon(GameQuestState quest)
        {
            if (quest == null || (!quest.IsActive && !quest.IsFailed))
            {
                return false;
            }

            quest.Status = QuestStatus.Abandoned;
            NotifyChanged();
            return true;
        }

        public bool TryClaimRewards(string questId)
        {
            return TryClaimRewards(Find(questId));
        }

        public bool TryClaimRewards(GameQuestState quest)
        {
            if (quest == null || !quest.CanClaimRewards)
            {
                return false;
            }

            if (!TryApplyQuestRewards(quest, out var failureMessage))
            {
                if (!string.IsNullOrWhiteSpace(failureMessage))
                {
                    AddQuestMessage(
                        GameEventCatalog.GE_任务奖励领取失败,
                        failureMessage,
                        _ => HandleQuestEventClicked(quest.QuestId));
                }

                return false;
            }

            quest.Status = QuestStatus.RewardClaimed;
            if (quest.IsMainline)
            {
                UnlockAvailableMainlineQuests(BuildCurrentQuestIdSet(), true);
            }

            NotifyChanged();
            return true;
        }

        internal void Initialize()
        {
            Initialize(null, false);
        }

        internal void Initialize(QuestSaveData saveData, bool emitMessages)
        {
            UnsubscribeRuntimeServices();
            questsInitialized = true;
            Features?.ResetToInitialFeatures();
            quests.Clear();
            NormalizeStartingQuests();
            NormalizeRandomQuestPool();
            NormalizeRuntimeExchangeQuestRules();
            nextRandomQuestRefreshTurn = saveData == null
                ? CurrentTurn + Mathf.Max(1, randomQuestRefreshIntervalTurns)
                : Mathf.Max(1, saveData.NextRandomQuestRefreshTurn);
            generatedRandomQuestSerial = saveData == null ? 0 : Mathf.Max(0, saveData.GeneratedRandomQuestSerial);

            var savedQuests = BuildSavedQuestLookup(saveData);
            var usedQuestIds = new HashSet<string>(StringComparer.Ordinal);
            AddSavedMainlineQuests(savedQuests, usedQuestIds, emitMessages);
            UnlockAvailableMainlineQuests(usedQuestIds, emitMessages);
            AddSavedRandomQuests(savedQuests, usedQuestIds, emitMessages);
            AddSavedGeneratedQuests(savedQuests, usedQuestIds, emitMessages);
            SynchronizeClaimedFeatureRewards();

            if (saveData == null)
            {
                EnsureRandomQuestCapacity(startingRandomQuestCount, usedQuestIds, emitMessages);
            }

            SubscribeQuestRuntimeServices();
            NotifyChanged();
        }

        private void NormalizeStartingQuests()
        {
            if (startingQuests == null)
            {
                startingQuests = Array.Empty<GameQuestDefinition>();
                return;
            }

            for (var i = 0; i < startingQuests.Length; i++)
            {
                startingQuests[i]?.Normalize();
            }
        }

        private void NormalizeRandomQuestPool()
        {
            if (randomQuestPool == null)
            {
                randomQuestPool = Array.Empty<GameQuestDefinition>();
                return;
            }

            for (var i = 0; i < randomQuestPool.Length; i++)
            {
                randomQuestPool[i]?.Normalize();
            }
        }

        private void NormalizeRuntimeExchangeQuestRules()
        {
            if (runtimeExchangeQuestRules == null)
            {
                runtimeExchangeQuestRules = Array.Empty<RandomExchangeQuestRule>();
                return;
            }

            for (var i = 0; i < runtimeExchangeQuestRules.Length; i++)
            {
                runtimeExchangeQuestRules[i]?.Normalize();
            }
        }

        private void AddSavedMainlineQuests(
            IReadOnlyDictionary<string, QuestStateSaveData> savedQuests,
            HashSet<string> usedQuestIds,
            bool emitMessages)
        {
            if (savedQuests == null || savedQuests.Count == 0 || startingQuests == null)
            {
                return;
            }

            for (var i = 0; i < startingQuests.Length; i++)
            {
                var definition = startingQuests[i];
                if (definition == null)
                {
                    continue;
                }

                definition.Normalize();
                if (!savedQuests.ContainsKey(definition.QuestId))
                {
                    continue;
                }

                TryAddQuestDefinition(
                    definition,
                    QuestCategory.Mainline,
                    savedQuests,
                    usedQuestIds,
                    emitMessages,
                    out _);
            }
        }

        private bool UnlockAvailableMainlineQuests(HashSet<string> usedQuestIds, bool emitMessages)
        {
            if (startingQuests == null || startingQuests.Length == 0)
            {
                return false;
            }

            usedQuestIds ??= BuildCurrentQuestIdSet();
            var addedAny = false;
            var addedThisPass = false;
            do
            {
                addedThisPass = false;
                for (var i = 0; i < startingQuests.Length; i++)
                {
                    var definition = startingQuests[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    definition.Normalize();
                    if (string.IsNullOrWhiteSpace(definition.QuestId)
                        || usedQuestIds.Contains(definition.QuestId)
                        || !AreMainlinePrerequisitesCompleted(definition))
                    {
                        continue;
                    }

                    if (!TryAddQuestDefinition(
                            definition,
                            QuestCategory.Mainline,
                            null,
                            usedQuestIds,
                            emitMessages,
                            out _))
                    {
                        continue;
                    }

                    addedAny = true;
                    addedThisPass = true;
                }
            }
            while (addedThisPass);

            return addedAny;
        }

        private bool AreMainlinePrerequisitesCompleted(GameQuestDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            var prerequisites = definition.PrerequisiteQuestIds;
            if (prerequisites == null || prerequisites.Count == 0)
            {
                return true;
            }

            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisiteQuestId = GameQuestDefinition.NormalizeQuestId(prerequisites[i]);
                if (string.IsNullOrWhiteSpace(prerequisiteQuestId) || !IsQuestRewardClaimed(prerequisiteQuestId))
                {
                    return false;
                }
            }

            return true;
        }

        private void AddSavedRandomQuests(
            IReadOnlyDictionary<string, QuestStateSaveData> savedQuests,
            HashSet<string> usedQuestIds,
            bool emitMessages)
        {
            if (savedQuests == null || savedQuests.Count == 0 || randomQuestPool == null)
            {
                return;
            }

            for (var i = 0; i < randomQuestPool.Length; i++)
            {
                var definition = randomQuestPool[i];
                if (definition == null)
                {
                    continue;
                }

                definition.Normalize();
                if (!savedQuests.ContainsKey(definition.QuestId))
                {
                    continue;
                }

                TryAddQuestDefinition(
                    definition,
                    QuestCategory.Random,
                    savedQuests,
                    usedQuestIds,
                    emitMessages,
                    out _);
            }
        }

        private void AddSavedGeneratedQuests(
            IReadOnlyDictionary<string, QuestStateSaveData> savedQuests,
            HashSet<string> usedQuestIds,
            bool emitMessages)
        {
            if (savedQuests == null || savedQuests.Count == 0)
            {
                return;
            }

            foreach (var pair in savedQuests)
            {
                var savedQuest = pair.Value;
                if (savedQuest == null
                    || savedQuest.GeneratedDefinition == null
                    || usedQuestIds.Contains(savedQuest.QuestId))
                {
                    continue;
                }

                var definition = CreateGeneratedQuestDefinition(savedQuest.GeneratedDefinition);
                if (definition == null)
                {
                    Debug.LogWarning($"运行时生成任务恢复失败，已跳过：{savedQuest.QuestId}", context);
                    continue;
                }

                TryAddQuestDefinition(
                    definition,
                    definition.Category,
                    savedQuests,
                    usedQuestIds,
                    emitMessages,
                    out _);
            }
        }

        private bool TryAddQuestDefinition(
            GameQuestDefinition definition,
            QuestCategory category,
            IReadOnlyDictionary<string, QuestStateSaveData> savedQuests,
            HashSet<string> usedQuestIds,
            bool emitMessages,
            out GameQuestState quest)
        {
            quest = null;
            if (definition == null)
            {
                return false;
            }

            definition.Normalize();
            if (!definition.IsValid)
            {
                Debug.LogWarning($"任务配置无效，已跳过：{definition.DisplayName}", context);
                return false;
            }

            usedQuestIds ??= new HashSet<string>(StringComparer.Ordinal);
            if (!usedQuestIds.Add(definition.QuestId))
            {
                Debug.LogWarning($"任务ID重复，已跳过后续配置：{definition.QuestId}", context);
                return false;
            }

            if (Find(definition.QuestId) != null)
            {
                return false;
            }

            QuestStateSaveData savedQuest = null;
            if (savedQuests != null)
            {
                savedQuests.TryGetValue(definition.QuestId, out savedQuest);
            }

            quest = new GameQuestState(definition, CurrentTurn, category);
            if (savedQuest != null)
            {
                ApplyQuestSaveData(quest, savedQuest, category);
            }

            RebuildQuestResourceProgresses(quest, savedQuest);
            quests.Add(quest);
            RefreshQuestProgress(quest, emitMessages);

            if (emitMessages && savedQuest == null)
            {
                AddNewQuestMessage(quest);
            }

            return true;
        }

        private GameQuestDefinition CreateGeneratedQuestDefinition(QuestGeneratedDefinitionSaveData saveData)
        {
            if (saveData == null)
            {
                return null;
            }

            saveData.Validate();
            if (!saveData.IsValid)
            {
                return null;
            }

            var requiredResources = BuildItemAmounts(saveData.RequiredResources);
            if (requiredResources.Count == 0)
            {
                return null;
            }

            var rewards = BuildItemAmounts(saveData.Rewards);
            return GameQuestDefinition.CreateRuntimeSubmitResourcesQuest(
                saveData.QuestId,
                string.Empty,
                string.Empty,
                saveData.Category,
                saveData.TurnLimit,
                requiredResources,
                rewards,
                saveData.TemplateId,
                saveData.RequesterId,
                saveData.RequesterSourceName,
                saveData.NameFallbackFormat,
                saveData.DescriptionFallbackFormat);
        }

        private List<ItemAmount> BuildItemAmounts(IReadOnlyList<QuestItemAmountSaveData> source)
        {
            var result = new List<ItemAmount>();
            if (source == null)
            {
                return result;
            }

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

                var definition = FindItemDefinition(item.ItemId);
                if (definition == null)
                {
                    Debug.LogWarning($"任务物品ID不存在，已跳过：{item.ItemId}", context);
                    continue;
                }

                result.Add(new ItemAmount(definition, item.Amount));
            }

            return result;
        }

        private static Dictionary<string, QuestStateSaveData> BuildSavedQuestLookup(QuestSaveData saveData)
        {
            var result = new Dictionary<string, QuestStateSaveData>(StringComparer.Ordinal);
            if (saveData == null || saveData.Quests == null)
            {
                return result;
            }

            for (var i = 0; i < saveData.Quests.Count; i++)
            {
                var questData = saveData.Quests[i];
                if (questData == null)
                {
                    continue;
                }

                questData.Validate();
                if (!string.IsNullOrWhiteSpace(questData.QuestId))
                {
                    result[questData.QuestId] = questData;
                }
            }

            return result;
        }

        private bool EnsureRandomQuestCapacity(
            int desiredActiveCount,
            HashSet<string> usedQuestIds,
            bool emitMessages)
        {
            var targetCount = Mathf.Clamp(desiredActiveCount, 0, Mathf.Max(0, maxActiveRandomQuests));
            if (targetCount <= 0 || !HasAnyRandomQuestSource())
            {
                return false;
            }

            usedQuestIds ??= BuildCurrentQuestIdSet();
            var activeRandomCount = CountActiveRandomQuests();
            var addedAny = false;
            while (activeRandomCount < targetCount)
            {
                var definition = PickRandomQuestDefinition(usedQuestIds);
                if (definition == null)
                {
                    break;
                }

                if (!TryAddQuestDefinition(
                        definition,
                        QuestCategory.Random,
                        null,
                        usedQuestIds,
                        emitMessages,
                        out var quest))
                {
                    continue;
                }

                addedAny = true;
                if (quest == null || !quest.IsActive)
                {
                    continue;
                }

                activeRandomCount++;
            }

            return addedAny;
        }

        private GameQuestDefinition PickRandomQuestDefinition(HashSet<string> usedQuestIds)
        {
            var candidates = new List<GameQuestDefinition>();
            if (randomQuestPool != null)
            {
                for (var i = 0; i < randomQuestPool.Length; i++)
                {
                    var definition = randomQuestPool[i];
                    if (definition == null)
                    {
                        continue;
                    }

                    definition.Normalize();
                    if (!definition.IsValid)
                    {
                        continue;
                    }

                    if (usedQuestIds != null && usedQuestIds.Contains(definition.QuestId))
                    {
                        continue;
                    }

                    candidates.Add(definition);
                }
            }

            var exchangeRules = BuildValidRuntimeExchangeQuestRules();
            var candidateCount = candidates.Count + exchangeRules.Count;
            if (candidateCount == 0)
            {
                return null;
            }

            var index = UnityEngine.Random.Range(0, candidateCount);
            if (index < candidates.Count)
            {
                return candidates[index];
            }

            var rule = exchangeRules[index - candidates.Count];
            var questId = CreateRuntimeRandomQuestId(rule.QuestIdPrefix, usedQuestIds);
            return rule.CreateDefinition(questId);
        }

        private List<RandomExchangeQuestRule> BuildValidRuntimeExchangeQuestRules()
        {
            var result = new List<RandomExchangeQuestRule>();
            if (runtimeExchangeQuestRules == null)
            {
                return result;
            }

            for (var i = 0; i < runtimeExchangeQuestRules.Length; i++)
            {
                var rule = runtimeExchangeQuestRules[i];
                rule?.Normalize();
                if (rule != null && rule.IsValid)
                {
                    result.Add(rule);
                }
            }

            return result;
        }

        private bool HasAnyRandomQuestSource()
        {
            if (randomQuestPool != null)
            {
                for (var i = 0; i < randomQuestPool.Length; i++)
                {
                    var definition = randomQuestPool[i];
                    definition?.Normalize();
                    if (definition != null && definition.IsValid)
                    {
                        return true;
                    }
                }
            }

            return BuildValidRuntimeExchangeQuestRules().Count > 0;
        }

        private string CreateRuntimeRandomQuestId(string prefix, HashSet<string> usedQuestIds)
        {
            prefix = string.IsNullOrWhiteSpace(prefix) ? "random_exchange" : prefix.Trim();
            for (var i = 0; i < 100; i++)
            {
                generatedRandomQuestSerial++;
                var questId = $"{prefix}_{CurrentTurn}_{generatedRandomQuestSerial}";
                if ((usedQuestIds == null || !usedQuestIds.Contains(questId)) && Find(questId) == null)
                {
                    return questId;
                }
            }

            return $"{prefix}_{Guid.NewGuid():N}";
        }

        private HashSet<string> BuildCurrentQuestIdSet()
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest != null && !string.IsNullOrWhiteSpace(quest.QuestId))
                {
                    result.Add(quest.QuestId);
                }
            }

            return result;
        }

        private int CountActiveRandomQuests()
        {
            var count = 0;
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest != null && quest.IsRandom && quest.IsActive)
                {
                    count++;
                }
            }

            return count;
        }

        private void RefreshRandomQuestsForTurn(bool emitMessages)
        {
            if (maxActiveRandomQuests <= 0 || !HasAnyRandomQuestSource())
            {
                return;
            }

            var currentTurn = CurrentTurn;
            if (nextRandomQuestRefreshTurn <= 0)
            {
                nextRandomQuestRefreshTurn = currentTurn;
            }

            if (currentTurn < nextRandomQuestRefreshTurn)
            {
                return;
            }

            var addedAny = EnsureRandomQuestCapacity(maxActiveRandomQuests, null, emitMessages);
            nextRandomQuestRefreshTurn = currentTurn + Mathf.Max(1, randomQuestRefreshIntervalTurns);
            if (addedAny)
            {
                NotifyChanged();
            }
        }

        private static void ApplyQuestSaveData(
            GameQuestState quest,
            QuestStateSaveData saveData,
            QuestCategory category)
        {
            if (quest == null || saveData == null)
            {
                return;
            }

            var wasPendingAcceptance = (int)saveData.Status == 3;
            saveData.Validate();
            quest.Category = GameQuestDefinition.NormalizeQuestCategory(category);
            if (wasPendingAcceptance)
            {
                return;
            }

            quest.Status = saveData.Status;
            quest.StartedTurn = Mathf.Max(1, saveData.StartedTurn);
            quest.DeadlineTurn = quest.IsTimed
                ? Mathf.Max(quest.StartedTurn, saveData.DeadlineTurn)
                : 0;
        }

        private void RebuildQuestResourceProgresses(GameQuestState quest, QuestStateSaveData saveData)
        {
            if (quest == null)
            {
                return;
            }

            quest.ClearResourceProgresses();
            if (!quest.IsResourceObjective || quest.Definition == null)
            {
                return;
            }

            var requiredAmounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var orderedItemIds = new List<string>();
            var requiredResources = quest.Definition.RequiredResources;
            for (var i = 0; i < requiredResources.Count; i++)
            {
                var requirement = requiredResources[i].Normalized();
                if (!requirement.IsValid)
                {
                    continue;
                }

                var itemId = requirement.ItemId.Trim();
                if (!requiredAmounts.ContainsKey(itemId))
                {
                    requiredAmounts.Add(itemId, 0);
                    orderedItemIds.Add(itemId);
                }

                requiredAmounts[itemId] += requirement.Amount;
            }

            var submittedAmounts = BuildSubmittedResourceLookup(saveData);
            for (var i = 0; i < orderedItemIds.Count; i++)
            {
                var itemId = orderedItemIds[i];
                var requiredAmount = Mathf.Max(0, requiredAmounts[itemId]);
                submittedAmounts.TryGetValue(itemId, out var submittedAmount);

                quest.AddResourceProgress(
                    new GameQuestResourceProgress
                    {
                        ItemId = itemId,
                        ItemDefinition = FindItemDefinition(itemId),
                        RequiredAmount = requiredAmount,
                        SubmittedAmount = Mathf.Clamp(submittedAmount, 0, requiredAmount),
                        InventoryAmount = Inventory == null ? 0 : Inventory.GetQuantity(itemId),
                        TracksInventoryAmount = quest.IsResourceCollection
                    });
            }
        }

        private static Dictionary<string, int> BuildSubmittedResourceLookup(QuestStateSaveData saveData)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            if (saveData == null || saveData.SubmittedResources == null)
            {
                return result;
            }

            for (var i = 0; i < saveData.SubmittedResources.Count; i++)
            {
                var submittedResource = saveData.SubmittedResources[i];
                if (submittedResource == null)
                {
                    continue;
                }

                submittedResource.Validate();
                if (string.IsNullOrWhiteSpace(submittedResource.ItemId))
                {
                    continue;
                }

                result[submittedResource.ItemId] = Mathf.Max(0, submittedResource.SubmittedAmount);
            }

            return result;
        }

        public GameQuestState Find(string questId)
        {
            questId = GameQuestDefinition.NormalizeQuestId(questId);
            if (string.IsNullOrWhiteSpace(questId))
            {
                return null;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest != null && string.Equals(quest.QuestId, questId, StringComparison.Ordinal))
                {
                    return quest;
                }
            }

            return null;
        }

        private bool IsQuestRewardClaimed(string questId)
        {
            var quest = Find(questId);
            return quest != null && quest.IsRewardClaimed;
        }

        private void RefreshAllQuestProgress(bool emitMessages)
        {
            var shouldEmitMessages = emitMessages && !suppressQuestRuntimeMessages;
            for (var i = 0; i < quests.Count; i++)
            {
                RefreshQuestProgress(quests[i], shouldEmitMessages);
            }

            NotifyChanged();
        }

        private void RefreshQuestProgress(GameQuestState quest, bool emitMessages)
        {
            if (quest == null || quest.Definition == null)
            {
                return;
            }

            switch (quest.Definition.ObjectiveType)
            {
                case QuestObjectiveType.BuildBuildings:
                    RefreshBuildQuestProgress(quest);
                    break;
                case QuestObjectiveType.SubmitResources:
                    RefreshResourceQuestProgress(quest);
                    break;
                case QuestObjectiveType.MoveCamera:
                    RefreshMoveCameraQuestProgress(quest);
                    break;
                case QuestObjectiveType.CollectResources:
                    RefreshResourceQuestProgress(quest);
                    break;
                case QuestObjectiveType.PlantCrops:
                    RefreshPlantCropQuestProgress(quest);
                    break;
                case QuestObjectiveType.SelectTechnology:
                    RefreshTechnologySelectionQuestProgress(quest);
                    break;
            }

            if (!quest.IsActive)
            {
                return;
            }

            if (IsQuestSatisfied(quest))
            {
                CompleteQuest(quest);
                return;
            }

            if (quest.IsTimed && CurrentTurn > quest.DeadlineTurn)
            {
                FailQuest(quest, emitMessages);
            }
        }

        private void RefreshBuildQuestProgress(GameQuestState quest)
        {
            var definition = quest.Definition;
            quest.TargetAmount = Mathf.Max(1, definition.TargetBuildingCount);
            quest.CurrentAmount = Mathf.Clamp(CountBuildings(definition.TargetBuildingId), 0, quest.TargetAmount);
            quest.TargetDisplayName = definition.TargetBuildingDisplayName;
            quest.Icon = definition.TargetBuildingIcon;
        }

        private static void RefreshMoveCameraQuestProgress(GameQuestState quest)
        {
            quest.TargetAmount = 1;
            quest.CurrentAmount = Mathf.Clamp(quest.CurrentAmount, 0, 1);
            quest.TargetDisplayName = Landsong.Localization.L10n.Gameplay(
                "gameplay.quest.target.move_camera",
                "移动视野");
            quest.Icon = null;
        }

        private void RefreshPlantCropQuestProgress(GameQuestState quest)
        {
            var definition = quest.Definition;
            quest.TargetAmount = Mathf.Max(1, definition.TargetBuildingCount);
            quest.CurrentAmount = Mathf.Clamp(CountPlantedCrops(definition.TargetBuildingId), 0, quest.TargetAmount);
            quest.TargetDisplayName = definition.TargetBuildingDisplayName;
            quest.Icon = definition.TargetBuildingIcon;
        }

        private void RefreshTechnologySelectionQuestProgress(GameQuestState quest)
        {
            var selectedTechnology = Technology == null ? null : Technology.CurrentResearchDefinition;
            quest.TargetAmount = 1;
            quest.CurrentAmount = selectedTechnology == null ? 0 : 1;
            quest.TargetDisplayName = selectedTechnology == null
                ? Landsong.Localization.L10n.Gameplay("gameplay.quest.target.any_technology", "任意科技")
                : selectedTechnology.DisplayName;
            quest.Icon = selectedTechnology == null ? null : selectedTechnology.Icon;
        }

        private void RefreshResourceQuestProgress(GameQuestState quest)
        {
            var totalRequired = 0;
            var totalProgress = 0;
            var firstIcon = (Sprite)null;
            var firstDisplayName = string.Empty;

            var resources = quest.ResourceProgresses;
            for (var i = 0; i < resources.Count; i++)
            {
                var progress = resources[i];
                if (progress == null)
                {
                    continue;
                }

                progress.InventoryAmount = Inventory == null ? 0 : Inventory.GetQuantity(progress.ItemId);
                totalRequired += Mathf.Max(0, progress.RequiredAmount);
                totalProgress += Mathf.Clamp(progress.ProgressAmount, 0, progress.RequiredAmount);
                if (firstIcon == null && progress.ItemDefinition != null)
                {
                    firstIcon = progress.ItemDefinition.Icon;
                }

                if (string.IsNullOrWhiteSpace(firstDisplayName))
                {
                    firstDisplayName = progress.DisplayName;
                }
            }

            quest.TargetAmount = Mathf.Max(0, totalRequired);
            quest.CurrentAmount = Mathf.Clamp(totalProgress, 0, quest.TargetAmount);
            quest.TargetDisplayName = resources.Count == 1
                ? firstDisplayName
                : Landsong.Localization.L10n.Gameplay("gameplay.quest.target.multiple_resources", "多种资源");
            quest.Icon = firstIcon;
        }

        private bool IsQuestSatisfied(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return false;
            }

            if (quest.IsResourceObjective)
            {
                var resources = quest.ResourceProgresses;
                if (resources.Count == 0)
                {
                    return false;
                }

                for (var i = 0; i < resources.Count; i++)
                {
                    if (resources[i] == null || !resources[i].IsComplete)
                    {
                        return false;
                    }
                }

                return true;
            }

            return quest.TargetAmount > 0 && quest.CurrentAmount >= quest.TargetAmount;
        }

        private void CompleteQuest(GameQuestState quest)
        {
            if (quest == null || !quest.IsActive)
            {
                return;
            }

            quest.Status = QuestStatus.Completed;
        }

        private bool TryApplyQuestRewards(GameQuestState quest, out string failureMessage)
        {
            failureMessage = string.Empty;
            if (quest == null || quest.Definition == null || !quest.Definition.HasRewards)
            {
                return true;
            }

            var validRewards = new List<ItemAmount>();
            var rewards = quest.Definition.Rewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i].Normalized();
                if (!reward.IsValid)
                {
                    continue;
                }

                if (reward.ItemDefinition == null)
                {
                    Debug.LogWarning($"任务奖励物品定义无效：{reward.ItemId} x{reward.Amount}", context);
                    failureMessage = Landsong.Localization.L10n.Gameplay(
                        "gameplay.quest.reward.invalid",
                        "任务“{0}”的奖励配置无效，暂时无法领取。",
                        quest.Definition.DisplayName);
                    return false;
                }

                validRewards.Add(reward);
            }

            if (validRewards.Count > 0)
            {
                if (Inventory == null)
                {
                    context.EnsureInventoryServiceForQuest();
                }

                if (Inventory == null)
                {
                    failureMessage = Landsong.Localization.L10n.Gameplay(
                        "gameplay.quest.reward.inventory_uninitialized",
                        "库存系统尚未初始化，暂时无法领取任务奖励。");
                    return false;
                }

                if (!Inventory.CanAddItems(validRewards))
                {
                    failureMessage = Landsong.Localization.L10n.Gameplay(
                        "gameplay.quest.reward.inventory_full",
                        "库存空间不足，无法领取任务“{0}”的奖励。请先腾出库存格。",
                        quest.Definition.DisplayName);
                    return false;
                }

                if (!Inventory.TryAddItems(validRewards))
                {
                    failureMessage = Landsong.Localization.L10n.Gameplay(
                        "gameplay.quest.reward.delivery_failed",
                        "任务“{0}”的奖励发放失败，请重试。",
                        quest.Definition.DisplayName);
                    return false;
                }
            }

            Features?.Unlock(quest.Definition.UnlockedFeatures);
            return true;
        }

        private void FailQuest(GameQuestState quest, bool emitMessage)
        {
            if (quest == null || !quest.IsActive)
            {
                return;
            }

            quest.Status = QuestStatus.Failed;
            if (emitMessage)
            {
                AddQuestMessageLocalized(
                    GameEventCatalog.GE_任务失败,
                    "gameplay.quest.failed",
                    "任务失败：{0}",
                    () => new object[] { quest.Definition.DisplayName });
            }
        }

        private int CountBuildings(string buildingId)
        {
            if (Buildings == null || string.IsNullOrWhiteSpace(buildingId))
            {
                return 0;
            }

            var count = 0;
            var buildings = Buildings.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
                    || !building.isActiveAndEnabled
                    || building.IsDemolishing
                    || !building.HasDefinition)
                {
                    continue;
                }

                if (string.Equals(building.FamilyId, buildingId, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private int CountPlantedCrops(string buildingId)
        {
            if (Buildings == null || string.IsNullOrWhiteSpace(buildingId))
            {
                return 0;
            }

            var count = 0;
            var buildings = Buildings.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building == null
                    || !building.isActiveAndEnabled
                    || building.IsDemolishing
                    || !building.HasDefinition
                    || !string.Equals(building.FamilyId, buildingId, StringComparison.Ordinal)
                    || !building.TryGetCapability<IBuildingCropFieldSource>(out var cropField)
                    || !cropField.HasCrop)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private void SynchronizeClaimedFeatureRewards()
        {
            if (Features == null)
            {
                return;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest != null && quest.IsRewardClaimed && quest.Definition != null)
                {
                    Features.Unlock(quest.Definition.UnlockedFeatures);
                }
            }
        }

        private ItemDefinition FindItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            var catalog = Inventory == null ? itemCatalog : Inventory.ItemCatalog;
            return catalog != null && catalog.TryGetDefinition(itemId, out var definition) ? definition : null;
        }

        private void AddNewQuestMessage(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return;
            }

            AddQuestMessageLocalized(
                GameEventCatalog.GE_任务出现,
                "gameplay.quest.new",
                "新任务：{0}",
                () => new object[] { quest.Definition.DisplayName },
                _ => HandleQuestEventClicked(quest.QuestId),
                true);
        }

        private void HandleQuestEventClicked(string questId)
        {
            var quest = Find(questId);
            if (quest == null || quest.IsRewardClaimed || quest.IsAbandoned)
            {
                return;
            }

            QuestRequested?.Invoke(this, quest);
        }

        private void AddQuestMessage(
            string eventTypeId,
            string message,
            Action<GameEventMessage> clicked = null,
            bool suppressDefaultPopup = false)
        {
            if (Events == null)
            {
                context.EnsureGameEventServiceForQuest();
            }

            Events?.AddMessage(
                GameEventMessage.ForGame(eventTypeId, message, CurrentTurn, clicked, suppressDefaultPopup));
        }

        private void AddQuestMessageLocalized(
            string eventTypeId,
            string textKey,
            string sourceMessage,
            Func<object[]> argumentsProvider,
            Action<GameEventMessage> clicked = null,
            bool suppressDefaultPopup = false)
        {
            if (Events == null)
            {
                context.EnsureGameEventServiceForQuest();
            }

            Events?.AddMessage(GameEventMessage.ForGameLocalized(
                eventTypeId,
                textKey,
                sourceMessage,
                CurrentTurn,
                argumentsProvider,
                clicked,
                suppressDefaultPopup));
        }

        internal void RefreshSubscriptions()
        {
            if (!questsInitialized)
            {
                return;
            }

            SubscribeQuestRuntimeServices();
        }

        private void SubscribeQuestRuntimeServices()
        {
            if (!questsInitialized)
            {
                return;
            }

            if (subscribedQuestInventory != Inventory)
            {
                if (subscribedQuestInventory != null)
                {
                    subscribedQuestInventory.InventoryChanged -= HandleQuestInventoryChanged;
                }

                subscribedQuestInventory = Inventory;
                if (subscribedQuestInventory != null)
                {
                    subscribedQuestInventory.InventoryChanged += HandleQuestInventoryChanged;
                }
            }

            if (subscribedQuestBuildings != Buildings)
            {
                if (subscribedQuestBuildings != null)
                {
                    subscribedQuestBuildings.BuildingsChanged -= HandleQuestBuildingsChanged;
                }

                subscribedQuestBuildings = Buildings;
                if (subscribedQuestBuildings != null)
                {
                    subscribedQuestBuildings.BuildingsChanged += HandleQuestBuildingsChanged;
                }

                SynchronizeBuildingStateSubscriptions();
            }

            if (subscribedQuestTurn != Turn)
            {
                if (subscribedQuestTurn != null)
                {
                    subscribedQuestTurn.TurnAdvanced -= HandleQuestTurnAdvanced;
                }

                subscribedQuestTurn = Turn;
                if (subscribedQuestTurn != null)
                {
                    subscribedQuestTurn.TurnAdvanced += HandleQuestTurnAdvanced;
                }
            }


            if (subscribedQuestTechnology != Technology)
            {
                if (subscribedQuestTechnology != null)
                {
                    subscribedQuestTechnology.StateChanged -= HandleQuestTechnologyChanged;
                }

                subscribedQuestTechnology = Technology;
                if (subscribedQuestTechnology != null)
                {
                    subscribedQuestTechnology.StateChanged += HandleQuestTechnologyChanged;
                }
            }

            if (!subscribedQuestCameraInput)
            {
                CameraController.AnyManualCameraPanPerformed += HandleManualCameraPanPerformed;
                subscribedQuestCameraInput = true;
            }
        }

        private void UnsubscribeRuntimeServices()
        {
            if (subscribedQuestInventory != null)
            {
                subscribedQuestInventory.InventoryChanged -= HandleQuestInventoryChanged;
                subscribedQuestInventory = null;
            }

            if (subscribedQuestBuildings != null)
            {
                subscribedQuestBuildings.BuildingsChanged -= HandleQuestBuildingsChanged;
                subscribedQuestBuildings = null;
            }

            ClearBuildingStateSubscriptions();

            if (subscribedQuestTurn != null)
            {
                subscribedQuestTurn.TurnAdvanced -= HandleQuestTurnAdvanced;
                subscribedQuestTurn = null;
            }


            if (subscribedQuestTechnology != null)
            {
                subscribedQuestTechnology.StateChanged -= HandleQuestTechnologyChanged;
                subscribedQuestTechnology = null;
            }

            if (subscribedQuestCameraInput)
            {
                CameraController.AnyManualCameraPanPerformed -= HandleManualCameraPanPerformed;
                subscribedQuestCameraInput = false;
            }
        }

        private void HandleQuestInventoryChanged(InventoryService changedInventory)
        {
            if (suppressQuestInventoryRefresh)
            {
                return;
            }

            RefreshAllQuestProgress(false);
        }

        private void HandleQuestBuildingsChanged(BuildingService changedBuildings)
        {
            SynchronizeBuildingStateSubscriptions();
            RefreshAllQuestProgress(true);
        }

        private void HandleQuestBuildingStateChanged(BuildingBase changedBuilding)
        {
            RefreshAllQuestProgress(true);
        }

        private void HandleQuestTechnologyChanged(TechnologyService changedTechnology)
        {
            RefreshAllQuestProgress(false);
        }

        private void HandleManualCameraPanPerformed(CameraController changedCamera)
        {
            var changed = false;
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null
                    || !quest.IsActive
                    || quest.Definition == null
                    || quest.Definition.ObjectiveType != QuestObjectiveType.MoveCamera)
                {
                    continue;
                }

                quest.CurrentAmount = 1;
                RefreshQuestProgress(quest, false);
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }
        }

        private void SynchronizeBuildingStateSubscriptions()
        {
            ClearBuildingStateSubscriptions();
            if (subscribedQuestBuildings == null)
            {
                return;
            }

            var buildings = subscribedQuestBuildings.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var building = buildings[i];
                if (building != null && subscribedQuestBuildingStates.Add(building))
                {
                    building.StateChanged += HandleQuestBuildingStateChanged;
                }
            }
        }

        private void ClearBuildingStateSubscriptions()
        {
            foreach (var building in subscribedQuestBuildingStates)
            {
                if (building != null)
                {
                    building.StateChanged -= HandleQuestBuildingStateChanged;
                }
            }

            subscribedQuestBuildingStates.Clear();
        }

        private void HandleQuestTurnAdvanced(TurnService changedTurn, TurnAdvanceSummary summary)
        {
            RefreshAllQuestProgress(true);
            RefreshRandomQuestsForTurn(!suppressQuestRuntimeMessages);
        }

        private void NotifyChanged()
        {
            StateChanged?.Invoke(this);
        }
    }
}
