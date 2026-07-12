using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.PolicySystem
{
    [CreateAssetMenu(menuName = "Landsong/Policy/Policy Catalog", fileName = "PolicyCatalog")]
    public sealed class PolicyCatalog : SingletonScriptableObject<PolicyCatalog>
    {
        [SerializeField, LabelText("政策定义")]
        private PolicyDefinition[] definitions = Array.Empty<PolicyDefinition>();

        private Dictionary<string, PolicyDefinition> definitionsById;

        public IReadOnlyList<PolicyDefinition> Definitions => definitions ?? Array.Empty<PolicyDefinition>();

        public static Task<PolicyCatalog> LoadAsync(string addressableKey)
        {
            return GetInstanceAsync(addressableKey);
        }

        private void OnEnable()
        {
            NormalizeDefinitions();
            RebuildIndex();
        }

        private void OnValidate()
        {
            NormalizeDefinitions();
            RebuildIndex();
        }

        public bool TryGetDefinition(string policyId, out PolicyDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(policyId))
            {
                definition = null;
                return false;
            }

            EnsureIndex();
            return definitionsById.TryGetValue(policyId.Trim(), out definition);
        }

        public IReadOnlyList<PolicyDefinition> GetDefinitionsForTree(string treeId)
        {
            var normalizedTreeId = NormalizeId(treeId);
            if (string.IsNullOrEmpty(normalizedTreeId))
            {
                return Array.Empty<PolicyDefinition>();
            }

            var result = new List<PolicyDefinition>();
            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition != null && string.Equals(definition.TreeId, normalizedTreeId, StringComparison.Ordinal))
                {
                    result.Add(definition);
                }
            }

            result.Sort(CompareDefinitions);
            return result;
        }

        public IReadOnlyList<PolicyDefinition> GetDefinitionsForTier(string treeId, int tier)
        {
            var normalizedTreeId = NormalizeId(treeId);
            if (string.IsNullOrEmpty(normalizedTreeId) || tier < 1)
            {
                return Array.Empty<PolicyDefinition>();
            }

            var result = new List<PolicyDefinition>();
            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition != null
                    && definition.Tier == tier
                    && string.Equals(definition.TreeId, normalizedTreeId, StringComparison.Ordinal))
                {
                    result.Add(definition);
                }
            }

            result.Sort(CompareDefinitions);
            return result;
        }

        public void RebuildIndex()
        {
            definitionsById = new Dictionary<string, PolicyDefinition>(StringComparer.Ordinal);
            var treeNamesById = new Dictionary<string, string>(StringComparer.Ordinal);

            for (var i = 0; i < Definitions.Count; i++)
            {
                var definition = Definitions[i];
                if (definition == null || !definition.IsValid)
                {
                    continue;
                }

                if (!definitionsById.TryAdd(definition.PolicyId, definition))
                {
                    Debug.LogWarning($"政策目录 '{name}' 中存在重复政策ID '{definition.PolicyId}'，将使用第一项。", this);
                }

                if (treeNamesById.TryGetValue(definition.TreeId, out var treeName))
                {
                    if (!string.Equals(treeName, definition.TreeDisplayName, StringComparison.Ordinal))
                    {
                        Debug.LogWarning($"政策树 '{definition.TreeId}' 使用了不一致的显示名称。", this);
                    }
                }
                else
                {
                    treeNamesById.Add(definition.TreeId, definition.TreeDisplayName);
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

        private void NormalizeDefinitions()
        {
            definitions ??= Array.Empty<PolicyDefinition>();
        }

        private static string NormalizeId(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private static int CompareDefinitions(PolicyDefinition left, PolicyDefinition right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var tierComparison = left.Tier.CompareTo(right.Tier);
            return tierComparison != 0
                ? tierComparison
                : string.Compare(left.PolicyId, right.PolicyId, StringComparison.Ordinal);
        }

#if UNITY_EDITOR
        [FolderPath(RequireExistingPath = true)]
        [SerializeField]
        private string folderPath = "Assets/Landsong/Objects/SO/Policy";

        [Button("从文件夹加载政策")]
        private void LoadDefinitionsFromFolder()
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"政策文件夹 '{folderPath}' 无效。", this);
                return;
            }

            var guids = AssetDatabase.FindAssets("t:PolicyDefinition", new[] { folderPath });
            var loaded = new List<PolicyDefinition>(guids.Length);
            for (var i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var definition = AssetDatabase.LoadAssetAtPath<PolicyDefinition>(assetPath);
                if (definition != null)
                {
                    loaded.Add(definition);
                }
            }

            loaded.Sort(CompareDefinitions);
            definitions = loaded.ToArray();
            EditorUtility.SetDirty(this);
            RebuildIndex();
            Debug.Log($"从文件夹加载了 {definitions.Length} 个政策。", this);
        }
#endif
    }
}
