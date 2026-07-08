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
        public const string GE_科技研究完成 = "technology_research_completed";
        public const string GE_未选择研发节点 = "technology_research_not_selected";
        public const string GE_科技自动重复研发 = "technology_repeat_research_continued";
        public const string GE_任务完成 = "quest_completed";
        public const string GE_任务失败 = "quest_failed";
        public const string GE_任务提交资源 = "quest_resource_submitted";
        public const string GE_随机任务出现 = "quest_random_added";
        public const string GE_远征出发 = "expedition_started";
        public const string GE_远征成功 = "expedition_succeeded";
        public const string GE_远征失败 = "expedition_failed";
        public const string GE_远征奖励待领取 = "expedition_rewards_pending";
        public const string GE_远征奖励领取 = "expedition_rewards_claimed";
        public const string GE_远征补贴不足 = "expedition_subsidy_missing";
        public const string GE_人才刷新 = "talent_refreshed";
        public const string GE_人才招募 = "talent_recruited";
        public const string GE_人才任命 = "talent_assigned";
        public const string GE_人才卸任 = "talent_unassigned";
        public const string GE_人才升级 = "talent_upgraded";
        public const string GE_人才薪资支付 = "talent_salary_paid";
        public const string GE_人才薪资不足 = "talent_salary_missing";
        public const string GE_人才效果触发 = "talent_effect_triggered";
        public const string GE_人才特性发现 = "talent_trait_discovered";
        public const string GE_人才特性激活 = "talent_trait_activated";
        public const string GE_王子出生 = "royal_prince_born";
        public const string GE_王族特性显现 = "royal_trait_discovered";
        public const string GE_王族特性激活 = "royal_trait_activated";
        public const string GE_王族后天特性获得 = "royal_acquired_trait_added";
        public const string GE_国王寿命预警 = "royal_lifetime_warning";
        public const string GE_国王死亡 = "royal_king_died";
        public const string GE_王位继承 = "royal_succession";
        public const string GE_王朝继承危机 = "royal_succession_crisis";
        public const string GE_国王特性效果触发 = "royal_trait_effect_triggered";

        private static readonly GameEventDefinition[] DefaultDefinitions =
        {
            new GameEventDefinition(GE_人口衰减, "人口衰减"),
            new GameEventDefinition(GE_工人离职, "工人离职"),
            new GameEventDefinition(GE_可用人口不足, "可用人口不足"),
            new GameEventDefinition(GE_自动补贴减少, "自动补贴减少"),
            new GameEventDefinition(GE_自动补贴增加, "自动补贴增加"),
            new GameEventDefinition(GE_补贴金币不足, "补贴金币不足"),
            new GameEventDefinition(GE_招工未完全补满, "招工未完全补满"),
            new GameEventDefinition(GE_科技研究完成, "科技研究完成"),
            new GameEventDefinition(GE_未选择研发节点, "未选择研发"),
            new GameEventDefinition(GE_科技自动重复研发, "科技自动重复研发"),
            new GameEventDefinition(GE_任务完成, "任务完成"),
            new GameEventDefinition(GE_任务失败, "任务失败"),
            new GameEventDefinition(GE_任务提交资源, "任务提交资源"),
            new GameEventDefinition(GE_随机任务出现, "随机任务出现"),
            new GameEventDefinition(GE_远征出发, "远征出发"),
            new GameEventDefinition(GE_远征成功, "远征成功"),
            new GameEventDefinition(GE_远征失败, "远征失败"),
            new GameEventDefinition(GE_远征奖励待领取, "远征奖励待领取"),
            new GameEventDefinition(GE_远征奖励领取, "远征奖励领取"),
            new GameEventDefinition(GE_远征补贴不足, "远征补贴不足"),
            new GameEventDefinition(GE_人才刷新, "人才刷新"),
            new GameEventDefinition(GE_人才招募, "人才招募"),
            new GameEventDefinition(GE_人才任命, "人才任命"),
            new GameEventDefinition(GE_人才卸任, "人才卸任"),
            new GameEventDefinition(GE_人才升级, "人才升级"),
            new GameEventDefinition(GE_人才薪资支付, "人才薪资支付"),
            new GameEventDefinition(GE_人才薪资不足, "人才薪资不足"),
            new GameEventDefinition(GE_人才效果触发, "人才效果触发"),
            new GameEventDefinition(GE_人才特性发现, "人才特性发现"),
            new GameEventDefinition(GE_人才特性激活, "人才特性激活"),
            new GameEventDefinition(GE_王子出生, "王子出生"),
            new GameEventDefinition(GE_王族特性显现, "王族特性显现"),
            new GameEventDefinition(GE_王族特性激活, "王族特性激活"),
            new GameEventDefinition(GE_王族后天特性获得, "王族后天特性获得"),
            new GameEventDefinition(GE_国王寿命预警, "国王寿命预警"),
            new GameEventDefinition(GE_国王死亡, "国王死亡"),
            new GameEventDefinition(GE_王位继承, "王位继承"),
            new GameEventDefinition(GE_王朝继承危机, "王朝继承危机"),
            new GameEventDefinition(GE_国王特性效果触发, "国王特性效果触发")
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
