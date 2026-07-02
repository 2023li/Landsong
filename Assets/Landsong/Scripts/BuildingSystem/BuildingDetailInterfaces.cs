using System.Collections.Generic;

namespace Landsong.BuildingSystem
{
    /// <summary>
    /// 建筑详情面板的数据源接口。
    /// </summary>
    public interface IBuildingDetailSource
    {
        /// <summary>
        /// 建筑详情面板中的分组数据。
        /// </summary>
        IReadOnlyList<BuildingDetailSection> DetailSections { get; }
    }

    /// <summary>
    /// 建筑详情面板中的一个信息分组。
    /// </summary>
    public readonly struct BuildingDetailSection
    {
        public BuildingDetailSection(string title, IReadOnlyList<BuildingDetailRow> rows)
        {
            Title = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
            Rows = rows;
        }

        /// <summary>
        /// 分组标题，例如“基础信息”“资源”“岗位”。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 分组下的显示行。
        /// </summary>
        public IReadOnlyList<BuildingDetailRow> Rows { get; }

        /// <summary>
        /// 至少有一行有效信息时，这个分组才应该显示。
        /// </summary>
        public bool IsValid => Rows != null && Rows.Count > 0;
    }

    /// <summary>
    /// 建筑详情面板中的一行信息。
    /// </summary>
    public readonly struct BuildingDetailRow
    {
        public BuildingDetailRow(string label, string value)
        {
            Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        /// <summary>
        /// 行标题，例如“人口”“当前消耗”。
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 行内容，例如“4/5”“蔬菜 x4”。
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// 标签或内容任意一个非空时，这一行有效。
        /// </summary>
        public bool IsValid => !string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(Value);
    }
}
