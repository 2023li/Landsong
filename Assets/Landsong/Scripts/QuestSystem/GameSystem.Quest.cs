using System;
using Landsong.GameEventSystem;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong
{
    /// <summary>
    /// GameSystem 上仅保留任务目录引用、起始任务 ID 和服务装配；任务公开 API 统一由 GameSystem.Services.Quest 提供。
    /// </summary>
    public sealed partial class GameSystem
    {
        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("任务目录")]
        private QuestCatalog questCatalog;

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("起始任务 ID")]
        private string[] startingQuestIds = Array.Empty<string>();

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("开局随机任务数量"), Min(0)]
        private int startingRandomQuestCount = 1;

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("随机任务同时存在上限"), Min(0)]
        private int maxActiveRandomQuests = 2;

        [SerializeField, FoldoutGroup(InspectorQuest), LabelText("随机任务补充间隔回合"), Min(1)]
        private int randomQuestRefreshIntervalTurns = 3;

        internal ItemCatalog QuestItemCatalog => itemCatalog;
        internal QuestCatalog QuestCatalog => questCatalog;

        [Button("重新初始化任务")]
        private void ReinitializeQuests()
        {
            CreateQuestService();
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
                questCatalog,
                startingQuestIds,
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

        private void NormalizeQuestConfiguration()
        {
            startingQuestIds ??= Array.Empty<string>();
            for (var i = 0; i < startingQuestIds.Length; i++)
            {
                startingQuestIds[i] = GameQuestDefinition.NormalizeQuestId(startingQuestIds[i]);
            }
        }
    }
}
