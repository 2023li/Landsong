using System;
using System.Threading;
using Landsong.BuildingSystem;

namespace Landsong.GameEventSystem
{
    public enum GameEventMessageKind
    {
        None = 0,
        Building = 1,
        Game = 2
    }

    public readonly struct GameEventMessage
    {
        private static long nextMessageId;

        public GameEventMessage(
            string eventTypeId,
            string message,
            int turn,
            GameEventMessageKind kind = GameEventMessageKind.Game,
            BuildingBase building = null,
            Action<GameEventMessage> clicked = null,
            string eventId = null,
            bool suppressDefaultPopup = false)
        {
            MessageId = Interlocked.Increment(ref nextMessageId);
            EventTypeId = GameEventCatalog.NormalizeEventTypeId(eventTypeId);
            EventId = string.IsNullOrWhiteSpace(eventId) ? $"{EventTypeId}:{MessageId}" : eventId.Trim();
            Message = string.IsNullOrWhiteSpace(message) ? string.Empty : message.Trim();
            Turn = Math.Max(0, turn);
            Kind = kind;
            Building = building;
            Clicked = clicked;
            SuppressDefaultPopup = suppressDefaultPopup;
        }

        public long MessageId { get; }
        public string EventTypeId { get; }
        public string EventId { get; }
        public string Message { get; }
        public int Turn { get; }
        public GameEventMessageKind Kind { get; }
        public BuildingBase Building { get; }
        public Action<GameEventMessage> Clicked { get; }
        public bool SuppressDefaultPopup { get; }
        public bool IsBuildingEvent => Kind == GameEventMessageKind.Building && Building != null;
        public bool IsValid => MessageId > 0
                               && !string.IsNullOrWhiteSpace(EventTypeId)
                               && !string.IsNullOrWhiteSpace(Message)
                               && (Kind switch
                               {
                                   GameEventMessageKind.Building => Building != null,
                                   GameEventMessageKind.Game => true,
                                   _ => false
                               });

        public static GameEventMessage ForBuildingEvent(
            string eventTypeId,
            BuildingBase building,
            string message,
            int turn,
            Action<GameEventMessage> clicked = null,
            bool suppressDefaultPopup = false)
        {
            return new GameEventMessage(
                eventTypeId,
                message,
                turn,
                GameEventMessageKind.Building,
                building,
                clicked,
                null,
                suppressDefaultPopup);
        }

        public static GameEventMessage ForGame(
            string eventTypeId,
            string message,
            int turn,
            Action<GameEventMessage> clicked = null,
            bool suppressDefaultPopup = false)
        {
            return new GameEventMessage(
                eventTypeId,
                message,
                turn,
                GameEventMessageKind.Game,
                null,
                clicked,
                null,
                suppressDefaultPopup);
        }
    }
}
