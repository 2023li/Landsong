using System;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_HUDQuestItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text progressLabel;
        [SerializeField] private TMP_Text deadlineLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private Slider progressSlider;
        [SerializeField] private Image icon;
        [SerializeField,LabelText("紧急标记")] private GameObject urgentRoot;
        [SerializeField,LabelText("可完成标记")] private GameObject completableRoot;

        private GameQuestState quest;
        private Action<GameQuestState> clicked;

        private void Reset()
        {
            button = GetComponent<Button>();
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(GameQuestState sourceQuest, Action<GameQuestState> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            quest = sourceQuest;
            clicked = onClicked;

            SetText(titleLabel, FormatTitle(quest));
            SetText(progressLabel, FormatProgress(quest));
            SetText(deadlineLabel, FormatDeadline(quest));
            SetText(statusLabel, FormatStatus(quest));
            SetProgress(quest == null ? 0f : quest.Progress01);
            SetIcon(quest == null ? null : quest.Icon);
            SetActive(urgentRoot, IsUrgent(quest));
            SetActive(completableRoot, quest != null && quest.CanSubmitResources());

            if (button != null)
            {
                button.interactable = quest != null;
                button.onClick.AddListener(HandleClicked);
            }
        }

        public void Unbind()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.interactable = false;
            }

            quest = null;
            clicked = null;
            SetText(titleLabel, string.Empty);
            SetText(progressLabel, string.Empty);
            SetText(deadlineLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetProgress(0f);
            SetIcon(null);
            SetActive(urgentRoot, false);
            SetActive(completableRoot, false);
        }

        private void HandleClicked()
        {
            clicked?.Invoke(quest);
        }

        private static string FormatProgress(GameQuestState quest)
        {
            if (quest == null || quest.Definition == null)
            {
                return string.Empty;
            }

            return quest.Definition.ObjectiveType switch
            {
                QuestObjectiveType.BuildBuildings => $"{quest.CurrentAmount}/{quest.TargetAmount}",
                QuestObjectiveType.SubmitResources => $"{quest.TotalSubmittedAmount}/{quest.TotalRequiredAmount}",
                _ => $"{quest.CurrentAmount}/{quest.TargetAmount}"
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
                return "已完成";
            }

            if (quest.IsFailed)
            {
                return "已失败";
            }

            var gameSystem = GameSystem.Instance;
            var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.CurrentTurn;
            var remainingTurns = quest.GetRemainingTurns(currentTurn);
            return remainingTurns <= 0 ? "本回合截止" : $"剩 {remainingTurns} 回合";
        }

        private static string FormatStatus(GameQuestState quest)
        {
            if (quest == null)
            {
                return string.Empty;
            }

            if (quest.IsCompleted)
            {
                return "已完成";
            }

            if (quest.IsFailed)
            {
                return "已失败";
            }

            if (quest.CanSubmitResources())
            {
                return "可提交";
            }

            return quest.IsResourceSubmission ? "资源不足" : "进行中";
        }

        private static bool IsUrgent(GameQuestState quest)
        {
            if (quest == null || !quest.IsActive)
            {
                return false;
            }

            var gameSystem = GameSystem.Instance;
            var currentTurn = gameSystem == null ? quest.StartedTurn : gameSystem.CurrentTurn;
            return quest.GetRemainingTurns(currentTurn) <= 1;
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

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
