using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Catalog", fileName = "BuildingCatalog")]
    public sealed class BuildingCatalog : SingletonScriptableObject<BuildingCatalog>
    {
        [SerializeField, LabelText("建筑家族")]
        private BuildingFamilyDefinition[] families = Array.Empty<BuildingFamilyDefinition>();

        private Dictionary<string, BuildingBase> prefabsById;
        private Dictionary<string, BuildingFamilyDefinition> familiesById;
        private BuildingBase[] runtimePrefabs = Array.Empty<BuildingBase>();

        public IReadOnlyList<BuildingFamilyDefinition> Families =>
            families ?? Array.Empty<BuildingFamilyDefinition>();
        public IReadOnlyList<BuildingBase> BuildingPrefabs => runtimePrefabs;

        public static Task<BuildingCatalog> LoadAsync(string addressableKey)
        {
            return GetInstanceAsync(addressableKey);
        }

        private void OnEnable()
        {
            NormalizePrefabs();
            RebuildIndex();
        }

        private void OnValidate()
        {
            NormalizePrefabs();
            RebuildIndex();
        }

        public bool TryGetBuildingPrefab(string familyId, out BuildingBase buildingPrefab)
        {
            if (string.IsNullOrWhiteSpace(familyId))
            {
                buildingPrefab = null;
                return false;
            }

            EnsureIndex();
            return prefabsById.TryGetValue(familyId.Trim(), out buildingPrefab);
        }

        public bool TryGetFamily(string familyId, out BuildingFamilyDefinition family)
        {
            family = null;
            if (string.IsNullOrWhiteSpace(familyId))
            {
                return false;
            }

            EnsureIndex();
            return familiesById.TryGetValue(familyId.Trim(), out family);
        }

        public BuildingBase GetBuildingPrefab(string familyId)
        {
            if (TryGetBuildingPrefab(familyId, out var buildingPrefab))
            {
                return buildingPrefab;
            }

            throw new KeyNotFoundException($"Building family '{familyId}' was not found in catalog '{name}'.");
        }

        public bool Contains(string familyId)
        {
            return TryGetBuildingPrefab(familyId, out _);
        }

        public void RebuildIndex()
        {
            prefabsById = new Dictionary<string, BuildingBase>(StringComparer.Ordinal);
            familiesById = new Dictionary<string, BuildingFamilyDefinition>(StringComparer.Ordinal);
            var prefabs = new List<BuildingBase>();

            if (families != null)
            {
                for (var i = 0; i < families.Length; i++)
                {
                    var family = families[i];
                    if (family == null
                        || !family.IsValid
                        || string.IsNullOrWhiteSpace(family.FamilyId)
                        || familiesById.ContainsKey(family.FamilyId))
                    {
                        continue;
                    }

                    familiesById.Add(family.FamilyId, family);
                    prefabsById.Add(family.FamilyId, family.RuntimePrefab);
                    prefabs.Add(family.RuntimePrefab);
                }

            }

            runtimePrefabs = prefabs.ToArray();
        }

        private void EnsureIndex()
        {
            if (prefabsById == null || familiesById == null)
            {
                RebuildIndex();
            }
        }

        private void NormalizePrefabs()
        {
            families ??= Array.Empty<BuildingFamilyDefinition>();
        }

        public void ConfigureFamilies(IEnumerable<BuildingFamilyDefinition> definitions)
        {
            families = definitions == null
                ? Array.Empty<BuildingFamilyDefinition>()
                : new List<BuildingFamilyDefinition>(definitions).ToArray();
            RebuildIndex();
        }

#if UNITY_EDITOR
        [FolderPath(RequireExistingPath = true)]
        [SerializeField]
        private string folderPath = "Assets/";

        [Button("从文件夹加载建筑家族")]
        private void LoadBuildingFamiliesFromFolder()
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

            string[] guids = AssetDatabase.FindAssets("t:BuildingFamilyDefinition", new[] { folderPath });
            var loaded = new List<BuildingFamilyDefinition>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var family = AssetDatabase.LoadAssetAtPath<BuildingFamilyDefinition>(assetPath);
                if (family != null)
                {
                    loaded.Add(family);
                }
            }

            ConfigureFamilies(loaded);
            EditorUtility.SetDirty(this);

            Debug.Log($"从文件中加载了 {families.Length} 个建筑家族。", this);
        }
#endif
    }
}
