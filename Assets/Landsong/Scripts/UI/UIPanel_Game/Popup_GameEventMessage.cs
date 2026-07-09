using System;
using System.Text;
using Landsong.GameEventSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class Popup_GameEventMessage : MonoBehaviour
    {
        [SerializeField] private TMP_Text eventNameLabel;
        [SerializeField] private TMP_Text eventContentLabel;
        [SerializeField] private Button confirmButton;
        [SerializeField] private bool hideOnAwake = true;

        private GameEventMessage message;
        private Action<GameEventMessage> confirmed;
        private bool showingMessage;

        public bool IsVisible => gameObject.activeSelf;

        private void Reset()
        {
            var labels = GetComponentsInChildren<TMP_Text>(true);
            if (labels.Length > 0)
            {
                eventNameLabel = labels[0];
            }

            if (labels.Length > 1)
            {
                eventContentLabel = labels[1];
            }

            confirmButton = GetComponentInChildren<Button>(true);
        }

        private void Awake()
        {
            if (hideOnAwake && !showingMessage)
            {
                Hide();
            }
        }

        private void OnEnable()
        {
            if (confirmButton == null)
            {
                Debug.LogError($"{nameof(Popup_GameEventMessage)} 配置错误：confirmButton 未绑定。", this);
                return;
            }

            confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            confirmButton.onClick.AddListener(HandleConfirmClicked);
        }

        private void OnDisable()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(HandleConfirmClicked);
            }
        }

        public void Show(GameEventMessage eventMessage, Action<GameEventMessage> onConfirmed)
        {
            if (!eventMessage.IsValid)
            {
                Hide();
                return;
            }

            message = eventMessage;
            confirmed = onConfirmed;
            showingMessage = true;

            SetText(eventNameLabel, FormatEventName(eventMessage));
            SetText(eventContentLabel, FormatEventContent(eventMessage));

            if (confirmButton != null)
            {
                confirmButton.interactable = true;
            }

            gameObject.SetActive(true);
        }

        public void Hide()
        {
            showingMessage = false;
            message = default;
            confirmed = null;

            SetText(eventNameLabel, string.Empty);
            SetText(eventContentLabel, string.Empty);

            if (confirmButton != null)
            {
                confirmButton.interactable = false;
            }

            gameObject.SetActive(false);
        }

        private void HandleConfirmClicked()
        {
            if (!message.IsValid)
            {
                Hide();
                return;
            }

            var confirmedMessage = message;
            var confirmCallback = confirmed;

            if (confirmButton != null)
            {
                confirmButton.interactable = false;
            }

            confirmCallback?.Invoke(confirmedMessage);
            Hide();
        }

        private static string FormatEventName(GameEventMessage eventMessage)
        {
            if (!eventMessage.IsValid)
            {
                return string.Empty;
            }

            var displayName = GameEventCatalog.GetDisplayName(eventMessage.EventTypeId);
            return string.IsNullOrWhiteSpace(displayName) ? eventMessage.EventTypeId : displayName;
        }

        private static string FormatEventContent(GameEventMessage eventMessage)
        {
            if (!eventMessage.IsValid)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            AppendLine(builder, $"第 {eventMessage.Turn} 回合");
            AppendLine(builder, ResolveSourceName(eventMessage));
            AppendLine(builder, eventMessage.Message);
            return builder.ToString();
        }

        private static string ResolveSourceName(GameEventMessage eventMessage)
        {
            if (!eventMessage.IsBuildingEvent || eventMessage.Building == null)
            {
                return string.Empty;
            }

            if (eventMessage.Building.HasDefinition
                && eventMessage.Building.Definition != null
                && !string.IsNullOrWhiteSpace(eventMessage.Building.Definition.DisplayName))
            {
                return eventMessage.Building.Definition.DisplayName;
            }

            return eventMessage.Building.name;
        }

        private static void AppendLine(StringBuilder builder, string value)
        {
            if (builder == null || string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(value.Trim());
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }
    }
}
