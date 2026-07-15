using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.BuildingSystem;
using Landsong.InventorySystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.Editor
{
    public static class BuildingArchitectureValidator
    {
        private const string CatalogPath = "Assets/Landsong/Objects/SO/BuildingCatalog.asset";
        private const string FamilyFolder = "Assets/Landsong/Objects/SO/Buildings/Families";
        private const string RuntimePrefabFolder = "Assets/Landsong/Objects/Prefabs/BuildingsRuntime";
        private const string ForbiddenLegacyPrefabFolder = "Assets/Landsong/Objects/Prefabs/建筑";

        [MenuItem("Landsong/Building/Validate Final Architecture")]
        public static void Execute()
        {
            var report = Validate();
            foreach (var warning in report.Warnings)
            {
                Debug.LogWarning(warning);
            }

            if (report.Errors.Count > 0)
            {
                var message = "建筑终态架构校验失败：\n- " + string.Join("\n- ", report.Errors);
                Debug.LogError(message);
                throw new InvalidOperationException(message);
            }

            Debug.Log(
                $"建筑终态架构校验通过：{report.FamilyCount} 个家族，" +
                $"{report.RuntimePrefabCount} 个 Runtime Prefab，{report.Warnings.Count} 条美术回填提醒。");
        }

        public static ValidationReport Validate()
        {
            var report = new ValidationReport();
            if (AssetDatabase.IsValidFolder(ForbiddenLegacyPrefabFolder))
            {
                report.Error($"旧等级 Prefab 目录仍存在：{ForbiddenLegacyPrefabFolder}");
            }

            ValidateForbiddenScripts(report);

            var catalog = AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CatalogPath);
            if (catalog == null)
            {
                report.Error($"找不到建筑目录：{CatalogPath}");
                return report;
            }

            var catalogFamilies = catalog.Families.Where(family => family != null).ToArray();
            report.FamilyCount = catalogFamilies.Length;
            if (catalogFamilies.Length == 0)
            {
                report.Error("BuildingCatalog 不包含任何建筑家族。");
                return report;
            }

            ValidateAllFamilyAssetsAreCatalogued(catalogFamilies, report);

            var familyIds = new HashSet<string>(StringComparer.Ordinal);
            var prefabPaths = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < catalogFamilies.Length; i++)
            {
                ValidateFamily(catalogFamilies[i], familyIds, prefabPaths, report);
            }

            ValidateRuntimePrefabFolder(prefabPaths, report);
            report.RuntimePrefabCount = prefabPaths.Count;
            return report;
        }

        [MenuItem("Landsong/Building/Normalize Runtime Prefabs")]
        public static void NormalizeRuntimePrefabs()
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { RuntimePrefabFolder });
            var normalizedCount = 0;
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var building = root.GetComponent<BuildingBase>();
                    if (building == null || building.GetType() != typeof(BuildingBase))
                    {
                        continue;
                    }

                    EditorUtility.SetDirty(root);
                    EditorUtility.SetDirty(building);
                    var presentation = root.GetComponent<BuildingPresentationController>();
                    if (presentation != null)
                    {
                        EditorUtility.SetDirty(presentation);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    normalizedCount++;
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"已规范化保存 {normalizedCount} 个统一建筑 Runtime Prefab。");
        }

        private static void ValidateFamily(
            BuildingFamilyDefinition family,
            HashSet<string> familyIds,
            HashSet<string> prefabPaths,
            ValidationReport report)
        {
            var assetPath = AssetDatabase.GetAssetPath(family);
            var label = string.IsNullOrWhiteSpace(family.FamilyId) ? assetPath : family.FamilyId;
            if (!family.IsValid)
            {
                report.Error($"家族定义无效：{assetPath}");
            }

            if (!familyIds.Add(family.FamilyId))
            {
                report.Error($"FamilyId 重复：{family.FamilyId}");
            }

            var size = family.Definition == null ? Vector2Int.zero : family.Definition.Size;
            if (size.x <= 0 || size.y <= 0)
            {
                report.Error($"{label} 的占地必须为正数，当前为 {size.x}x{size.y}。");
            }

            ValidateLevels(family, label, report);
            ValidateModules(family, label, report);
            ValidatePresentation(family, label, report);

            var prefab = family.RuntimePrefab;
            if (prefab == null)
            {
                report.Error($"{label} 未配置唯一 Runtime Prefab。");
                return;
            }

            var prefabPath = AssetDatabase.GetAssetPath(prefab.gameObject);
            if (string.IsNullOrWhiteSpace(prefabPath) || !prefabPath.StartsWith(RuntimePrefabFolder, StringComparison.Ordinal))
            {
                report.Error($"{label} 的 Runtime Prefab 必须放在 {RuntimePrefabFolder}，当前为 {prefabPath}。");
            }
            else if (!prefabPaths.Add(prefabPath))
            {
                report.Error($"多个家族引用了同一个 Runtime Prefab：{prefabPath}");
            }

            var buildingComponents = prefab.GetComponentsInChildren<BuildingBase>(true);
            if (buildingComponents.Length != 1 || buildingComponents[0].gameObject != prefab.gameObject)
            {
                report.Error($"{label} 的 Runtime Prefab 根节点必须且只能有一个 BuildingBase：{prefabPath}");
            }

            if (prefab.GetType() != typeof(BuildingBase))
            {
                report.Error(
                    $"{label} 使用了家族专属运行时类型 {prefab.GetType().FullName}；" +
                    "终态要求所有普通建筑直接使用统一 BuildingBase，差异由模块承载。");
            }

            if (prefab.FamilyDefinition != family)
            {
                report.Error($"{label} 的 Runtime Prefab 没有反向引用自身家族定义：{prefabPath}");
            }

            var controller = prefab.GetComponent<BuildingPresentationController>();
            if (controller == null || controller.ViewRoot == null)
            {
                report.Error($"{label} 的 Runtime Prefab 缺少 BuildingPresentationController/ViewRoot。");
                return;
            }

            if (controller.ViewRoot.childCount != 0)
            {
                report.Error($"{label} 的 Runtime Prefab/ViewRoot 必须保持空白，表现对象只能在运行时加载。");
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length > 0
                || prefab.GetComponentsInChildren<Animator>(true).Length > 0)
            {
                report.Error($"{label} 的 Runtime Prefab 内含 Renderer/Animator；美术资源必须拆成 View Prefab。");
            }
        }

        private static void ValidateLevels(
            BuildingFamilyDefinition family,
            string label,
            ValidationReport report)
        {
            var moduleIds = new HashSet<string>(StringComparer.Ordinal);
            if (family.ModuleSet != null)
            {
                for (var moduleIndex = 0; moduleIndex < family.ModuleSet.BuildingModules.Count; moduleIndex++)
                {
                    var module = family.ModuleSet.BuildingModules[moduleIndex];
                    if (module != null && module.IsEnabled && !string.IsNullOrWhiteSpace(module.ModuleId))
                    {
                        moduleIds.Add(module.ModuleId);
                    }
                }
            }

            var expectedLevel = 1;
            for (var i = 0; i < family.Levels.Count; i++)
            {
                var level = family.Levels[i];
                if (level == null)
                {
                    report.Error($"{label} 的等级数组包含空项。");
                    continue;
                }

                if (level.Level != expectedLevel)
                {
                    report.Error($"{label} 的等级必须从 LV1 连续递增；期望 LV{expectedLevel}，实际 LV{level.Level}。");
                    expectedLevel = level.Level;
                }

                if (level.Level == 1 && !level.IsConfigured)
                {
                    report.Error($"{label} 的 LV1 必须可用。");
                }

                var configurationIds = new HashSet<string>(StringComparer.Ordinal);
                for (var configurationIndex = 0; configurationIndex < level.Configurations.Count; configurationIndex++)
                {
                    var configuration = level.Configurations[configurationIndex];
                    if (configuration == null || string.IsNullOrWhiteSpace(configuration.ConfigurationId))
                    {
                        report.Error($"{label} LV{level.Level} 包含无 ID 的等级配置。");
                        continue;
                    }

                    if (!configurationIds.Add(configuration.ConfigurationId))
                    {
                        report.Error($"{label} LV{level.Level} 的等级配置 ID 重复：{configuration.ConfigurationId}");
                    }

                    var requiredModuleId = GetRequiredModuleId(configuration);
                    if (!string.IsNullOrWhiteSpace(requiredModuleId) && !moduleIds.Contains(requiredModuleId))
                    {
                        report.Error(
                            $"{label} LV{level.Level} 的配置 {configuration.ConfigurationId} " +
                            $"需要已启用模块 {requiredModuleId}。");
                    }

                    ValidateLevelItemDefinitions(configuration, label, level.Level, report);
                }

                expectedLevel++;
            }
        }

        private static void ValidateModules(
            BuildingFamilyDefinition family,
            string label,
            ValidationReport report)
        {
            if (family.ModuleSet == null)
            {
                report.Error($"{label} 未配置 ModuleSet；无模块建筑也必须使用空 ModuleSet 资产。");
                return;
            }

            var moduleIds = new HashSet<string>(StringComparer.Ordinal);
            var moduleIndexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < family.ModuleSet.BuildingModules.Count; i++)
            {
                var module = family.ModuleSet.BuildingModules[i];
                if (module == null || string.IsNullOrWhiteSpace(module.ModuleId))
                {
                    report.Error($"{label} 的模块 #{i} 缺少稳定 BuildingModuleId。");
                    continue;
                }

                if (!moduleIds.Add(module.ModuleId))
                {
                    report.Error($"{label} 的模块 ID 重复：{module.ModuleId}");
                }

                else if (module.IsEnabled)
                {
                    moduleIndexes[module.ModuleId] = i;
                }

                ValidateModuleItemDefinitions(module, label, report);
            }


            ValidateModuleOrder(label, moduleIndexes, "crop", "workforce", report);
            ValidateModuleOrder(label, moduleIndexes, "production", "workforce", report);
            ValidateModuleOrder(label, moduleIndexes, "production.rare_bonus", "workforce", report);
            ValidateModuleOrder(label, moduleIndexes, "production.rare_bonus", "production", report);
            ValidateModuleOrder(label, moduleIndexes, "market.resource_accounting", "workforce", report);
        }

        private static void ValidateLevelItemDefinitions(
            BuildingLevelConfigurationBase configuration,
            string label,
            int level,
            ValidationReport report)
        {
            var prefix = $"{label} LV{level}/{configuration.ConfigurationId}";
            switch (configuration)
            {
                case BuildingWorkforceLevelConfiguration workforce:
                    ValidateItemDefinition(workforce.GoldItemDefinition, $"{prefix} 金币物品", report);
                    break;
                case BuildingProductionLevelConfiguration production:
                    if (production.Outputs.Count == 0)
                    {
                        report.Error($"{prefix} 至少需要一个产出物品。");
                        break;
                    }

                    for (var i = 0; i < production.Outputs.Count; i++)
                    {
                        ValidateItemDefinition(
                            production.Outputs[i].ItemDefinition,
                            $"{prefix} 产出物品 #{i + 1}",
                            report);
                    }
                    break;
                case ResidentialHousingLevelConfiguration residential:
                    ValidateItemDefinition(residential.FoodItemDefinition, $"{prefix} 食物物品", report);
                    ValidateItemDefinition(residential.TaxItemDefinition, $"{prefix} 税收物品", report);
                    break;
                case FishingHutLevelConfiguration fishing when fishing.EnableSpecialCatch:
                    ValidateItemDefinition(fishing.SpecialItemDefinition, $"{prefix} 特殊产出物品", report);
                    break;
            }
        }

        private static void ValidateModuleItemDefinitions(
            BuildingModuleBase module,
            string label,
            ValidationReport report)
        {
            var prefix = $"{label}/ModuleSet/{module.ModuleId}";
            switch (module)
            {
                case BM_岗位运营 workforce:
                    ValidateItemDefinition(workforce.GoldItemDefinition, $"{prefix} 金币物品", report);
                    break;
                case BM_资源产出 production when !production.HasOnlyValidConfiguredItemDefinitions:
                    report.Error($"{prefix} 包含空物品引用或无 ItemId 的产出配置。");
                    break;
                case BM_居民运营 residential:
                    ValidateItemDefinition(residential.FoodItemDefinition, $"{prefix} 食物物品", report);
                    ValidateItemDefinition(residential.TaxItemDefinition, $"{prefix} 税收物品", report);
                    break;
                case BM_稀有产出 rare:
                    ValidateItemDefinition(rare.ItemDefinition, $"{prefix} 稀有物品", report);
                    break;
                case BM_市场资源结算 market:
                    ValidateItemDefinition(market.GoldItemDefinition, $"{prefix} 金币物品", report);
                    break;
                case BM_树木采集 tree:
                    ValidateItemDefinition(tree.WoodItemDefinition, $"{prefix} 原木物品", report);
                    ValidateItemDefinition(tree.SaplingItemDefinition, $"{prefix} 树苗物品", report);
                    break;
            }
        }

        private static void ValidateItemDefinition(
            ItemDefinition definition,
            string label,
            ValidationReport report)
        {
            if (definition == null)
            {
                report.Error($"{label} 未选择 ItemDefinition。");
            }
            else if (string.IsNullOrWhiteSpace(definition.ItemId))
            {
                report.Error($"{label} 引用的 ItemDefinition 没有稳定 ItemId：{AssetDatabase.GetAssetPath(definition)}");
            }
        }

        private static void ValidateModuleOrder(
            string label,
            IReadOnlyDictionary<string, int> moduleIndexes,
            string moduleId,
            string dependencyId,
            ValidationReport report)
        {
            if (!moduleIndexes.TryGetValue(moduleId, out var moduleIndex))
            {
                return;
            }

            if (!moduleIndexes.TryGetValue(dependencyId, out var dependencyIndex))
            {
                report.Error($"{label} 的模块 {moduleId} 依赖已启用模块 {dependencyId}。");
                return;
            }

            if (dependencyIndex > moduleIndex)
            {
                report.Error(
                    $"{label} 的模块顺序无效：依赖模块 {dependencyId} 必须位于 {moduleId} 之前。");
            }
        }

        private static string GetRequiredModuleId(BuildingLevelConfigurationBase configuration)
        {
            return configuration switch
            {
                BuildingWorkforceLevelConfiguration _ => "workforce",
                BuildingProductionLevelConfiguration _ => "production",
                BuildingInventoryLevelConfiguration _ => "inventory.capacity",
                BuildingTechnologyPointLevelConfiguration _ => "technology.points",
                PlayerHomeLevelConfiguration _ => "population.fixed",
                ResidentialHousingLevelConfiguration _ => "residential.operation",
                FishingHutLevelConfiguration _ => "production.rare_bonus",
                _ => string.Empty
            };
        }

        private static void ValidatePresentation(
            BuildingFamilyDefinition family,
            string label,
            ValidationReport report)
        {
            var presentation = family.Presentation;
            if (presentation == null)
            {
                report.Error($"{label} 未配置 PresentationDefinition。");
                return;
            }

            var styleIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < presentation.Styles.Count; i++)
            {
                var style = presentation.Styles[i];
                if (style == null || !style.IsValid)
                {
                    report.Error($"{label} 包含无效样式 #{i}。");
                    continue;
                }

                if (!styleIds.Add(style.StyleId))
                {
                    report.Error($"{label} 的 StyleId 重复：{style.StyleId}");
                }
            }

            var mappingKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < presentation.ViewMappings.Count; i++)
            {
                var mapping = presentation.ViewMappings[i];
                if (mapping == null)
                {
                    report.Error($"{label} 的 ViewMapping 包含空项。");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(mapping.StyleId) && !styleIds.Contains(mapping.StyleId))
                {
                    report.Error($"{label} 的 ViewMapping 引用了未声明样式：{mapping.StyleId}");
                }

                var key = $"LV{mapping.Level}:{mapping.StyleId}";
                if (!mappingKeys.Add(key))
                {
                    report.Error($"{label} 的 ViewMapping 重复：{key}");
                }

                ValidateViewPrefab(mapping.View, $"{label}/{key}", report);
            }

            ValidateViewPrefab(presentation.ConstructionView, $"{label}/Construction", report);
            ValidateViewPrefab(presentation.PlacementPreviewView, $"{label}/PlacementPreview", report);
            ValidateViewPrefab(presentation.DefaultOperationalView, $"{label}/DefaultOperational", report);

            if (presentation.ConstructionView == null || !presentation.ConstructionView.IsConfigured)
            {
                report.Warn($"{label} 尚未回填施工 View Prefab，将使用统一占位表现。");
            }

            if (styleIds.Count == 0)
            {
                if (!presentation.TryResolveView(BuildingLifecycleStage.Operational, 1, string.Empty, out _))
                {
                    report.Warn($"{label} 尚未回填 LV1 运营 View Prefab，将使用统一占位表现。");
                }
            }
            else
            {
                foreach (var styleId in styleIds)
                {
                    if (!presentation.TryResolveView(BuildingLifecycleStage.Operational, 1, styleId, out _))
                    {
                        report.Warn($"{label}/{styleId} 尚未回填 LV1 View Prefab，将使用统一占位表现。");
                    }
                }
            }
        }

        private static void ValidateViewPrefab(
            BuildingVisualAssetReference reference,
            string label,
            ValidationReport report)
        {
            if (reference == null || !reference.HasDirectPrefab)
            {
                return;
            }

            var prefab = reference.DirectPrefab;
            if (prefab.GetComponentInChildren<BuildingBase>(true) != null
                || prefab.GetComponentInChildren<BuildingPresentationController>(true) != null)
            {
                report.Error($"{label} 的 View Prefab 含建筑运行时组件：{AssetDatabase.GetAssetPath(prefab)}");
            }

            if (prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Collider2D>(true) != null)
            {
                report.Error($"{label} 的 View Prefab 含 Collider；碰撞与占地属于 Runtime Prefab：{AssetDatabase.GetAssetPath(prefab)}");
            }
        }

        private static void ValidateAllFamilyAssetsAreCatalogued(
            IReadOnlyCollection<BuildingFamilyDefinition> catalogFamilies,
            ValidationReport report)
        {
            var catalogued = new HashSet<BuildingFamilyDefinition>(catalogFamilies);
            var guids = AssetDatabase.FindAssets("t:BuildingFamilyDefinition", new[] { FamilyFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var family = AssetDatabase.LoadAssetAtPath<BuildingFamilyDefinition>(path);
                if (family != null && !catalogued.Contains(family))
                {
                    report.Error($"存在未加入 BuildingCatalog 的建筑家族：{path}");
                }
            }
        }

        private static void ValidateRuntimePrefabFolder(
            HashSet<string> cataloguedPrefabPaths,
            ValidationReport report)
        {
            var guids = AssetDatabase.FindAssets("t:Prefab", new[] { RuntimePrefabFolder });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!cataloguedPrefabPaths.Contains(path))
                {
                    report.Error($"Runtime Prefab 目录包含未被家族引用的 Prefab：{path}");
                }
            }
        }

        private static void ValidateForbiddenScripts(ValidationReport report)
        {
            var forbiddenNames = new[]
            {
                "BuildingUnderConstruction",
                "PlayerHomeLV2",
                "PlayerHomeLV3",
                "PlayerHomeLV4",
                "ResidentialHousingLV2",
                "ResidentialHousingLV3",
                "ResidentialHousingLV4",
                "CloudspirePalaceLV0",
                "CloudspirePalaceLV1",
                "PlayerHome",
                "ResidentialHousing",
                "Market",
                "FarmField",
                "FishingHutBuilding",
                "LumberCabin",
                "RoadBuilding",
                "Building_Tree",
                "WorkforceProductionBuildingBase"
            };

            for (var i = 0; i < forbiddenNames.Length; i++)
            {
                var guids = AssetDatabase.FindAssets($"{forbiddenNames[i]} t:MonoScript", new[] { "Assets/Landsong" });
                if (guids.Length > 0)
                {
                    report.Error($"旧等级/施工脚本仍存在：{forbiddenNames[i]}");
                }
            }

            var derivedTypes = TypeCache.GetTypesDerivedFrom<BuildingBase>();
            for (var i = 0; i < derivedTypes.Count; i++)
            {
                var type = derivedTypes[i];
                if (type != null && !type.IsAbstract)
                {
                    report.Error(
                        $"发现具体 BuildingBase 派生类型：{type.FullName}。" +
                        "建筑差异应实现为 BuildingModuleBase 模块。");
                }
            }
        }

        public sealed class ValidationReport
        {
            public readonly List<string> Errors = new List<string>();
            public readonly List<string> Warnings = new List<string>();
            public int FamilyCount;
            public int RuntimePrefabCount;

            public void Error(string message) => Errors.Add(message);
            public void Warn(string message) => Warnings.Add(message);
        }
    }
}
