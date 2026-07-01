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
        [SerializeField, LabelText("建筑预制体")]
        private BuildingBase[] buildingPrefabs = Array.Empty<BuildingBase>();

        private Dictionary<string, BuildingBase> prefabsById;

        public IReadOnlyList<BuildingBase> BuildingPrefabs => buildingPrefabs ?? Array.Empty<BuildingBase>();

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

        public bool TryGetBuildingPrefab(string buildingId, out BuildingBase buildingPrefab)
        {
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                buildingPrefab = null;
                return false;
            }

            EnsureIndex();
            return prefabsById.TryGetValue(buildingId, out buildingPrefab);
        }

        public BuildingBase GetBuildingPrefab(string buildingId)
        {
            if (TryGetBuildingPrefab(buildingId, out var buildingPrefab))
            {
                return buildingPrefab;
            }

            throw new KeyNotFoundException($"Building prefab '{buildingId}' was not found in catalog '{name}'.");
        }

        public bool Contains(string buildingId)
        {
            return TryGetBuildingPrefab(buildingId, out _);
        }

        public void RebuildIndex()
        {
            prefabsById = new Dictionary<string, BuildingBase>(StringComparer.Ordinal);

            if (buildingPrefabs == null)
            {
                return;
            }

            foreach (var buildingPrefab in buildingPrefabs)
            {
                if (buildingPrefab == null || !buildingPrefab.HasDefinition)
                {
                    continue;
                }

                var buildingId = buildingPrefab.Definition.BuildingId;
                if (prefabsById.ContainsKey(buildingId))
                {
                    Debug.LogWarning($"Duplicate building prefab id '{buildingId}' in catalog '{name}'. The first entry will be used.", this);
                    continue;
                }

                prefabsById.Add(buildingId, buildingPrefab);
            }
        }

        private void EnsureIndex()
        {
            if (prefabsById == null)
            {
                RebuildIndex();
            }
        }

        private void NormalizePrefabs()
        {
            buildingPrefabs ??= Array.Empty<BuildingBase>();
        }

#if UNITY_EDITOR
        [FolderPath(RequireExistingPath = true)]
        [SerializeField]
        private string folderPath = "Assets/";

        [Button("从文件夹加载建筑 Prefab")]
        private void LoadBuildingPrefabsFromFolder()
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

            string[] guids = AssetDatabase.FindAssets("t:GameObject", new[] { folderPath });
            var loaded = new List<BuildingBase>(guids.Length);

            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                var building = prefabObject == null ? null : prefabObject.GetComponent<BuildingBase>();
                if (building != null)
                {
                    loaded.Add(building);
                }
            }

            buildingPrefabs = loaded.ToArray();
            EditorUtility.SetDirty(this);
            RebuildIndex();

            Debug.Log($"从文件中加载了 {buildingPrefabs.Length} 个建筑 Prefab.", this);
        }
#endif
    }
}
