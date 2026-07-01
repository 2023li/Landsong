using System.Collections.Generic;
using System.Text;
using Landsong.BuildingSystem;

namespace Landsong.UISystem
{
    public readonly struct BuildingStatusDisplayData
    {
        public BuildingStatusDisplayData(
            string buildingName,
            string statusText,
            string valueLabel,
            string valueText,
            string detailText,
            bool hasAbnormalStatus)
        {
            BuildingName = buildingName;
            StatusText = statusText;
            ValueLabel = valueLabel;
            ValueText = valueText;
            DetailText = detailText;
            HasAbnormalStatus = hasAbnormalStatus;
        }

        public string BuildingName { get; }
        public string StatusText { get; }
        public string ValueLabel { get; }
        public string ValueText { get; }
        public string DetailText { get; }
        public bool HasAbnormalStatus { get; }
    }

    public static class BuildingStatusUIFormatter
    {
        private static readonly IReadOnlyList<BuildingRuntimeStatus> EmptyStatuses =
            System.Array.Empty<BuildingRuntimeStatus>();

        public static BuildingStatusDisplayData CreateDisplayData(BuildingBase building, string normalStatusText = "正常")
        {
            var buildingName = GetBuildingName(building);
            var statuses = GetRuntimeStatuses(building);
            var hasAbnormalStatus = HasAnyStatus(statuses);
            var statusText = hasAbnormalStatus ? FormatStatuses(statuses) : normalStatusText;
            var valueLabel = string.Empty;
            var valueText = string.Empty;

            if (building is IBuildingOverviewSource overview)
            {
                valueLabel = overview.OverviewValueLabel ?? string.Empty;
                valueText = overview.OverviewValueText ?? string.Empty;
            }

            var detailText = FormatDetail(buildingName, statusText, valueLabel, valueText);
            return new BuildingStatusDisplayData(
                buildingName,
                statusText,
                valueLabel,
                valueText,
                detailText,
                hasAbnormalStatus);
        }

        public static IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses(BuildingBase building)
        {
            return building is IBuildingRuntimeStatusSource statusSource && statusSource.RuntimeStatuses != null
                ? statusSource.RuntimeStatuses
                : EmptyStatuses;
        }

        public static bool HasAnyStatus(IReadOnlyList<BuildingRuntimeStatus> statuses)
        {
            if (statuses == null)
            {
                return false;
            }

            for (var i = 0; i < statuses.Count; i++)
            {
                if (statuses[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        public static string GetBuildingName(BuildingBase building)
        {
            if (building == null)
            {
                return string.Empty;
            }

            var definition = building.Definition;
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName;
            }

            return building.name;
        }

        private static string FormatStatuses(IReadOnlyList<BuildingRuntimeStatus> statuses)
        {
            if (!HasAnyStatus(statuses))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (!status.IsValid)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("、");
                }

                builder.Append(FormatStatus(status));
            }

            return builder.ToString();
        }

        private static string FormatStatus(BuildingRuntimeStatus status)
        {
            if (status.Target > 0)
            {
                return $"{status.DisplayName} {status.Progress}/{status.Target}";
            }

            return status.DisplayName;
        }

        private static string FormatDetail(string buildingName, string statusText, string valueLabel, string valueText)
        {
            var builder = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(buildingName))
            {
                builder.Append(buildingName);
                builder.Append("：");
            }

            builder.Append(string.IsNullOrWhiteSpace(statusText) ? "正常" : statusText);

            if (!string.IsNullOrWhiteSpace(valueText))
            {
                builder.Append("，");
                if (!string.IsNullOrWhiteSpace(valueLabel))
                {
                    builder.Append(valueLabel);
                    builder.Append(" ");
                }

                builder.Append(valueText);
            }

            return builder.ToString();
        }
    }
}
