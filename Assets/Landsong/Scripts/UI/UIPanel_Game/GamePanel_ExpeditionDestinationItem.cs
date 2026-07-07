using System;
using Landsong.ExpeditionSystem;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_ExpeditionDestinationItem : MonoBehaviour
    {
        [SerializeField, LabelText("选择按钮")] private Button button;
        [SerializeField, LabelText("图标")] private Image icon;
        [SerializeField, LabelText("名称文本")] private TMP_Text titleLabel;
        [SerializeField, LabelText("描述文本")] private TMP_Text descriptionLabel;
        [SerializeField, LabelText("状态文本")] private TMP_Text statusLabel;
        [SerializeField, LabelText("回合文本")] private TMP_Text turnWindowLabel;
        [SerializeField, LabelText("选中根节点")] private GameObject selectedRoot;
        [SerializeField, LabelText("不可用根节点")] private GameObject unavailableRoot;

        private ExpeditionDestinationAvailability availability;
        private Action<ExpeditionDestinationDefinition> clicked;

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

        public void Bind(
            ExpeditionDestinationAvailability sourceAvailability,
            bool selected,
            Action<ExpeditionDestinationDefinition> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            availability = sourceAvailability;
            clicked = onClicked;
            Refresh(selected);

            if (button != null)
            {
                button.interactable = availability.Destination != null;
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

            availability = default;
            clicked = null;
            SetIcon(null);
            SetText(titleLabel, string.Empty);
            SetText(descriptionLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(turnWindowLabel, string.Empty);
            SetActive(selectedRoot, false);
            SetActive(unavailableRoot, false);
        }

        private void Refresh(bool selected)
        {
            var destination = availability.Destination;
            if (destination == null)
            {
                Unbind();
                return;
            }

            SetIcon(destination.Icon);
            SetText(titleLabel, destination.DisplayName);
            SetText(descriptionLabel, destination.Description);
            SetText(statusLabel, FormatStatus(availability));
            SetText(turnWindowLabel, FormatTurnWindow(destination));
            SetActive(selectedRoot, selected);
            SetActive(unavailableRoot, !availability.IsAvailable);
        }

        private void HandleClicked()
        {
            clicked?.Invoke(availability.Destination);
        }

        private static string FormatStatus(ExpeditionDestinationAvailability availability)
        {
            if (availability.IsAvailable)
            {
                return "可出发";
            }

            return availability.Reason switch
            {
                ExpeditionDestinationUnavailableReason.WindowClosed => "不在窗口期",
                ExpeditionDestinationUnavailableReason.ConditionLocked => "条件未满足",
                ExpeditionDestinationUnavailableReason.AlreadyCompleted => "已完成",
                _ => "不可用"
            };
        }

        private static string FormatTurnWindow(ExpeditionDestinationDefinition destination)
        {
            if (destination == null)
            {
                return string.Empty;
            }

            var hasEarliest = destination.EarliestAvailableTurn > 0;
            var hasLatest = destination.LatestAvailableTurn > 0;
            if (!hasEarliest && !hasLatest)
            {
                return "常驻";
            }

            if (hasEarliest && hasLatest)
            {
                return $"第 {destination.EarliestAvailableTurn}-{destination.LatestAvailableTurn} 回合";
            }

            return hasEarliest
                ? $"第 {destination.EarliestAvailableTurn} 回合后"
                : $"截止第 {destination.LatestAvailableTurn} 回合";
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

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
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
