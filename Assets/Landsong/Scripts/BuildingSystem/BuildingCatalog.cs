using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Landsong.ConditionSystem;
using Landsong.TechnologySystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Landsong.BuildingSystem
{
    [CreateAssetMenu(menuName = "Landsong/Building/Building Catalog", fileName = "BuildingCatalog")]
    public sealed class BuildingCatalog : SingletonScriptableObject<BuildingCatalog>, ITechnologyUnlockContentProducer
    {
        [SerializeField, LabelText("建筑家族")]
        private BuildingFamilyDefinition[] families = Array.Empty<BuildingFamilyDefinition>();

        private Dictionary<string, BuildingBase> prefabsById;
        private Dictionary<string, BuildingFamilyDefinition> familiesById;
        private BuildingBase[] runtimePrefabs = Array.Empty<BuildingBase>();

        public IReadOnlyList<BuildingFamilyDefinition> Families =>
            families ?? Array.Empty<BuildingFamilyDefinition>();
        public IReadOnlyList<BuildingBase> BuildingPrefabs => runtimePrefabs;
        public string TechnologyUnlockContentSourceId => "building.catalog";

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

        public void InjectTechnologyUnlockContents(TechnologyUnlockContentRegistry registry)
        {
            if (registry == null)
            {
                return;
            }

            var bindings = new List<TechnologyUnlockContentBinding>();
            if (families == null)
            {
                registry.ReplaceSource(TechnologyUnlockContentSourceId, bindings);
                return;
            }

            for (var familyIndex = 0; familyIndex < families.Length; familyIndex++)
            {
                var family = families[familyIndex];
                var definition = family == null ? null : family.Definition;
                if (family == null || definition == null || !definition.IsValid)
                {
                    continue;
                }

                if (definition.AutomaticBlueprintUnlockCondition
                    is GameCondition_TechnologyUnlocked blueprintTechnology
                    && blueprintTechnology.TechnologyDefinition != null)
                {
                    bindings.Add(new TechnologyUnlockContentBinding(
                        blueprintTechnology.TechnologyDefinition.TechnologyId,
                        new TechnologyUnlockContent(
                            $"building-blueprint:{family.FamilyId}",
                            definition.Icon,
                            $"解锁建筑：{definition.DisplayName}",
                            TechnologyUnlockContentKind.Building,
                            shortLabel: "建筑")));
                }

                var levels = family.Levels;
                for (var levelIndex = 0; levelIndex < levels.Count; levelIndex++)
                {
                    var level = levels[levelIndex];
                    if (level == null
                        || level.Level <= 1
                        || !level.IsConfigured
                        || level.UpgradeCondition is not GameCondition_TechnologyUnlocked technologyCondition
                        || technologyCondition.TechnologyDefinition == null)
                    {
                        continue;
                    }

                    bindings.Add(new TechnologyUnlockContentBinding(
                        technologyCondition.TechnologyDefinition.TechnologyId,
                        new TechnologyUnlockContent(
                            $"building-upgrade:{family.FamilyId}:lv{level.Level}",
                            definition.Icon,
                            $"允许{definition.DisplayName}升级至 LV{level.Level}",
                            TechnologyUnlockContentKind.BuildingUpgrade,
                            shortLabel: $"LV{level.Level}")));
                }
            }

            registry.ReplaceSource(TechnologyUnlockContentSourceId, bindings);
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
