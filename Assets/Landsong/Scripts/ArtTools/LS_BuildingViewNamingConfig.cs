using System;
using System.Collections.Generic;
using Landsong.ECS.Authoring;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.VisualSystem
{
    public enum LS_BuildingViewPurpose
    {
        Operational = 0,
        Construction = 10
    }

    /// <summary>
    /// 建筑制作场景名称、输出 Prefab 前缀和 ECS Definition 的唯一显式映射。
    /// 解析只接受完整名称规则，不进行包含、近似或本地化名称猜测。
    /// </summary>
    [CreateAssetMenu(
        menuName = "Landsong/Building/View Naming Config",
        fileName = "BuildingViewNamingConfig")]
    public sealed class LS_BuildingViewNamingConfig : ScriptableObject
    {
        public const string DefaultAssetPath =
            "Assets/Landsong/Objects/SO/Buildings/BuildingViewNamingConfig.asset";
        public const string DefaultViewOutputRoot =
            "Assets/Landsong/Objects/Generated/BuildingVisuals";

        [Serializable]
        public sealed class Entry
        {
            [LabelText("ECS Definition")]
            [Tooltip("允许暂时留空，但批量绑定会明确报告该前缀没有 ECS 定义，绝不会猜测。")]
            public GameDefinitionAsset Definition;

            [LabelText("制作场景名称前缀"), Required]
            [Tooltip("仅匹配“前缀_LV数字”“前缀_建造阶段”或显式开启的裸名称。")]
            public string AuthoringPrefix;

            [LabelText("输出 Prefab 名称前缀"), Required]
            public string PrefabPrefix;

            [LabelText("建造对象完整名称覆盖")]
            [Tooltip("留空时固定为“制作场景名称前缀_建造阶段”；用于仓库等已经稳定的特殊名称。")]
            public string ConstructionAuthoringNameOverride;

            [LabelText("裸名称视为 LV1")]
            [Tooltip("开启后，名称与制作前缀完全相等的对象会被视为 LV1。")]
            public bool BareNameIsLevelOne;

            [LabelText("预期运营等级覆盖")]
            [Tooltip("预期制作的美术等级，可包含玩法尚未开放的等级。不会增加 ECS 建筑的可升级等级。")]
            public int[] ExpectedOperationalLevelsOverride = Array.Empty<int>();

            [LabelText("要求建造阶段 View")]
            public bool ExpectsConstructionView = true;

            [LabelText("导出时同步 ECS 对应等级/施工槽位")]
            public bool BindLevelOneAsPlacementPreview = true;

            public string NormalizedAuthoringPrefix => Normalize(AuthoringPrefix);
            public string NormalizedPrefabPrefix => Normalize(PrefabPrefix);
            public string ConstructionAuthoringName => string.IsNullOrWhiteSpace(
                    ConstructionAuthoringNameOverride)
                ? NormalizedAuthoringPrefix + "_建造阶段"
                : ConstructionAuthoringNameOverride.Trim();

            private static string Normalize(string value)
            {
                return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            }
        }

        public readonly struct ResolvedView
        {
            public ResolvedView(Entry entry, LS_BuildingViewPurpose purpose, int level)
            {
                Entry = entry;
                Purpose = purpose;
                Level = Mathf.Max(1, level);
            }

            public Entry Entry { get; }
            public LS_BuildingViewPurpose Purpose { get; }
            public int Level { get; }
            public string PrefabBaseName => Purpose == LS_BuildingViewPurpose.Construction
                ? $"{Entry.NormalizedPrefabPrefix}_建造阶段_Optimized"
                : $"{Entry.NormalizedPrefabPrefix}_LV{Level}_Optimized";
            public string BuildingFolderName => Entry.NormalizedPrefabPrefix;
        }

        [SerializeField, LabelText("视觉优化产物根目录"), FolderPath(AbsolutePath = false)]
        private string viewOutputRoot = DefaultViewOutputRoot;

        [SerializeField, LabelText("固定名称映射")]
        [ListDrawerSettings(ShowIndexLabels = true, ListElementLabelName = nameof(Entry.AuthoringPrefix))]
        private Entry[] entries = Array.Empty<Entry>();

        public string ViewOutputRoot => string.IsNullOrWhiteSpace(viewOutputRoot)
            ? DefaultViewOutputRoot
            : viewOutputRoot.Trim().Replace('\\', '/');
        public IReadOnlyList<Entry> Entries => entries ?? Array.Empty<Entry>();

        public bool TryResolveAuthoringName(
            string gameObjectName,
            out ResolvedView result,
            out string error)
        {
            result = default;
            error = string.Empty;
            gameObjectName = string.IsNullOrWhiteSpace(gameObjectName)
                ? string.Empty
                : gameObjectName.Trim();
            gameObjectName = LS_BuildingViewOptimizer.RemoveCompletedNameSuffix(gameObjectName);

            Entry matchedEntry = null;
            var matchedPurpose = LS_BuildingViewPurpose.Operational;
            var matchedLevel = 1;
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null
                    || string.IsNullOrEmpty(entry.NormalizedAuthoringPrefix)
                    || string.IsNullOrEmpty(entry.NormalizedPrefabPrefix)
                    || !TryMatchEntry(entry, gameObjectName, out var purpose, out var level))
                {
                    continue;
                }

                if (matchedEntry != null)
                {
                    error =
                        $"名称“{gameObjectName}”同时命中多个固定映射：“{matchedEntry.NormalizedAuthoringPrefix}”与“{entry.NormalizedAuthoringPrefix}”。";
                    return false;
                }

                matchedEntry = entry;
                matchedPurpose = purpose;
                matchedLevel = level;
            }

            if (matchedEntry == null)
            {
                error =
                    $"名称“{gameObjectName}”不符合任何固定映射。请在 {DefaultAssetPath} 中显式添加前缀，不会进行模糊猜测。";
                return false;
            }

            if (matchedPurpose == LS_BuildingViewPurpose.Operational
                && matchedEntry.Definition != null
                && matchedLevel > matchedEntry.Definition.Data.Level
                && Array.IndexOf(matchedEntry.ExpectedOperationalLevelsOverride ?? Array.Empty<int>(), matchedLevel) < 0)
            {
                error =
                    $"固定映射“{matchedEntry.NormalizedAuthoringPrefix}”关联的 ECS 定义 不包含 LV{matchedLevel}。";
                return false;
            }

            result = new ResolvedView(matchedEntry, matchedPurpose, matchedLevel);
            return true;
        }

        public bool TryValidate(out string error)
        {
            error = string.Empty;
            if (!ViewOutputRoot.StartsWith("Assets/", StringComparison.Ordinal)
                && !string.Equals(ViewOutputRoot, "Assets", StringComparison.Ordinal))
            {
                error = $"View 输出根目录必须位于 Assets 内：{ViewOutputRoot}";
                return false;
            }

            var authoringPrefixes = new HashSet<string>(StringComparer.Ordinal);
            var prefabPrefixes = new HashSet<string>(StringComparer.Ordinal);
            var constructionNames = new HashSet<string>(StringComparer.Ordinal);
            var families = new HashSet<GameDefinitionAsset>();
            for (var i = 0; i < Entries.Count; i++)
            {
                var entry = Entries[i];
                if (entry == null)
                {
                    error = $"固定名称映射 #{i + 1} 为空。";
                    return false;
                }

                if (string.IsNullOrEmpty(entry.NormalizedAuthoringPrefix)
                    || string.IsNullOrEmpty(entry.NormalizedPrefabPrefix))
                {
                    error = $"固定名称映射 #{i + 1} 缺少制作前缀或 Prefab 前缀。";
                    return false;
                }

                if (!authoringPrefixes.Add(entry.NormalizedAuthoringPrefix))
                {
                    error = $"制作场景名称前缀重复：{entry.NormalizedAuthoringPrefix}";
                    return false;
                }

                if (!prefabPrefixes.Add(entry.NormalizedPrefabPrefix))
                {
                    error = $"输出 Prefab 名称前缀重复：{entry.NormalizedPrefabPrefix}";
                    return false;
                }

                if (entry.ExpectsConstructionView
                    && !constructionNames.Add(entry.ConstructionAuthoringName))
                {
                    error = $"建造对象完整名称重复：{entry.ConstructionAuthoringName}";
                    return false;
                }

                if (entry.Definition != null && !families.Add(entry.Definition))
                {
                    error = $"同一个 ECS Definition 被配置了多次：{entry.Definition.name}";
                    return false;
                }

                var expectedLevels = new HashSet<int>();
                foreach (var level in entry.ExpectedOperationalLevelsOverride ?? Array.Empty<int>())
                {
                    if (level <= 0 || !expectedLevels.Add(level))
                    {
                        error = $"“{entry.NormalizedAuthoringPrefix}”的预期运营等级覆盖包含无效或重复等级：{level}";
                        return false;
                    }

                }
            }

            return true;
        }

        private static bool TryMatchEntry(
            Entry entry,
            string objectName,
            out LS_BuildingViewPurpose purpose,
            out int level)
        {
            purpose = LS_BuildingViewPurpose.Operational;
            level = 1;
            if (string.Equals(objectName, entry.ConstructionAuthoringName, StringComparison.Ordinal))
            {
                purpose = LS_BuildingViewPurpose.Construction;
                return true;
            }

            if (entry.BareNameIsLevelOne
                && string.Equals(objectName, entry.NormalizedAuthoringPrefix, StringComparison.Ordinal))
            {
                return true;
            }

            var levelPrefix = entry.NormalizedAuthoringPrefix + "_LV";
            if (!objectName.StartsWith(levelPrefix, StringComparison.Ordinal))
            {
                return false;
            }

            var levelText = objectName.Substring(levelPrefix.Length);
            return int.TryParse(levelText, out level)
                   && level > 0
                   && string.Equals(
                       objectName,
                       levelPrefix + level,
                       StringComparison.Ordinal);
        }
    }
}
