using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.GameEventSystem
{
    public sealed class GameEventService
    {
        private readonly List<GameEventMessage> messages = new List<GameEventMessage>();
        private readonly Dictionary<string, bool> acceptedByEventType =
            new Dictionary<string, bool>(StringComparer.Ordinal);

        private int maxMessages = 20;
        private bool newestMessageFirst = true;

        public GameEventService()
        {
            ResetEventAcceptanceToDefaults(false);
        }

        public event Action<GameEventService> MessagesChanged;
        public event Action<GameEventService, string, bool> EventAcceptanceChanged;

        public IReadOnlyList<GameEventMessage> Messages => messages;

        public void Configure(int newMaxMessages, bool newNewestMessageFirst)
        {
            maxMessages = Mathf.Max(1, newMaxMessages);
            newestMessageFirst = newNewestMessageFirst;
            TrimMessages();
        }

        public IReadOnlyList<GameEventAcceptanceData> GetEventAcceptanceData()
        {
            var definitions = GameEventCatalog.Definitions;
            var result = new GameEventAcceptanceData[definitions.Count];
            for (var i = 0; i < definitions.Count; i++)
            {
                result[i] = new GameEventAcceptanceData(definitions[i], IsEventAccepted(definitions[i].EventTypeId));
            }

            return result;
        }

        public bool IsEventAccepted(string eventTypeId)
        {
            var normalizedEventTypeId = GameEventCatalog.NormalizeEventTypeId(eventTypeId);
            if (string.IsNullOrWhiteSpace(normalizedEventTypeId))
            {
                return false;
            }

            return acceptedByEventType.TryGetValue(normalizedEventTypeId, out var accepted)
                ? accepted
                : GameEventCatalog.GetDefaultAccepted(normalizedEventTypeId);
        }

        public bool SetEventAccepted(string eventTypeId, bool accepted)
        {
            var normalizedEventTypeId = GameEventCatalog.NormalizeEventTypeId(eventTypeId);
            if (string.IsNullOrWhiteSpace(normalizedEventTypeId))
            {
                return false;
            }

            if (IsEventAccepted(normalizedEventTypeId) == accepted
                && acceptedByEventType.ContainsKey(normalizedEventTypeId))
            {
                return false;
            }

            acceptedByEventType[normalizedEventTypeId] = accepted;
            if (!accepted)
            {
                RemoveMessagesByEventType(normalizedEventTypeId);
            }

            EventAcceptanceChanged?.Invoke(this, normalizedEventTypeId, accepted);
            return true;
        }

        public void ResetEventAcceptanceToDefaults(bool notify)
        {
            acceptedByEventType.Clear();
            var definitions = GameEventCatalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                acceptedByEventType[definitions[i].EventTypeId] = definitions[i].AcceptedByDefault;
            }

            if (notify)
            {
                MessagesChanged?.Invoke(this);
            }
        }

        public bool AddMessage(GameEventMessage message)
        {
            if (!message.IsValid || !IsEventAccepted(message.EventTypeId))
            {
                return false;
            }

            if (newestMessageFirst)
            {
                messages.Insert(0, message);
            }
            else
            {
                messages.Add(message);
            }

            TrimMessages();
            MessagesChanged?.Invoke(this);
            return true;
        }

        public bool RemoveMessage(GameEventMessage message)
        {
            if (!message.IsValid)
            {
                return false;
            }

            for (var i = 0; i < messages.Count; i++)
            {
                if (messages[i].MessageId != message.MessageId)
                {
                    continue;
                }

                messages.RemoveAt(i);
                MessagesChanged?.Invoke(this);
                return true;
            }

            return false;
        }

        public void ClearMessages()
        {
            if (messages.Count <= 0)
            {
                return;
            }

            messages.Clear();
            MessagesChanged?.Invoke(this);
        }

        private bool RemoveMessagesByEventType(string eventTypeId)
        {
            var normalizedEventTypeId = GameEventCatalog.NormalizeEventTypeId(eventTypeId);
            if (string.IsNullOrWhiteSpace(normalizedEventTypeId))
            {
                return false;
            }

            var removedAny = false;
            for (var i = messages.Count - 1; i >= 0; i--)
            {
                if (!string.Equals(messages[i].EventTypeId, normalizedEventTypeId, StringComparison.Ordinal))
                {
                    continue;
                }

                messages.RemoveAt(i);
                removedAny = true;
            }

            if (removedAny)
            {
                MessagesChanged?.Invoke(this);
            }

            return removedAny;
        }

        private void TrimMessages()
        {
            while (messages.Count > maxMessages)
            {
                var removeIndex = newestMessageFirst ? messages.Count - 1 : 0;
                messages.RemoveAt(removeIndex);
            }
        }
    }
}
