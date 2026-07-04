using System;
using System.Collections.Generic;
using System.Text;
using Landsong.BuildingSystem;

namespace Landsong.UISystem
{
    public static class BuildingDetailUIFormatter
    {
        private static readonly IReadOnlyList<BuildingDetailSection> EmptySections =
            Array.Empty<BuildingDetailSection>();

        public static IReadOnlyList<BuildingDetailSection> CreateDetailSections(BuildingBase building)
        {
            if (building == null)
            {
                return EmptySections;
            }

            return CreateFallbackDetailSections(building);
        }

        public static bool HasAnyValidSection(IReadOnlyList<BuildingDetailSection> sections)
        {
            if (sections == null)
            {
                return false;
            }

            for (int i = 0; i < sections.Count; i++)
            {
                if (HasAnyValidRow(sections[i].Rows))
                {
                    return true;
                }
            }

            return false;
        }

        public static string FormatResourceChanges(IReadOnlyList<BuildingResourceChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                return "无";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < changes.Count; i++)
            {
                BuildingResourceChange change = changes[i];
                if (!change.IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("，");
                }

                builder.Append(change.ItemId);
                builder.Append(" x");
                builder.Append(change.Amount);
            }

            return builder.Length == 0 ? "无" : builder.ToString();
        }

        public static string FormatStatuses(IReadOnlyList<BuildingRuntimeStatus> statuses)
        {
            if (!BuildingStatusUIFormatter.HasAnyStatus(statuses))
            {
                return "正常";
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < statuses.Count; i++)
            {
                BuildingRuntimeStatus status = statuses[i];
                if (!status.IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("，");
                }

                builder.Append(status.DisplayName);
                if (status.Target > 0)
                {
                    builder.Append(" ");
                    builder.Append(status.Progress);
                    builder.Append("/");
                    builder.Append(status.Target);
                }
            }

            return builder.Length == 0 ? "正常" : builder.ToString();
        }

        private static IReadOnlyList<BuildingDetailSection> CreateFallbackDetailSections(BuildingBase building)
        {
            List<BuildingDetailSection> sections = new List<BuildingDetailSection>();

            AddFallbackOverviewSection(sections, building);
            AddFallbackStatusSection(sections, building);
            AddFallbackResourceSection(sections, building);

            return sections.Count == 0 ? EmptySections : sections;
        }

        private static void AddFallbackOverviewSection(List<BuildingDetailSection> sections, BuildingBase building)
        {
            List<BuildingDetailRow> rows = new List<BuildingDetailRow>
            {
                new BuildingDetailRow("建筑名", BuildingStatusUIFormatter.GetBuildingName(building))
            };

            if (building.Definition != null)
            {
                rows.Add(new BuildingDetailRow("建筑ID", building.Definition.BuildingId));
            }

            if (building.HasPlacement)
            {
                rows.Add(new BuildingDetailRow("格子位置", building.GridPosition.ToString()));
            }

            string overviewText = building.GetOverviewInfo();
            if (!string.IsNullOrWhiteSpace(overviewText))
            {
                rows.Add(new BuildingDetailRow("概览", overviewText));
            }

            AddSectionIfValid(sections, "基础信息", rows);
        }

        private static void AddFallbackStatusSection(List<BuildingDetailSection> sections, BuildingBase building)
        {
            IReadOnlyList<BuildingRuntimeStatus> statuses = BuildingStatusUIFormatter.GetRuntimeStatuses(building);
            List<BuildingDetailRow> rows = new List<BuildingDetailRow>
            {
                new BuildingDetailRow("状态", FormatStatuses(statuses))
            };

            AddSectionIfValid(sections, "运行状态", rows);
        }

        private static void AddFallbackResourceSection(List<BuildingDetailSection> sections, BuildingBase building)
        {
            List<BuildingDetailRow> rows = new List<BuildingDetailRow>();

            if (building is IBuildingResourceConsumptionSource consumptionSource)
            {
                rows.Add(new BuildingDetailRow(
                    "预计消耗",
                    FormatResourceChanges(consumptionSource.CurrentResourceConsumptions)));
                rows.Add(new BuildingDetailRow(
                    "上回合消耗",
                    FormatResourceChanges(consumptionSource.LastResourceConsumptions)));
            }

            if (building is IBuildingResourceProductionSource productionSource)
            {
                rows.Add(new BuildingDetailRow(
                    "预计产出",
                    FormatResourceChanges(productionSource.CurrentResourceProductions)));
                rows.Add(new BuildingDetailRow(
                    "上回合产出",
                    FormatResourceChanges(productionSource.LastResourceProductions)));
            }

            if (building is IBuildingTaxSource taxSource)
            {
                rows.Add(new BuildingDetailRow(
                    "预计税收",
                    FormatResourceChanges(taxSource.CurrentTaxRewards)));
                rows.Add(new BuildingDetailRow(
                    "上回合税收",
                    FormatResourceChanges(taxSource.LastTaxRewards)));
            }

            AddSectionIfValid(sections, "资源", rows);
        }

        private static void AddSectionIfValid(
            List<BuildingDetailSection> sections,
            string title,
            IReadOnlyList<BuildingDetailRow> rows)
        {
            if (!HasAnyValidRow(rows))
            {
                return;
            }

            sections.Add(new BuildingDetailSection(title, rows));
        }

        private static bool HasAnyValidRow(IReadOnlyList<BuildingDetailRow> rows)
        {
            if (rows == null)
            {
                return false;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
