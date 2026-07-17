using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Landsong.BuildingSystem;
using Landsong.ExpeditionSystem;
using Landsong.GameEventSystem;
using Landsong.InheritanceSystem;
using Landsong.InventorySystem;
using Landsong.Localization;
using Landsong.PolicySystem;
using Landsong.TalentSystem;
using Landsong.TechnologySystem;
using TMPro;
using UnityEditor;
using UnityEditor.Localization;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using UnityEngine.SceneManagement;

namespace Landsong.EditorTools.Localization
{
    public static class LocalizationEditorPipeline
    {
        private const string TableRoot = "Assets/Landsong/Objects/本地化/Tables";

        [MenuItem("Landsong/Localization/Run Static Text Migration")]
        public static void RunStaticTextMigration()
        {
            var tables = EnsureCollections();
            var missingTranslations = new List<string>();
            var migratedCount = 0;
            migratedCount += MigratePrefabs(tables, missingTranslations);
            migratedCount += MigrateScenes(tables, missingTranslations);
            for (var i = 0; i < UiEntries.Length; i++)
            {
                var entry = UiEntries[i];
                Upsert(tables[LocalizationTables.Ui], entry.Key, entry.Chinese, entry.English, entry.IsSmart);
            }
            SaveTables(tables);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (missingTranslations.Count > 0)
            {
                var distinctMissing = missingTranslations.Distinct().OrderBy(value => value).ToArray();
                throw new InvalidOperationException(
                    $"静态文本迁移缺少 {distinctMissing.Length} 条英文翻译：\n" +
                    string.Join("\n", distinctMissing));
            }

            Debug.Log($"静态文本迁移完成：绑定 {migratedCount} 个 TMP 文本。");
        }

        [MenuItem("Landsong/Localization/Export Language Pack Template")]
        public static void ExportLanguagePackTemplate()
        {
            var path = EditorUtility.SaveFilePanel(
                "Export Language Pack Template",
                Application.dataPath,
                "strings-template",
                "csv");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            var tables = EnsureCollections();
            var builder = new StringBuilder("Table,Key,Text\n");
            for (var tableIndex = 0; tableIndex < LocalizationTables.All.Length; tableIndex++)
            {
                var tableName = LocalizationTables.All[tableIndex];
                var collection = tables[tableName];
                var english = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
                var entries = collection.SharedData.Entries.OrderBy(entry => entry.Key).ToArray();
                for (var i = 0; i < entries.Length; i++)
                {
                    builder.Append(EscapeCsv(tableName)).Append(',')
                        .Append(EscapeCsv(entries[i].Key)).Append(',')
                        .Append(EscapeCsv(english?.GetEntry(entries[i].Id)?.Value ?? string.Empty))
                        .Append('\n');
                }
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(false));
            Debug.Log($"语言包模板已导出：{path}");
        }

        [MenuItem("Landsong/Localization/Run Release Validation")]
        public static void RunReleaseValidation()
        {
            var errors = new List<string>();
            var tables = EnsureCollections();
            foreach (var pair in tables)
            {
                ValidateCollection(pair.Key, pair.Value, errors);
            }

            ValidateStaticPrefabBindings(errors);
            ValidateStaticSceneBindings(errors);
            ValidateRuntimeSourceHardcoding(errors);
            var sampleRoot = Path.GetFullPath("Document/音频与本地化/LanguagePackSample");
            var sample = new ExternalLanguagePackRepository().Read(sampleRoot);
            if (!sample.Success)
            {
                errors.Add("示例语言包未通过解析：" + string.Join(
                    "; ",
                    sample.Info?.Diagnostics?.Select(item => item.Message) ?? Array.Empty<string>()));
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"本地化发布校验失败（{errors.Count}）：\n" + string.Join("\n", errors));
            }

            Debug.Log($"本地化发布校验通过：UI={tables[LocalizationTables.Ui].SharedData.Entries.Count}，" +
                      $"Content={tables[LocalizationTables.Content].SharedData.Entries.Count}，" +
                      $"Gameplay={tables[LocalizationTables.Gameplay].SharedData.Entries.Count}。 ");
        }

