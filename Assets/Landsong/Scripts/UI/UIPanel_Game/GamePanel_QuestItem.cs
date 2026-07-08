using System;
using System.Text;
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
        [SerializeField, LabelText("提交按钮")] private Button submitButton;
        [SerializeField, LabelText("提交按钮文本")] private TMP_Text submitButtonLabel;
        [SerializeField, LabelText("图标")] private Image icon;
        [SerializeField, LabelText("任务名称")] private TMP_Text titleLabel;
        [SerializeField, LabelText("任务描述")] private TMP_Text descriptionLabel;
        [SerializeField, LabelText("状态文本")] private TMP_Text statusLabel;
        [SerializeField, LabelText("期限文本")] private TMP_Text deadlineLabel;
        [SerializeField, LabelText("进度文本")] private TMP_Text progressLabel;
        [SerializeField, LabelText("资源明细文本")] private TMP_Text resourceDetailsLabel;
        [SerializeField, LabelText("进度条")] private Slider progressSlider;
        [SerializeField, LabelText("详情根节点")] private GameObject detailsRoot;
        [SerializeField, LabelText("选中状态根节点")] private GameObject selectedRoot;
        [SerializeField, LabelText("已完成状态根节点")] private GameObject completedRoot;
        [SerializeField, LabelText("已失败状态根节点")] private GameObject failedRoot;

        private GameQuestState quest;
        private Action<GameQuestState> selected;
        private Action<GameQuestState> submitClicked;
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
        }

        public void Bind(
            GameQuestState sourceQuest,
            bool selectedState,
            Action<GameQuestState> onSelected,
            Action<GameQuestState> onSubmitClicked)
        {
            if (selectButton != null)
            {
                selectButton.onClick.RemoveListener(HandleSelected);
            }

            if (submitButton != null)
            {
                submitButton.onClick.RemoveListener(HandleSubmitClicked);
            }

            quest = sourceQuest;
            isSelected = selectedState;
            selected = onSelected;
            submitClicked = onSubmitClicked;
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
        }

        public void Unbind()
        {
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

            quest = null;
            selected = null;
            submitClicked = null;
            isSelected = false;
            SetText(titleLabel, string.Empty);
            SetText(descriptionLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(deadlineLabel, string.Empty);
            SetText(progressLabel, string.Empty);
            SetText(resourceDetailsLabel, string.Empty);
            SetActive(detailsRoot, false);
            SetActive(selectedRoot, false);
            SetActive(completedRoot, false);
            SetActive(failedRoot, false);
            SetIcon(null);
            SetProgress(0f);
        }

        public void Refresh()
        {
            if (quest == null || quest.Definition == null)
            {
                Unbind();
                return;
            }

            SetText(titleLabel, FormatTitle(quest));
            SetText(descriptionLabel, quest.Definition.Description);
            SetText(statusLabel, FormatStatus(quest));
            SetText(deadlineLabel, FormatDeadline(quest));
            SetText(progressLabel, FormatProgress(quest));
            ResourceRichTextFormatter.ApplySpriteAsset(resourceDetailsLabel);
            SetText(resourceDetailsLabel, FormatResourceDetails(quest));
            SetDetailsVisible(isSelected);
            SetActive(selectedRoot, isSelected);
            SetActive(completedRoot, quest.IsCompleted);
            SetActive(failedRoot, quest.IsFailed);
            SetIcon(quest.Icon);
            SetProgress(quest.Progress01);
            RefreshSubmitButton();
        }

        private void RefreshSubmitButton()
        {
            if (submitButton == null)
            {
                return;
            }

            var canSubmit = quest != null && quest.CanSubmitResources();
            submitButton.gameObject.SetActive(quest != null && isSelected && quest.IsResourceSubmission && quest.IsActive);
            submitButton.interactable = canSubmit;
            SetText(submitButtonLabel, canSubmit ? "提交" : "资源不足");
        }

        private void HandleSelected()
        {
            selected?.Invoke(quest);
        }

        private void HandleSubmitClicked()
        {
            submitClicked?.Invoke(quest);
        }

        private void SetDetailsVisible(bool visible)
        {
            if (detailsRoot != null)
            {
                detailsRoot.SetActive(visible);
                return;
            }

            SetGameObjectActive(descriptionLabel, visible);
            SetGameObjectActive(
                resourceDetailsLabel,
                visible && quest != null && (quest.IsResourceSubmission || quest.Definition.HasRewards));
        }

        private static string FormatStatus(GameQuestState quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            return quest.Status switch
            {
                QuestStatus.Completed => $"{quest.CategoryDisplayName} · 已完成",
                QuestStatus.Failed => $"{quest.CategoryDisplayName} · 已失败",
                _ => $"{quest.CategoryDisplayName} · 进行中"
            };
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
                return $"已超过期限：截止第 {quest.DeadlineTurn} 回合";
            }

            var gameSystem = GameSystem.Instance;
            var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.CurrentTurn;
            return $"截止第 {quest.DeadlineTurn} 回合，剩余 {quest.GetRemainingTurns(currentTurn)} 回合";
        }

        private static string FormatProgress(GameQuestState quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            var targetName = string.IsNullOrWhiteSpace(quest.TargetDisplayName)
                ? "目标"
                : quest.TargetDisplayName;

            return quest.Definition.ObjectiveType switch
            {
                QuestObjectiveType.BuildBuildings => $"建造 {targetName}：{quest.CurrentAmount}/{quest.TargetAmount}",
                QuestObjectiveType.SubmitResources => $"提交 {targetName}：{quest.TotalSubmittedAmount}/{quest.TotalRequiredAmount}",
                _ => $"{quest.CurrentAmount}/{quest.TargetAmount}"
            };
        }

        private static string FormatResourceDetails(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendResourceDetails(builder, quest);
            AppendRewardDetails(builder, quest);
            return builder.ToString();
        }

        private static void AppendResourceDetails(StringBuilder builder, GameQuestState quest)
        {
            if (builder == null || quest == null || !quest.IsResourceSubmission)
            {
                return;
            }

            var resources = quest.ResourceProgresses;
            for (var i = 0; i < resources.Count; i++)
            {
                var progress = resources[i];
                if (progress == null)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(
                    ResourceRichTextFormatter.FormatProgress(
                        progress.ItemDefinition,
                        progress.ItemId,
                        progress.DisplayName,
                        progress.SubmittedAmount,
                        progress.RequiredAmount,
                        progress.InventoryAmount,
                        quest.IsActive && progress.RemainingAmount > 0));
            }
        }

        private static void AppendRewardDetails(StringBuilder builder, GameQuestState quest)
        {
            if (builder == null || quest == null || quest.Definition == null || !quest.Definition.HasRewards)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("奖励：");
            var rewards = quest.Definition.Rewards;
            var appendedAny = false;
            for (var i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i].Normalized();
                if (!reward.IsValid)
                {
                    continue;
                }

                if (appendedAny)
                {
                    builder.Append("，");
                }

                builder.Append(
                    ResourceRichTextFormatter.FormatAmount(
                        reward.ItemDefinition,
                        reward.ItemId,
                        reward.ItemDefinition == null ? reward.ItemId : reward.ItemDefinition.DisplayName,
                        reward.Amount));
                appendedAny = true;
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

        private void SetProgress(float value)
        {
            if (progressSlider == null)
            {
                return;
            }

            progressSlider.minValue = 0f;
            progressSlider.maxValue = 1f;
            progressSlider.value = Mathf.Clamp01(value);
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
