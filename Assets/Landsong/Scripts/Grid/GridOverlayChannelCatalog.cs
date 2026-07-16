using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    public static class GridOverlayChannelIds
    {
        public const string ValidFootprint = "C1";
        public const string InvalidFootprint = "C2";
        public const string RoadPath = "C3";
        public const string DemolitionTarget = "C4";
        public const string ResourceReachable = "C5";
        public const string ResourceProviders = "C6";
        public const string SelectedResourceProvider = "C7";
        public const string ResourcePath = "C8";
        public const string BuffRange = "C9";
        public const string SelectedBuildingFootprint = "C10";
        public const string SelectedBuildingReachableRange = "C11";
    }

    [CreateAssetMenu(
        menuName = "Landsong/Grid/Overlay Channel Catalog",
        fileName = "GridOverlayChannelCatalog")]
    public sealed class GridOverlayChannelCatalog : ScriptableObject
    {
        [SerializeField, LabelText("通道")]
        private GridOverlayChannelDefinition[] channels = Array.Empty<GridOverlayChannelDefinition>();

        public IReadOnlyList<GridOverlayChannelDefinition> Channels =>
            channels ?? Array.Empty<GridOverlayChannelDefinition>();

        public bool TryGet(string channelId, out GridOverlayChannelDefinition channel)
        {
            channel = null;
            channelId = NormalizeChannelId(channelId);
            if (string.IsNullOrEmpty(channelId) || channels == null)
            {
                return false;
            }

            for (var i = 0; i < channels.Length; i++)
            {
                var candidate = channels[i];
                if (candidate != null
                    && string.Equals(candidate.ChannelId, channelId, StringComparison.Ordinal))
                {
                    channel = candidate;
                    return candidate.IsValid;
                }
            }

            return false;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (channels == null || channels.Length == 0)
            {
                error = "GridOverlayChannelCatalog 没有配置任何通道。";
                return false;
            }

            var channelIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < channels.Length; i++)
            {
                var channel = channels[i];
                if (channel == null || !channel.IsValid)
                {
                    error = $"GridOverlayChannelCatalog.channels[{i}] 配置无效。";
                    return false;
                }

                if (!channelIds.Add(channel.ChannelId))
                {
                    error = $"GridOverlayChannelCatalog 存在重复 ChannelId：{channel.ChannelId}";
                    return false;
                }
            }

            string[] requiredIds =
            {
                GridOverlayChannelIds.ValidFootprint,
                GridOverlayChannelIds.InvalidFootprint,
                GridOverlayChannelIds.RoadPath,
                GridOverlayChannelIds.DemolitionTarget,
                GridOverlayChannelIds.ResourceReachable,
                GridOverlayChannelIds.ResourceProviders,
                GridOverlayChannelIds.SelectedResourceProvider,
                GridOverlayChannelIds.ResourcePath,
                GridOverlayChannelIds.BuffRange,
                GridOverlayChannelIds.SelectedBuildingFootprint,
                GridOverlayChannelIds.SelectedBuildingReachableRange
            };
            for (var i = 0; i < requiredIds.Length; i++)
            {
                if (!channelIds.Contains(requiredIds[i]))
                {
                    error = $"GridOverlayChannelCatalog 缺少必要通道：{requiredIds[i]}";
                    return false;
                }
            }

            return true;
        }

        private static string NormalizeChannelId(string channelId)
        {
            return string.IsNullOrWhiteSpace(channelId) ? string.Empty : channelId.Trim();
        }
    }
}
