using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Landsong.InventorySystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Catalog", fileName = "BuildingCatalog")]
    public sealed class BuildingCatalog : SingletonScriptableObject<BuildingCatalog>
    {
        [SerializeField] private BuildingDefinition[] definitions = Array.Empty<BuildingDefinition>();

        private Dictionary<string, BuildingDefinition> definitionsById;

        public IReadOnlyList<BuildingDefinition> Definitions => definitions ?? Array.Empty<BuildingDefinition>();

        public static Task<BuildingCatalog> LoadAsync(string addressableKey)
        {
            return GetInstanceAsync(addressableKey);
        }

        private void OnEnable()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            if (definitions == null)
            {
                definitions = Array.Empty<BuildingDefinition>();
            }

            RebuildIndex();
        }

        public bool TryGetDefinition(string buildingId, out BuildingDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                definition = null;
                return false;
            }

            EnsureIndex();
            return definitionsById.TryGetValue(buildingId, out definition);
        }

        public BuildingDefinition GetDefinition(string buildingId)
        {
            if (TryGetDefinition(buildingId, out var definition))
            {
                return definition;
            }

            throw new KeyNotFoundException($"Building definition '{buildingId}' was not found in catalog '{name}'.");
        }

        public bool Contains(string buildingId)
        {
            return TryGetDefinition(buildingId, out _);
        }

        public void RebuildIndex()
        {
            definitionsById = new Dictionary<string, BuildingDefinition>(StringComparer.Ordinal);

            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.BuildingId))
                {
                    continue;
                }

                if (definitionsById.ContainsKey(definition.BuildingId))
                {
                    Debug.LogWarning($"Duplicate building definition id '{definition.BuildingId}' in catalog '{name}'. The first entry will be used.", this);
                    continue;
                }

                definitionsById.Add(definition.BuildingId, definition);
            }
        }

        private void EnsureIndex()
        {
            if (definitionsById == null)
            {
                RebuildIndex();
            }
        }

#if UNITY_EDITOR

        [FolderPath(RequireExistingPath = true)]
        [SerializeField]
        private string folderPath = "Assets/";

        [Button]
        private void LoadDefinitionsFromFolder()
        {
            if (string.IsNullOrWhiteSpace(folderPath))
            {
                Debug.LogWarning("Folder path is empty.", this);
                return;
            }

            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                Debug.LogWarning($"Folder path '{folderPath}' is not a valid asset folder.", this);
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:BuildingDefinition", new[] { folderPath });
            List<BuildingDefinition> loaded = new List<BuildingDefinition>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                BuildingDefinition def = AssetDatabase.LoadAssetAtPath<BuildingDefinition>(assetPath);
                if (def != null) loaded.Add(def);
            }

            definitions = loaded.ToArray();
            EditorUtility.SetDirty(this);
            RebuildIndex();

            Debug.Log($"从文件中加载了 {definitions.Length} 个建筑定义.", this);
        }
#endif


    }
}
