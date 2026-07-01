using System;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public readonly struct BuildingEventMessage
    {
        public BuildingEventMessage(BuildingBase building, BuildingRuntimeStatus status, string message, int turn)
        {
            Building = building;
            Status = status;
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            Turn = Mathf.Max(0, turn);
        }

        public BuildingBase Building { get; }
        public BuildingRuntimeStatus Status { get; }
        public string Message { get; }
        public int Turn { get; }
        public bool IsValid => Building != null && Status.IsValid && !string.IsNullOrWhiteSpace(Message);
    }

    public sealed class GamePanel_BuildingEventMessageItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text messageLabel;

        private BuildingEventMessage message;
        private Action<BuildingEventMessage> clicked;

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
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(BuildingEventMessage newMessage, Action<BuildingEventMessage> onClicked)
        {
            message = newMessage;
            clicked = onClicked;

            if (messageLabel != null)
            {
                messageLabel.text = message.Message;
            }
        }

        public void Unbind()
        {
            message = default;
            clicked = null;

            if (messageLabel != null)
            {
                messageLabel.text = string.Empty;
            }
        }

        private void HandleClicked()
        {
            if (!message.IsValid)
            {
                return;
            }

            clicked?.Invoke(message);
        }
    }
}
