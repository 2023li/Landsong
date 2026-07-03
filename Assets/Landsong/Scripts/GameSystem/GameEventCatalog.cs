using System;
using System.Collections.Generic;

namespace Landsong.GameEventSystem
{
    public readonly struct GameEventDefinition
    {
        public GameEventDefinition(string eventTypeId, string displayName, bool acceptedByDefault = true)
        {
            EventTypeId = GameEventCatalog.NormalizeEventTypeId(eventTypeId);
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? EventTypeId : displayName.Trim();
            AcceptedByDefault = acceptedByDefault;
        }

        public string EventTypeId { get; }
        public string DisplayName { get; }
        public bool AcceptedByDefault { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(EventTypeId);
    }

    public readonly struct GameEventAcceptanceData
    {
        public GameEventAcceptanceData(GameEventDefinition definition, bool isAccepted)
        {
            EventTypeId = definition.EventTypeId;
            DisplayName = definition.DisplayName;
            IsAccepted = isAccepted;
        }

        public string EventTypeId { get; }
        public string DisplayName { get; }
        public bool IsAccepted { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(EventTypeId);
    }

    public static class GameEventCatalog
    {
        public const string GE_人口衰减 = "population_decayed";
        public const string GE_工人离职 = "worker_resigned";
        public const string GE_可用人口不足 = "no_available_population";
        public const string GE_自动补贴减少 = "subsidy_auto_decreased";
        public const string GE_自动补贴增加 = "subsidy_auto_increased";
        public const string GE_补贴金币不足 = "subsidy_gold_missing";
        public const string GE_招工未完全补满 = "recruit_partially_done";

        private static readonly GameEventDefinition[] DefaultDefinitions =
        {
            new GameEventDefinition(GE_人口衰减, "人口衰减"),
            new GameEventDefinition(GE_工人离职, "工人离职"),
            new GameEventDefinition(GE_可用人口不足, "可用人口不足"),
            new GameEventDefinition(GE_自动补贴减少, "自动补贴减少"),
            new GameEventDefinition(GE_自动补贴增加, "自动补贴增加"),
            new GameEventDefinition(GE_补贴金币不足, "补贴金币不足"),
            new GameEventDefinition(GE_招工未完全补满, "招工未完全补满")
        };

        public static IReadOnlyList<GameEventDefinition> Definitions => DefaultDefinitions;

        public static string NormalizeEventTypeId(string eventTypeId)
        {
            return string.IsNullOrWhiteSpace(eventTypeId) ? string.Empty : eventTypeId.Trim();
        }

        public static bool TryGetDefinition(string eventTypeId, out GameEventDefinition definition)
        {
            var normalizedEventTypeId = NormalizeEventTypeId(eventTypeId);
            for (var i = 0; i < DefaultDefinitions.Length; i++)
            {
                if (!string.Equals(DefaultDefinitions[i].EventTypeId, normalizedEventTypeId, StringComparison.Ordinal))
                {
                    continue;
                }

                definition = DefaultDefinitions[i];
                return true;
            }

            definition = default;
            return false;
        }

        public static bool GetDefaultAccepted(string eventTypeId)
        {
            return !TryGetDefinition(eventTypeId, out var definition) || definition.AcceptedByDefault;
        }

        public static string GetDisplayName(string eventTypeId)
        {
            return TryGetDefinition(eventTypeId, out var definition)
                ? definition.DisplayName
                : NormalizeEventTypeId(eventTypeId);
        }
    }
}
