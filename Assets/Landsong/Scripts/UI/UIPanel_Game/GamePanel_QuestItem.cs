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
        private const int MaxRewardColumns = 4;

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
        [SerializeField, LabelText("任务奖励根节点")] private RectTransform rewardRoot;
        [SerializeField, LabelText("任务奖励文本预制体")] private TMP_Text rewardTextPrefab;
        [SerializeField, LabelText("任务奖励布局")] private GridLayoutGroup rewardLayout;
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
        private readonly List<TMP_Text> activeRewardTexts = new List<TMP_Text>();
        private readonly List<TMP_Text> rewardTextPool = new List<TMP_Text>();
        private bool isSelected;
        private int requirementRenderIndex;

        private void OnRectTransformDimensionsChange()
        {
            RefreshRewardLayout();
        }

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
            ReleaseActiveRewardTexts();

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
            SetActive(rewardRoot == null ? null : rewardRoot.gameObject, false);
            SetActive(actionRoot, false);
            SetActive(detailsRoot, false);
            SetActive(selectedRoot, false);
            SetActive(completedRoot, false);
            SetActive(submitButton == null ? null : submitButton.gameObject, false);
            SetIcon(null);
        }

        public void Refresh()
        {
            ReleaseActiveRewardTexts();
            if (quest == null || quest.Definition == null)
            {
                Unbind();
                return;
            }

            SetText(titleLabel, FormatTitle(quest));
            SetText(descriptionLabel, quest.Definition.Description);
            SetText(deadlineLabel, FormatDeadline(quest));
            requirementRenderIndex = 0;
            RenderRequirements(quest);
            ReleaseSurplusRequirementItems(requirementRenderIndex);
            RenderRewards(quest);
            SetDetailsVisible(isSelected);
            SetActive(selectedRoot, isSelected);
            SetActive(completedRoot, quest.IsCompleted);
            SetIcon(quest.Icon);
            RefreshActionButtons();
            RefreshRewardLayout();
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
                    ? Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.claim_rewards", "领取奖励")
                    : canSubmitResources
                        ? Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.submit", "提交")
                        : Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.resources_missing", "资源不足");
                SetText(submitButtonLabel, actionLabel);
            }

            if (abandonButton != null)
            {
                var showAbandon = isSelected && canAbandon;
                abandonButton.gameObject.SetActive(showAbandon);
                abandonButton.interactable = canAbandon;
                SetText(abandonButtonLabel, Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.abandon", "放弃任务"));
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
            SetActive(rewardRoot == null ? null : rewardRoot.gameObject, visible && activeRewardTexts.Count > 0);
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

            if (!quest.IsTimed)
            {
                return "∞";
            }

            if (quest.IsCompleted)
            {
                return Landsong.Localization.L10n.Gameplay(
                    "gameplay.quest.ui.completed_by_deadline",
                    "完成于期限内：截止第 {0} 回合",
                    quest.DeadlineTurn);
            }

            if (quest.IsFailed)
            {
                return Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.remaining_turns", "剩余 {0} 回合", 0);
            }

            var gameSystem = GameSystem.Instance;
            var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.Services.Turn.CurrentTurn;
            return Landsong.Localization.L10n.Gameplay(
                "gameplay.quest.ui.deadline",
                "截止第 {0} 回合，剩余 {1} 回合",
                quest.DeadlineTurn,
                quest.GetRemainingTurns(currentTurn));
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
                case QuestObjectiveType.MoveCamera:
                    AddNumericRequirement(sourceQuest, Landsong.Localization.L10n.Gameplay("gameplay.quest.target.move_camera", "移动视野"));
                    break;
                case QuestObjectiveType.CollectResources:
                    AddResourceRequirements(sourceQuest);
                    break;
                case QuestObjectiveType.PlantCrops:
                    AddNumericRequirement(sourceQuest, Landsong.Localization.L10n.Gameplay("gameplay.quest.target.plant_crops", "播种农田"));
                    break;
                case QuestObjectiveType.SelectTechnology:
                    AddNumericRequirement(sourceQuest, Landsong.Localization.L10n.Gameplay("gameplay.quest.target.select_technology", "选择研究科技"));
                    break;
            }
        }

        private void AddBuildRequirement(GameQuestState sourceQuest)
        {
            var targetName = string.IsNullOrWhiteSpace(sourceQuest.TargetDisplayName)
                ? Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.target", "目标")
                : sourceQuest.TargetDisplayName;
            var targetAmount = Mathf.Max(1, sourceQuest.TargetAmount);
            var currentAmount = Mathf.Clamp(sourceQuest.CurrentAmount, 0, targetAmount);
            AddRequirement(
                Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.build_progress", "建造 {0}：{1}/{2}", targetName, currentAmount, targetAmount),
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

                var progressText = ResourceRichTextFormatter.FormatProgress(
                    progress.ItemDefinition,
                    progress.ItemId,
                    progress.DisplayName,
                    Mathf.Clamp(progress.ProgressAmount, 0, progress.RequiredAmount),
                    Mathf.Max(0, progress.RequiredAmount),
                    progress.InventoryAmount,
                    false);
                var text = sourceQuest.IsResourceCollection
                    ? Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.collect_progress", "收集 {0}", progressText)
                    : Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.submit_progress", "提交 {0}", progressText);
                AddRequirement(text, sourceQuest.IsCompleted || progress.IsComplete);
            }
        }

        private void AddNumericRequirement(GameQuestState sourceQuest, string action)
        {
            var targetAmount = Mathf.Max(1, sourceQuest.TargetAmount);
            var currentAmount = Mathf.Clamp(sourceQuest.CurrentAmount, 0, targetAmount);
            AddRequirement(
                Landsong.Localization.L10n.Gameplay("gameplay.quest.ui.numeric_progress", "{0}：{1}/{2}", action, currentAmount, targetAmount),
                sourceQuest.IsCompleted || currentAmount >= targetAmount);
        }

        private void AddRequirement(string requirementText, bool isCompleted)
        {
            var item = GetRequirementItemForRender(requirementRenderIndex);
            if (item == null)
            {
                return;
            }

            item.Bind(requirementText, isCompleted);
            item.SetTextAlignment(TextAlignmentOptions.Center);
            requirementRenderIndex++;
        }

        private void RenderRewards(GameQuestState sourceQuest)
        {
            if (sourceQuest == null || sourceQuest.Definition == null || !sourceQuest.Definition.HasRewards)
            {
                return;
            }

            var rewards = sourceQuest.Definition.Rewards;
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i].Normalized();
                if (!reward.IsValid)
                {
                    continue;
                }

                var text = ResourceRichTextFormatter.FormatAmount(
                    reward.ItemDefinition,
                    reward.ItemId,
                    reward.ItemDefinition == null ? reward.ItemId : reward.ItemDefinition.DisplayName,
                    reward.Amount);
                AddReward(text);
            }

            var unlockedFeatures = sourceQuest.Definition.UnlockedFeatures;
            for (var i = 0; i < unlockedFeatures.Count; i++)
            {
                var feature = unlockedFeatures[i];
                if (GameFeatureUnlockService.IsValid(feature))
                {
                    AddReward(Landsong.Localization.L10n.Gameplay(
                        "gameplay.quest.ui.unlock_reward",
                        "解锁 {0}",
                        GameFeatureUnlockService.GetDisplayName(feature)));
                }
            }
        }

        private void AddReward(string rewardText)
        {
            var text = GetRewardTextFromPool();
            if (text == null)
            {
                return;
            }

            ResourceRichTextFormatter.ApplySpriteAsset(text);
            SetText(text, rewardText);
            activeRewardTexts.Add(text);
        }

        private TMP_Text GetRewardTextFromPool()
        {
            if (rewardRoot == null)
            {
                return null;
            }

            TMP_Text text;
            var lastIndex = rewardTextPool.Count - 1;
            if (lastIndex >= 0)
            {
                text = rewardTextPool[lastIndex];
                rewardTextPool.RemoveAt(lastIndex);
            }
            else if (rewardTextPrefab != null)
            {
                text = Instantiate(rewardTextPrefab, rewardRoot);
            }
            else
            {
                return null;
            }

            text.transform.SetParent(rewardRoot, false);
            text.gameObject.SetActive(true);
            return text;
        }

        private void ReleaseActiveRewardTexts()
        {
            for (var i = activeRewardTexts.Count - 1; i >= 0; i--)
            {
                var text = activeRewardTexts[i];
                if (text == null)
                {
                    continue;
                }

                SetText(text, string.Empty);
                text.gameObject.SetActive(false);
                if (rewardRoot != null)
                {
                    text.transform.SetParent(rewardRoot, false);
                }

                if (!rewardTextPool.Contains(text))
                {
                    rewardTextPool.Add(text);
                }
            }

            activeRewardTexts.Clear();
        }

        private void RefreshRewardLayout()
        {
            if (rewardLayout == null || rewardRoot == null || activeRewardTexts.Count <= 0)
            {
                return;
            }

            var columnCount = Mathf.Clamp(activeRewardTexts.Count, 1, MaxRewardColumns);
            rewardLayout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            rewardLayout.constraintCount = columnCount;

            var availableWidth = rewardRoot.rect.width
                                 - rewardLayout.padding.left
                                 - rewardLayout.padding.right
                                 - rewardLayout.spacing.x * Mathf.Max(0, columnCount - 1);
            if (availableWidth <= 0f)
            {
                return;
            }

            var cellSize = rewardLayout.cellSize;
            cellSize.x = availableWidth / columnCount;
            rewardLayout.cellSize = cellSize;
            LayoutRebuilder.MarkLayoutForRebuild(rewardRoot);
        }

        private GamePanelItem_Quest_Requirement GetRequirementItemForRender(int index)
        {
            EnsureRequirementItemPool();
            if (requirementRoot == null)
            {
                return null;
            }

            if (index >= 0 && index < activeRequirementItems.Count)
            {
                return activeRequirementItems[index];
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
            if (!item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(true);
            }

            activeRequirementItems.Add(item);
            return item;
        }

        private void ReleaseSurplusRequirementItems(int usedCount)
        {
            for (var i = activeRequirementItems.Count - 1; i >= Mathf.Max(0, usedCount); i--)
            {
                var item = activeRequirementItems[i];
                activeRequirementItems.RemoveAt(i);
                ReleaseRequirementItem(item);
            }
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
            for (var i = activeRequirementItems.Count - 1; i >= 0; i--)
            {
                var item = activeRequirementItems[i];
                ReleaseRequirementItem(item);
            }

            activeRequirementItems.Clear();
        }

        private void ReleaseRequirementItem(GamePanelItem_Quest_Requirement item)
        {
            if (item == null)
            {
                return;
            }

            item.Clear();
            if (item.gameObject.activeSelf)
            {
                item.gameObject.SetActive(false);
            }

            if (requirementRoot != null)
            {
                item.transform.SetParent(requirementRoot, false);
            }

            if (!requirementItemPool.Contains(item))
            {
                requirementItemPool.Add(item);
            }
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
