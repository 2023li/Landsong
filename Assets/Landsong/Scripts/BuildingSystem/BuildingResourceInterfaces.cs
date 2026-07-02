using System.Collections.Generic;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 标记建筑是否可以作为居民房等建筑的资源连接点。
    /// </summary>
    public interface IResourceProviderPoint
    {
        /// <summary>
        /// 返回 true 时，该建筑会被视为可连接的资源提供点。
        /// </summary>
        bool IsResourceProviderPoint { get; }
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
    /// 向 UI 暴露建筑当前或上一回合遗留的运行时异常状态。
    /// </summary>
    public interface IBuildingRuntimeStatusSource
    {
        /// <summary>
        /// 建筑当前需要显示的状态列表。空列表表示 UI 可视为正常。
        /// </summary>
        IReadOnlyList<BuildingRuntimeStatus> RuntimeStatuses { get; }
    }

    /// <summary>
    /// 单条建筑运行状态，用于概览列表、地图 Marker 和消息栏显示。
    /// </summary>
    public readonly struct BuildingRuntimeStatus
    {
        public BuildingRuntimeStatus(string statusId,string displayName,int progress = 0,int target = 0,string eventMessage = null)
        {
            StatusId = string.IsNullOrWhiteSpace(statusId) ? string.Empty : statusId.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? StatusId : displayName.Trim();
            Progress = progress < 0 ? 0 : progress;
            Target = target < 0 ? 0 : target;
            EventMessage = string.IsNullOrWhiteSpace(eventMessage) ? string.Empty : eventMessage.Trim();
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
        /// 可选事件消息文本。用于通用信息栏生成“居民房人口衰减！”这类短消息。
        /// 为空时，UI 会用建筑名和 DisplayName 自动拼接。
        /// </summary>
        public string EventMessage { get; }

        /// <summary>
        /// 状态 ID 非空时表示这条状态有效。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(StatusId);
    }

    /// <summary>
    /// 向建筑概览列表暴露一组短文本数值。
    /// </summary>
    public interface IBuildingOverviewSource
    {
        /// <summary>
        /// 概览数值标签，例如“人口”或“岗位”。
        /// </summary>
        string OverviewValueLabel { get; }

        /// <summary>
        /// 概览数值内容，例如“4/5”或“补贴 0，工人 2/3，吸引力 35”。
        /// </summary>
        string OverviewValueText { get; }
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
