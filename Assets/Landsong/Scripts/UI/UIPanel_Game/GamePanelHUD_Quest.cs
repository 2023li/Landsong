using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanelHUD_Quest : MonoBehaviour
    {
        [SerializeField] private TMP_Text txt_任务名称;
        [SerializeField] private TMP_Text txt_剩余回合数;
        [SerializeField] private Transform root_任务要求父对象;
        [SerializeField] private GameObject root_任务整体已完成;
        [SerializeField, LabelText("任务要求Item预制体")] private GamePanelItem_Quest_Requirement prefab_任务要求Item;
        [SerializeField, LabelText("打开任务面板按钮")] private Button btn_打开任务面板;
        [SerializeField, LabelText("领取奖励按钮")] private Button btn_领取奖励;

        private readonly List<GamePanelItem_Quest_Requirement> activeRequirementItems =
            new List<GamePanelItem_Quest_Requirement>();
        private readonly List<GamePanelItem_Quest_Requirement> requirementItemPool =
            new List<GamePanelItem_Quest_Requirement>();

        private UIPanel_Game gamePanel;
        private GameSystem gameSystem;
        private GameSystem subscribedGameSystem;
        private GameQuestState currentQuest;

        private void Reset()
        {
            txt_任务名称 = GetComponentInChildren<TMP_Text>(true);
            root_任务要求父对象 = transform;
            prefab_任务要求Item = GetComponentInChildren<GamePanelItem_Quest_Requirement>(true);
            btn_打开任务面板 = GetComponent<Button>();
        }

        private void Awake()
        {
            ResolveStaticReferences();
            BindButton();

            for (int i = 0; i < root_任务要求父对象.childCount; i++)
            {
                root_任务要求父对象.GetChild(i).gameObject.SetActive(false);
            }
        }

        private void OnEnable()
        {
            ResolveRuntimeReferences();
            SubscribeGameSystem();
            RefreshFromGameSystem();
        }

        private void OnDisable()
        {
            UnsubscribeGameSystem();
        }

        private void OnDestroy()
        {
            UnsubscribeGameSystem();
            UnbindButton();
        }

        public void RefreshFromGameSystem()
        {
            ResolveRuntimeReferences();
            Bind(SelectHudQuest());
        }

        public void Bind(GameQuestState quest)
        {
            currentQuest = quest;
            ReleaseActiveRequirementItems();

            if (quest == null || quest.Definition == null)
            {
                SetText(txt_任务名称, string.Empty);
                SetText(txt_剩余回合数, string.Empty);
                SetActive(root_任务整体已完成, false);
                SetClaimButtonVisible(false);
                SetButtonInteractable(false);
                return;
            }

            SetText(txt_任务名称, quest.Definition.DisplayName);
            SetText(txt_剩余回合数, FormatRemainingTurns(quest));
            SetActive(root_任务整体已完成, quest.CanClaimRewards);
            SetClaimButtonVisible(quest.CanClaimRewards);
            SetButtonInteractable(true);
            RenderRequirements(quest);
        }

        private void ResolveStaticReferences()
        {
            if (root_任务要求父对象 == null)
            {
                root_任务要求父对象 = transform;
            }

            if (prefab_任务要求Item == null && root_任务要求父对象 != null)
            {
                prefab_任务要求Item = root_任务要求父对象.GetComponentInChildren<GamePanelItem_Quest_Requirement>(true);
            }

            if (prefab_任务要求Item != null && prefab_任务要求Item.transform.IsChildOf(transform))
            {
                prefab_任务要求Item.gameObject.SetActive(false);
            }

            if (btn_打开任务面板 == null)
            {
                btn_打开任务面板 = GetComponent<Button>();
            }
        }

        private void ResolveRuntimeReferences()
        {
            gamePanel = gamePanel == null ? GetComponentInParent<UIPanel_Game>() : gamePanel;
            gameSystem = GameSystem.Instance;
        }

        private void BindButton()
        {
            if (btn_打开任务面板 == null)
            {
                return;
            }

            btn_打开任务面板.onClick.RemoveListener(HandleOpenQuestPanelClicked);
            btn_打开任务面板.onClick.AddListener(HandleOpenQuestPanelClicked);

            if (btn_领取奖励 != null)
            {
                btn_领取奖励.onClick.RemoveListener(HandleClaimRewardsClicked);
                btn_领取奖励.onClick.AddListener(HandleClaimRewardsClicked);
            }
        }

        private void UnbindButton()
        {
            if (btn_打开任务面板 != null)
            {
                btn_打开任务面板.onClick.RemoveListener(HandleOpenQuestPanelClicked);
            }

            if (btn_领取奖励 != null)
            {
                btn_领取奖励.onClick.RemoveListener(HandleClaimRewardsClicked);
            }
        }

        private void SubscribeGameSystem()
        {
            if (gameSystem == null || subscribedGameSystem == gameSystem)
            {
                return;
            }

            UnsubscribeGameSystem();
            gameSystem.Services.Quest.StateChanged += HandleQuestsChanged;
            gameSystem.Services.Quest.QuestRequested += HandleQuestEventClicked;
            subscribedGameSystem = gameSystem;
        }

        private void UnsubscribeGameSystem()
        {
            if (subscribedGameSystem == null)
            {
                return;
            }

            subscribedGameSystem.Services.Quest.StateChanged -= HandleQuestsChanged;
            subscribedGameSystem.Services.Quest.QuestRequested -= HandleQuestEventClicked;
            subscribedGameSystem = null;
        }

        private void HandleQuestsChanged(QuestService changedQuestService)
        {
            RefreshFromGameSystem();
        }

        private void HandleQuestEventClicked(QuestService changedQuestService, GameQuestState quest)
        {
            if (quest == null)
            {
                return;
            }

            gamePanel = gamePanel == null ? GetComponentInParent<UIPanel_Game>() : gamePanel;
            gamePanel?.Show_Quest(quest);
        }

        private GameQuestState SelectHudQuest()
        {
            var quests = gameSystem == null ? null : gameSystem.Services.Quest.Quests;
            if (quests == null || quests.Count == 0)
            {
                return null;
            }

            GameQuestState selectedActiveQuest = null;
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || !quest.IsActive)
                {
                    continue;
                }

                if (selectedActiveQuest == null || CompareActiveQuests(quest, selectedActiveQuest) < 0)
                {
                    selectedActiveQuest = quest;
                }
            }

            if (selectedActiveQuest != null)
            {
                return selectedActiveQuest;
            }

            GameQuestState selectedCompletedQuest = null;
            GameQuestState selectedFailedQuest = null;
            for (var i = 0; i < quests.Count; i++)
            {
                var quest = quests[i];
                if (quest == null || quest.IsRewardClaimed || quest.IsAbandoned)
                {
                    continue;
                }

                if (quest.CanClaimRewards)
                {
                    if (selectedCompletedQuest == null || CompareFinishedQuests(quest, selectedCompletedQuest) < 0)
                    {
                        selectedCompletedQuest = quest;
                    }
                    continue;
                }

                if (quest.IsFailed
                    && (selectedFailedQuest == null
                        || CompareFinishedQuests(quest, selectedFailedQuest) < 0))
                {
                    selectedFailedQuest = quest;
                }
            }

            return selectedCompletedQuest ?? selectedFailedQuest;
        }

        private void RenderRequirements(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return;
            }

            switch (quest.Definition.ObjectiveType)
            {
                case QuestObjectiveType.BuildBuildings:
                    AddBuildRequirement(quest);
                    break;
                case QuestObjectiveType.SubmitResources:
                    AddResourceRequirements(quest);
                    break;
                default:
                    AddRequirement($"{quest.CurrentAmount}/{quest.TargetAmount}", quest.IsCompleted);
                    break;
            }
        }

        private void AddBuildRequirement(GameQuestState quest)
        {
            var targetName = string.IsNullOrWhiteSpace(quest.TargetDisplayName)
                ? "目标建筑"
                : quest.TargetDisplayName;
            var targetAmount = Mathf.Max(1, quest.TargetAmount);
            var currentAmount = Mathf.Clamp(quest.CurrentAmount, 0, targetAmount);
            var isCompleted = quest.IsCompleted || currentAmount >= targetAmount;
            AddRequirement($"建造 {targetName}：{currentAmount}/{targetAmount}", isCompleted);
        }

        private void AddResourceRequirements(GameQuestState quest)
        {
            var resources = quest.ResourceProgresses;
            for (var i = 0; i < resources.Count; i++)
            {
                var progress = resources[i];
                if (progress == null)
                {
                    continue;
                }

                var text = "提交 " + ResourceRichTextFormatter.FormatProgress(
                    progress.ItemDefinition,
                    progress.ItemId,
                    progress.DisplayName,
                    Mathf.Clamp(progress.SubmittedAmount, 0, progress.RequiredAmount),
                    Mathf.Max(0, progress.RequiredAmount),
                    progress.InventoryAmount,
                    false);
                var isCompleted = quest.IsCompleted || progress.IsComplete;
                AddRequirement(text, isCompleted);
            }
        }

        private void AddRequirement(string requirementText, bool isCompleted)
        {
            var item = GetRequirementItemFromPool();
            if (item == null)
            {
                return;
            }

            item.Bind(requirementText, isCompleted);
            activeRequirementItems.Add(item);
        }

        private GamePanelItem_Quest_Requirement GetRequirementItemFromPool()
        {
            ResolveStaticReferences();
            if (prefab_任务要求Item == null || root_任务要求父对象 == null)
            {
                return null;
            }

            GamePanelItem_Quest_Requirement item;
            var lastIndex = requirementItemPool.Count - 1;
            if (lastIndex >= 0)
            {
                item = requirementItemPool[lastIndex];
                requirementItemPool.RemoveAt(lastIndex);
            }
            else
            {
                item = Instantiate(prefab_任务要求Item);
            }

            item.transform.SetParent(root_任务要求父对象, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void ReleaseActiveRequirementItems()
        {
            for (var i = 0; i < activeRequirementItems.Count; i++)
            {
                var item = activeRequirementItems[i];
                if (item == null)
                {
                    continue;
                }

                item.Clear();
                item.gameObject.SetActive(false);
                item.transform.SetParent(root_任务要求父对象 == null ? transform : root_任务要求父对象, false);
                requirementItemPool.Add(item);
            }

            activeRequirementItems.Clear();
        }

        private void HandleOpenQuestPanelClicked()
        {
            if (gamePanel == null)
            {
                gamePanel = GetComponentInParent<UIPanel_Game>();
            }

            if (currentQuest == null)
            {
                gamePanel?.Show_Quest();
                return;
            }

            gamePanel?.Show_Quest(currentQuest);
        }

        private void HandleClaimRewardsClicked()
        {
            if (gameSystem == null || currentQuest == null || !currentQuest.CanClaimRewards)
            {
                return;
            }

            gameSystem.Services.Quest.TryClaimRewards(currentQuest);
        }

        private void SetButtonInteractable(bool interactable)
        {
            if (btn_打开任务面板 != null)
            {
                btn_打开任务面板.interactable = interactable;
            }
        }

        private void SetClaimButtonVisible(bool visible)
        {
            if (btn_领取奖励 == null)
            {
                return;
            }

            btn_领取奖励.gameObject.SetActive(visible);
            btn_领取奖励.interactable = visible;
        }

        private static int CompareActiveQuests(GameQuestState left, GameQuestState right)
        {
            var categoryCompare = GetQuestCategoryPriority(left).CompareTo(GetQuestCategoryPriority(right));
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            var leftDeadline = left == null ? int.MaxValue : left.DeadlineTurn;
            var rightDeadline = right == null ? int.MaxValue : right.DeadlineTurn;
            if (leftDeadline != rightDeadline)
            {
                return leftDeadline.CompareTo(rightDeadline);
            }

            var leftCanSubmit = left != null && left.CanSubmitResources();
            var rightCanSubmit = right != null && right.CanSubmitResources();
            if (leftCanSubmit != rightCanSubmit)
            {
                return leftCanSubmit ? -1 : 1;
            }

            return CompareQuestName(left, right);
        }

        private static int CompareFinishedQuests(GameQuestState left, GameQuestState right)
        {
            var categoryCompare = GetQuestCategoryPriority(left).CompareTo(GetQuestCategoryPriority(right));
            if (categoryCompare != 0)
            {
                return categoryCompare;
            }

            var statusCompare = GetFinishedStatusPriority(left).CompareTo(GetFinishedStatusPriority(right));
            if (statusCompare != 0)
            {
                return statusCompare;
            }

            var leftDeadline = left == null ? int.MinValue : left.DeadlineTurn;
            var rightDeadline = right == null ? int.MinValue : right.DeadlineTurn;
            if (leftDeadline != rightDeadline)
            {
                return rightDeadline.CompareTo(leftDeadline);
            }

            return CompareQuestName(left, right);
        }

        private static int GetQuestCategoryPriority(GameQuestState quest)
        {
            if (quest == null)
            {
                return int.MaxValue;
            }

            return quest.IsMainline ? 0 : 1;
        }

        private static int GetFinishedStatusPriority(GameQuestState quest)
        {
            if (quest == null)
            {
                return int.MaxValue;
            }

            if (quest.IsFailed)
            {
                return 0;
            }

            return quest.IsCompleted ? 1 : 2;
        }

        private static int CompareQuestName(GameQuestState left, GameQuestState right)
        {
            return string.Compare(
                left == null || left.Definition == null ? string.Empty : left.Definition.DisplayName,
                right == null || right.Definition == null ? string.Empty : right.Definition.DisplayName,
                StringComparison.Ordinal);
        }

        private static string FormatRemainingTurns(GameQuestState quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (quest.IsCompleted)
            {
                return "已完成";
            }

            var gameSystem = GameSystem.Instance;
            var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.Services.Turn.CurrentTurn;
            return $"剩余 {quest.GetRemainingTurns(currentTurn)} 回合";
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