        private static void ValidateCollection(
            string tableName,
            StringTableCollection collection,
            List<string> errors)
        {
            var chinese = collection.GetTable(new LocaleIdentifier("zh-Hans")) as StringTable;
            var english = collection.GetTable(new LocaleIdentifier("en")) as StringTable;
            if (chinese == null || english == null)
            {
                errors.Add($"{tableName} 缺少 zh-Hans 或 en 表。 ");
                return;
            }

            var entries = collection.SharedData.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                var shared = entries[i];
                var zhEntry = chinese.GetEntry(shared.Id);
                var enEntry = english.GetEntry(shared.Id);
                if (zhEntry == null || enEntry == null
                    || string.IsNullOrEmpty(zhEntry.Value)
                    || string.IsNullOrEmpty(enEntry.Value))
                {
                    errors.Add($"{tableName}/{shared.Key} 缺少内置中英文文本。 ");
                    continue;
                }

                if (zhEntry.IsSmart != enEntry.IsSmart)
                {
                    errors.Add($"{tableName}/{shared.Key} 的中英文 Smart 标记不一致。 ");
                    continue;
                }

                if (zhEntry.IsSmart
                    && !ExtractFormatArguments(zhEntry.Value).SetEquals(ExtractFormatArguments(enEntry.Value)))
                {
                    errors.Add($"{tableName}/{shared.Key} 的中英文 Smart 参数不一致。 ");
                }
            }
        }

        private static void ValidateStaticPrefabBindings(List<string> errors)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Landsong/Objects" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var texts = root.GetComponentsInChildren<TMP_Text>(true);
                    for (var textIndex = 0; textIndex < texts.Length; textIndex++)
                    {
                        var text = texts[textIndex];
                        if (!ContainsHan(text.text ?? string.Empty))
                        {
                            continue;
                        }

                        var binding = text.GetComponent<LocalizedTextBinding>();
                        if (binding == null || string.IsNullOrWhiteSpace(binding.Key))
                        {
                            errors.Add($"Prefab 固定中文未绑定：{path}/{AnimationUtility.CalculateTransformPath(text.transform, root.transform)}");
                        }
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void ValidateStaticSceneBindings(List<string> errors)
        {
            var originalScenePath = SceneManager.GetActiveScene().path;
            var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Landsong/Scenes" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    ValidateStaticHierarchyBindings(roots[rootIndex], path, errors);
                }
            }

            if (!string.IsNullOrWhiteSpace(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }
        }

        private static void ValidateStaticHierarchyBindings(GameObject root, string assetPath, List<string> errors)
        {
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                if (!ContainsHan(text.text ?? string.Empty))
                {
                    continue;
                }

                var binding = text.GetComponent<LocalizedTextBinding>();
                if (binding == null || string.IsNullOrWhiteSpace(binding.Key))
                {
                    errors.Add($"Scene 固定中文未绑定：{assetPath}/{AnimationUtility.CalculateTransformPath(text.transform, root.transform)}");
                }
            }
        }

        private static void ValidateRuntimeSourceHardcoding(List<string> errors)
        {
            var sourceRoot = Path.GetFullPath("Assets/Landsong/Scripts");
            var files = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (var fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                var file = files[fileIndex].Replace('\\', '/');
                if (file.Contains("/Editor/") || file.Contains("/Debug/"))
                {
                    continue;
                }

                var lines = File.ReadAllLines(file);
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];
                    if (!IsSuspectedPlayerFacingHardcoding(file, line))
                    {
                        continue;
                    }

                    var assetPath = "Assets" + file.Substring(file.IndexOf("/Assets/", StringComparison.Ordinal) + 7);
                    errors.Add($"运行时疑似玩家文本硬编码：{assetPath}:{lineIndex + 1} {line.Trim()}");
                }
            }
        }

        private static bool IsSuspectedPlayerFacingHardcoding(string file, string line)
        {
            if (string.IsNullOrWhiteSpace(line)
                || !Regex.IsMatch(line, "\\\"[^\\\"\\r\\n]*[\\u3400-\\u9fff][^\\\"\\r\\n]*\\\"")
                || line.Contains("L10n.")
                || line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            var nonPlayerMarkers = new[]
            {
                "LabelText(", "Header(", "Tooltip(", "InspectorName(", "BoxGroup(", "FoldoutGroup(",
                "Button(", "InfoBox(", "Debug.", "ModuleDescription", "throw new", "error =", "nameof(",
                "new BuildingRuntimeStatus", "SetStatus(", "new BuildingFunctionBlock", "new BuildingDetailsSidebarRow",
                "return \"BM_", "return \"Item_"
            };
            for (var i = 0; i < nonPlayerMarkers.Length; i++)
            {
                if (line.Contains(nonPlayerMarkers[i]))
                {
                    return false;
                }
            }

            if (file.EndsWith("/BuildingJobSystem.cs", StringComparison.Ordinal)
                && line.Contains("builder.Append"))
            {
                return false;
            }

            var playerTextMarkers = new[]
            {
                ".text =", "SetText(", "return \"", "return $\"", "builder.Append(", "builder.AppendLine(",
                "new TalentRefreshResult", "new TalentRecruitResult", "new TalentAssignResult", "new TalentUpgradeResult",
                "new BuildingPlacementResult", "new BuildingBatchPlacementResult", "failureMessage ="
            };
            for (var i = 0; i < playerTextMarkers.Length; i++)
            {
                if (line.Contains(playerTextMarkers[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static HashSet<string> ExtractFormatArguments(string value)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var matches = Regex.Matches(value ?? string.Empty, @"(?<!\{)\{([A-Za-z0-9_]+)(?:[^}]*)\}(?!\})");
            for (var i = 0; i < matches.Count; i++)
            {
                result.Add(matches[i].Groups[1].Value);
            }

            return result;
        }

        private static string EscapeCsv(string value)
        {
            var source = value ?? string.Empty;
            return source.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0
                ? source
                : '"' + source.Replace("\"", "\"\"") + '"';
        }

        public static void RunBatchMigration()
        {
            RemoveLegacyGameStringCollection();
            RunStaticTextMigration();
            RunContentMigration();
            RunGameplayMigration();
        }

        private static void RemoveLegacyGameStringCollection()
        {
            const string legacyFolder = "Assets/Landsong/Objects/本地化/Tables/GameTable";
            if (!AssetDatabase.IsValidFolder(legacyFolder))
            {
                return;
            }

            if (!AssetDatabase.DeleteAsset(legacyFolder))
            {
                throw new InvalidOperationException($"无法移除已废弃的本地化表目录：{legacyFolder}");
            }

            AssetDatabase.Refresh();
            Debug.Log("已移除废弃的 GameString 本地化表。 ");
        }

        [MenuItem("Landsong/Localization/Run Gameplay Migration")]
        public static void RunGameplayMigration()
        {
            var tables = EnsureCollections();
            var gameplay = tables[LocalizationTables.Gameplay];
            ClearCollection(gameplay);
            for (var i = 0; i < GameEventCatalog.Definitions.Count; i++)
            {
                var definition = GameEventCatalog.Definitions[i];
                if (!EventNameEnglish.TryGetValue(definition.EventTypeId, out var english))
                {
                    throw new InvalidOperationException($"事件名称缺少英文翻译：{definition.EventTypeId}/{definition.DisplayName}");
                }

                Upsert(
                    gameplay,
                    $"gameplay.event.{Landsong.Localization.L10n.NormalizeKeyPart(definition.EventTypeId)}.name",
                    definition.DisplayName,
                    english,
                    false);
            }

            for (var i = 0; i < GameplayEntries.Length; i++)
            {
                var entry = GameplayEntries[i];
                Upsert(gameplay, entry.Key, entry.Chinese, entry.English, entry.IsSmart);
            }

            SaveTables(tables);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Gameplay 迁移完成：写入 {GameEventCatalog.Definitions.Count + GameplayEntries.Length} 个中英文表项。");
        }

        [MenuItem("Landsong/Localization/Run Content Migration")]
        public static void RunContentMigration()
        {
            var tables = EnsureCollections();
            var content = tables[LocalizationTables.Content];
            ClearCollection(content);
            var missingTranslations = new List<string>();
            var migratedCount = 0;

            ForEachAsset<ItemDefinition>(asset =>
                MigrateContentEntry(content, "item", asset.ItemId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<ItemDefinition>(asset =>
                MigrateContentEntry(content, "item", asset.ItemId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount));
            ForEachAsset<ItemGroupDefinition>(asset =>
                MigrateContentEntry(content, "item_group", asset.GroupId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<ItemGroupDefinition>(asset =>
                MigrateContentEntry(content, "item_group", asset.GroupId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount));
            ForEachAsset<InventorySlotTypeDefinition>(asset =>
                MigrateContentEntry(content, "inventory_slot", asset.SlotType.ToString(), "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<TechnologyDefinition>(asset =>
                MigrateContentEntry(content, "technology", asset.TechnologyId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<TechnologyDefinition>(asset =>
                MigrateContentEntry(content, "technology", asset.TechnologyId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount));
            ForEachAsset<QuestDefinition>(asset =>
                MigrateContentEntry(content, "quest", asset.QuestId, "name", Raw(asset, "definition.displayName", asset.Data?.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<QuestDefinition>(asset =>
                MigrateContentEntry(content, "quest", asset.QuestId, "description", Raw(asset, "definition.description", asset.Data?.Description), missingTranslations, ref migratedCount));
            ForEachAsset<BuildingFamilyDefinition>(asset =>
                MigrateContentEntry(content, "building", asset.FamilyId, "name", Raw(asset, "definition.displayName", asset.Definition?.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<BuildingPresentationDefinition>(asset =>
            {
                var serialized = new SerializedObject(asset);
                var styles = serialized.FindProperty("styles");
                for (var i = 0; styles != null && i < styles.arraySize && i < asset.Styles.Count; i++)
                {
                    var source = styles.GetArrayElementAtIndex(i).FindPropertyRelative("displayName")?.stringValue;
                    var style = asset.Styles[i];
                    MigrateContentEntry(content, "building_style", style.StyleId, "name", source, missingTranslations, ref migratedCount);
                }
            });
            ForEachAsset<BuildingSpatialEffectDefinition>(asset =>
                MigrateContentEntry(content, "building_effect", asset.EffectId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<MapDefinition>(asset =>
                MigrateContentEntry(content, "map", asset.MapId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));
            ForEachAsset<MapDefinition>(asset =>
                MigrateContentEntry(content, "map", asset.MapId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount));
            ForEachAsset<PolicyDefinition>(asset =>
            {
                MigrateContentEntry(content, "policy", asset.PolicyId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount);
                MigrateContentEntry(content, "policy", asset.PolicyId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount);
                MigrateContentEntry(content, "policy_tree", asset.TreeId, "name", Raw(asset, "treeDisplayName", asset.TreeDisplayName), missingTranslations, ref migratedCount);
            });
            ForEachAsset<ExpeditionDestinationDefinition>(asset =>
            {
                MigrateContentEntry(content, "expedition", asset.DestinationId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount);
                MigrateContentEntry(content, "expedition", asset.DestinationId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount);
            });
            ForEachAsset<TalentDefinition>(asset =>
            {
                MigrateContentEntry(content, "talent", asset.TalentId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount);
                MigrateContentEntry(content, "talent", asset.TalentId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount);
            });
            ForEachAsset<RoyalTraitDefinition>(asset =>
            {
                MigrateContentEntry(content, "royal_trait", asset.TraitId, "name", Raw(asset, "traitName", asset.TraitName), missingTranslations, ref migratedCount);
                MigrateContentEntry(content, "royal_trait", asset.TraitId, "description", Raw(asset, "description", asset.Description), missingTranslations, ref migratedCount);
            });
            ForEachAsset<TechnologyGlobalBuffDefinition>(asset =>
                MigrateContentEntry(content, "technology_buff", asset.BuffId, "name", Raw(asset, "displayName", asset.DisplayName), missingTranslations, ref migratedCount));

            SaveTables(tables);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            if (missingTranslations.Count > 0)
            {
                var missing = missingTranslations.Distinct().OrderBy(value => value).ToArray();
                throw new InvalidOperationException($"Content 缺少 {missing.Length} 条英文翻译：\n" + string.Join("\n", missing));
            }

            Debug.Log($"Content 迁移完成：写入 {migratedCount} 个中英文表项。");
        }

        private static void ForEachAsset<T>(Action<T> action) where T : UnityEngine.Object
        {
            var guids = AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { "Assets/Landsong/Objects" });
            for (var i = 0; i < guids.Length; i++)
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guids[i]));
                if (asset != null)
                {
                    action(asset);
                }
            }
        }

        private static string Raw(UnityEngine.Object asset, string propertyPath, string fallback)
        {
            var property = new SerializedObject(asset).FindProperty(propertyPath);
            return property == null ? fallback ?? string.Empty : property.stringValue ?? string.Empty;
        }

        private static void MigrateContentEntry(
            StringTableCollection content,
            string category,
            string stableId,
            string field,
            string chinese,
            List<string> missingTranslations,
            ref int migratedCount)
        {
            if (string.IsNullOrWhiteSpace(stableId) || string.IsNullOrWhiteSpace(chinese))
            {
                return;
            }

            chinese = chinese.Trim();
            if (!ChineseToEnglish.TryGetValue(chinese, out var english))
            {
                missingTranslations.Add(chinese);
                return;
            }

            var key = Landsong.Localization.L10n.BuildContentKey(category, stableId, field);
            Upsert(content, key, chinese, english, false);
            migratedCount++;
        }

        private static Dictionary<string, StringTableCollection> EnsureCollections()
        {
            Directory.CreateDirectory(TableRoot);
            var result = new Dictionary<string, StringTableCollection>(StringComparer.Ordinal);
            for (var i = 0; i < LocalizationTables.All.Length; i++)
            {
                var tableName = LocalizationTables.All[i];
                var collection = LocalizationEditorSettings.GetStringTableCollection(tableName)
                                 ?? LocalizationEditorSettings.CreateStringTableCollection(tableName, TableRoot);
                if (collection == null)
                {
                    throw new InvalidOperationException($"无法创建 StringTable Collection：{tableName}");
                }

                EnsureLocaleTable(collection, "zh-Hans");
                EnsureLocaleTable(collection, "en");
                result.Add(tableName, collection);
            }

            return result;
        }

        private static void EnsureLocaleTable(StringTableCollection collection, string localeCode)
        {
            var identifier = new LocaleIdentifier(localeCode);
            if (collection.GetTable(identifier) == null)
            {
                collection.AddNewTable(identifier);
            }
        }

        private static int MigratePrefabs(
            IReadOnlyDictionary<string, StringTableCollection> tables,
            List<string> missingTranslations)
        {
            var migratedCount = 0;
            var prefabGuids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/Landsong/Objects" });
            for (var i = 0; i < prefabGuids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
                var root = PrefabUtility.LoadPrefabContents(assetPath);
                try
                {
                    var changed = MigrateHierarchy(root, assetPath, tables, missingTranslations, ref migratedCount);
                    if (changed)
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                    }
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            return migratedCount;
        }

        private static int MigrateScenes(
            IReadOnlyDictionary<string, StringTableCollection> tables,
            List<string> missingTranslations)
        {
            var migratedCount = 0;
            var originalScenePath = SceneManager.GetActiveScene().path;
            var sceneGuids = AssetDatabase.FindAssets(
                "t:Scene",
                new[] { "Assets/Landsong/Scenes" });
            for (var i = 0; i < sceneGuids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(sceneGuids[i]);
                var scene = EditorSceneManager.OpenScene(assetPath, OpenSceneMode.Single);
                var changed = false;
                var roots = scene.GetRootGameObjects();
                for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    changed |= MigrateHierarchy(
                        roots[rootIndex],
                        assetPath,
                        tables,
                        missingTranslations,
                        ref migratedCount);
                }

                if (changed)
                {
                    EditorSceneManager.SaveScene(scene);
                }
            }

            if (!string.IsNullOrWhiteSpace(originalScenePath))
            {
                EditorSceneManager.OpenScene(originalScenePath, OpenSceneMode.Single);
            }

            return migratedCount;
        }

        private static bool MigrateHierarchy(
            GameObject root,
            string assetPath,
            IReadOnlyDictionary<string, StringTableCollection> tables,
            List<string> missingTranslations,
            ref int migratedCount)
        {
            var changed = false;
            var texts = root.GetComponentsInChildren<TMP_Text>(true);
            for (var i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                var source = text.text ?? string.Empty;
                if (!ShouldLocalize(source))
                {
                    continue;
                }

                if (!TryGetTranslations(source, out var chinese, out var english))
                {
                    if (ContainsHan(source))
                    {
                        missingTranslations.Add(source);
                    }

                    continue;
                }

                var hierarchyPath = AnimationUtility.CalculateTransformPath(text.transform, root.transform);
                var componentIndex = Array.IndexOf(text.GetComponents<TMP_Text>(), text);
                var key = BuildStaticUiKey(assetPath, hierarchyPath, componentIndex);
                Upsert(tables[LocalizationTables.Ui], key, chinese, english, false);

                var binding = text.GetComponent<LocalizedTextBinding>();
                if (binding == null)
                {
                    binding = text.gameObject.AddComponent<LocalizedTextBinding>();
                }

                binding.Configure(LocalizationTables.Ui, key, source);
                EditorUtility.SetDirty(binding);
                changed = true;
                migratedCount++;
            }

            return changed;
        }

        private static void Upsert(
            StringTableCollection collection,
            string key,
            string chinese,
            string english,
            bool isSmart)
        {
            var sharedEntry = collection.SharedData.GetEntry(key) ?? collection.SharedData.AddKey(key);
            SetTableValue(collection, new LocaleIdentifier("zh-Hans"), sharedEntry.Id, chinese, isSmart);
            SetTableValue(collection, new LocaleIdentifier("en"), sharedEntry.Id, english, isSmart);
            EditorUtility.SetDirty(collection.SharedData);
        }

        private static void SetTableValue(
            StringTableCollection collection,
            LocaleIdentifier localeIdentifier,
            long entryId,
            string value,
            bool isSmart)
        {
            var table = collection.GetTable(localeIdentifier) as StringTable;
            if (table == null)
            {
                throw new InvalidOperationException(
                    $"Collection {collection.TableCollectionName} 缺少 Locale {localeIdentifier.Code}。 ");
            }

            var entry = table.GetEntry(entryId) ?? table.AddEntry(entryId, value ?? string.Empty);
            entry.Value = value ?? string.Empty;
            entry.IsSmart = isSmart;
            EditorUtility.SetDirty(table);
        }

        private static void SaveTables(IReadOnlyDictionary<string, StringTableCollection> tables)
        {
            foreach (var collection in tables.Values)
            {
                EditorUtility.SetDirty(collection.SharedData);
                for (var i = 0; i < collection.StringTables.Count; i++)
                {
                    EditorUtility.SetDirty(collection.StringTables[i]);
                }
            }
        }

        private static void ClearCollection(StringTableCollection collection)
        {
            for (var i = 0; i < collection.StringTables.Count; i++)
            {
                collection.StringTables[i].Clear();
                EditorUtility.SetDirty(collection.StringTables[i]);
            }

            collection.SharedData.Clear();
            EditorUtility.SetDirty(collection.SharedData);
        }

        private static string BuildStaticUiKey(string assetPath, string hierarchyPath, int componentIndex)
        {
            var readableAssetName = Landsong.Localization.L10n.NormalizeKeyPart(
                Path.GetFileNameWithoutExtension(assetPath));
            var fingerprint = Fnv1a32($"{assetPath}|{hierarchyPath}|{componentIndex}");
            return $"ui.asset.{readableAssetName}.{fingerprint:x8}";
        }

        private static uint Fnv1a32(string value)
        {
            const uint offset = 2166136261;
            const uint prime = 16777619;
            var hash = offset;
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            for (var i = 0; i < bytes.Length; i++)
            {
                hash ^= bytes[i];
                hash *= prime;
            }

            return hash;
        }

        private static bool ShouldLocalize(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return false;
            }

            var trimmed = source.Trim();
            if (trimmed is "New Text" or "Label Text" or "Button")
            {
                return false;
            }

            var hasLetter = false;
            for (var i = 0; i < trimmed.Length; i++)
            {
                if (char.IsLetter(trimmed[i]))
                {
                    hasLetter = true;
                    break;
                }
            }

            return hasLetter;
        }

        private static bool TryGetTranslations(string source, out string chinese, out string english)
        {
            if (ChineseToEnglish.TryGetValue(source, out english))
            {
                chinese = source;
                return true;
            }

            if (EnglishToChinese.TryGetValue(source, out chinese))
            {
                english = source;
                return true;
            }

            if (source.EndsWith(" 科技点", StringComparison.Ordinal)
                && int.TryParse(source.Substring(0, source.Length - 4).Trim(), out var sciencePoints))
            {
                chinese = source;
                english = $"{sciencePoints} Research Points";
                return true;
            }

            if (source.StartsWith("人口：", StringComparison.Ordinal))
            {
                chinese = source;
                english = "Population: " + source.Substring(3);
                return true;
            }

            if (source.StartsWith("回合:", StringComparison.Ordinal))
            {
                chinese = source;
                english = "Turn: " + source.Substring(3);
                return true;
            }

            if (source.Length == 2 && source[1] is >= '0' and <= '9')
            {
                chinese = source;
                english = source[0] switch
                {
                    '土' => $"Dirt {source[1]}",
                    '石' => $"Stone {source[1]}",
                    _ => string.Empty
                };
                if (!string.IsNullOrEmpty(english))
                {
                    return true;
                }
            }

            chinese = string.Empty;
            english = string.Empty;
            return false;
        }

        private static bool ContainsHan(string value)
        {
            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] is >= '\u3400' and <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }

        private static readonly Dictionary<string, string> EventNameEnglish =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GameEventCatalog.GE_人口衰减] = "Population Decline",
                [GameEventCatalog.GE_工人离职] = "Worker Resignation",
                [GameEventCatalog.GE_可用人口不足] = "Insufficient Available Population",
                [GameEventCatalog.GE_自动补贴减少] = "Automatic Subsidy Decreased",
                [GameEventCatalog.GE_自动补贴增加] = "Automatic Subsidy Increased",
                [GameEventCatalog.GE_补贴金币不足] = "Insufficient Subsidy Funds",
                [GameEventCatalog.GE_招工未完全补满] = "Recruitment Incomplete",
                [GameEventCatalog.GE_科技研究完成] = "Research Completed",
                [GameEventCatalog.GE_未选择研发节点] = "No Research Selected",
                [GameEventCatalog.GE_科技自动重复研发] = "Repeat Research Continued",
                [GameEventCatalog.GE_任务出现] = "New Quest",
                [GameEventCatalog.GE_任务完成] = "Quest Completed",
                [GameEventCatalog.GE_任务失败] = "Quest Failed",
                [GameEventCatalog.GE_任务奖励领取失败] = "Quest Reward Claim Failed",
                [GameEventCatalog.GE_任务提交资源] = "Quest Resources Submitted",
                [GameEventCatalog.GE_随机任务出现] = "Random Quest Appeared",
                [GameEventCatalog.GE_远征出发] = "Expedition Departed",
                [GameEventCatalog.GE_远征成功] = "Expedition Succeeded",
                [GameEventCatalog.GE_远征失败] = "Expedition Failed",
                [GameEventCatalog.GE_远征奖励待领取] = "Expedition Rewards Pending",
                [GameEventCatalog.GE_远征奖励领取] = "Expedition Rewards Claimed",
                [GameEventCatalog.GE_远征补贴不足] = "Expedition Subsidy Shortfall",
                [GameEventCatalog.GE_人才刷新] = "Talent Candidates Refreshed",
                [GameEventCatalog.GE_人才招募] = "Talent Recruited",
                [GameEventCatalog.GE_人才任命] = "Talent Assigned",
                [GameEventCatalog.GE_人才卸任] = "Talent Unassigned",
                [GameEventCatalog.GE_人才升级] = "Talent Upgraded",
                [GameEventCatalog.GE_人才薪资支付] = "Talent Salaries Paid",
                [GameEventCatalog.GE_人才薪资不足] = "Talent Salary Shortfall",
                [GameEventCatalog.GE_人才效果触发] = "Talent Effect Triggered",
                [GameEventCatalog.GE_人才特性发现] = "Talent Trait Discovered",
                [GameEventCatalog.GE_人才特性激活] = "Talent Trait Activated",
                [GameEventCatalog.GE_王子出生] = "Prince Born",
                [GameEventCatalog.GE_王族特性显现] = "Royal Trait Revealed",
                [GameEventCatalog.GE_王族特性激活] = "Royal Trait Activated",
                [GameEventCatalog.GE_王族后天特性获得] = "Royal Acquired Trait Gained",
                [GameEventCatalog.GE_国王寿命预警] = "Royal Lifespan Warning",
                [GameEventCatalog.GE_国王死亡] = "King Died",
                [GameEventCatalog.GE_王位继承] = "Royal Succession",
                [GameEventCatalog.GE_王朝继承危机] = "Succession Crisis",
                [GameEventCatalog.GE_国王特性效果触发] = "Royal Trait Effect Triggered"
            };

        private static readonly (string Key, string Chinese, string English, bool IsSmart)[] UiEntries =
        {
            ("ui.language.none_available", "无可用语言", "No Languages Available", false),
            ("ui.map.invalid", "无效地图", "Invalid Map", false),
            ("ui.save.invalid", "无效存档", "Invalid Save", false),
            ("ui.loading.press_any_key", "按任意键继续", "Press Any Key to Continue", false)
        };

        private static readonly (string Key, string Chinese, string English, bool IsSmart)[] GameplayEntries =
        {
            ("gameplay.common.turn_heading", "第 {0} 回合", "Turn {0}", true),
            ("gameplay.common.unknown_character", "未知角色", "Unknown Character", false),
            ("gameplay.common.previous_king", "前任国王", "the previous king", false),
            ("gameplay.common.unknown_destination", "未知目的地", "Unknown Destination", false),
            ("gameplay.common.item_amount", "{0} x{1}", "{0} x{1}", true),
            ("gameplay.common.list_separator", "、", ", ", false),
            ("gameplay.quest.category.random", "随机", "Random", false),
            ("gameplay.quest.category.mainline", "主线", "Mainline", false),
            ("gameplay.quest.target.move_camera", "移动视野", "Move the Camera", false),
            ("gameplay.quest.target.any_technology", "任意科技", "Any Technology", false),
            ("gameplay.quest.target.multiple_resources", "多种资源", "Multiple Resources", false),
            ("gameplay.quest.template.random_exchange.name", "{0}的交换请求", "Trade Request from {0}", true),
            ("gameplay.quest.template.random_exchange.description", "{0}派来一些使者，希望使用{2}和你交换{1}。", "Envoys from {0} offer {2} in exchange for {1}.", true),
            ("gameplay.quest.template.debug_delivery.name", "调试随机委托：交付{1}", "Debug Random Quest: Deliver {1}", true),
            ("gameplay.quest.template.debug_delivery.description", "交付 {1}，获得 {2}。", "Deliver {1} and receive {2}.", true),
            ("gameplay.quest.requester.distant_empire.name", "遥远的帝国", "a Distant Empire", false),
            ("gameplay.technology.research_completed", "科技研究完成：{0}", "Research completed: {0}", true),
            ("gameplay.technology.research_completed_with_effects", "科技研究完成：{0}（{1}）", "Research completed: {0} ({1})", true),
            ("gameplay.technology.repeat_continued", "科技已自动继续重复研发：{0}", "Repeat research continued automatically: {0}", true),
            ("gameplay.technology.no_research_selected", "未选择研发节点：本次下回合已取消；再次点击下一回合将继续。", "No research is selected. This turn advance was canceled; click Next Turn again to continue.", false),
            ("gameplay.talent.refreshed", "刷新人才：消耗 {0} 金币，获得 {1} 张候选卡。", "Refreshed talent candidates: spent {0} Gold and received {1} candidate cards.", true),
            ("gameplay.talent.recruited", "招募人才：{0}。", "Recruited talent: {0}.", true),
            ("gameplay.talent.assigned", "任命人才：{0} -> {1}。", "Assigned talent: {0} -> {1}.", true),
            ("gameplay.talent.unassigned", "卸任人才：{0}。", "Unassigned talent: {0}.", true),
            ("gameplay.talent.upgraded", "人才升级：{0} {1}->{2}。", "Talent upgraded: {0} {1}->{2}.", true),
            ("gameplay.talent.salary_missing", "人才薪资不足：需要 {0} 金币，已支付 {1}，缺口 {2}。", "Talent salary shortfall: {0} Gold required, {1} paid, {2} missing.", true),
            ("gameplay.talent.salary_paid", "人才薪资支付：{1} 金币。", "Talent salaries paid: {1} Gold.", true),
            ("gameplay.talent.effect_triggered", "人才效果：{0}。", "Talent effect: {0}.", true),
            ("gameplay.talent.trait_discovered", "人才特性发现：{0} 的 {1}。", "Talent trait discovered: {0}'s {1}.", true),
            ("gameplay.talent.trait_activated", "人才特性激活：{0} 的 {1}。", "Talent trait activated: {0}'s {1}.", true),
            ("gameplay.inheritance.prince_born", "王子出生：{0}。", "A prince was born: {0}.", true),
            ("gameplay.inheritance.trait_effect_triggered", "国王特性效果：{0}。", "Royal trait effect: {0}.", true),
            ("gameplay.inheritance.acquired_trait_added", "后天特性获得：{0} 获得 {1}。", "Acquired trait: {0} gained {1}.", true),
            ("gameplay.inheritance.trait_discovered", "王族特性显现：{0} 的 {1}。", "Royal trait revealed: {0}'s {1}.", true),
            ("gameplay.inheritance.trait_activated", "王族特性激活：{0} 的 {1}。", "Royal trait activated: {0}'s {1}.", true),
            ("gameplay.inheritance.lifetime_warning", "寿命预警：{0} 预计还剩 {1} 回合。", "Lifespan warning: {0} is expected to have {1} turns remaining.", true),
            ("gameplay.inheritance.succession_crisis", "王朝继承危机：没有可继承王位的继承人。", "Succession crisis: there is no eligible heir to the throne.", false),
            ("gameplay.inheritance.succession", "王位继承：{0} 因{1}离位，{2} 登基。", "Royal succession: {0} left the throne due to {1}; {2} has ascended.", true),
            ("gameplay.inheritance.reason.abdication", "退位", "abdication", false),
            ("gameplay.inheritance.reason.death", "死亡", "death", false),
            ("gameplay.expedition.subsidy_penalty.name", "远征补贴不足", "Expedition Subsidy Shortfall", false),
            ("gameplay.expedition.subsidy_penalty.description", "远征失败补贴不足造成的全局岗位吸引力惩罚。", "A global job-attraction penalty caused by insufficient compensation after a failed expedition.", false),
            ("gameplay.expedition.started", "远征出发：{0}，预计第 {1} 回合归来，成功率 {2:0.#}%。", "Expedition departed for {0}; expected back on turn {1}, success chance {2:0.#}%.", true),
            ("gameplay.expedition.rewards_claimed", "领取远征奖励：{0}。", "Claimed expedition rewards from {0}.", true),
            ("gameplay.expedition.returned_rewards_pending", "远征归来：{0} 成功，人口已返还，收益率 {1:0.#}%，仓库空间不足，奖励待领取。", "The expedition to {0} succeeded and the population returned. Yield {1:0.#}%; rewards are pending because storage is full.", true),
            ("gameplay.expedition.returned_success", "远征归来：{0} 成功，人口已返还，收益率 {1:0.#}%，奖励已结算。", "The expedition to {0} succeeded and the population returned. Yield {1:0.#}%; rewards have been settled.", true),
            ("gameplay.expedition.failed_subsidy_missing", "远征失败：{0}，损失人口 {1}，补贴需 {2} 金币，已支付 {3}，缺口 {4}，触发全局惩罚 {5} 层。", "The expedition to {0} failed: {1} population lost; {2} Gold compensation required, {3} paid, {4} missing; {5} global penalty stacks applied.", true),
            ("gameplay.expedition.failed", "远征失败：{0}，损失人口 {1}，已支付补贴 {3} 金币。", "The expedition to {0} failed: {1} population lost and {3} Gold compensation paid.", true),
            ("gameplay.common.preview", "预览", "Preview", false),
            ("gameplay.common.upgrade", "升级", "Upgrade", false),
            ("gameplay.common.building", "建筑", "Building", false),
            ("gameplay.common.reward", "奖励", "Reward", false),
            ("gameplay.common.effect", "效果", "Effect", false),
            ("gameplay.common.waiting", "等待中", "Waiting", false),
            ("gameplay.common.unlimited", "不限", "Unlimited", false),
            ("gameplay.common.conditions_not_met", "条件未满足", "Conditions Not Met", false),
            ("gameplay.common.completed", "已完成", "Completed", false),
            ("gameplay.common.unavailable", "不可用", "Unavailable", false),
            ("gameplay.common.optional", "可选", "Optional", false),
            ("gameplay.common.comma", "，", ", ", false),
            ("gameplay.common.none", "无", "None", false),
            ("gameplay.common.not_configured", "未配置", "Not Configured", false),
            ("gameplay.technology.ui.none_selected", "未选择科技", "No Technology Selected", false),
            ("gameplay.technology.ui.not_initialized", "科技服务未初始化", "Technology Service Not Initialized", false),
            ("gameplay.technology.ui.no_nodes", "没有可显示的科技节点", "No Technology Nodes to Display", false),
            ("gameplay.technology.ui.research_cost", "研究需求：{0} 科技点", "Research Cost: {0} Research Points", true),
            ("gameplay.technology.ui.unlocks_and_effects", "解锁与完成效果", "Unlocks and Completion Effects", false),
            ("gameplay.technology.ui.repeating", "重复研究中：{0}", "Repeating Research: {0}", true),
            ("gameplay.technology.ui.researching", "研究中：{0}", "Researching: {0}", true),
            ("gameplay.technology.ui.queued", "已加入研发队列", "Added to Research Queue", false),
            ("gameplay.technology.ui.completed", "已研究", "Researched", false),
            ("gameplay.technology.ui.repeatable_switch", "可重复研究，点击节点切换当前研究", "Repeatable; click to switch current research", false),
            ("gameplay.technology.ui.repeatable_start", "可重复研究，点击节点开始研究", "Repeatable; click to begin research", false),
            ("gameplay.technology.ui.available_switch", "可研究，点击节点切换当前研究", "Available; click to switch current research", false),
            ("gameplay.technology.ui.available_start", "可研究，点击节点开始研究", "Available; click to begin research", false),
            ("gameplay.technology.ui.prerequisites_locked_queue", "前置科技未完成，点击节点加入研发队列", "Prerequisites incomplete; click to queue", false),
            ("gameplay.technology.ui.invalid", "科技配置无效", "Invalid Technology Configuration", false),
            ("gameplay.technology.ui.unavailable", "不可研究", "Unavailable for Research", false),
            ("gameplay.technology.ui.no_points_required", "无需科技点", "No Research Points Required", false),
            ("gameplay.technology.ui.no_prerequisites", "前置：无", "Prerequisites: None", false),
            ("gameplay.technology.ui.prerequisites", "前置：{0}", "Prerequisites: {0}", true),
            ("gameplay.technology.ui.point_cost", "{0} 科技点", "{0} Research Points", true),
            ("gameplay.technology.node.unconfigured", "未配置科技", "Technology Not Configured", false),
            ("gameplay.technology.node.prerequisite_incomplete", "前置未完成", "Prerequisite Incomplete", false),
            ("gameplay.technology.node.available", "可研究", "Available", false),
            ("gameplay.technology.node.repeatable", "可重复研究", "Repeatable", false),
            ("gameplay.technology.node.in_queue", "队列中", "Queued", false),
            ("gameplay.technology.node.queue_position", "队列 {0}", "Queue {0}", true),
            ("gameplay.technology.node.more_unlocks", "另有 {0} 项解锁内容", "{0} more unlocks", true),
            ("gameplay.technology.ui.waiting_queue", "等待研发队列", "Waiting for Research Queue", false),
            ("gameplay.technology.ui.waiting_for", "等待：{0}", "Waiting: {0}", true),
            ("gameplay.expedition.ui.not_initialized", "远征系统未初始化", "Expedition System Not Initialized", false),
            ("gameplay.expedition.ui.no_destinations", "当前没有可显示的远征目的地", "No Expedition Destinations Available", false),
            ("gameplay.expedition.ui.none_selected", "未选择目的地", "No Destination Selected", false),
            ("gameplay.expedition.ui.allocate", "配资", "Allocate Supplies", false),
            ("gameplay.expedition.ui.select_destination", "选择目的地", "Select a Destination", false),
            ("gameplay.expedition.ui.penalty", "补贴不足惩罚 {0} 层，持续至第 {1} 回合", "Subsidy-shortfall penalty: {0} stacks through turn {1}", true),
            ("gameplay.expedition.ui.assigned_population", "派遣人口 {0}", "Assigned Population: {0}", true),
            ("gameplay.expedition.ui.success_with_bonus", "预计成功率 {0:0.#}%，额外收益 +{1:0.#}%", "Estimated success {0:0.#}%, bonus yield +{1:0.#}%", true),
            ("gameplay.expedition.ui.success", "预计成功率 {0:0.#}%", "Estimated success {0:0.#}%", true),
            ("gameplay.expedition.ui.population_range", "基础人口 {0}，需要 {1}-{2}", "Base population {0}; requires {1}-{2}", true),
            ("gameplay.expedition.ui.population_min", "基础人口 {0}，至少 {1}", "Base population {0}; minimum {1}", true),
            ("gameplay.expedition.ui.depart", "出发", "Depart", false),
            ("gameplay.expedition.ui.available", "可出发", "Ready to Depart", false),
            ("gameplay.expedition.ui.destination_summary", "持续 {0} 回合，人口 {1}-{2}，基础成功率 {3:0.#}%，收益率 {4:0.#}%，{5}", "Duration {0} turns, population {1}-{2}, base success {3:0.#}%, yield {4:0.#}%, {5}", true),
            ("gameplay.expedition.ui.team_full", "远征队伍已满", "All Expedition Teams Are Busy", false),
            ("gameplay.expedition.ui.population_insufficient", "人口不足", "Insufficient Population", false),
            ("gameplay.expedition.ui.population_exceeded", "人口超限", "Population Exceeds Limit", false),
            ("gameplay.expedition.ui.base_population_insufficient", "基础人口不足", "Insufficient Base Population", false),
            ("gameplay.expedition.ui.cannot_depart", "不可出发", "Cannot Depart", false),
            ("gameplay.expedition.ui.return_progress", "第 {0} 回合归来，剩余 {1} 回合", "Returns on turn {0}; {1} turns remaining", true),
            ("gameplay.expedition.supply.minimum", "最低 {0}", "Minimum {0}", true),
            ("gameplay.expedition.supply.inventory", "库存 {0}", "Inventory {0}", true),
            ("gameplay.expedition.supply.success_per_extra", "+{0:0.#}%成功/额外", "+{0:0.#}% success/extra", true),
            ("gameplay.expedition.supply.yield_per_extra", "+{0:0.#}%收益/额外", "+{0:0.#}% yield/extra", true),
            ("gameplay.expedition.supply.extra_limit", "额外最多 {0}{1}{2}{3}{4}", "Up to {0} extra{1}{2}{3}{4}", true),
            ("gameplay.expedition.supply.amount_extra", "携带 {0}（额外 {1}）", "Carrying {0} ({1} extra)", true),
            ("gameplay.expedition.supply.amount", "携带 {0}", "Carrying {0}", true),
            ("gameplay.talent.profession.any", "通用", "General", false),
            ("gameplay.talent.profession.steward", "大总管", "Steward", false),
            ("gameplay.talent.profession.archmage", "大法师", "Archmage", false),
            ("gameplay.talent.profession.general", "大将军", "General", false),
            ("gameplay.talent.profession.scholar", "大学者", "Scholar", false),
            ("gameplay.talent.rarity.common", "普通", "Common", false),
            ("gameplay.talent.rarity.uncommon", "优秀", "Uncommon", false),
            ("gameplay.talent.rarity.rare", "稀有", "Rare", false),
            ("gameplay.talent.rarity.epic", "史诗", "Epic", false),
            ("gameplay.talent.rarity.legendary", "传说", "Legendary", false),
            ("gameplay.talent.level.max", "Lv.{0} 最高级", "Lv.{0} Max", true),
            ("gameplay.talent.level.progress", "Lv.{0} 经验 {1}/{2}", "Lv.{0} XP {1}/{2}", true),
            ("gameplay.talent.salary", "薪资 {0}/回合", "Salary {0}/turn", true),
            ("gameplay.talent.offer_details", "{0} / {1} / Lv.{2}", "{0} / {1} / Lv.{2}", true),
            ("gameplay.talent.no_unlocked_effects", "无已解锁效果", "No Unlocked Effects", false),
            ("gameplay.talent.no_base_effects", "无基础效果", "No Base Effects", false),
            ("gameplay.talent.no_hidden_traits", "无隐藏特性", "No Hidden Traits", false),
            ("gameplay.talent.trait_active", "已激活：{0}", "Active: {0}", true),
            ("gameplay.talent.trait_discovered_label", "已发现：{0}", "Discovered: {0}", true),
            ("gameplay.talent.any_profession", "任意职业", "Any Profession", false),
            ("gameplay.talent.restricted_profession", "限定 {0}", "Restricted to {0}", true),
            ("gameplay.talent.ui.unassigned", "未任命", "Unassigned", false),
            ("gameplay.talent.ui.max_level", "已满级", "Max Level", false),
            ("gameplay.talent.ui.unassign", "卸任", "Unassign", false),
            ("gameplay.talent.ui.has_hidden_traits", "含隐藏特性", "Has Hidden Traits", false),
            ("gameplay.talent.ui.recruit", "招募", "Recruit", false),
            ("gameplay.talent.ui.empty_slot", "空槽", "Empty Slot", false),
            ("gameplay.talent.ui.select_talent", "选择人才", "Select Talent", false),
            ("gameplay.talent.ui.assign", "任命", "Assign", false),
            ("gameplay.talent.ui.clear", "清空", "Clear", false),
            ("gameplay.talent.ui.select_from_pool", "先从人才池选择人才", "Select a talent from the pool first", false),
            ("gameplay.talent.ui.can_assign", "可任命：{0}", "Can assign: {0}", true),
            ("gameplay.talent.ui.cannot_assign", "不可任命：{0} 职业不匹配", "Cannot assign: {0} has the wrong profession", true),
            ("gameplay.talent.ui.not_initialized", "人才系统未初始化", "Talent System Not Initialized", false),
            ("gameplay.talent.ui.none_selected", "未选择人才", "No Talent Selected", false),
            ("gameplay.talent.ui.selected", "已选择：{0}", "Selected: {0}", true),
            ("gameplay.talent.ui.refresh", "刷新人才（{0}）", "Refresh Talents ({0})", true),
            ("gameplay.talent.ui.salary_summary", "{0} {1} / 薪资 {2}/回合", "{0} {1} / Salary {2}/turn", true),
            ("gameplay.talent.ui.pool_summary", "人才池 {0} / 候选 {1} / 槽位 {2}", "Talent Pool {0} / Candidates {1} / Slots {2}", true),
            ("gameplay.talent.ui.assigned_to", "任命：{0}", "Assigned to: {0}", true),
            ("gameplay.talent.result.catalog_missing", "人才目录未配置。", "The talent catalog is not configured.", false),
            ("gameplay.talent.result.no_candidates", "没有符合条件的人才。", "No eligible talents were found.", false),
            ("gameplay.talent.result.refreshed", "已刷新 {0} 张人才卡。", "Refreshed {0} talent cards.", true),
            ("gameplay.talent.result.offer_missing", "人才卡不存在。", "The talent card does not exist.", false),
            ("gameplay.talent.result.unique_recruited", "该唯一人才已经被招募。", "This unique talent has already been recruited.", false),
            ("gameplay.talent.result.recruited", "已招募：{0}。", "Recruited: {0}.", true),
            ("gameplay.talent.result.talent_missing", "人才不存在。", "The talent does not exist.", false),
            ("gameplay.talent.result.slot_missing", "人才槽不存在。", "The talent slot does not exist.", false),
            ("gameplay.talent.result.profession_mismatch", "职业不符合该人才槽要求。", "The talent's profession does not meet this slot's requirements.", false),
            ("gameplay.talent.result.assigned", "已任命 {0} 至 {1}。", "Assigned {0} to {1}.", true),
            ("gameplay.talent.result.slot_empty", "该槽位没有任命人才。", "No talent is assigned to this slot.", false),
            ("gameplay.talent.result.unassigned", "已卸任：{0}。", "Unassigned: {0}.", true),
            ("gameplay.talent.result.not_assigned", "该人才尚未任命。", "This talent is not assigned.", false),
            ("gameplay.talent.result.upgrade_unavailable", "经验不足或已达到最高等级。", "Not enough experience, or maximum level reached.", false),
            ("gameplay.talent.result.upgraded", "{0} 已提升至 {1} 级。", "{0} reached level {1}.", true),
            ("gameplay.talent.result.inventory_missing", "库存服务未初始化。", "The inventory service is not initialized.", false),
            ("gameplay.talent.result.gold_item_missing", "人才金币物品未配置。", "The Gold item used by the talent system is not configured.", false),
            ("gameplay.talent.result.refresh_gold_missing", "金币不足：刷新需要 {0}。", "Insufficient Gold: refreshing costs {0}.", true),
            ("gameplay.talent.result.refresh_spend_failed", "扣除刷新费用失败。", "Failed to spend the refresh cost.", false),
            ("gameplay.effect.item_added", "{0}：{1}+{2}", "{0}: {1}+{2}", true),
            ("gameplay.effect.item_storage_full", "{0}：{1}+0（仓库已满）", "{0}: {1}+0 (storage full)", true),
            ("gameplay.effect.research_added", "{0}：研究点+{1}", "{0}: Research Points +{1}", true),
            ("gameplay.effect.blueprint_unlocked", "{0}：解锁蓝图 {1}", "{0}: Unlocked blueprint {1}", true),
            ("gameplay.effect.blueprint_already_unlocked", "{0}：蓝图已解锁 {1}", "{0}: Blueprint already unlocked {1}", true),
            ("gameplay.technology.buff.production_bonus", "所有{0}每次产出额外提供 {1} {2}", "All {0} produce an additional {1} {2} per cycle", true),
            ("gameplay.inheritance.role.king", "国王", "King", false),
            ("gameplay.inheritance.role.queen", "王后", "Queen", false),
            ("gameplay.inheritance.role.prince", "王子", "Prince", false),
            ("gameplay.inheritance.status.reigning", "在位", "Reigning", false),
            ("gameplay.inheritance.status.consort", "王后", "Consort", false),
            ("gameplay.inheritance.status.heir", "继承人", "Heir", false),
            ("gameplay.inheritance.status.retired", "退位", "Retired", false),
            ("gameplay.inheritance.status.dead", "死亡", "Dead", false),
            ("gameplay.inheritance.legal_heir", "合法继承人", "Eligible Heir", false),
            ("gameplay.inheritance.underage_heir", "未成年，{0} 岁成年", "Underage; comes of age at {0}", true),
            ("gameplay.inheritance.character_line", "{0} / {1} / {2} / {3}/{4} 岁", "{0} / {1} / {2} / age {3}/{4}", true),
            ("gameplay.inheritance.no_traits", "无特性", "No Traits", false),
            ("gameplay.inheritance.unknown_trait", "未知特性", "Unknown Trait", false),
            ("gameplay.inheritance.trait_state.active", "生效", "Active", false),
            ("gameplay.inheritance.trait_state.discovered", "已发现", "Discovered", false),
            ("gameplay.inheritance.trait_origin.acquired", "后天", "Acquired", false),
            ("gameplay.inheritance.trait_origin.innate", "先天", "Innate", false),
            ("gameplay.inheritance.trait_line", "{0}（{1} / {2}）", "{0} ({1} / {2})", true),
            ("gameplay.inheritance.relation.father", "父 {0}", "Father {0}", true),
            ("gameplay.inheritance.relation.mother", "母 {0}", "Mother {0}", true),
            ("gameplay.inheritance.relation.children", "子嗣 {0}", "Children {0}", true),
            ("gameplay.inheritance.ui.age_summary", "年龄 {0} / 寿命 {1} / 剩余 {2}", "Age {0} / Lifespan {1} / Remaining {2}", true),
            ("gameplay.inheritance.ui.reign_turns", "统治 {0} 回合", "Reigned for {0} turns", true),
            ("gameplay.inheritance.ui.underage_progress", "未成年：{0}/{1}", "Underage: {0}/{1}", true),
            ("gameplay.inheritance.ui.not_initialized", "继承系统未初始化", "Succession System Not Initialized", false),
            ("gameplay.inheritance.ui.dynasty_continues", "王族血脉延续中", "The royal bloodline continues", false),
            ("gameplay.inheritance.ui.generation", "第 {0} 代 / 成年年龄 {1}", "Generation {0} / Adult Age {1}", true),
            ("gameplay.inheritance.ui.current_king", "国王：{0}", "King: {0}", true),
            ("gameplay.inheritance.ui.current_queen", "王后：{0}", "Queen: {0}", true),
            ("gameplay.inheritance.ui.prince_born", "王子出生：{0}", "Prince Born: {0}", true),
            ("gameplay.inheritance.ui.prince_unavailable", "当前无法生育王子", "A prince cannot be born right now", false),
            ("gameplay.inheritance.ui.new_king", "新王登基：{0}", "New King Crowned: {0}", true),
            ("gameplay.inheritance.ui.abdication_failed", "退位失败：没有可用继承人", "Abdication failed: no eligible heir", false),
            ("gameplay.building.status.normal", "正常", "Normal", false),
            ("gameplay.building.status.progress", "{0} {1}/{2}", "{0} {1}/{2}", true),
            ("gameplay.building.status.abandoned", "建筑荒废", "Abandoned", false),
            ("gameplay.building.status.consumption_failed", "消耗失败", "Consumption Failed", false),
            ("gameplay.building.status.missing_inventory", "库存缺失", "Inventory Missing", false),
            ("gameplay.building.status.invalid_food_item", "食物配置异常", "Invalid Food Configuration", false),
            ("gameplay.building.status.missing_resource_provider", "无法连接资源点", "Resource Provider Unreachable", false),
            ("gameplay.building.status.missing_food", "食物不足", "Food Shortage", false),
            ("gameplay.building.status.invalid_tax_item", "税收配置异常", "Invalid Tax Configuration", false),
            ("gameplay.building.status.tax_reward_failed", "税收存入失败", "Tax Storage Failed", false),
            ("gameplay.building.status.market_income_failed", "市场收入存入失败", "Market Income Storage Failed", false),
            ("gameplay.building.status.invalid_wood_item", "原木配置异常", "Invalid Wood Configuration", false),
            ("gameplay.building.status.invalid_gold_item", "金币配置异常", "Invalid Gold Configuration", false),
            ("gameplay.building.status.wood_storage_failed", "原木存入失败", "Wood Storage Failed", false),
            ("gameplay.building.status.insufficient_workers", "工人不足", "Insufficient Workers", false),
            ("gameplay.building.status.worker_shortage", "缺工", "Worker Shortage", false),
            ("gameplay.building.status.recruit_gold_missing", "招工金币不足", "Insufficient Recruitment Gold", false),
            ("gameplay.building.status.subsidy_gold_missing", "补贴金币不足", "Insufficient Subsidy Gold", false),
            ("gameplay.building.status.road_blocked", "道路不通", "Road Unreachable", false),
            ("gameplay.building.status.warehouse_maintenance_missing", "仓库维护费不足", "Warehouse Maintenance Shortfall", false),
            ("gameplay.building.status.warehouse_maintenance_invalid", "仓库维护配置异常", "Invalid Warehouse Maintenance Configuration", false),
            ("gameplay.building.status.maintenance_missing", "维护费不足", "Maintenance Shortfall", false),
            ("gameplay.building.status.maintenance_invalid", "维护配置异常", "Invalid Maintenance Configuration", false),
            ("gameplay.building.available", "可建造", "Available to Build", false),
            ("gameplay.building.available_materials_missing", "可用，材料不足", "Available, Materials Missing", false),
            ("gameplay.building.development_incomplete", "建筑未开发完成", "Building Development Incomplete", false),
            ("gameplay.building.limit_reached", "数量已达上限", "Build Limit Reached", false),
            ("gameplay.building.materials_missing", "材料不足", "Materials Missing", false),
            ("gameplay.building.placement_cost", "放置消耗", "Placement Cost", false),
            ("gameplay.building.construction_cost", "施工消耗", "Construction Cost", false),
            ("gameplay.building.unnamed_material", "未命名材料", "Unnamed Material", false),
            ("gameplay.building.crop.auto_harvest_on", "自动收获：开启", "Auto-Harvest: On", false),
            ("gameplay.building.crop.auto_harvest_off", "自动收获：关闭", "Auto-Harvest: Off", false),
            ("gameplay.building.crop.none_planted", "当前作物：未种植", "Current Crop: None", false),
            ("gameplay.building.crop.current_ready", "当前作物：{0}（可收获）", "Current Crop: {0} (Ready to Harvest)", true),
            ("gameplay.building.crop.current", "当前作物：{0}", "Current Crop: {0}", true),
            ("gameplay.building.crop.no_growth", "成熟进度：无", "Growth Progress: None", false),
            ("gameplay.building.crop.growth_progress", "成熟进度：{0}/{1}", "Growth Progress: {0}/{1}", true),
            ("gameplay.building.crop.turns_remaining", "成熟剩余：{0}回合", "Matures in: {0} turns", true),
            ("gameplay.building.crop.option", "{0}（{1}回合）", "{0} ({1} turns)", true),
            ("gameplay.building.workforce.recruit_cost", "消耗{0}金币 招募1名工人", "Spend {0} Gold to recruit 1 worker", true),
            ("gameplay.building.workforce.summary", "工人：{0}/{1}（稳定：{2}）", "Workers: {0}/{1} (Stable: {2})", true),
            ("gameplay.building.upgrade.not_initialized", "升级服务未初始化。", "Upgrade Service Not Initialized.", false),
            ("gameplay.building.upgrade.no_cost", "升级消耗：无", "Upgrade Cost: None", false),
            ("gameplay.building.upgrade.cost", "升级消耗：{0}", "Upgrade Cost: {0}", true),
            ("gameplay.building.function.group_summary", "{0}：{1}", "{0}: {1}", true),
            ("gameplay.building.function.functional", "功能性", "Functions", false),
            ("gameplay.building.function.resources", "资源", "Resources", false),
            ("gameplay.building.details.none", "暂无详情", "No Details", false),
            ("gameplay.building.details.status", "状态：{0}", "Status: {0}", true),
            ("gameplay.building.overview.construction", "施工 {0}/{1}", "Construction {0}/{1}", true),
            ("gameplay.building.overview.level", "等级 {0}", "Level {0}", true),
            ("gameplay.building.label.population", "人口", "Population", false),
            ("gameplay.building.label.resource_provider", "资源提供点", "Resource Provider", false),
            ("gameplay.building.label.harvestable_tree", "可采集树木", "Harvestable Tree", false),
            ("gameplay.building.label.limited_harvest", "有限次数采集", "Limited Harvest", false),
            ("gameplay.building.label.not_planted", "未种植", "Not Planted", false),
            ("gameplay.building.label.ready_to_harvest", "可收获", "Ready to Harvest", false),
            ("gameplay.building.label.growth_turns_remaining", "成熟剩余回合", "Turns Until Mature", false),
            ("gameplay.building.label.inventory_slots", "库存格", "Inventory Slots", false),
            ("gameplay.building.label.expedition_yield", "远征收益率", "Expedition Yield", false),
            ("gameplay.building.label.research_per_turn", "研究点/回合", "Research Points/Turn", false),
            ("gameplay.building.label.operational_experience", "运营经验", "Operational Experience", false),
            ("gameplay.building.label.crop_options", "可选作物", "Crop Options", false),
            ("gameplay.building.label.current_crop", "当前作物", "Current Crop", false),
            ("gameplay.building.label.growth_progress", "生长进度", "Growth Progress", false),
            ("gameplay.building.label.auto_harvest", "自动收获", "Auto-Harvest", false),
            ("gameplay.building.label.auto_harvest_cost", "自动收获消耗", "Auto-Harvest Cost", false),
            ("gameplay.building.label.harvest_output", "收获产出", "Harvest Output", false),
            ("gameplay.building.label.minimum_workers", "最低工人", "Minimum Workers", false),
            ("gameplay.building.label.trigger_chance", "触发概率", "Trigger Chance", false),
            ("gameplay.building.label.provider_priority", "提供优先级", "Provider Priority", false),
            ("gameplay.building.label.last_turn_value", "上回合经手价值", "Value Handled Last Turn", false),
            ("gameplay.building.label.gold_settlement", "金币结算", "Gold Settlement", false),
            ("gameplay.building.label.health", "生命", "Health", false),
            ("gameplay.building.label.wood_reward", "原木奖励", "Wood Reward", false),
            ("gameplay.building.label.sapling_reward", "树苗奖励", "Sapling Reward", false),
            ("gameplay.building.label.harvests_remaining", "剩余采集次数", "Harvests Remaining", false),
            ("gameplay.building.label.reward_per_harvest", "每次获得", "Reward per Harvest", false),
            ("gameplay.building.label.remaining_output", "剩余总产出", "Remaining Output", false),
            ("gameplay.building.label.production_cycle", "生产周期", "Production Cycle", false),
            ("gameplay.building.label.manhattan_radius", "曼哈顿半径", "Manhattan Radius", false),
            ("gameplay.building.label.stacking_rule", "叠加规则", "Stacking Rule", false),
            ("gameplay.building.label.current_population", "当前人口", "Current Population", false),
            ("gameplay.building.label.population_growth", "增长进度", "Growth Progress", false),
            ("gameplay.building.label.resource_path_cost", "资源路径行动力", "Resource Path Cost", false),
            ("gameplay.building.label.diet_score", "饮食评分", "Diet Score", false),
            ("gameplay.building.label.diet_variety", "饮食种类", "Diet Variety", false),
            ("gameplay.building.label.population_limit", "人口上限", "Population Limit", false),
            ("gameplay.building.label.tax_progress", "税收进度", "Tax Progress", false),
            ("gameplay.building.label.failure_decay", "失败衰减", "Failure Decay", false),
            ("gameplay.building.label.life_quality", "生活质量", "Quality of Life", false),
            ("gameplay.building.label.worker_requirement", "工人要求", "Worker Requirement", false),
            ("gameplay.building.label.maintenance_cost", "维护费", "Maintenance Cost", false),
            ("gameplay.common.enabled", "开启", "On", false),
            ("gameplay.common.disabled", "关闭", "Off", false),
            ("gameplay.building.fragment.per_turn", "/回合", "/turn", false),
            ("gameplay.building.fragment.total_value_multiplier", "总价值 × ", "Total value × ", false),
            ("gameplay.building.overview.population_bonus", "人口 +{0}", "Population +{0}", true),
            ("gameplay.building.overview.last_turn_gold", "上回合 +{0} 金币", "Last Turn +{0} Gold", true),
            ("gameplay.building.overview.health", "生命 {0}/{1}", "Health {0}/{1}", true),
            ("gameplay.building.overview.harvests_remaining", "剩余采集 {0}/{1}", "Harvests Remaining {0}/{1}", true),
            ("gameplay.building.overview.production", "生产 {0}/{1}", "Production {0}/{1}", true),
            ("gameplay.building.overview.maintenance", "维护 {0} {1}/回合", "Maintenance {0} {1}/turn", true),
            ("gameplay.building.overview.crop_ready", "作物可收获", "Crop Ready to Harvest", false),
            ("gameplay.building.overview.crop_turns", "成熟剩余 {0} 回合", "Matures in {0} turns", true),
            ("gameplay.building.overview.experience", "经验 {0}/{1}", "Experience {0}/{1}", true),
            ("gameplay.building.overview.experience_max", "经验 {0}（最高等级）", "Experience {0} (Max Level)", true),
            ("gameplay.building.overview.abandoned", "已荒废", "Abandoned", false),
            ("gameplay.building.overview.population", "人口 {0}/{1}", "Population {0}/{1}", true),
            ("gameplay.building.overview.workers_protected", "工人 {0}/{1}，保护 {2} 回合", "Workers {0}/{1}, protected for {2} turns", true),
            ("gameplay.building.overview.workers", "工人 {0}/{1}", "Workers {0}/{1}", true),
            ("gameplay.building.overview.warehouse", "库存格 {0} · 经验 {1}/{2}", "Inventory Slots {0} · Experience {1}/{2}", true),
            ("gameplay.building.overview.warehouse_max", "库存格 {0} · 最高等级", "Inventory Slots {0} · Max Level", true),
            ("gameplay.building.placement.prefab_invalid", "建筑预制体缺失或缺少有效定义。", "The building prefab is missing or has no valid definition.", false),
            ("gameplay.building.placement.grid_missing", "缺少 GridMap。", "GridMap is missing.", false),
            ("gameplay.building.placement.invalid_cell", "目标格子不可放置。", "The target cell cannot be occupied.", false),
            ("gameplay.building.placement.materials_missing", "建造材料不足。", "Insufficient construction materials.", false),
            ("gameplay.building.placement.occupy_failed", "占用格子失败。", "Failed to occupy the target cell.", false),
            ("gameplay.building.placement.instantiate_failed", "建筑实例化失败。", "Failed to instantiate the building.", false),
            ("gameplay.building.placement.spend_failed", "扣除建造材料失败。", "Failed to spend construction materials.", false),
            ("gameplay.building.placement.no_cells", "没有可放置的建筑格子。", "There are no cells available for placement.", false),
            ("gameplay.building.placement.spatial_legal", "占地合法", "Footprint Valid", false),
            ("gameplay.building.placement.spatial_invalid", "占地非法：{0}", "Footprint Invalid: {0}", true),
            ("gameplay.building.placement.provider", "资源点：{0}（行动力 {1}）", "Resource Provider: {0} (Path Cost {1})", true),
            ("gameplay.building.placement.provider_unreachable_optional", "资源点：当前范围内无连接（不阻止放置）", "Resource Provider: none in range (placement is still allowed)", false),
            ("gameplay.building.placement.buff_range", "Buff：{0}，范围 {1}", "Buff: {0}, Range {1}", true),
            ("gameplay.building.upgrade.building_missing", "建筑或家族定义缺失。", "The building or family definition is missing.", false),
            ("gameplay.building.upgrade.under_construction", "施工阶段不能升级。", "A building cannot be upgraded during construction.", false),
            ("gameplay.building.upgrade.max_level", "已经达到最高等级。", "Maximum level reached.", false),
            ("gameplay.building.upgrade.level_not_configured", "目标等级数值尚未配置。", "The target level is not configured.", false),
            ("gameplay.building.upgrade.conditions_not_met", "升级科技或其他条件尚未满足。", "Technology or other upgrade conditions are not met.", false),
            ("gameplay.building.upgrade.requirements_not_met", "建筑升级条件尚未满足。", "Building upgrade requirements are not met.", false),
            ("gameplay.building.upgrade.resources_missing", "升级金币或资源不足。", "Insufficient Gold or resources for the upgrade.", false),
            ("gameplay.building.production.workers_equals", "工人=", "Workers=", false),
            ("gameplay.building.upgrade.operational_experience_missing", "运营经验不足：{0}/{1}。", "Insufficient operational experience: {0}/{1}.", true),
            ("gameplay.building.upgrade.warehouse_experience_missing", "仓库经验不足：{0}/{1}。", "Insufficient warehouse experience: {0}/{1}.", true),
            ("gameplay.building.demolish.inventory_not_empty", "该建筑提供的库存格中仍有物品，请先清空这些槽位。", "This building's inventory slots still contain items. Empty them before demolition.", false),
            ("gameplay.building.workforce.gold_per_turn", "金币/回合 {0}", "Gold/Turn {0}", true),
            ("gameplay.building.label.full_workforce_attraction", "满岗位需要的最少就业吸引力", "Minimum Job Attraction for Full Workforce", false),
            ("gameplay.building.label.current_job_attraction", "当前就业吸引力", "Current Job Attraction", false),
            ("gameplay.building.label.full_workforce_gap", "满岗位吸引力差值", "Job Attraction Gap to Full Workforce", false),
            ("gameplay.building.label.job_attraction_factors", "就业吸引力影响因素", "Job Attraction Factors", false),
            ("gameplay.building.label.current_modifiers", "当前修正", "Current Modifiers", false),
            ("gameplay.inheritance.ui.eligible", "可继承", "Eligible", false),
            ("gameplay.quest.reward.invalid", "任务“{0}”的奖励配置无效，暂时无法领取。", "The reward configuration for “{0}” is invalid and cannot be claimed yet.", true),
            ("gameplay.quest.reward.inventory_uninitialized", "库存系统尚未初始化，暂时无法领取任务奖励。", "The inventory system is not initialized; quest rewards cannot be claimed yet.", false),
            ("gameplay.quest.reward.inventory_full", "库存空间不足，无法领取任务“{0}”的奖励。请先腾出库存格。", "Not enough inventory space to claim rewards for “{0}”. Free some slots first.", true),
            ("gameplay.quest.reward.delivery_failed", "任务“{0}”的奖励发放失败，请重试。", "Failed to deliver rewards for “{0}”. Please try again.", true),
            ("gameplay.quest.failed", "任务失败：{0}", "Quest failed: {0}", true),
            ("gameplay.quest.new", "新任务：{0}", "New quest: {0}", true),
            ("gameplay.quest.target.plant_crops", "播种农田", "Plant Crops", false),
            ("gameplay.quest.target.select_technology", "选择研究科技", "Select a Technology", false),
            ("gameplay.quest.ui.claim_rewards", "领取奖励", "Claim Rewards", false),
            ("gameplay.quest.ui.submit", "提交", "Submit", false),
            ("gameplay.quest.ui.resources_missing", "资源不足", "Insufficient Resources", false),
            ("gameplay.quest.ui.abandon", "放弃任务", "Abandon Quest", false),
            ("gameplay.quest.ui.completed_by_deadline", "完成于期限内：截止第 {0} 回合", "Completed by the deadline: turn {0}", true),
            ("gameplay.quest.ui.remaining_turns", "剩余 {0} 回合", "{0} turns remaining", true),
            ("gameplay.quest.ui.deadline", "截止第 {0} 回合，剩余 {1} 回合", "Deadline: turn {0}; {1} turns remaining", true),
            ("gameplay.quest.ui.target", "目标", "Target", false),
            ("gameplay.quest.ui.target_building", "目标建筑", "Target Building", false),
            ("gameplay.quest.ui.build_progress", "建造 {0}：{1}/{2}", "Build {0}: {1}/{2}", true),
            ("gameplay.quest.ui.collect_progress", "收集 {0}", "Collect {0}", true),
            ("gameplay.quest.ui.submit_progress", "提交 {0}", "Submit {0}", true),
            ("gameplay.quest.ui.numeric_progress", "{0}：{1}/{2}", "{0}: {1}/{2}", true),
            ("gameplay.quest.ui.unlock_reward", "解锁 {0}", "Unlock {0}", true),
            ("gameplay.resource.with_inventory", "{0}  库存 {1}", "{0}  Inventory {1}", true),
            ("gameplay.economy.not_initialized", "经济预测服务未初始化。", "Economy Forecast Service Not Initialized.", false),
            ("gameplay.economy.summary.range", "未来 {0} 回合{1}", "Next {0} turns{1}", true),
            ("gameplay.economy.summary.conservative", " · 保守预测", " · Conservative Forecast", false),
            ("gameplay.economy.summary.exact", " · 精确预测", " · Exact Forecast", false),
            ("gameplay.economy.summary.slots", "库存格 {0}/{1}  →  {2}/{1}（T+{3}）", "Inventory Slots {0}/{1}  →  {2}/{1} (T+{3})", true),
            ("gameplay.economy.summary.no_risk", "预测范围内未发现明确的短缺或入库阻塞。", "No definite shortages or storage blockages were found in the forecast range.", false),
            ("gameplay.economy.summary.risk", "T+{0} 起存在风险：{1} 种资源异常，{2} 个计划事件受阻。", "Risk begins at T+{0}: {1} resources affected and {2} scheduled events blocked.", true),
            ("gameplay.economy.resources.empty", "预测范围内没有库存物品变化。", "No inventory changes in the forecast range.", false),
            ("gameplay.economy.resources.current", "当前 {0}", "Current {0}", true),
            ("gameplay.economy.resources.more", "另有 {0} 种资源未展开。", "{0} more resources are collapsed.", true),
            ("gameplay.economy.cell.consume", "耗{0}", "use {0}", true),
            ("gameplay.economy.cell.produce", "产{0}", "gain {0}", true),
            ("gameplay.economy.cell.produce_range", "产{0}~{1}", "gain {0}–{1}", true),
            ("gameplay.economy.cell.loss", "损{0}", "lose {0}", true),
            ("gameplay.economy.cell.shortage", "缺{0}", "short {0}", true),
            ("gameplay.economy.cell.overflow", "溢{0}", "overflow {0}", true),
            ("gameplay.economy.cell.details", "（{0}）", " ({0})", true),
            ("gameplay.economy.events.empty", "预测范围内没有周期产出、施工、收获或税收事件。", "No production, construction, harvest, or tax events in the forecast range.", false),
            ("gameplay.economy.events.more", "另有 {0} 个计划事件未展开。", "{0} more scheduled events are collapsed.", true),
            ("gameplay.economy.residential.empty", "当前没有居民饮食与生活质量预测。", "No resident diet or quality-of-life forecasts are available.", false),
            ("gameplay.economy.residential.line", "T+{0}  人口 {1}→{2}  饮食{3} · {4}类 · {5:0.#}分  生活质量 {6:0.#}→{7:0.#}", "T+{0}  Population {1}→{2}  Diet {3} · {4} types · score {5:0.#}  Quality of Life {6:0.#}→{7:0.#}", true),
            ("gameplay.economy.residential.satisfied", "满足", "satisfied", false),
            ("gameplay.economy.residential.insufficient", "不足", "insufficient", false),
            ("gameplay.economy.warnings.more", "• 另有 {0} 条说明未展开。", "• {0} more notes are collapsed.", true),
            ("gameplay.economy.event.construction", "[施工]", "[Construction]", false),
            ("gameplay.economy.event.production", "[生产]", "[Production]", false),
            ("gameplay.economy.event.processing", "[加工]", "[Processing]", false),
            ("gameplay.economy.event.harvest", "[收获]", "[Harvest]", false),
            ("gameplay.economy.event.tax", "[税收]", "[Tax]", false),
            ("gameplay.economy.event.market", "[市场]", "[Market]", false),
            ("gameplay.economy.event.population", "[人口]", "[Population]", false),
            ("gameplay.economy.event.risk", "[风险]", "[Risk]", false),
            ("gameplay.economy.certainty.range", "  [范围]", "  [Range]", false),
            ("gameplay.economy.certainty.conditional", "  [条件]", "  [Conditional]", false),
            ("gameplay.economy.certainty.manual", "  [需操作]", "  [Action Required]", false),
            ("gameplay.economy.service.inventory_missing", "库存服务未初始化，无法生成经济预测。", "The inventory service is not initialized; an economy forecast cannot be generated.", false),
            ("gameplay.economy.service.assumptions", "预测假设未来工人数、连接关系和科技状态保持不变；随机产出以范围显示，并以最小产出进行库存可行性判断。", "The forecast assumes workforce, connections, and technology remain unchanged. Random output is shown as a range, using the minimum output for storage feasibility.", false),
            ("gameplay.economy.service.construction_completed_note", "{0}：预测期内施工完成；竣工后的新运营能力从下一次预测开始计算。", "{0}: construction completes within the forecast; new operations are included starting with the next forecast.", true),
            ("gameplay.economy.service.construction_config_missing", "施工配置缺失", "Construction configuration missing", false),
            ("gameplay.economy.service.construction_provider_missing", "施工无法连接资源提供点", "Construction cannot reach a resource provider", false),
            ("gameplay.economy.service.construction_resources_missing", "施工第 {0}/{1} 回合资源不足", "Insufficient resources for construction turn {0}/{1}", true),
            ("gameplay.economy.service.construction_storage_blocked", "施工第 {0}/{1} 回合产出无法入库", "Construction output cannot be stored on turn {0}/{1}", true),
            ("gameplay.economy.service.construction_progress", "施工 {0}/{1}", "Construction {0}/{1}", true),
            ("gameplay.economy.service.expected_completion", "，预计竣工", ", expected to complete", false),
            ("gameplay.economy.service.food_shortage", "预计食物不足 {0}", "Expected food shortage: {0}", true),
            ("gameplay.economy.service.provider_unreachable", "预计无法连接资源提供点", "Expected to be unable to reach a resource provider", false),
            ("gameplay.economy.service.tax_storage_blocked", "预计税收无法入库 {0}", "Expected tax storage overflow: {0}", true),
            ("gameplay.economy.service.tax_income", "预计税收 +{0} {1}", "Expected tax +{0} {1}", true),
            ("gameplay.economy.service.population_growth", "预计人口增长至 {0}", "Population expected to grow to {0}", true),
            ("gameplay.economy.service.production_workforce_missing", "生产缺少岗位配置", "Production workforce configuration missing", false),
            ("gameplay.economy.service.workers_missing", "工人不足（{0}/{1}）", "Insufficient workers ({0}/{1})", true),
            ("gameplay.economy.service.production_progress", "生产进度 {0}/{1}", "Production progress {0}/{1}", true),
            ("gameplay.economy.service.item_storage_blocked", "{0} 预计无法入库", "{0} is expected to exceed storage", true),
            ("gameplay.economy.service.production_cycle", "周期生产", "Production cycle", false),
            ("gameplay.economy.service.processing_recipe_invalid", "加工配方无效", "Invalid processing recipe", false),
            ("gameplay.economy.service.processing_workers_missing", "加工工人不足（{0}/{1}）", "Insufficient processing workers ({0}/{1})", true),
            ("gameplay.economy.service.processing_progress", "加工进度 {0}/{1}", "Processing progress {0}/{1}", true),
            ("gameplay.economy.service.processing_provider_missing", "加工无法连接资源提供点", "Processing cannot reach a resource provider", false),
            ("gameplay.economy.service.processing_inputs_missing", "加工原料不足", "Insufficient processing inputs", false),
            ("gameplay.economy.service.processing_storage_blocked", "加工产出无法入库", "Processed output cannot be stored", false),
            ("gameplay.economy.service.processing_complete", "加工完成", "Processing complete", false),
            ("gameplay.economy.service.crop_workers_missing", "农田工人不足，作物暂停生长", "Farm workers are insufficient; crop growth is paused", false),
            ("gameplay.economy.service.crop_turns_remaining", "{0} 成熟剩余 {1} 回合", "{0} matures in {1} turns", true),
            ("gameplay.economy.service.crop_manual_harvest", "{0} 可手动收获（尚未计入库存）", "{0} can be harvested manually (not yet included in inventory)", true),
            ("gameplay.economy.service.auto_harvest_storage_blocked", "自动收获产出预计无法入库", "Auto-harvest output is expected to exceed storage", false),
            ("gameplay.economy.service.auto_harvest_cost_missing", "自动收获费用不足 {0}", "Auto-harvest costs are short by {0}", true),
            ("gameplay.economy.service.auto_harvest", "自动收获 {0}", "Auto-harvest {0}", true),
            ("gameplay.economy.service.rare_production_range", "稀有产出概率 {0:0.##}%：{1} 0~{2}", "Rare output chance {0:0.##}%: {1} 0–{2}", true),
            ("gameplay.economy.service.rare_production", "稀有产出 +{0} {1}", "Rare output +{0} {1}", true),
            ("gameplay.economy.service.resource_shortage", "预计资源不足 {0}", "Expected resource shortage: {0}", true),
            ("gameplay.economy.service.market_storage_blocked", "市场收入无法入库 {0}", "Market income cannot be stored: {0}", true),
            ("gameplay.economy.service.market_income", "经手价值 {0}，预计金币 +{1}", "Handled value {0}; expected Gold +{1}", true),
            ("gameplay.economy.service.blocked_warning", "T+{0} {1}：{2}。", "T+{0} {1}: {2}.", true),
            ("gameplay.economy.service.resource_changes", "：{0}", ": {0}", true),
            ("gameplay.feature.building", "建造系统", "Building", false),
            ("gameplay.feature.inventory", "库存系统", "Inventory", false),
            ("gameplay.feature.technology", "科技系统", "Technology", false),
            ("gameplay.feature.expedition", "远征系统", "Expedition", false),
            ("gameplay.feature.inheritance", "继承系统", "Succession", false),
            ("gameplay.feature.congress", "国会系统", "Congress", false),
            ("gameplay.feature.unknown", "未知系统", "Unknown System", false)
        };

        private static readonly Dictionary<string, string> EnglishToChinese =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["An error has occurred and the game will restart."] = "发生错误，游戏将重新启动。",
                ["Are you sure you want to overwrite this save game?  "] = "确定要覆盖这个存档吗？",
                ["Are you sure you want to quit?"] = "确定要退出吗？",
                ["BUFF"] = "增益",
                ["Close"] = "关闭",
                ["Confirm"] = "确认",
                ["Continue"] = "继续游戏",
                ["Create New Save"] = "创建新存档",
                ["Credits"] = "制作人员",
                ["Enter a name for your Save "] = "输入存档名称",
                ["Load Game"] = "读取游戏",
                ["MANUAL SAVE"] = "手动存档",
                ["New "] = "新建",
                ["New Game"] = "新游戏",
                ["New Game +++"] = "新游戏",
                ["NEW QUESTS"] = "新任务",
                ["No"] = "否",
                ["Overwrite game"] = "覆盖存档",
                ["PERMADEATH MODE"] = "永久死亡模式",
                ["Promote corporate synergy"] = "提升协作效率",
                ["Quit"] = "退出",
                ["Quit Game"] = "退出游戏",
                ["Save_Game_Location"] = "存档位置",
                ["Settings"] = "设置",
                ["Type a message here..."] = "在此输入名称……"
            };

        private static readonly Dictionary<string, string> ChineseToEnglish =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["关闭"] = "Close",
                ["伐木"] = "Logging",
                ["9回合"] = "9 turns",
                ["候选人才"] = "Candidates",
                ["关系"] = "Relationship",
                ["特性"] = "Traits",
                ["状态"] = "Status",
                ["帝王谷"] = "Valley of Kings",
                ["任务"] = "Quests",
                ["建筑"] = "Buildings",
                ["远征"] = "Expeditions",
                ["数量"] = "Amount",
                ["继承状态"] = "Succession Status",
                ["每回合：2木材"] = "Per turn: 2 Wood",
                ["岗位"] = "Position",
                ["概览"] = "Overview",
                ["加成"] = "Bonus",
                ["居民房"] = "Residence",
                ["年龄 / 寿命"] = "Age / Lifespan",
                ["身份 / 状态"] = "Role / Status",
                ["王族成员"] = "Royal Family",
                ["值"] = "Value",
                ["准备一些描述XXX"] = "Quest description placeholder",
                ["屯田储备"] = "Agricultural Reserves",
                ["金币*30"] = "Gold ×30",
                ["txt_描述"] = "Description",
                ["确认"] = "Confirm",
                ["取消"] = "Cancel",
                ["国王："] = "King:",
                ["王后："] = "Queen:",
                ["继承人"] = "Heir",
                ["退位"] = "Abdicate",
                ["王族血脉延续中"] = "The royal line endures",
                ["第 1 代"] = "Generation 1",
                ["用稳定符号记录语言，让经验、契约与学说能够跨越个人持续传承。"] = "Record language with stable symbols so experience, contracts, and theories can endure beyond any one person.",
                ["当前研究的科技名称"] = "Current research",
                ["出发文本"] = "Launch",
                ["王族世代表"] = "Royal Lineage",
                ["当前没有继承人"] = "There is currently no heir",
                ["没有王族记录"] = "No royal records",
                ["自动计算补贴维持满员"] = "Automatically calculate subsidies to maintain full staffing",
                ["能够为你的城市提供人口、和税收"] = "Provides population and tax revenue to your settlement",
                ["选择种子"] = "Select Seed",
                ["消耗金币*30"] = "Costs Gold ×30",
                ["自动升级"] = "Auto Upgrade",
                ["补贴："] = "Subsidy:",
                ["成熟：0/3"] = "Growth: 0/3",
                ["成熟"] = "Mature",
                ["工人：0/3（稳定：2）"] = "Workers: 0/3 (Stable: 2)",
                ["  每回合产出一个苹果"] = "  Produces one apple per turn",
                ["伐"] = "Log",
                ["收获"] = "Harvest",
                ["消耗x金币，招募x名工人"] = "Spend x Gold to recruit x workers",
                ["自动收货"] = "Auto Harvest",
                ["铲除"] = "Remove",
                ["在这片土地上开始故事"] = "Begin your story on this land",
                ["库存"] = "Inventory",
                ["5 科技点"] = "5 Research Points",
                ["预览"] = "Preview",
                ["需求"] = "Required",
                ["建筑名称"] = "Building Name",
                ["黑海"] = "Black Sea",
                ["名称"] = "Name",
                ["解锁内容"] = "Unlocks",
                ["测试王国"] = "Test Kingdom",
                ["史诗开始"] = "Begin the Chronicle",
                ["给你的文明取一个名称"] = "Name your civilization",
                ["109回合"] = "Turn 109",
                ["启蒙"] = "Enlightenment",
                ["手动保存"] = "Manual Save",
                ["城邦阶段"] = "City-State Era",
                ["生育王子"] = "Have a Prince",
                ["指定物资"] = "Selected Resource",
                ["Gameplay 调试"] = "Gameplay Debug",
                ["获取一个随机任务"] = "Get a Random Quest",
                ["F8 可关闭 Gameplay 调试面板。"] = "Press F8 to close the Gameplay debug panel.",
                ["石头*10"] = "Stone ×10",
                ["添加 9999 金币"] = "Add 9999 Gold",
                ["物品目录中没有有效物资"] = "No valid resources in the item catalog",
                ["添加所选物资"] = "Add Selected Resource",
                ["仓库"] = "Warehouse",
                ["建筑的概览信息"] = "Building overview",
                ["前置：启蒙"] = "Prerequisite: Enlightenment",
                ["建造"] = "Build",
                ["惩罚"] = "Penalty",
                ["人口数"] = "Population",
                ["解锁与完成效果"] = "Unlocks and Completion Effects",
                ["采石3"] = "Quarry 3",
                ["仓1"] = "Warehouse 1",
                ["仓s"] = "Warehouse (Construction)",
                ["伐2"] = "Logging 2",
                ["仓3"] = "Warehouse 3",
                ["详情"] = "Details",
                ["可达"] = "Reachable",
                ["仓2"] = "Warehouse 2",
                ["田"] = "Farm",
                ["采石"] = "Quarry",
                ["采石1"] = "Quarry 1",
                ["采石2"] = "Quarry 2",
                ["警"] = "Police",
                ["医"] = "Hospital",
                ["鱼1"] = "Fishing 1",
                ["鱼2"] = "Fishing 2",
                ["招募"] = "Recruit",
                ["人才"] = "Talents",
                ["等级"] = "Level",
                ["岗位槽"] = "Positions",
                ["薪资 0/回合"] = "Salary 0/turn",
                ["未选择人才"] = "No talent selected",
                ["人才池"] = "Talent Pool",
                ["未任命"] = "Unassigned",
                ["任命"] = "Assign",
                ["清空"] = "Clear",
                ["王室继承"] = "Royal Succession",
                ["先从人才池选择人才"] = "Select a talent from the talent pool first",
                ["升级"] = "Upgrade",
                ["卸任"] = "Unassign",
                ["空槽"] = "Empty Slot",
                ["人才系统未初始化"] = "Talent system is not initialized",
                ["研究需求：8 科技点"] = "Research Cost: 8 Research Points",
                ["人口"] = "Population",
                ["配资"] = "Allocate Supplies",
                ["文字"] = "Text",
                ["建筑的名称"] = "Building Name",
                ["继承"] = "Succession",
                ["国会"] = "Council",
                ["科技服务未初始化"] = "Technology service is not initialized",
                ["回合"] = "Turn",
                ["人才系统"] = "Talent System",
                ["刷新人才"] = "Refresh Talents",
                ["成功率"] = "Success Chance",
                ["规则"] = "Rules",
                ["拆除"] = "Demolish",
                ["科技面板"] = "Technology"
                ,["产出概览"] = "Production Overview"
                ,["体系战争"] = "Systems Warfare"
                ,["保存"] = "Save"
                ,["信息化"] = "Information Technology"
                ,["元素学"] = "Elemental Studies"
                ,["光学"] = "Optics"
                ,["军工理论"] = "Military Industry Theory"
                ,["农业"] = "Agriculture"
                ,["力学"] = "Mechanics"
                ,["功能"] = "Functions"
                ,["加载上一次存档"] = "Load Last Save"
                ,["化学"] = "Chemistry"
                ,["医学"] = "Medicine"
                ,["协调作战"] = "Coordinated Operations"
                ,["占星术"] = "Astrology"
                ,["历法"] = "Calendar"
                ,["咒语"] = "Spellcraft"
                ,["哲学"] = "Philosophy"
                ,["回合处理中"] = "Processing Turn"
                ,["地图设置"] = "Map Settings"
                ,["天文学"] = "Astronomy"
                ,["导航术"] = "Navigation"
                ,["工业化"] = "Industrialization"
                ,["工程学"] = "Engineering"
                ,["帝国阶段"] = "Imperial Era"
                ,["建筑学"] = "Architecture"
                ,["建筑概览"] = "Building Overview"
                ,["快速保存"] = "Quick Save"
                ,["战术学"] = "Tactics"
                ,["打开存档面板"] = "Open Save Panel"
                ,["捕鱼"] = "Fishing"
                ,["提交"] = "Submit"
                ,["放弃"] = "Abandon"
                ,["教育制度"] = "Education System"
                ,["数学"] = "Mathematics"
                ,["显示地图网格"] = "Show Map Grid"
                ,["木工术"] = "Carpentry"
                ,["未来"] = "The Future"
                ,["机械化"] = "Mechanization"
                ,["材料学"] = "Materials Science"
                ,["法律"] = "Law"
                ,["火药"] = "Gunpowder"
                ,["炼金术"] = "Alchemy"
                ,["物理学"] = "Physics"
                ,["狩猎"] = "Hunting"
                ,["生态工程"] = "Ecological Engineering"
                ,["生物学"] = "Biology"
                ,["石工术"] = "Masonry"
                ,["神学"] = "Theology"
                ,["种植"] = "Plant"
                ,["科学理论"] = "Scientific Theory"
                ,["符文学"] = "Runology"
                ,["经济理论"] = "Economic Theory"
                ,["考古学"] = "Archaeology"
                ,["航空"] = "Aviation"
                ,["蒸汽动力"] = "Steam Power"
                ,["设置"] = "Settings"
                ,["读档"] = "Load"
                ,["货币"] = "Currency"
                ,["轮子"] = "The Wheel"
                ,["返回标题页"] = "Return to Title"
                ,["这是事件"] = "This is an event"
                ,["退出到标题"] = "Exit to Title"
                ,["通灵术"] = "Spiritualism"
                ,["采矿"] = "Mining"
                ,["铁器"] = "Ironworking"
                ,["银行学"] = "Banking"
                ,["阵列作战"] = "Formation Warfare"
                ,["附魔术"] = "Enchanting"
                ,["零件"] = "Components"
                ,["青铜"] = "Bronze"
                ,["驯养"] = "Domestication"
                ,["魔导理论"] = "Arcane Engineering Theory"
                ,["从大尺度环境观测出发重构工业流程，使发展与生态承载能力保持平衡。"] = "Redesign industrial processes through large-scale environmental observation, balancing development with ecological capacity."
                ,["从季节性采集转向有计划的播种与收获，建立稳定食物来源。"] = "Move from seasonal gathering to planned sowing and harvesting to establish a stable food supply."
                ,["从规则度量与轮轴比例中提炼数量关系，建立可复用的计算方法。"] = "Derive numerical relationships from standardized measurement and wheel-and-axle ratios to establish reusable calculation methods."
                ,["以人口、资源和资产生命周期评估组织储蓄、借贷与大规模长期投资。"] = "Use population, resources, and asset life cycles to organize savings, lending, and large-scale long-term investment."
                ,["以实验方法研究生命结构、繁衍与环境适应，建立可验证的生命科学。"] = "Study life, reproduction, and environmental adaptation experimentally to establish verifiable biological science."
                ,["以数学为表达工具，通过元素变化研究运动、受力和能量的普遍规律。"] = "Use mathematics to study the universal laws of motion, force, and energy through elemental change."
                ,["以数量编组和制式武备组织队列，使个体战力转化为稳定的集体行动。"] = "Organize formations with numbered units and standardized arms, turning individual strength into coordinated action."
                ,["以渔汛变化和星象观测记录日月循环，建立统一的季节与日期体系。"] = "Record solar and lunar cycles through fishing seasons and celestial observation to establish a shared calendar."
                ,["以灵性媒介、标准材料和反应控制发展提纯、合成与转化物质的工艺。"] = "Develop purification, synthesis, and transmutation using spiritual media, standardized materials, and controlled reactions."
                ,["以稳定财政支持分级教学，让基础知识和专业技能能够持续复制。"] = "Fund tiered education so foundational knowledge and professional skills can be reproduced continuously."
                ,["以统一计时、远距观测和任务分工协调多个战斗单位的行动。"] = "Coordinate multiple combat units through synchronized timing, long-range observation, and divided responsibilities."
                ,["以资本调度和信息管理组织标准化生产，让工业能力从工坊扩展到体系。"] = "Organize standardized production through capital allocation and information management, scaling industry beyond workshops."
                ,["伐木小屋"] = "Logging Hut"
                ,["依靠信息控制、动力装置与体系化保障实现可持续的空中航行。"] = "Enable sustained flight through information control, propulsion, and systematic support."
                ,["借助光学仪器与航行观测建立天体模型，提高时间和位置计算精度。"] = "Build celestial models with optical instruments and navigational observations to improve time and position calculations."
                ,["农田"] = "Farm"
                ,["冻库"] = "Cold Storage"
                ,["分析知识、生产工具、劳动组织与稀缺资源之间的关系，形成宏观调配方法。"] = "Analyze knowledge, tools, labor organization, and scarce resources to develop methods for economic allocation."
                ,["初识领地"] = "Survey the Territory"
                ,["利用制式武备和结构知识改进炼铁与锻造，获得更坚韧的金属制品。"] = "Improve iron smelting and forging with standardized arms and structural knowledge to produce tougher metal goods."
                ,["利用密封控制和协同机械，将热能稳定转化为连续动力。"] = "Use sealed controls and coordinated machinery to convert heat into continuous power."
                ,["利用支护工具和野外勘察经验开采地下矿脉，获得稳定矿石来源。"] = "Mine underground veins with supports and field-survey knowledge to secure a steady ore supply."
                ,["利用火器、地形和时间差组织局部行动，提高部队在复杂环境中的应变能力。"] = "Use firearms, terrain, and timing to organize local actions and improve battlefield adaptability."
                ,["医院"] = "Hospital"
                ,["医院 LV1 基础医疗"] = "Hospital Lv.1 Basic Care"
                ,["医院 LV1 满岗医疗"] = "Hospital Lv.1 Fully Staffed Care"
                ,["医院 LV2 基础医疗"] = "Hospital Lv.2 Basic Care"
                ,["医院 LV2 满岗医疗"] = "Hospital Lv.2 Fully Staffed Care"
                ,["医院 LV3 基础医疗"] = "Hospital Lv.3 Basic Care"
                ,["医院 LV3 满岗医疗"] = "Hospital Lv.3 Fully Staffed Care"
                ,["原木"] = "Logs"
                ,["可作为蔬菜满足居民饮食需求。"] = "Counts as a vegetable for residents' dietary needs."
                ,["可作为谷物满足居民饮食需求，也可用于农田播种。"] = "Counts as grain for residents' dietary needs and can also be sown on farms."
                ,["在文字积累的知识基础上追问共同规律，形成系统论证和抽象思考。"] = "Seek common principles in recorded knowledge to develop systematic argument and abstract thought."
                ,["备齐基石"] = "Gather the Foundations"
                ,["女神雕塑"] = "Goddess Statue"
                ,["安居成邑"] = "A Settlement to Call Home"
                ,["将体系战争经验、工业产能与军事需求统一规划，形成持续迭代的军工体系。"] = "Unify systems-warfare experience, industrial capacity, and military needs into an evolving defense industry."
                ,["将物理规律应用于梁、杠杆和传动结构，精确分析器械与建筑的受力。"] = "Apply physical laws to beams, levers, and transmissions to analyze forces on machines and buildings."
                ,["将长期观察和劳作需求结合起来，选择适合饲养与役使的动物。"] = "Combine long observation with labor needs to select animals suited for breeding and work."
                ,["将零散经验整理成可讨论、可验证的知识，开启聚落有组织的研究。"] = "Organize scattered experience into testable knowledge and begin structured research."
                ,["小土堆"] = "Small Dirt Pile"
                ,["小石堆"] = "Small Stone Pile"
                ,["小麦"] = "Wheat"
                ,["小麦等谷物物品，可作为居民食物与农田种子。"] = "Grains such as wheat can feed residents and serve as farm seed."
                ,["市场"] = "Market"
                ,["开垦良田"] = "Break New Ground"
                ,["总结追踪、设伏与处理猎物的方法，扩大聚落对野外资源的利用。"] = "Systematize tracking, ambush, and game processing to expand the settlement's use of wild resources."
                ,["所有可食用物品的父级分类，用于冻库等通用食物规则。"] = "Parent category for all edible items, used by general food rules such as cold storage."
                ,["把伦理讨论和统一时间制度转化为公开规则，明确权利、义务与裁断程序。"] = "Turn ethical debate and shared timekeeping into public rules defining rights, duties, and judgment."
                ,["把动力、控制和标准部件广泛部署到生产与运输中，降低对人工操作的依赖。"] = "Deploy power, controls, and standard parts across production and transport to reduce manual dependence."
                ,["把天象、声音与刻写符号组合成可重复施放的基础术式。"] = "Combine celestial signs, sound, and inscribed symbols into repeatable basic spells."
                ,["把工业生产、后勤、情报和军事行动视为相互依赖的完整体系。"] = "Treat industrial production, logistics, intelligence, and military action as one interdependent system."
                ,["把工具加工经验用于石材切割与堆砌，为耐久建筑奠定基础。"] = "Apply toolmaking experience to cutting and laying stone, enabling durable construction."
                ,["把建筑经验和力学计算整合为可复核的设计、施工与维护流程。"] = "Combine building experience and mechanical calculation into verifiable design, construction, and maintenance."
                ,["把标准构件和组织协作转化为结构设计方法，支持更大规模的建筑。"] = "Turn standard components and organized cooperation into structural design for larger buildings."
                ,["把测量、航路成本和器械可靠性结合起来，实现可规划的长距离航行。"] = "Combine surveying, route costs, and equipment reliability to plan long-distance voyages."
                ,["把稳定材料反应与精确控制结合起来，为物品赋予可重复维持的术式。"] = "Combine stable material reactions with precise control to give items repeatable, sustained enchantments."
                ,["把长期时间记录、照护规范与仪式经验整理为诊断和治疗方法。"] = "Organize long-term records, care standards, and ritual experience into diagnosis and treatment."
                ,["按统一尺寸制造可替换构件，让复杂器械能够被拆分、维修和复用。"] = "Make interchangeable parts to standard dimensions so complex machines can be disassembled, repaired, and reused."
                ,["捕鱼小屋"] = "Fishing Hut"
                ,["掌握木材选取、切削和榫接，使木料成为可靠的建筑与工具材料。"] = "Master timber selection, cutting, and joinery to make wood reliable for buildings and tools."
                ,["控制活性材料在金属容器中的快速反应，获得可储存和定向释放的爆发力。"] = "Control rapid reactions in metal vessels to store and direct explosive force."
                ,["播下希望"] = "Sow the Seeds of Hope"
                ,["新的领地正在眼前展开。移动视野巡视四周，为聚落选定第一片落脚之地。"] = "A new territory lies before you. Survey the surroundings and choose where the settlement will take root."
                ,["普通库存"] = "Standard Storage"
                ,["木头"] = "Wood"
                ,["树木"] = "Tree"
                ,["树木 1"] = "Tree 1"
                ,["树木 2"] = "Tree 2"
                ,["树木 3"] = "Tree 3"
                ,["树木 4"] = "Tree 4"
                ,["树木 5"] = "Tree 5"
                ,["树木 6"] = "Tree 6"
                ,["树木 7"] = "Tree 7"
                ,["树木 8"] = "Tree 8"
                ,["树木美化"] = "Tree Beauty"
                ,["树苗"] = "Sapling"
                ,["比较仪式、人体与自然物质的性质，将变化归纳为元素间的作用。"] = "Compare ritual, bodily, and natural properties to explain change as interaction among elements."
                ,["汇合科学、生态、魔导、机械与军工路线，开启仍未被定义的下一阶段文明。"] = "Unite science, ecology, magic, machinery, and military industry to open civilization's next undefined age."
                ,["泥土"] = "Dirt"
                ,["泥路"] = "Dirt Road"
                ,["测试地图"] = "Test Map"
                ,["点亮智慧"] = "Kindle Wisdom"
                ,["王宫"] = "Royal Palace"
                ,["生存有了保障，聚落该把目光投向未来。打开科技系统，选择一项科技作为当前研究。"] = "With survival secured, the settlement can look ahead. Open Technology and select a current research project."
                ,["用哲学解释仪式与神秘经验，建立能够组织信仰和术式传承的教义。"] = "Use philosophy to explain ritual and mystical experience, forming doctrine for faith and magical tradition."
                ,["用工业标准和符文语言描述魔力传导，形成可计算、可复制的魔导体系。"] = "Describe magical conduction with industrial standards and runic language to create a calculable, reproducible system."
                ,["用标准媒介和自动装置高效记录、复制、传递并处理大规模信息。"] = "Use standard media and automated devices to record, copy, transmit, and process information at scale."
                ,["用标准计量重新解释炼金反应，建立元素、化合与守恒的实验体系。"] = "Reinterpret alchemical reactions with standard measurement to establish experimental elements, compounds, and conservation."
                ,["用系统训练和元素感应研究意识与灵体之间的交流。"] = "Study communication between consciousness and spirits through systematic training and elemental perception."
                ,["用计量方法和稳定制度建立通用价值凭证，降低交换与征收成本。"] = "Create a universal store of value through measurement and stable institutions, reducing exchange and tax costs."
                ,["田地已经开始生长，百姓也需要稳定的居所。建造 3 座居民房，让聚落真正扎根。"] = "The fields are growing, but the people need homes. Build 3 residences so the settlement can truly take root."
                ,["白菜"] = "Cabbage"
                ,["石头"] = "Stone"
                ,["石工术采石加成"] = "Masonry Quarry Bonus"
                ,["研究光线传播、反射与折射，制造用于观测和精密瞄准的光学器具。"] = "Study light propagation, reflection, and refraction to make instruments for observation and precise aiming."
                ,["神鹿雕塑"] = "Sacred Deer Statue"
                ,["空置的田地不会自行带来收成。打开农田详情，为 3 座农田选择作物并完成播种。"] = "Empty fields yield nothing. Open farm details, choose crops for 3 farms, and sow them."
                ,["简陋库存"] = "Rudimentary Storage"
                ,["粮仓"] = "Granary"
                ,["粮库"] = "Food Storage"
                ,["粮食是聚落延续的根基。建造 3 座农田，让荒地拥有孕育收获的可能。"] = "Food sustains the settlement. Build 3 farms and turn barren ground toward a future harvest."
                ,["结合石材加工与畜力应用发展轮轴结构，提高运输和机械传动效率。"] = "Develop wheel-and-axle systems from stoneworking and animal power to improve transport and transmission."
                ,["结合精密观测与附魔工艺，按用途设计材料的强度、韧性和传导特性。"] = "Combine precise observation and enchanting to design strength, toughness, and conductivity for each use."
                ,["结合食物储备与猎获经验发展水域捕捞，补充不受耕地限制的食物来源。"] = "Develop fishing from food-storage and hunting experience, adding food sources independent of farmland."
                ,["统一材料实验与工业验证，建立能够持续修正自身的现代科学方法。"] = "Unify material experiments and industrial verification into a modern scientific method that continually self-corrects."
                ,["聚落的建设离不开最基础的材料。收集泥土、原木与石头，为第一批农田备齐物资。"] = "A settlement needs basic materials. Gather dirt, logs, and stone for the first farms."
                ,["胡萝卜"] = "Carrot"
                ,["胡萝卜、白菜等蔬菜物品，可替代满足居民的蔬菜需求。"] = "Vegetables such as carrots and cabbage can satisfy residents' vegetable needs interchangeably."
                ,["蔬菜"] = "Vegetables"
                ,["观察星辰与昼夜周期，用天象为时间、方向和仪式建立共同参照。"] = "Observe stars and day-night cycles to establish shared references for time, direction, and ritual."
                ,["解读古代符号并用信息编码方法重建可组合、可传递的高阶符文语言。"] = "Decode ancient symbols and reconstruct a composable, transmissible high runic language through information encoding."
                ,["警局"] = "Police Station"
                ,["警局 LV1 基础治安"] = "Police Station Lv.1 Basic Security"
                ,["警局 LV1 满岗治安"] = "Police Station Lv.1 Fully Staffed Security"
                ,["警局 LV2 基础治安"] = "Police Station Lv.2 Basic Security"
                ,["警局 LV2 满岗治安"] = "Police Station Lv.2 Fully Staffed Security"
                ,["警局 LV3 基础治安"] = "Police Station Lv.3 Basic Security"
                ,["警局 LV3 满岗治安"] = "Police Station Lv.3 Fully Staffed Security"
                ,["谷物"] = "Grain"
                ,["轮子库存保存改良"] = "Wheel Storage Preservation"
                ,["这是测试地图01"] = "This is test map 01."
                ,["通过役力生产和旋转工具掌握铜合金冶炼，获得稳定的工具与武备材料。"] = "Master bronze smelting through draft power and rotary tools to supply dependable tools and arms."
                ,["通过附魔痕迹、材料年代和遗迹结构重建失落文明的技术与观念。"] = "Reconstruct lost technologies and ideas from enchantment traces, material dating, and ruin structures."
                ,["采石场"] = "Quarry"
                ,["金币"] = "Gold"
                ,["雕塑"] = "Statue"
                ,["雕塑美化"] = "Statue Beauty"
                ,["食物"] = "Food"
                ,["高级库存"] = "Advanced Storage"
                ,["鱼"] = "Fish"
                ,["黄金鱼"] = "Golden Fish"
            };
    }
}
