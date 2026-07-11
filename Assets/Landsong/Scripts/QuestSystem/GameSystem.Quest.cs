using System;
using System.Collections.Generic;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong
{
    /// <summary>
    /// GameSystem 的任务配置和兼容门面。任务运行时规则由 QuestService 独立持有。
    /// </summary>
    public sealed partial class GameSystem
    {
        private static readonly IReadOnlyList<GameQuestState> EmptyQuests = Array.Empty<GameQuestState>();

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("主线任务")]
        private GameQuestDefinition[] startingQuests = Array.Empty<GameQuestDefinition>();

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("随机任务池")]
        private GameQuestDefinition[] randomQuestPool = Array.Empty<GameQuestDefinition>();

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("运行时交换任务规则")]
        private RandomExchangeQuestRule[] runtimeExchangeQuestRules = Array.Empty<RandomExchangeQuestRule>();

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("开局随机任务数量"), Min(0)]
        private int startingRandomQuestCount = 1;

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("随机任务同时存在上限"), Min(0)]
        private int maxActiveRandomQuests = 2;

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("随机任务补充间隔回合"), Min(1)]
        private int randomQuestRefreshIntervalTurns = 3;

        public event Action<GameSystem> QuestsChanged;
        public event Action<GameSystem, GameQuestState> QuestEventClicked;

        public IReadOnlyList<GameQuestState> Quests => Quest == null ? EmptyQuests : Quest.Quests;
        internal ItemCatalog QuestItemCatalog => itemCatalog;

        [Button("重新初始化任务")]
        public void ReinitializeQuests()
        {
            CreateQuestService();
        }

        /// <summary>
        /// 通过正常库存服务添加调试物品。库存不足时返回实际加入数量。
        /// </summary>
        public int AddGameplayDebugItem(string itemId, int amount)
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            amount = Mathf.Max(0, amount);
            if (amount <= 0 || FindQuestItemDefinition(itemId) == null)
            {
                return 0;
            }

            var inventory = EnsureInventoryServiceForQuest();
            return inventory == null ? 0 : inventory.AddItem(itemId, amount);
        }

        public int AddGameplayDebugGold()
        {
            return AddGameplayDebugItem(GameplayDebugGoldItemId, 9999);
        }

        public bool TryAddGameplayDebugRandomQuest(out GameQuestState quest)
        {
            return EnsureQuestService().TryAddDebugRandomQuest(out quest);
        }

        public QuestSaveData CaptureQuestData()
        {
            return EnsureQuestService().CaptureSaveData();
        }

        public void RestoreQuestData(QuestSaveData questData)
        {
            ConfigureQuestService(EnsureQuestService());
            Quest.RestoreSaveData(questData);
        }

        internal void BeginQuestRuntimeRestore()
        {
            EnsureQuestService().BeginRuntimeRestore();
        }

        internal void EndQuestRuntimeRestore()
        {
            Quest?.EndRuntimeRestore();
        }

        public bool TrySubmitQuestResources(string questId)
        {
            return EnsureQuestService().TrySubmitResources(questId);
        }

        public bool TrySubmitQuestResources(GameQuestState quest)
        {
            return EnsureQuestService().TrySubmitResources(quest);
        }

        public bool TryAbandonQuest(string questId)
        {
            return EnsureQuestService().TryAbandon(questId);
        }

        public bool TryAbandonQuest(GameQuestState quest)
        {
            return EnsureQuestService().TryAbandon(quest);
        }

        public bool TryClaimQuestRewards(string questId)
        {
            return EnsureQuestService().TryClaimRewards(questId);
        }

        public bool TryClaimQuestRewards(GameQuestState quest)
        {
            return EnsureQuestService().TryClaimRewards(quest);
        }

        private QuestService EnsureQuestService()
        {
            Quest ??= new QuestService(this);
            ConfigureQuestService(Quest);
            return Quest;
        }

        private void ConfigureQuestService(QuestService service)
        {
            service?.Configure(
                startingQuests,
                randomQuestPool,
                runtimeExchangeQuestRules,
                startingRandomQuestCount,
                maxActiveRandomQuests,
                randomQuestRefreshIntervalTurns);
        }

        private void CreateQuestService()
        {
            CreateQuestService(null, false);
        }

        private void CreateQuestService(QuestSaveData saveData, bool emitMessages)
        {
            var service = EnsureQuestService();
            service.Initialize(saveData, emitMessages);
        }

        private void RefreshQuestSubscriptions()
        {
            Quest?.RefreshSubscriptions();
        }

        private void UnsubscribeQuestRuntimeServices()
        {
            Quest?.Dispose();
        }

        internal InventoryService EnsureInventoryServiceForQuest()
        {
            if (Inventory == null)
            {
                CreateInventoryService();
            }

            return Inventory;
        }

        internal GameEventService EnsureGameEventServiceForQuest()
        {
            if (Events == null)
            {
                CreateGameEventService();
            }

            return Events;
        }

        internal void NotifyLegacyQuestsChanged()
        {
            QuestsChanged?.Invoke(this);
        }

        internal void NotifyLegacyQuestRequested(GameQuestState quest)
        {
            QuestEventClicked?.Invoke(this, quest);
        }

        private ItemDefinition FindQuestItemDefinition(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return null;
            }

            var catalog = Inventory == null ? itemCatalog : Inventory.ItemCatalog;
            return catalog != null && catalog.TryGetDefinition(itemId, out var definition) ? definition : null;
        }

        private void NormalizeStartingQuests()
        {
            startingQuests ??= Array.Empty<GameQuestDefinition>();
            for (var i = 0; i < startingQuests.Length; i++)
            {
                startingQuests[i]?.Normalize();
            }
        }

        private void NormalizeRandomQuestPool()
        {
            randomQuestPool ??= Array.Empty<GameQuestDefinition>();
            for (var i = 0; i < randomQuestPool.Length; i++)
            {
                randomQuestPool[i]?.Normalize();
            }
        }

        private void NormalizeRuntimeExchangeQuestRules()
        {
            runtimeExchangeQuestRules ??= Array.Empty<RandomExchangeQuestRule>();
            for (var i = 0; i < runtimeExchangeQuestRules.Length; i++)
            {
                runtimeExchangeQuestRules[i]?.Normalize();
            }
        }
    }
}
