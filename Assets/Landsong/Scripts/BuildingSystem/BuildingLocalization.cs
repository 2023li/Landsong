using Landsong.Localization;

namespace Landsong.BuildingSystem
{
    internal static class BuildingLocalization
    {
        public static string Text(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            var source = value.Trim();
            var localized = source switch
            {
                "人口" => L10n.Gameplay("gameplay.building.label.population", "人口"),
                "资源提供点" => L10n.Gameplay("gameplay.building.label.resource_provider", "资源提供点"),
                "可采集树木" => L10n.Gameplay("gameplay.building.label.harvestable_tree", "可采集树木"),
                "有限次数采集" => L10n.Gameplay("gameplay.building.label.limited_harvest", "有限次数采集"),
                "未种植" => L10n.Gameplay("gameplay.building.label.not_planted", "未种植"),
                "可收获" => L10n.Gameplay("gameplay.building.label.ready_to_harvest", "可收获"),
                "成熟剩余回合" => L10n.Gameplay("gameplay.building.label.growth_turns_remaining", "成熟剩余回合"),
                "库存格" => L10n.Gameplay("gameplay.building.label.inventory_slots", "库存格"),
                "远征收益率" => L10n.Gameplay("gameplay.building.label.expedition_yield", "远征收益率"),
                "研究点/回合" => L10n.Gameplay("gameplay.building.label.research_per_turn", "研究点/回合"),
                "运营经验" => L10n.Gameplay("gameplay.building.label.operational_experience", "运营经验"),
                "可选作物" => L10n.Gameplay("gameplay.building.label.crop_options", "可选作物"),
                "当前作物" => L10n.Gameplay("gameplay.building.label.current_crop", "当前作物"),
                "生长进度" => L10n.Gameplay("gameplay.building.label.growth_progress", "生长进度"),
                "自动收获" => L10n.Gameplay("gameplay.building.label.auto_harvest", "自动收获"),
                "自动收获消耗" => L10n.Gameplay("gameplay.building.label.auto_harvest_cost", "自动收获消耗"),
                "收获产出" => L10n.Gameplay("gameplay.building.label.harvest_output", "收获产出"),
                "最低工人" => L10n.Gameplay("gameplay.building.label.minimum_workers", "最低工人"),
                "触发概率" => L10n.Gameplay("gameplay.building.label.trigger_chance", "触发概率"),
                "提供优先级" => L10n.Gameplay("gameplay.building.label.provider_priority", "提供优先级"),
                "上回合经手价值" => L10n.Gameplay("gameplay.building.label.last_turn_value", "上回合经手价值"),
                "金币结算" => L10n.Gameplay("gameplay.building.label.gold_settlement", "金币结算"),
                "生命" => L10n.Gameplay("gameplay.building.label.health", "生命"),
                "原木奖励" => L10n.Gameplay("gameplay.building.label.wood_reward", "原木奖励"),
                "树苗奖励" => L10n.Gameplay("gameplay.building.label.sapling_reward", "树苗奖励"),
                "剩余采集次数" => L10n.Gameplay("gameplay.building.label.harvests_remaining", "剩余采集次数"),
                "每次获得" => L10n.Gameplay("gameplay.building.label.reward_per_harvest", "每次获得"),
                "剩余总产出" => L10n.Gameplay("gameplay.building.label.remaining_output", "剩余总产出"),
                "生产周期" => L10n.Gameplay("gameplay.building.label.production_cycle", "生产周期"),
                "曼哈顿半径" => L10n.Gameplay("gameplay.building.label.manhattan_radius", "曼哈顿半径"),
                "叠加规则" => L10n.Gameplay("gameplay.building.label.stacking_rule", "叠加规则"),
                "当前人口" => L10n.Gameplay("gameplay.building.label.current_population", "当前人口"),
                "增长进度" => L10n.Gameplay("gameplay.building.label.population_growth", "增长进度"),
                "资源路径行动力" => L10n.Gameplay("gameplay.building.label.resource_path_cost", "资源路径行动力"),
                "饮食评分" => L10n.Gameplay("gameplay.building.label.diet_score", "饮食评分"),
                "饮食种类" => L10n.Gameplay("gameplay.building.label.diet_variety", "饮食种类"),
                "人口上限" => L10n.Gameplay("gameplay.building.label.population_limit", "人口上限"),
                "税收进度" => L10n.Gameplay("gameplay.building.label.tax_progress", "税收进度"),
                "失败衰减" => L10n.Gameplay("gameplay.building.label.failure_decay", "失败衰减"),
                "生活质量" => L10n.Gameplay("gameplay.building.label.life_quality", "生活质量"),
                "工人要求" => L10n.Gameplay("gameplay.building.label.worker_requirement", "工人要求"),
                "维护费" => L10n.Gameplay("gameplay.building.label.maintenance_cost", "维护费"),
                "满岗位需要的最少就业吸引力" => L10n.Gameplay("gameplay.building.label.full_workforce_attraction", "满岗位需要的最少就业吸引力"),
                "当前就业吸引力" => L10n.Gameplay("gameplay.building.label.current_job_attraction", "当前就业吸引力"),
                "满岗位吸引力差值" => L10n.Gameplay("gameplay.building.label.full_workforce_gap", "满岗位吸引力差值"),
                "就业吸引力影响因素" => L10n.Gameplay("gameplay.building.label.job_attraction_factors", "就业吸引力影响因素"),
                "当前修正" => L10n.Gameplay("gameplay.building.label.current_modifiers", "当前修正"),
                "开启" => L10n.Gameplay("gameplay.common.enabled", "开启"),
                "关闭" => L10n.Gameplay("gameplay.common.disabled", "关闭"),
                "无" => L10n.Gameplay("gameplay.common.none", "无"),
                _ => source
            };

            if (!ReferenceEquals(localized, source) || localized != source)
            {
                return localized;
            }

            return source
                .Replace("/回合", L10n.Gameplay("gameplay.building.fragment.per_turn", "/回合"))
                .Replace("总价值 × ", L10n.Gameplay("gameplay.building.fragment.total_value_multiplier", "总价值 × "));
        }
    }
}
