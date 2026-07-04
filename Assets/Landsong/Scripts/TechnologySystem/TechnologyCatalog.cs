using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.TechnologySystem
{
    [CreateAssetMenu(menuName = "Landsong/Technology/Technology Catalog", fileName = "TechnologyCatalog")]
    public sealed class TechnologyCatalog : SingletonScriptableObject<TechnologyCatalog>
    {
        [SerializeField, LabelText("科技节点")]
        private TechnologyDefinition[] definitions = Array.Empty<TechnologyDefinition>();

        private Dictionary<string, TechnologyDefinition> definitionsById;

        public IReadOnlyList<TechnologyDefinition> Definitions => definitions ?? Array.Empty<TechnologyDefinition>();

        public static Task<TechnologyCatalog> LoadAsync(string addressableKey)
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

        public bool TryGetDefinition(string technologyId, out TechnologyDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(technologyId))
            {
                definition = null;
                return false;
            }

            EnsureIndex();
            return definitionsById.TryGetValue(technologyId.Trim(), out definition);
        }

        public TechnologyDefinition GetDefinition(string technologyId)
        {
            if (TryGetDefinition(technologyId, out var definition))
            {
                return definition;
            }

            throw new KeyNotFoundException($"Technology definition '{technologyId}' was not found in catalog '{name}'.");
        }

        public bool Contains(string technologyId)
        {
            return TryGetDefinition(technologyId, out _);
        }

        public void RebuildIndex()
        {
            definitionsById = new Dictionary<string, TechnologyDefinition>(StringComparer.Ordinal);

            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.TechnologyId))
                {
                    continue;
                }

                var technologyId = definition.TechnologyId.Trim();
                if (definitionsById.ContainsKey(technologyId))
                {
                    Debug.LogWarning($"Duplicate technology id '{technologyId}' in catalog '{name}'. The first entry will be used.", this);
                    continue;
                }

                definitionsById.Add(technologyId, definition);
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
            definitions ??= Array.Empty<TechnologyDefinition>();
        }

#if UNITY_EDITOR
        [FolderPath(RequireExistingPath = true)]
        [SerializeField]
        private string folderPath = "Assets/Landsong/Objects/SO/Technology";

        [Button("从文件夹加载科技节点")]
        private void LoadDefinitionsFromFolder()
        {
            EditorLoadDefinitionsFromFolder(folderPath);
        }

        public void EditorLoadDefinitionsFromFolder(string targetFolderPath)
        {
            if (string.IsNullOrWhiteSpace(targetFolderPath))
            {
                Debug.LogWarning("Technology folder path is empty.", this);
                return;
            }

            if (!AssetDatabase.IsValidFolder(targetFolderPath))
            {
                Debug.LogWarning($"Technology folder path '{targetFolderPath}' is not a valid asset folder.", this);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:TechnologyDefinition", new[] { targetFolderPath });
            var loaded = new List<TechnologyDefinition>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var definition = AssetDatabase.LoadAssetAtPath<TechnologyDefinition>(assetPath);
                if (definition != null)
                {
                    loaded.Add(definition);
                }
            }

            definitions = loaded.ToArray();
            EditorUtility.SetDirty(this);
            RebuildIndex();

            Debug.Log($"从文件夹加载了 {definitions.Length} 个科技节点。", this);
        }

        public void EditorAddDefinition(TechnologyDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            var loaded = new List<TechnologyDefinition>(Definitions);
            if (!loaded.Contains(definition))
            {
                loaded.Add(definition);
                definitions = loaded.ToArray();
                EditorUtility.SetDirty(this);
                RebuildIndex();
            }
        }

        public void EditorRemoveDefinition(TechnologyDefinition definition)
        {
            if (definition == null || definitions == null)
            {
                return;
            }

            var loaded = new List<TechnologyDefinition>(Definitions);
            if (loaded.Remove(definition))
            {
                definitions = loaded.ToArray();
                EditorUtility.SetDirty(this);
                RebuildIndex();
            }
        }
#endif
    }
}
