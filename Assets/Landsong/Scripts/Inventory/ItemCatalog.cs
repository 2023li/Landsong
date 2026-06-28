using System;
using System.Collections.Generic;
using UnityEngine;
using Moyo.Unity;

#if UNITY_EDITOR
using UnityEditor;
using Sirenix.OdinInspector;
#endif


namespace Landsong.InventorySystem
{
    [CreateAssetMenu(menuName = "Landsong/Inventory/Item Catalog", fileName = "ItemCatalog")]
    public sealed class ItemCatalog : SingletonScriptableObject<ItemCatalog>
    {
        [SerializeField] private ItemDefinition[] definitions = Array.Empty<ItemDefinition>();

        private Dictionary<string, ItemDefinition> definitionsById;

        public IReadOnlyList<ItemDefinition> Definitions => definitions ?? Array.Empty<ItemDefinition>();

        private void OnEnable()
        {
            RebuildIndex();
        }

        private void OnValidate()
        {
            if (definitions == null)
            {
                definitions = Array.Empty<ItemDefinition>();
            }

            RebuildIndex();
        }

        public bool TryGetDefinition(string itemId, out ItemDefinition definition)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                definition = null;
                return false;
            }

            EnsureIndex();
            return definitionsById.TryGetValue(itemId.Trim(), out definition);
        }

        public ItemDefinition GetDefinition(string itemId)
        {
            if (TryGetDefinition(itemId, out var definition))
            {
                return definition;
            }

            throw new KeyNotFoundException($"Item definition '{itemId}' was not found in catalog '{name}'.");
        }

        public bool Contains(string itemId)
        {
            return TryGetDefinition(itemId, out _);
        }

        public int GetMaxStackSize(string itemId, int fallbackStackSize = 99)
        {
            if (TryGetDefinition(itemId, out var definition))
            {
                return definition.MaxStackSize;
            }

            return Mathf.Max(1, fallbackStackSize);
        }

        public void RebuildIndex()
        {
            definitionsById = new Dictionary<string, ItemDefinition>(StringComparer.Ordinal);

            if (definitions == null)
            {
                return;
            }

            foreach (var definition in definitions)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.ItemId))
                {
                    continue;
                }

                if (definitionsById.ContainsKey(definition.ItemId))
                {
                    Debug.LogWarning($"Duplicate item definition id '{definition.ItemId}' in catalog '{name}'. The first entry will be used.", this);
                    continue;
                }

                definitionsById.Add(definition.ItemId, definition);
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

            string[] guids = AssetDatabase.FindAssets("t:ItemDefinition", new[] { folderPath });
            List<ItemDefinition> loaded = new List<ItemDefinition>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                ItemDefinition def = AssetDatabase.LoadAssetAtPath<ItemDefinition>(assetPath);
                if (def != null) loaded.Add(def);
            }

            definitions = loaded.ToArray();
            EditorUtility.SetDirty(this);
            RebuildIndex();

            Debug.Log($"Loaded {definitions.Length} ItemDefinition(s) from folder.", this);
        }
#endif




    }
}
