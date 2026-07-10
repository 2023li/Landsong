using System.Collections.Generic;
using Landsong;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_Quest : MonoBehaviour
    {
        [SerializeField, LabelText("关闭按钮")] private Button closeButton;
        [SerializeField, LabelText("任务条目根节点")] private RectTransform root_QuestItem;
        [SerializeField, LabelText("任务条目预制体")] private GamePanel_QuestItem prefab_QuestItem;
        [SerializeField, LabelText("空任务提示根节点")] private GameObject emptyRoot;
        [SerializeField, LabelText("空任务提示文本")] private TMP_Text emptyLabel;
        [SerializeField, LabelText("显示已完成任务")] private bool showCompletedQuests = true;

        private readonly List<GamePanel_QuestItem> activeItems = new List<GamePanel_QuestItem>();
        private readonly List<GamePanel_QuestItem> itemPool = new List<GamePanel_QuestItem>();
        private UIPanel_Game gamePanel;
        private GameSystem gameSystem;
        private bool subscribedToQuests;
        private string selectedQuestId = string.Empty;

        private void Reset()
        {
            root_QuestItem = transform as RectTransform;
            prefab_QuestItem = GetComponentInChildren<GamePanel_QuestItem>(true);
        }

        private void Awake()
        {
            ResolveReferences();
            BindButtons();

            for (int i = 0; i < root_QuestItem.childCount; i++)
            {
                root_QuestItem.GetChild(i).gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeQuests();
            Refresh();
        }

        private void OnDisable()
        {
            UnsubscribeQuests();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            UnsubscribeQuests();
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ResolveReferences();
            SubscribeQuests();
            Refresh();
        }

        public void Show(GameQuestState focusedQuest)
        {
            selectedQuestId = focusedQuest == null ? selectedQuestId : focusedQuest.QuestId;
            Show();
        }

        public void Hide()
        {
            UnsubscribeQuests();
            gameObject.SetActive(false);
        }

        public void Refresh()
        {
            ReleaseActiveItems();

            if (gameSystem == null)
            {
                ResolveReferences();
            }

            if (root_QuestItem == null || prefab_QuestItem == null || gameSystem == null)
            {
                SetEmptyState(true, "任务系统未初始化");
                return;
            }

            var quests = gameSystem.Quests;
            EnsureSelectedQuest(quests);

            var visibleCount = 0;
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (!CanShowQuest(quest))
                {
                    continue;
                }

                var item = GetItemFromPool();
                item.Bind(
                    quest,
                    IsSelectedQuest(quest),
                    HandleQuestSelected,
                    HandleSubmitClicked,
                    HandleAbandonClicked);
                activeItems.Add(item);
                visibleCount++;
            }

            SetEmptyState(visibleCount <= 0, "当前没有任务");
        }

        private void ResolveReferences()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            gameSystem = GameSystem.Instance;

            if (root_QuestItem == null)
            {
                root_QuestItem = transform as RectTransform;
            }

            if (prefab_QuestItem == null)
            {
                prefab_QuestItem = GetComponentInChildren<GamePanel_QuestItem>(true);
            }
        }

        private void BindButtons()
        {
            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(HandleCloseClicked);
            closeButton.onClick.AddListener(HandleCloseClicked);
        }

        private void UnbindButtons()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveListener(HandleCloseClicked);
            }
        }

        private void SubscribeQuests()
        {
            if (subscribedToQuests || gameSystem == null)
            {
                return;
            }

            gameSystem.QuestsChanged += HandleQuestsChanged;
            subscribedToQuests = true;
        }

        private void UnsubscribeQuests()
        {
            if (!subscribedToQuests || gameSystem == null)
            {
                subscribedToQuests = false;
                return;
            }

            gameSystem.QuestsChanged -= HandleQuestsChanged;
            subscribedToQuests = false;
        }

        private bool CanShowQuest(GameQuestState quest)
        {
            if (quest == null)
            {
                return false;
            }

            if (quest.IsRewardClaimed || quest.IsAbandoned)
            {
                return false;
            }

            if (quest.IsCompleted && !showCompletedQuests)
            {
                return false;
            }

            return true;
        }

        private void EnsureSelectedQuest(IReadOnlyList<GameQuestState> quests)
        {
            if (HasVisibleSelectedQuest(quests))
            {
                return;
            }

            selectedQuestId = string.Empty;
            if (quests == null)
            {
                return;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (!CanShowQuest(quest))
                {
                    continue;
                }

                selectedQuestId = quest.QuestId;
                return;
            }
        }

        private bool HasVisibleSelectedQuest(IReadOnlyList<GameQuestState> quests)
        {
            if (string.IsNullOrWhiteSpace(selectedQuestId) || quests == null)
            {
                return false;
            }

            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (IsSelectedQuest(quest) && CanShowQuest(quest))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsSelectedQuest(GameQuestState quest)
        {
            return quest != null
                   && !string.IsNullOrWhiteSpace(selectedQuestId)
                   && string.Equals(quest.QuestId, selectedQuestId, System.StringComparison.Ordinal);
        }

        private GamePanel_QuestItem GetItemFromPool()
        {
            GamePanel_QuestItem item;
            var lastIndex = itemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = itemPool[lastIndex];
                itemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(prefab_QuestItem);
            }

            item.transform.SetParent(root_QuestItem, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveItems()
        {
            for (var i = 0; i < activeItems.Count; i++)
            {
                var item = activeItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Unbind();
                item.gameObject.SetActive(false);
                item.transform.SetParent(root_QuestItem, false);
                itemPool.Add(item);
            }

            activeItems.Clear();
        }

        private void HandleQuestSelected(GameQuestState quest)
        {
            if (quest == null)
            {
                return;
            }

            selectedQuestId = quest.QuestId;
            Refresh();
        }

        private void HandleSubmitClicked(GameQuestState quest)
        {
            if (gameSystem == null || quest == null)
            {
                return;
            }

            if (quest.CanClaimRewards)
            {
                gameSystem.TryClaimQuestRewards(quest);
            }
            else
            {
                gameSystem.TrySubmitQuestResources(quest);
            }

            Refresh();
        }

        private void HandleAbandonClicked(GameQuestState quest)
        {
            if (gameSystem == null || quest == null)
            {
                return;
            }

            gameSystem.TryAbandonQuest(quest);
            Refresh();
        }

        private void HandleQuestsChanged(GameSystem changedGameSystem)
        {
            gameSystem = changedGameSystem;
            Refresh();
        }

        private void HandleCloseClicked()
        {
            if (gamePanel != null)
            {
                gamePanel.Hide_Quest();
                return;
            }

            Hide();
        }

        private void SetEmptyState(bool visible, string message)
        {
            if (emptyRoot != null)
            {
                emptyRoot.SetActive(visible);
            }

            if (emptyLabel != null)
            {
                emptyLabel.text = visible ? message : string.Empty;
            }
        }
    }
}
