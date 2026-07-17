using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong
{
    [CreateAssetMenu(menuName = "Landsong/Quest/Quest Catalog", fileName = "QuestCatalog")]
    public sealed class QuestCatalog : ScriptableObject
    {
        [SerializeField, LabelText("任务定义")]
        private QuestDefinition[] definitions = Array.Empty<QuestDefinition>();

        [SerializeField, LabelText("运行时交换任务规则")]
        private RandomExchangeQuestRule[] runtimeExchangeQuestRules = Array.Empty<RandomExchangeQuestRule>();

        private Dictionary<string, QuestDefinition> definitionsById;

        public IReadOnlyList<QuestDefinition> Definitions => definitions ?? Array.Empty<QuestDefinition>();
        public IReadOnlyList<RandomExchangeQuestRule> RuntimeExchangeQuestRules =>
            runtimeExchangeQuestRules ?? Array.Empty<RandomExchangeQuestRule>();

        public GameQuestDefinition Find(string questId)
        {
            return TryGetDefinition(questId, out var definition) ? definition.Data : null;
        }

        public bool TryGetDefinition(string questId, out QuestDefinition definition)
        {
            var normalizedQuestId = GameQuestDefinition.NormalizeQuestId(questId);
            if (string.IsNullOrWhiteSpace(normalizedQuestId))
            {
                definition = null;
                return false;
            }

            EnsureIndex();
            return definitionsById.TryGetValue(normalizedQuestId, out definition);
        }

        public GameQuestDefinition[] Resolve(IEnumerable<string> questIds)
        {
            if (questIds == null)
            {
                return Array.Empty<GameQuestDefinition>();
            }

            var result = new List<GameQuestDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var questId in questIds)
            {
                var definition = Find(questId);
                if (definition != null && definition.IsValid && seen.Add(definition.QuestId))
                {
                    result.Add(definition);
                }
            }

            return result.Count == 0 ? Array.Empty<GameQuestDefinition>() : result.ToArray();
        }

        public GameQuestDefinition[] GetDefinitions(QuestCategory category)
        {
            if (definitions == null || definitions.Length == 0)
            {
                return Array.Empty<GameQuestDefinition>();
            }

            var normalizedCategory = GameQuestDefinition.NormalizeQuestCategory(category);
            var result = new List<GameQuestDefinition>();
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                if (definition != null
                    && definition.IsValid
                    && definition.Category == normalizedCategory)
                {
                    result.Add(definition.Data);
                }
            }

            return result.Count == 0 ? Array.Empty<GameQuestDefinition>() : result.ToArray();
        }

        public RandomExchangeQuestRule[] CopyRuntimeExchangeQuestRules()
        {
            if (runtimeExchangeQuestRules == null || runtimeExchangeQuestRules.Length == 0)
            {
                return Array.Empty<RandomExchangeQuestRule>();
            }

            var result = new RandomExchangeQuestRule[runtimeExchangeQuestRules.Length];
            Array.Copy(runtimeExchangeQuestRules, result, runtimeExchangeQuestRules.Length);
            return result;
        }

        private void OnEnable()
        {
            Normalize();
            RebuildIndex();
        }

        private void OnValidate()
        {
            Normalize();
            RebuildIndex();
        }

        public void RebuildIndex()
        {
            definitionsById = new Dictionary<string, QuestDefinition>(StringComparer.Ordinal);
            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition == null || !definition.IsValid)
                {
                    continue;
                }

                if (!definitionsById.TryAdd(definition.QuestId, definition))
                {
                    Debug.LogWarning($"任务目录 '{name}' 中存在重复任务 ID '{definition.QuestId}'，将使用第一项。", this);
                }
            }
        }

        private void EnsureIndex()
        {
            if (definitionsById == null)
            {
                RebuildIndex();
            }
        }

        private void Normalize()
        {
            definitions ??= Array.Empty<QuestDefinition>();
            runtimeExchangeQuestRules ??= Array.Empty<RandomExchangeQuestRule>();
            for (var i = 0; i < runtimeExchangeQuestRules.Length; i++)
            {
                runtimeExchangeQuestRules[i]?.Normalize();
            }
        }

#if UNITY_EDITOR
        [FolderPath(RequireExistingPath = true), SerializeField, LabelText("任务定义目录")]
        private string folderPath = "Assets/Landsong/Objects/SO/Quests";

        [Button("从目录登记任务定义")]
        private void LoadDefinitionsFromFolder()
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"任务定义目录 '{folderPath}' 不存在。", this);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:QuestDefinition", new[] { folderPath });
            var loaded = new List<QuestDefinition>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var definition = AssetDatabase.LoadAssetAtPath<QuestDefinition>(path);
                if (definition != null)
                {
                    loaded.Add(definition);
                }
            }

            loaded.Sort((left, right) => string.Compare(left.QuestId, right.QuestId, StringComparison.Ordinal));
            definitions = loaded.ToArray();
            RebuildIndex();
            EditorUtility.SetDirty(this);
            Debug.Log($"任务目录已登记 {definitions.Length} 个任务定义。", this);
        }
#endif
    }
}
