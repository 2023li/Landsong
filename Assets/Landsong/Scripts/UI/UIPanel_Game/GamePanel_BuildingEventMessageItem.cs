using System;
using Landsong.GameEventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingEventMessageItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Button deleteButton;
        [SerializeField] private Button detailButton;
        [SerializeField] private TMP_Text messageLabel;
        [SerializeField] private TMP_Text detailLabel;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private string detailButtonCollapsedText = "详细";
        [SerializeField] private string detailButtonExpandedText = "收起";
        [SerializeField, Min(1)] private int collapsedPreviewMaxLength = 28;
        [SerializeField, Min(1f)] private float collapsedHeight = 100f;
        [SerializeField, Min(1f)] private float expandedHeight = 160f;

        private GameEventMessage message;
        private Action<GameEventMessage> clicked;
        private Action<GameEventMessage> deleted;
        private bool isExpanded;

        private void Reset()
        {
            button = GetComponent<Button>();
            messageLabel = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            SetExpanded(false);
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(HandleDeleteClicked);
            }

            if (detailButton != null)
            {
                detailButton.onClick.AddListener(HandleDetailClicked);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveListener(HandleDeleteClicked);
            }

            if (detailButton != null)
            {
                detailButton.onClick.RemoveListener(HandleDetailClicked);
            }
        }

        public void Bind(
            GameEventMessage newMessage,
            Action<GameEventMessage> onClicked,
            Action<GameEventMessage> onDeleted)
        {
            message = newMessage;
            clicked = onClicked;
            deleted = onDeleted;

            if (messageLabel != null)
            {
                messageLabel.text = FormatCollapsedMessage(message);
            }

            if (detailLabel != null)
            {
                detailLabel.text = FormatDetailMessage(message);
            }

            if (deleteButton != null)
            {
                deleteButton.interactable = message.IsValid;
            }

            if (detailButton != null)
            {
                detailButton.interactable = message.IsValid;
            }

            SetExpanded(false);
        }

        public void Unbind()
        {
            message = default;
            clicked = null;
            deleted = null;
            isExpanded = false;

            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
            }

            if (detailLabel != null)
            {
                detailLabel.text = string.Empty;
            }

            if (deleteButton != null)
            {
                deleteButton.interactable = false;
            }

            if (detailButton != null)
            {
                detailButton.interactable = false;
            }

            SetExpanded(false);
        }

        private void HandleClicked()
        {
            if (!message.IsValid)
            {
                return;
            }

            clicked?.Invoke(message);
        }

        private void HandleDeleteClicked()
        {
            if (!message.IsValid)
            {
                return;
            }

            deleted?.Invoke(message);
        }

        private void HandleDetailClicked()
        {
            if (!message.IsValid)
            {
                return;
            }

            SetExpanded(!isExpanded);
        }

        private void SetExpanded(bool expanded)
        {
            isExpanded = expanded;

            if (detailRoot != null)
            {
                detailRoot.SetActive(isExpanded);
            }

            if (detailButton != null)
            {
                var buttonLabel = detailButton.GetComponentInChildren<TMP_Text>(true);
                if (buttonLabel != null)
                {
                    buttonLabel.text = isExpanded ? detailButtonExpandedText : detailButtonCollapsedText;
                }
            }

            if (transform is RectTransform rectTransform)
            {
                rectTransform.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    isExpanded ? expandedHeight : collapsedHeight);
            }
        }

        private string FormatCollapsedMessage(GameEventMessage eventMessage)
        {
            var text = eventMessage.Message;
            if (string.IsNullOrWhiteSpace(text) || text.Length <= collapsedPreviewMaxLength)
            {
                return text;
            }

            return $"{text.Substring(0, collapsedPreviewMaxLength)}...";
        }

        private static string FormatDetailMessage(GameEventMessage eventMessage)
        {
            if (!eventMessage.IsValid)
            {
                return string.Empty;
            }

            var source = eventMessage.IsBuildingEvent && eventMessage.Building != null && eventMessage.Building.HasDefinition
                ? eventMessage.Building.Definition.DisplayName
                : string.Empty;
            return string.IsNullOrWhiteSpace(source)
                ? $"第 {eventMessage.Turn} 回合\n{eventMessage.Message}"
                : $"第 {eventMessage.Turn} 回合\n{source}\n{eventMessage.Message}";
        }
    }
}
