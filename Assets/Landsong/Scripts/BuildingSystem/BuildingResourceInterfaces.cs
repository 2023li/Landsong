using System.Collections.Generic;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 向库存系统暴露建筑当前实际提供的额外库存格。
    /// 实现可以根据等级、工人或运行状态动态返回容量。
    /// </summary>
    public interface IBuildingInventoryCapacitySource
    {
        int CurrentProvidedSlotCount { get; }
    }

    /// <summary>
    /// 向 UI 或统计系统暴露建筑的资源消耗信息。
    /// </summary>
    public interface IBuildingResourceConsumptionSource
    {
        /// <summary>
        /// 当前状态下预计每回合会消耗的资源。
        /// </summary>
        IReadOnlyList<BuildingResourceChange> CurrentResourceConsumptions { get; }

        /// <summary>
        /// 上一次回合实际成功消耗的资源。
        /// </summary>
        IReadOnlyList<BuildingResourceChange> LastResourceConsumptions { get; }
    }

    /// <summary>
    /// 向 UI 或统计系统暴露建筑的税收产出信息。
    /// </summary>
    public interface IBuildingTaxSource
    {
        /// <summary>
        /// 当前状态下预计可获得的税收奖励。
        /// </summary>
        IReadOnlyList<BuildingResourceChange> CurrentTaxRewards { get; }

        /// <summary>
        /// 上一次回合实际成功获得的税收奖励。
        /// </summary>
        IReadOnlyList<BuildingResourceChange> LastTaxRewards { get; }
    }

    /// <summary>
    /// 向科技系统暴露建筑每回合提供的科技点。
    /// </summary>
    public interface IBuildingTechnologyPointSource
    {
        /// <summary>
        /// 当前状态下预计每回合会提供的科技点。
        /// </summary>
        int CurrentTechnologyPointsPerTurn { get; }

        /// <summary>
        /// 上一次回合实际成功提供的科技点。
        /// </summary>
        int LastTechnologyPoints { get; }
    }

    /// <summary>
    /// 向 UI 或统计系统暴露建筑的生产产出信息。
    /// </summary>
    public interface IBuildingResourceProductionSource
    {
        /// <summary>
        /// 当前状态下预计每回合会生产的资源。
        /// </summary>
        IReadOnlyList<BuildingResourceChange> CurrentResourceProductions { get; }

        /// <summary>
        /// 上一次回合实际成功生产的资源。
        /// </summary>
        IReadOnlyList<BuildingResourceChange> LastResourceProductions { get; }
    }

    /// <summary>
    /// 单条建筑运行状态，用于概览列表、地图 Marker 和建筑详情显示。
    /// </summary>
    public readonly struct BuildingRuntimeStatus
    {
        public BuildingRuntimeStatus(string statusId,string displayName,int progress = 0,int target = 0)
        {
            StatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StatusId : displayName.Trim();
            Progress = progress < 0 ? 0 : progress;
            Target = target < 0 ? 0 : target;
        }

        /// <summary>
        /// 稳定的状态 ID，用于程序判断和去重。
        /// </summary>
        public string StatusId { get; }

        /// <summary>
        /// 面向玩家显示的状态文本。
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// 可选进度值，例如连续失败次数、当前人口或当前工人数。
        /// </summary>
        public int Progress { get; }

        /// <summary>
        /// 可选目标值，例如失败阈值、人口上限或稳定工人数。
        /// </summary>
        public int Target { get; }

        /// <summary>
        /// 状态 ID 非空时表示这条状态有效。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(StatusId);
    }

    /// <summary>
    /// 表示一次建筑资源变化。
    /// </summary>
    public readonly struct BuildingResourceChange
    {
        public BuildingResourceChange(string itemId, int amount)
        {
            ItemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            Amount = amount < 0 ? 0 : amount;
        }

        /// <summary>
        /// 物品 ID，对应库存系统中的 itemId。
        /// </summary>
        public string ItemId { get; }

        /// <summary>
        /// 资源数量。小于 0 的输入会被归零。
        /// </summary>
        public int Amount { get; }

        /// <summary>
        /// 物品 ID 非空且数量大于 0 时表示这条资源变化有效。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && Amount > 0;
    }
}
