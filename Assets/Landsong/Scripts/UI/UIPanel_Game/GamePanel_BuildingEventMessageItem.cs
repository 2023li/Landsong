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
        [SerializeField] private TMP_Text messageLabel;

        private GameEventMessage message;
        private Action<GameEventMessage> clicked;
        private Action<GameEventMessage> deleted;

        private void Reset()
        {
            button = GetComponent<Button>();
            messageLabel = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            ResolveButton();
            RefreshDeleteButton();
        }

        private void OnEnable()
        {
            ResolveButton();
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.AddListener(HandleDeleted);
            }

            RefreshDeleteButton();
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            if (deleteButton != null)
            {
                deleteButton.onClick.RemoveListener(HandleDeleted);
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
                messageLabel.text = FormatEventName(message);
            }

            if (button != null)
            {
                button.interactable = message.IsValid;
            }

            RefreshDeleteButton();
        }

        public void Unbind()
        {
            message = default;
            clicked = null;
            deleted = null;

            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
            }

            if (button != null)
            {
                button.interactable = false;
            }

            RefreshDeleteButton();
        }

        private void HandleClicked()
        {
            if (!message.IsValid)
            {
                return;
            }

            clicked?.Invoke(message);
        }

        private void HandleDeleted()
        {
            if (!message.IsValid)
            {
                return;
            }

          

            deleted?.Invoke(message);
        }

        private void ResolveButton()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void RefreshDeleteButton()
        {
            if (deleteButton != null)
            {
                var canDelete = message.IsValid && deleted != null;
                deleteButton.gameObject.SetActive(canDelete);
                deleteButton.interactable = canDelete;
            }
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
    }
}
