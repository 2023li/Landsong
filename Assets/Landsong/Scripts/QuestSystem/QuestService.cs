using System;
using System.Collections.Generic;

namespace Landsong
{
    /// <summary>
    /// 任务领域的公开入口。GameSystem 上的旧任务 API 暂时保留为兼容门面，
    /// 新代码应优先通过 GameSystem.Services.Quest 使用任务系统。
    /// </summary>
    public sealed class QuestService
    {
        private readonly GameSystem context;

        internal QuestService(GameSystem context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public event Action<QuestService> StateChanged;
        public event Action<QuestService, GameQuestState> QuestRequested;

        public IReadOnlyList<GameQuestState> Quests => context.Quests;

        public GameQuestState Find(string questId)
        {
            questId = GameQuestDefinition.NormalizeQuestId(questId);
            if (string.IsNullOrEmpty(questId))
            {
                return null;
            }

            var quests = context.Quests;
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

        public QuestSaveData CaptureSaveData()
        {
            return context.CaptureQuestData();
        }

        public void RestoreSaveData(QuestSaveData saveData)
        {
            context.RestoreQuestData(saveData);
        }

        public void BeginRuntimeRestore()
        {
            context.BeginQuestRuntimeRestore();
        }

        public void EndRuntimeRestore()
        {
            context.EndQuestRuntimeRestore();
        }

        public void Reinitialize()
        {
            context.ReinitializeQuests();
        }

        public bool TrySubmitResources(string questId)
        {
            return context.TrySubmitQuestResources(questId);
        }

        public bool TrySubmitResources(GameQuestState quest)
        {
            return context.TrySubmitQuestResources(quest);
        }

        public bool TryAbandon(string questId)
        {
            return context.TryAbandonQuest(questId);
        }

        public bool TryAbandon(GameQuestState quest)
        {
            return context.TryAbandonQuest(quest);
        }

        public bool TryClaimRewards(string questId)
        {
            return context.TryClaimQuestRewards(questId);
        }

        public bool TryClaimRewards(GameQuestState quest)
        {
            return context.TryClaimQuestRewards(quest);
        }

        public bool TryAddDebugRandomQuest(out GameQuestState quest)
        {
            return context.TryAddGameplayDebugRandomQuest(out quest);
        }

        internal void NotifyStateChanged()
        {
            StateChanged?.Invoke(this);
        }

        internal void NotifyQuestRequested(GameQuestState quest)
        {
            QuestRequested?.Invoke(this, quest);
        }
    }
}
