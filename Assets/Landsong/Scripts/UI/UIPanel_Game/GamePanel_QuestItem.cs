using System;
using System.Collections.Generic;
using Landsong;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_QuestItem : MonoBehaviour
    {
        [SerializeField, LabelText("选择按钮")] private Button selectButton;
        [SerializeField, LabelText("任务操作按钮")] private Button submitButton;
        [SerializeField, LabelText("任务操作按钮文本")] private TMP_Text submitButtonLabel;
        [SerializeField, LabelText("放弃任务按钮")] private Button abandonButton;
        [SerializeField, LabelText("放弃任务按钮文本")] private TMP_Text abandonButtonLabel;
        [SerializeField, LabelText("图标")] private Image icon;
        [SerializeField, LabelText("任务名称")] private TMP_Text titleLabel;
        [SerializeField, LabelText("任务描述")] private TMP_Text descriptionLabel;
        [SerializeField, LabelText("期限文本")] private TMP_Text deadlineLabel;
        [SerializeField, LabelText("任务要求根节点")] private RectTransform requirementRoot;
        [SerializeField, LabelText("任务要求Item预制体")] private GamePanelItem_Quest_Requirement requirementItemPrefab;
        [SerializeField, LabelText("任务操作根节点")] private GameObject actionRoot;
        [SerializeField, LabelText("详情根节点")] private GameObject detailsRoot;
        [SerializeField, LabelText("选中状态根节点")] private GameObject selectedRoot;
        [SerializeField, LabelText("已完成状态根节点")] private GameObject completedRoot;

        private GameQuestState quest;
        private Action<GameQuestState> selected;
        private Action<GameQuestState> submitClicked;
        private Action<GameQuestState> abandoned;
        private readonly List<GamePanelItem_Quest_Requirement> activeRequirementItems =
            new List<GamePanelItem_Quest_Requirement>();
        private readonly List<GamePanelItem_Quest_Requirement> requirementItemPool =
            new List<GamePanelItem_Quest_Requirement>();
        private bool isSelected;

        private void Reset()
        {
            selectButton = GetComponent<Button>();
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnDestroy()
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
            }

            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(HandleSubmitClicked);
            }

            if (abandonButton != null)
            {
                abandonButton.onClick.RemoveListener(HandleAbandoned);
            }
        }

        public void Bind(
            GameQuestState sourceQuest,
            bool selectedState,
            Action<GameQuestState> onSelected,
            Action<GameQuestState> onSubmitClicked,
            Action<GameQuestState> onAbandoned)
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
            }

            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(HandleSubmitClicked);
            }

            if (abandonButton != null)
            {
                abandonButton.onClick.RemoveListener(HandleAbandoned);
            }

            quest = sourceQuest;
            isSelected = selectedState;
            selected = onSelected;
            submitClicked = onSubmitClicked;
            abandoned = onAbandoned;
            Refresh();

            if (selectButton != null)
            {
                selectButton.interactable = quest != null;
                selectButton.onClick.AddListener(HandleSelected);
            }

            if (submitButton != null)
            {
                submitButton.onClick.AddListener(HandleSubmitClicked);
            }

            if (abandonButton != null)
            {
                abandonButton.onClick.AddListener(HandleAbandoned);
            }
        }

        public void Unbind()
        {
            ReleaseActiveRequirementItems();

            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
                selectButton.interactable = false;
            }

            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(HandleSubmitClicked);
                submitButton.interactable = false;
            }

            if (abandonButton != null)
            {
                abandonButton.onClick.RemoveListener(HandleAbandoned);
                abandonButton.interactable = false;
                abandonButton.gameObject.SetActive(false);
            }

            quest = null;
            selected = null;
            submitClicked = null;
            abandoned = null;
            isSelected = false;
            SetText(titleLabel, string.Empty);
            SetText(descriptionLabel, string.Empty);
            SetText(deadlineLabel, string.Empty);
            SetActive(requirementRoot == null ? null : requirementRoot.gameObject, false);
            SetActive(actionRoot, false);
            SetActive(detailsRoot, false);
            SetActive(selectedRoot, false);
            SetActive(completedRoot, false);
            SetActive(submitButton == null ? null : submitButton.gameObject, false);
            SetIcon(null);
        }

        public void Refresh()
        {
            ReleaseActiveRequirementItems();
            if (quest == null || quest.Definition == null)
            {
                Unbind();
                return;
            }

            SetText(titleLabel, FormatTitle(quest));
            SetText(descriptionLabel, quest.Definition.Description);
            SetText(deadlineLabel, FormatDeadline(quest));
            RenderRequirements(quest);
            SetDetailsVisible(isSelected);
            SetActive(selectedRoot, isSelected);
            SetActive(completedRoot, quest.IsCompleted);
            SetIcon(quest.Icon);
            RefreshActionButtons();
        }

        private void RefreshActionButtons()
        {
            var canSubmitResources = quest != null && quest.CanSubmitResources();
            var canClaimRewards = quest != null && quest.CanClaimRewards;
            var canAbandon = quest != null && (quest.IsActive || quest.IsFailed);
            var showPrimaryAction = quest != null
                                    && isSelected
                                    && (canClaimRewards
                                        || (quest.IsResourceSubmission && quest.IsActive));

            if (submitButton != null)
            {
                submitButton.gameObject.SetActive(showPrimaryAction);
                submitButton.interactable = canClaimRewards || canSubmitResources;

                var actionLabel = canClaimRewards
                    ? "领取奖励"
                    : canSubmitResources
                        ? "提交"
                        : "资源不足";
                SetText(submitButtonLabel, actionLabel);
            }

            if (abandonButton != null)
            {
                var showAbandon = isSelected && canAbandon;
                abandonButton.gameObject.SetActive(showAbandon);
                abandonButton.interactable = canAbandon;
                SetText(abandonButtonLabel, "放弃任务");
            }

            SetActive(actionRoot, isSelected && (showPrimaryAction || canAbandon));
        }

        private void HandleSelected()
        {
            selected?.Invoke(quest);
        }

        private void HandleSubmitClicked()
        {
            submitClicked?.Invoke(quest);
        }

        private void HandleAbandoned()
        {
            abandoned?.Invoke(quest);
        }

        private void SetDetailsVisible(bool visible)
        {
            if (detailsRoot != null)
            {
                detailsRoot.SetActive(visible);
                return;
            }

            SetGameObjectActive(descriptionLabel, visible);
            SetActive(requirementRoot == null ? null : requirementRoot.gameObject, visible);
        }

        private static string FormatTitle(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return string.Empty;
            }

            return $"[{quest.CategoryDisplayName}] {quest.Definition.DisplayName}";
        }

        private static string FormatDeadline(GameQuestState quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (quest.IsCompleted)
            {
                return $"完成于期限内：截止第 {quest.DeadlineTurn} 回合";
            }

            if (quest.IsFailed)
            {
                return "剩余 0 回合";
            }

            var gameSystem = GameSystem.Instance;
            var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.CurrentTurn;
            return $"截止第 {quest.DeadlineTurn} 回合，剩余 {quest.GetRemainingTurns(currentTurn)} 回合";
        }

        private void RenderRequirements(GameQuestState sourceQuest)
        {
            if (sourceQuest == null || sourceQuest.Definition == null)
            {
                return;
            }

            switch (sourceQuest.Definition.ObjectiveType)
            {
                case QuestObjectiveType.BuildBuildings:
                    AddBuildRequirement(sourceQuest);
                    break;
                case QuestObjectiveType.SubmitResources:
                    AddResourceRequirements(sourceQuest);
                    break;
            }
        }

        private void AddBuildRequirement(GameQuestState sourceQuest)
        {
            var targetName = string.IsNullOrWhiteSpace(sourceQuest.TargetDisplayName)
                ? "目标"
                : sourceQuest.TargetDisplayName;
            var targetAmount = Mathf.Max(1, sourceQuest.TargetAmount);
            var currentAmount = Mathf.Clamp(sourceQuest.CurrentAmount, 0, targetAmount);
            AddRequirement(
                $"建造 {targetName}：{currentAmount}/{targetAmount}",
                sourceQuest.IsCompleted || currentAmount >= targetAmount);
        }

        private void AddResourceRequirements(GameQuestState sourceQuest)
        {
            var resources = sourceQuest.ResourceProgresses;
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
                AddRequirement(text, sourceQuest.IsCompleted || progress.IsComplete);
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
            EnsureRequirementItemPool();
            if (requirementRoot == null)
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
            else if (requirementItemPrefab != null)
            {
                item = Instantiate(requirementItemPrefab, requirementRoot);
            }
            else
            {
                return null;
            }

            item.transform.SetParent(requirementRoot, false);
            item.gameObject.SetActive(true);
            return item;
        }

        private void EnsureRequirementItemPool()
        {
            if (requirementRoot == null)
            {
                return;
            }

            var items = requirementRoot.GetComponentsInChildren<GamePanelItem_Quest_Requirement>(true);
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (activeRequirementItems.Contains(item) || requirementItemPool.Contains(item))
                {
                    continue;
                }

                item.Clear();
                item.gameObject.SetActive(false);
                requirementItemPool.Add(item);
            }
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
                if (requirementRoot != null)
                {
                    item.transform.SetParent(requirementRoot, false);
                }

                if (!requirementItemPool.Contains(item))
                {
                    requirementItemPool.Add(item);
                }
            }

            activeRequirementItems.Clear();
        }

        private void SetIcon(Sprite sprite)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.enabled = sprite != null;
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static void SetGameObjectActive(Component target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
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
