using System;
using System.Collections.Generic;

namespace Landsong.BuildingSystem
{
    public enum BuildingFunctionBlockGroup
    {
        /// <summary>
        /// 显示在资源组这一行
        /// </summary>
        资源组,
        /// <summary>
        /// 显示在功能性这一行   
        /// </summary>
        功能性
    }

    public readonly struct BuildingFunctionBlockSidebarRow
    {
        public BuildingFunctionBlockSidebarRow(string label, string value)
            : this(label, value, 0f, false)
        {
        }

        public BuildingFunctionBlockSidebarRow(string label, string value, float signedValue, bool hasSignedValue)
        {
            Label = string.IsNullOrWhiteSpace(label) ? string.Empty : label.Trim();
            Value = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            SignedValue = signedValue;
            HasSignedValue = hasSignedValue;
        }

        public string Label { get; }

        public string Value { get; }

        public float SignedValue { get; }

        public bool HasSignedValue { get; }

        public bool IsValid => !string.IsNullOrWhiteSpace(Label) || !string.IsNullOrWhiteSpace(Value);
    }

    public readonly struct BuildingFunctionBlockEntry
    {
        private static readonly IReadOnlyList<BuildingFunctionBlockSidebarRow> EmptySidebarRows =
            Array.Empty<BuildingFunctionBlockSidebarRow>();

        public BuildingFunctionBlockEntry(
            BuildingFunctionBlockGroup group,
            string displayName,
            int amount)
            : this(group, displayName, amount, EmptySidebarRows)
        {
        }

        public BuildingFunctionBlockEntry(
            BuildingFunctionBlockGroup group,
            string displayName,
            int amount,
            BuildingFunctionBlockSidebarRow sidebarRow)
            : this(
                group,
                displayName,
                amount,
                sidebarRow.IsValid
                    ? new[] { sidebarRow }
                    : EmptySidebarRows)
        {
        }

        public BuildingFunctionBlockEntry(
            BuildingFunctionBlockGroup group,
            string displayName,
            int amount,
            IReadOnlyList<BuildingFunctionBlockSidebarRow> sidebarRows)
        {
            Group = group;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            Amount = amount;
            SidebarRows = sidebarRows ?? EmptySidebarRows;
        }

        public BuildingFunctionBlockGroup Group { get; }

        public string DisplayName { get; }

        public int Amount { get; }

        public IReadOnlyList<BuildingFunctionBlockSidebarRow> SidebarRows { get; }

        public bool HasSidebarRows => SidebarRows != null && SidebarRows.Count > 0;

        public bool IsValid => !string.IsNullOrWhiteSpace(DisplayName) && Amount != 0;
    }

    public interface IBuildingFunctionBlockSource
    {
        IReadOnlyList<BuildingFunctionBlockEntry> GetFunctionBlockEntries();
    }
}
