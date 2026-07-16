using System;
using System.Collections.Generic;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 建筑运行状态的统一定义。这里只配置会显示为异常的建筑状态。
    /// </summary>
    public static class BuildingRuntimeStatusCatalog
    {
        public const string BS_废弃 = "abandoned";
        public const string BS_消耗失败 = "consumption_failed";
        public const string BS_库存缺失 = "missing_inventory";
        public const string BS_食物配置异常 = "invalid_food_item";
        public const string BS_无法连接资源点 = "missing_resource_provider";
        public const string BS_食物不足 = "missing_food";
        public const string BS_税收配置异常 = "invalid_tax_item";
        public const string BS_税收存入失败 = "tax_reward_failed";
        public const string BS_市场收入存入失败 = "market_income_failed";
        public const string BS_原木配置异常 = "invalid_wood_item";
        public const string BS_金币配置异常 = "invalid_gold_item";
        public const string BS_原木存入失败 = "wood_storage_failed";
        public const string BS_工人不足 = "insufficient_workers";
        public const string BS_缺工 = "worker_shortage";
        public const string BS_招工金币不足 = "recruit_gold_missing";
        public const string BS_补贴金币不足 = "subsidy_gold_missing";
        public const string BS_道路不通 = "road_blocked";
        public const string BS_仓库维护费不足 = "warehouse_maintenance_missing";
        public const string BS_仓库维护配置异常 = "warehouse_maintenance_invalid";
        public const string BS_维护费不足 = "maintenance_missing";
        public const string BS_维护配置异常 = "maintenance_invalid";

        private static readonly HashSet<string> AbnormalStatusIds =
            new HashSet<string>(StringComparer.Ordinal)
            {
                BS_废弃,
                BS_消耗失败,
                BS_库存缺失,
                BS_食物配置异常,
                BS_无法连接资源点,
                BS_食物不足,
                BS_税收配置异常,
                BS_税收存入失败,
                BS_市场收入存入失败,
                BS_原木配置异常,
                BS_金币配置异常,
                BS_原木存入失败,
                BS_工人不足,
                BS_缺工,
                BS_招工金币不足,
                BS_补贴金币不足,
                BS_道路不通,
                BS_仓库维护费不足,
                BS_仓库维护配置异常,
                BS_维护费不足,
                BS_维护配置异常
            };

        public static bool IsAbnormalStatus(BuildingRuntimeStatus status)
        {
            return status.IsValid && AbnormalStatusIds.Contains(status.StatusId);
        }

        public static bool HasAbnormalStatus(IReadOnlyList<BuildingRuntimeStatus> statuses)
        {
            if (statuses == null)
            {
                return false;
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                if (IsAbnormalStatus(statuses[i]))
                {
                    return true;
                }
            }

            return false;
        }

    }
}
