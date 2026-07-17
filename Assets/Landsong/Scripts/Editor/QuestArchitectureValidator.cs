using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Quests
{
    public static class QuestArchitectureValidator
    {
        private const string CatalogPath = "Assets/Landsong/Objects/SO/Quests/QuestCatalog.asset";
        private const string GameSystemPrefabPath = "Assets/Landsong/Objects/Prefabs/GameSystem.prefab";

        [MenuItem("Landsong/Quest/Validate Catalog")]
        public static void ValidateMenu()
        {
            var errors = Validate();
            if (errors.Count == 0)
            {
                Debug.Log("QUEST_ARCHITECTURE_VALIDATION_SUCCESS");
                return;
            }

            for (var i = 0; i < errors.Count; i++)
            {
                Debug.LogError(errors[i]);
            }
        }

        public static IReadOnlyList<string> Validate()
        {
            var errors = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<QuestCatalog>(CatalogPath);
            if (catalog == null)
            {
                errors.Add($"任务目录不存在：{CatalogPath}");
                return errors;
            }

            catalog.RebuildIndex();
            ValidateDefinitions(catalog, errors);
            ValidateGameSystemBinding(catalog, errors);
            return errors;
        }

        private static void ValidateDefinitions(QuestCatalog catalog, List<string> errors)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var asset = definitions[i];
                if (asset == null)
                {
                    errors.Add($"QuestCatalog definitions[{i}] 为空。");
                    continue;
                }

                var definition = asset.Data;
                if (definition == null || !definition.IsValid)
                {
                    errors.Add($"任务资产 '{asset.name}' 的定义无效。");
                    continue;
                }

                if (!ids.Add(definition.QuestId))
                {
                    errors.Add($"任务 ID 重复：{definition.QuestId}");
                }

                var prerequisites = definition.PrerequisiteQuestIds;
                for (var j = 0; j < prerequisites.Count; j++)
                {
                    var prerequisiteId = GameQuestDefinition.NormalizeQuestId(prerequisites[j]);
                    if (string.Equals(prerequisiteId, definition.QuestId, StringComparison.Ordinal))
                    {
                        errors.Add($"任务 '{definition.QuestId}' 不能把自己作为前置。");
                    }
                    else if (catalog.Find(prerequisiteId) == null)
                    {
                        errors.Add($"任务 '{definition.QuestId}' 的前置任务不存在：{prerequisiteId}");
                    }
                }
            }

            ValidatePrerequisiteCycles(catalog, errors);
        }

        private static void ValidatePrerequisiteCycles(QuestCatalog catalog, List<string> errors)
        {
            var visitStates = new Dictionary<string, int>(StringComparer.Ordinal);
            var definitions = catalog.Definitions;
            for (var i = 0; i < definitions.Count; i++)
            {
                var definition = definitions[i] == null ? null : definitions[i].Data;
                if (definition != null && definition.IsValid)
                {
                    VisitPrerequisites(definition, catalog, visitStates, errors);
                }
            }
        }

        private static void VisitPrerequisites(
            GameQuestDefinition definition,
            QuestCatalog catalog,
            Dictionary<string, int> visitStates,
            List<string> errors)
        {
            if (visitStates.TryGetValue(definition.QuestId, out var state))
            {
                if (state == 1)
                {
                    errors.Add($"任务前置关系存在循环：{definition.QuestId}");
                }

                return;
            }

            visitStates[definition.QuestId] = 1;
            var prerequisites = definition.PrerequisiteQuestIds;
            for (var i = 0; i < prerequisites.Count; i++)
            {
                var prerequisite = catalog.Find(prerequisites[i]);
                if (prerequisite != null && prerequisite.IsValid)
                {
                    VisitPrerequisites(prerequisite, catalog, visitStates, errors);
                }
            }

            visitStates[definition.QuestId] = 2;
        }

        private static void ValidateGameSystemBinding(QuestCatalog catalog, List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GameSystemPrefabPath);
            var gameSystem = prefab == null ? null : prefab.GetComponent<GameSystem>();
            if (gameSystem == null)
            {
                errors.Add($"GameSystem Prefab 或组件不存在：{GameSystemPrefabPath}");
                return;
            }

            var serializedGameSystem = new SerializedObject(gameSystem);
            var catalogProperty = serializedGameSystem.FindProperty("questCatalog");
            if (catalogProperty == null || catalogProperty.objectReferenceValue != catalog)
            {
                errors.Add("GameSystem.prefab 没有绑定唯一 QuestCatalog.asset。");
            }

            var startingIdsProperty = serializedGameSystem.FindProperty("startingQuestIds");
            if (startingIdsProperty == null || !startingIdsProperty.isArray)
            {
                errors.Add("GameSystem.prefab 缺少起始任务 ID 数组。");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < startingIdsProperty.arraySize; i++)
            {
                var id = GameQuestDefinition.NormalizeQuestId(
                    startingIdsProperty.GetArrayElementAtIndex(i).stringValue);
                var definition = catalog.Find(id);
                if (string.IsNullOrWhiteSpace(id))
                {
                    errors.Add($"GameSystem.prefab 起始任务 ID[{i}] 为空。");
                }
                else if (!seen.Add(id))
                {
                    errors.Add($"GameSystem.prefab 起始任务 ID 重复：{id}");
                }
                else if (definition == null)
                {
                    errors.Add($"GameSystem.prefab 起始任务无法在 Catalog 中解析：{id}");
                }
                else if (definition.Category != QuestCategory.Mainline)
                {
                    errors.Add($"起始任务必须是 Mainline：{id}");
                }
            }
        }
    }
}
