using System.Collections.Generic;
using System.Text;
using Landsong.BuildingSystem;
using Landsong.Localization;

namespace Landsong.UISystem
{
    public readonly struct BuildingStatusDisplayData
    {
        public BuildingStatusDisplayData(
            string buildingName,
            string statusInfoText,
            string baseInfoText,
            bool hasAbnormalStatus)
        {
            BuildingName = buildingName;
            StatusInfoText = statusInfoText ?? string.Empty;
            BaseInfoText = baseInfoText ?? string.Empty;
            HasAbnormalStatus = hasAbnormalStatus;
        }

        public string BuildingName { get; }
        public string StatusInfoText { get; }
        public string BaseInfoText { get; }
        public bool HasAbnormalStatus { get; }
    }

    public static class BuildingStatusUIFormatter
    {
        private static readonly IReadOnlyList<BuildingRuntimeStatus> EmptyStatuses =
            System.Array.Empty<BuildingRuntimeStatus>();

        public static BuildingStatusDisplayData CreateDisplayData(BuildingBase building, string normalStatusText = null)
        {
            normalStatusText ??= L10n.Gameplay("gameplay.building.status.normal", "正常");
            var buildingName = GetBuildingName(building);
            var statuses = GetRuntimeStatuses(building);
            var hasAbnormalStatus = BuildingRuntimeStatusCatalog.HasAbnormalStatus(statuses);
            var statusText = hasAbnormalStatus ? FormatAbnormalStatuses(statuses) : normalStatusText;
            var baseInfoText = building == null ? string.Empty : building.GetOverviewInfo() ?? string.Empty;

            return new BuildingStatusDisplayData(
                buildingName,
                statusText,
                baseInfoText,
                hasAbnormalStatus);
        }

        public static IReadOnlyList<BuildingRuntimeStatus> GetRuntimeStatuses(BuildingBase building)
        {
            return building == null ? EmptyStatuses : building.GetRuntimeStatuses() ?? EmptyStatuses;
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

        //格式化异常状态为字符串，多个状态用顿号分隔
        private static string FormatAbnormalStatuses(IReadOnlyList<BuildingRuntimeStatus> statuses)
        {
            if (!BuildingRuntimeStatusCatalog.HasAbnormalStatus(statuses))
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            for (var i = 0; i < statuses.Count; i++)
            {
                var status = statuses[i];
                if (!BuildingRuntimeStatusCatalog.IsAbnormalStatus(status))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(L10n.Gameplay("gameplay.common.list_separator", "、"));
                }

                builder.Append(FormatStatus(status));
            }

            return builder.ToString();
        }

        private static string FormatStatus(BuildingRuntimeStatus status)
        {
            if (status.Target > 0)
            {
                return L10n.Gameplay(
                    "gameplay.building.status.progress",
                    "{0} {1}/{2}",
                    BuildingRuntimeStatusCatalog.GetLocalizedDisplayName(status),
                    status.Progress,
                    status.Target);
            }

            return BuildingRuntimeStatusCatalog.GetLocalizedDisplayName(status);
        }

    }
}
