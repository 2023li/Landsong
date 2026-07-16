using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.BuildingSystem;
using UnityEditor;

namespace Landsong.EditorTools.Buildings
{
    internal static class BuildingPresentationMappingSynchronizer
    {
        public static bool TrySynchronize(
            BuildingFamilyDefinition family,
            out string error)
        {
            error = string.Empty;
            if (family == null || family.Presentation == null)
            {
                error = "建筑家族未绑定 Presentation。";
                return false;
            }

            var levels = family.Levels
                .Where(level => level != null)
                .Select(level => level.Level)
                .Distinct()
                .OrderBy(level => level)
                .ToArray();
            return TrySynchronize(family.Presentation, levels, out error);
        }

        public static bool TrySynchronize(
            BuildingPresentationDefinition presentation,
            IReadOnlyList<int> sourceLevels,
            out string error)
        {
            error = string.Empty;
            if (!TryBuildExpectedKeys(presentation, sourceLevels, out var expectedKeys, out error))
            {
                return false;
            }

            if (!TryBuildExistingMappings(
                    presentation,
                    expectedKeys,
                    out var existingByKey,
                    out error))
            {
                return false;
            }

            var synchronized = new List<BuildingViewMapping>(expectedKeys.Count);
            for (var i = 0; i < expectedKeys.Count; i++)
            {
                var key = expectedKeys[i];
                synchronized.Add(
                    existingByKey.TryGetValue(key, out var existing)
                        ? existing
                        : new BuildingViewMapping(key.Level, key.StyleId));
            }

            if (HasSameOrderedMappings(presentation.ViewMappings, synchronized))
            {
                return false;
            }

            presentation.ConfigureViewMappings(synchronized);
            EditorUtility.SetDirty(presentation);
            return true;
        }

        public static bool NeedsSynchronization(
            BuildingPresentationDefinition presentation,
            IReadOnlyList<int> sourceLevels,
            out string error)
        {
            error = string.Empty;
            if (!TryBuildExpectedKeys(presentation, sourceLevels, out var expectedKeys, out error))
            {
                return false;
            }

            if (!TryBuildExistingMappings(
                    presentation,
                    expectedKeys,
                    out _,
                    out error))
            {
                return false;
            }

            if (presentation.ViewMappings.Count != expectedKeys.Count)
            {
                return true;
            }

            for (var i = 0; i < expectedKeys.Count; i++)
            {
                var mapping = presentation.ViewMappings[i];
                if (mapping == null
                    || mapping.Level != expectedKeys[i].Level
                    || !string.Equals(
                        mapping.StyleId,
                        expectedKeys[i].StyleId,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildExistingMappings(
            BuildingPresentationDefinition presentation,
            IReadOnlyList<MappingKey> expectedKeys,
            out Dictionary<MappingKey, BuildingViewMapping> existingByKey,
            out string error)
        {
            existingByKey = new Dictionary<MappingKey, BuildingViewMapping>();
            error = string.Empty;
            var expectedKeySet = new HashSet<MappingKey>(expectedKeys);
            for (var i = 0; i < presentation.ViewMappings.Count; i++)
            {
                var mapping = presentation.ViewMappings[i];
                if (mapping == null)
                {
                    continue;
                }

                var key = new MappingKey(mapping.Level, mapping.StyleId);
                if (!expectedKeySet.Contains(key) && mapping.View?.IsConfigured == true)
                {
                    error =
                        $"即将移除的映射 {key} 仍配置了 View。请撤销本次等级/样式删除；如确需删除，先恢复该等级/样式并清空对应 View。";
                    return false;
                }

                if (!existingByKey.TryGetValue(key, out var existing))
                {
                    existingByKey.Add(key, mapping);
                    continue;
                }

                var existingConfigured = existing.View?.IsConfigured == true;
                var candidateConfigured = mapping.View?.IsConfigured == true;
                if (existingConfigured && candidateConfigured)
                {
                    error = $"映射 {key} 存在多个已配置 View，无法自动合并。";
                    return false;
                }

                if (!existingConfigured && candidateConfigured)
                {
                    existingByKey[key] = mapping;
                }
            }

            return true;
        }

        private static bool TryBuildExpectedKeys(
            BuildingPresentationDefinition presentation,
            IReadOnlyList<int> sourceLevels,
            out List<MappingKey> expectedKeys,
            out string error)
        {
            expectedKeys = new List<MappingKey>();
            error = string.Empty;
            if (presentation == null)
            {
                error = "Presentation 为空。";
                return false;
            }

            var levels = sourceLevels == null
                ? Array.Empty<int>()
                : sourceLevels
                    .Where(level => level > 0)
                    .Distinct()
                    .OrderBy(level => level)
                    .ToArray();
            if (levels.Length == 0)
            {
                error = "建筑未声明任何运营等级。";
                return false;
            }

            var styleIds = new List<string>();
            var uniqueStyleIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < presentation.Styles.Count; i++)
            {
                var style = presentation.Styles[i];
                if (style == null || !style.IsValid)
                {
                    error = $"视觉样式 #{i + 1} 缺少有效 StyleId。";
                    return false;
                }

                if (!uniqueStyleIds.Add(style.StyleId))
                {
                    error = $"视觉样式 ID 重复：{style.StyleId}";
                    return false;
                }

                styleIds.Add(style.StyleId);
            }

            if (styleIds.Count == 0)
            {
                for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
                {
                    expectedKeys.Add(new MappingKey(levels[levelIndex], string.Empty));
                }

                return true;
            }

            for (var styleIndex = 0; styleIndex < styleIds.Count; styleIndex++)
            {
                for (var levelIndex = 0; levelIndex < levels.Length; levelIndex++)
                {
                    expectedKeys.Add(new MappingKey(levels[levelIndex], styleIds[styleIndex]));
                }
            }

            return true;
        }

        private static bool HasSameOrderedMappings(
            IReadOnlyList<BuildingViewMapping> current,
            IReadOnlyList<BuildingViewMapping> expected)
        {
            if (current == null || current.Count != expected.Count)
            {
                return false;
            }

            for (var i = 0; i < current.Count; i++)
            {
                if (!ReferenceEquals(current[i], expected[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private readonly struct MappingKey : IEquatable<MappingKey>
        {
            public MappingKey(int level, string styleId)
            {
                Level = Math.Max(1, level);
                StyleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            }

            public int Level { get; }
            public string StyleId { get; }

            public bool Equals(MappingKey other)
            {
                return Level == other.Level
                       && string.Equals(StyleId, other.StyleId, StringComparison.Ordinal);
            }

            public override bool Equals(object obj)
            {
                return obj is MappingKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (Level * 397) ^ StringComparer.Ordinal.GetHashCode(StyleId);
                }
            }

            public override string ToString()
            {
                return string.IsNullOrEmpty(StyleId)
                    ? $"默认样式/LV{Level}"
                    : $"{StyleId}/LV{Level}";
            }
        }
    }
}
