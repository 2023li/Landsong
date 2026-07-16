using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Landsong.BuildingSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Buildings
{
    internal static class BuildingAuthoringService
    {
        private const string CanonicalCatalogPath =
            "Assets/Landsong/Objects/SO/BuildingCatalog.asset";

        private static readonly Regex FamilyIdPattern =
            new Regex("^building\\.[a-z0-9_]+$", RegexOptions.Compiled);
        private static readonly Regex StyleIdPattern =
            new Regex("^[a-z0-9_]+$", RegexOptions.Compiled);
        private static readonly Regex AssetNamePattern =
            new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        public static BuildingAuthoringPaths GetTargetPaths(BuildingAuthoringDraft draft) =>
            new BuildingAuthoringPaths(draft);

        public static List<string> ValidateDraft(
            BuildingAuthoringDraft draft,
            BuildingCatalog catalog)
        {
            var errors = new List<string>();
            if (draft == null)
            {
                errors.Add("创建参数为空。");
                return errors;
            }

            if (catalog == null)
            {
                errors.Add("必须选择 BuildingCatalog。");
            }
            else if (!string.Equals(
                         AssetDatabase.GetAssetPath(catalog),
                         CanonicalCatalogPath,
                         StringComparison.Ordinal))
            {
                errors.Add($"终态架构只允许使用标准 BuildingCatalog：{CanonicalCatalogPath}");
            }

            draft.FamilyId = Normalize(draft.FamilyId);
            draft.DisplayName = Normalize(draft.DisplayName);
            draft.AssetName = Normalize(draft.AssetName);
            if (!FamilyIdPattern.IsMatch(draft.FamilyId))
            {
                errors.Add("FamilyId 必须符合 building.<snake_case>，只能使用小写字母、数字和下划线。");
            }

            if (string.IsNullOrWhiteSpace(draft.DisplayName))
            {
                errors.Add("显示名称不能为空。");
            }

            if (!AssetNamePattern.IsMatch(draft.AssetName))
            {
                errors.Add("资产名只能使用英文字母、数字和下划线，且不能以数字开头。");
            }

            if (draft.Footprint.x <= 0 || draft.Footprint.y <= 0)
            {
                errors.Add("固定占地必须大于 0。");
            }

            ValidateTerrain(draft, errors);
            if (draft.InitialLevelCount <= 0)
            {
                errors.Add("初始等级数量必须至少为 1。");
            }

            ValidateCosts(draft.PlacementCosts, "放置费用", errors);
            if (draft.ConstructionTurns == null || draft.ConstructionTurns.Count == 0)
            {
                errors.Add("每个建筑必须至少有一个施工回合；允许该回合费用为空。");
            }
            else
            {
                for (var i = 0; i < draft.ConstructionTurns.Count; i++)
                {
                    var turn = draft.ConstructionTurns[i];
                    if (turn == null)
                    {
                        errors.Add($"施工回合 #{i + 1} 为空。");
                    }
                    else
                    {
                        ValidateCosts(turn.Costs, $"施工回合 #{i + 1} 消耗", errors);
                        ValidateCosts(turn.Rewards, $"施工回合 #{i + 1} 产出", errors);
                    }
                }
            }

            switch (draft.ConstructionViewMode)
            {
                case BuildingConstructionViewMode.Single:
                    ValidateViewPrefab(draft.ConstructionViewPrefab, "单一施工 View", errors);
                    break;
                case BuildingConstructionViewMode.PerTurn:
                    if (draft.ConstructionTurnViewPrefabs == null
                        || draft.ConstructionTurnViewPrefabs.Count
                        != (draft.ConstructionTurns?.Count ?? 0))
                    {
                        errors.Add("逐回合施工视图必须与施工回合数量一致；允许某一回合的 View 暂时留空。");
                        break;
                    }

                    for (var i = 0; i < draft.ConstructionTurnViewPrefabs.Count; i++)
                    {
                        ValidateViewPrefab(
                            draft.ConstructionTurnViewPrefabs[i],
                            $"施工回合 #{i + 1} View",
                            errors);
                    }
                    break;
                default:
                    errors.Add($"不支持的施工视图模式：{draft.ConstructionViewMode}");
                    break;
            }

            ValidateStyles(draft, errors);
            ValidateViewPrefab(draft.PlacementPreviewViewPrefab, "放置预览 View", errors);
            ValidateViewPrefab(draft.DefaultOperationalViewPrefab, "默认 LV1 View", errors);
            if (draft.UsesWorkforceProduction)
            {
                ValidateWorkforceProduction(draft, errors);
            }

            ValidateAssetPaths(draft, errors);
            ValidateFamilyIdUniqueness(draft.FamilyId, errors);
            return errors;
        }

        public static bool BeginCreation(BuildingAuthoringDraft draft, BuildingCatalog catalog)
        {
            var errors = ValidateDraft(draft, catalog);
            if (errors.Count > 0)
            {
                Debug.LogError("建筑创建参数无效：\n- " + string.Join("\n- ", errors));
                return false;
            }

            var paths = new BuildingAuthoringPaths(draft);
            var createdAssets = new List<string>();
            BuildingFamilyDefinition family = null;
            try
            {
                EnsureAssetFolder(draft.FamilyFolder);
                EnsureAssetFolder(draft.ModuleFolder);
                EnsureAssetFolder(draft.PresentationFolder);
                EnsureAssetFolder(draft.RuntimePrefabFolder);

                var moduleSet = CreateModuleSet(draft);
                AssetDatabase.CreateAsset(moduleSet, paths.ModuleAssetPath);
                createdAssets.Add(paths.ModuleAssetPath);

                var presentation = CreatePresentation(draft);
                AssetDatabase.CreateAsset(presentation, paths.PresentationAssetPath);
                createdAssets.Add(paths.PresentationAssetPath);

                family = CreateFamily(draft, moduleSet, presentation);
                AssetDatabase.CreateAsset(family, paths.FamilyAssetPath);
                createdAssets.Add(paths.FamilyAssetPath);

                var runtimePrefab = CreateRuntimePrefab(paths.RuntimePrefabPath, draft, family);
                createdAssets.Add(paths.RuntimePrefabPath);
                family.ConfigureRuntime(
                    runtimePrefab,
                    family.Construction,
                    family.Levels,
                    moduleSet,
                    presentation);
                EditorUtility.SetDirty(family);

                RegisterFamily(catalog, family);
                AssetDatabase.SaveAssets();

                var report = Landsong.Editor.BuildingArchitectureValidator.Validate();
                if (report.Errors.Count > 0)
                {
                    throw new InvalidOperationException(
                        "生成后建筑架构校验失败：\n- " + string.Join("\n- ", report.Errors));
                }

                Selection.activeObject = family;
                EditorGUIUtility.PingObject(family);
                Debug.Log(
                    $"建筑创建完成：{draft.FamilyId}。使用统一 BuildingBase，无脚本编译阶段；" +
                    $"当前有 {report.Warnings.Count} 条表现资源回填提醒。",
                    family);
                BuildingAuthoringWindow.RepaintOpenWindow();
                return true;
            }
            catch (Exception exception)
            {
                if (family != null)
                {
                    UnregisterFamily(catalog, family);
                }

                RollbackCreatedAssets(createdAssets);
                Debug.LogException(exception);
                BuildingAuthoringWindow.RepaintOpenWindow();
                return false;
            }
        }

        public static void RegisterFamily(BuildingCatalog catalog, BuildingFamilyDefinition family)
        {
            if (catalog == null || family == null)
            {
                return;
            }

            var families = new List<BuildingFamilyDefinition>();
            var found = false;
            for (var i = 0; i < catalog.Families.Count; i++)
            {
                var candidate = catalog.Families[i];
                if (candidate == null)
                {
                    continue;
                }

                if (candidate == family
                    || string.Equals(candidate.FamilyId, family.FamilyId, StringComparison.Ordinal))
                {
                    if (!found)
                    {
                        families.Add(family);
                        found = true;
                    }
                    continue;
                }

                families.Add(candidate);
            }

            if (!found)
            {
                families.Add(family);
            }

            catalog.ConfigureFamilies(families);
            EditorUtility.SetDirty(catalog);
        }

        private static void UnregisterFamily(BuildingCatalog catalog, BuildingFamilyDefinition family)
        {
            if (catalog == null || family == null)
            {
                return;
            }

            var families = new List<BuildingFamilyDefinition>();
            for (var i = 0; i < catalog.Families.Count; i++)
            {
                if (catalog.Families[i] != null && catalog.Families[i] != family)
                {
                    families.Add(catalog.Families[i]);
                }
            }

            catalog.ConfigureFamilies(families);
            EditorUtility.SetDirty(catalog);
        }

        private static BuildingModuleSetDefinition CreateModuleSet(BuildingAuthoringDraft draft)
        {
            var moduleSet = ScriptableObject.CreateInstance<BuildingModuleSetDefinition>();
            var modules = new List<BuildingModuleBase>();
            if (draft.UsesWorkforceProduction)
            {
                var workforce = new BM_岗位运营();
                workforce.ApplyConfiguration(
                    draft.MaxWorkers,
                    draft.InitialWorkers,
                    draft.BaseJobAttraction,
                    draft.RecruitCost,
                    draft.AutoSubsidy,
                    draft.TargetStableWorkers,
                    draft.GoldItemDefinition);

                var tiers = new WorkerProductionTier[draft.ProductionTiers.Count];
                for (var i = 0; i < draft.ProductionTiers.Count; i++)
                {
                    tiers[i] = new WorkerProductionTier(
                        draft.ProductionTiers[i].MinimumWorkers,
                        draft.ProductionTiers[i].Amount);
                }

                var production = new BM_资源产出();
                production.EnsureSingleOutput(
                    draft.ProductionItem,
                    draft.ProductionIntervalTurns,
                    tiers);

                modules.Add(workforce);
                if (draft.UsesMaintenance)
                {
                    var maintenance = new BM_维护费();
                    maintenance.ApplyConfiguration(
                        draft.MaintenanceItemDefinition,
                        draft.MaintenanceAmountPerTurn);
                    modules.Add(maintenance);
                }
                modules.Add(production);
            }

            moduleSet.Configure(modules);
            return moduleSet;
        }

        private static BuildingPresentationDefinition CreatePresentation(BuildingAuthoringDraft draft)
        {
            var presentation = ScriptableObject.CreateInstance<BuildingPresentationDefinition>();
            presentation.ConfigureDefaultViews(
                draft.ConstructionViewMode == BuildingConstructionViewMode.Single
                    ? draft.ConstructionViewPrefab
                    : null,
                draft.PlacementPreviewViewPrefab,
                draft.DefaultOperationalViewPrefab);

            var constructionMappings = new List<BuildingConstructionViewMapping>();
            if (draft.ConstructionViewMode == BuildingConstructionViewMode.PerTurn
                && draft.ConstructionTurnViewPrefabs != null)
            {
                for (var i = 0; i < draft.ConstructionTurnViewPrefabs.Count; i++)
                {
                    var viewPrefab = draft.ConstructionTurnViewPrefabs[i];
                    if (viewPrefab == null)
                    {
                        continue;
                    }

                    constructionMappings.Add(new BuildingConstructionViewMapping(
                        i + 1,
                        string.Empty,
                        viewPrefab));
                }
            }
            presentation.ConfigureConstructionViews(
                draft.ConstructionViewMode,
                constructionMappings);

            var styles = new List<BuildingStyleDefinition>();
            var mappings = new List<BuildingViewMapping>();
            if (draft.Styles != null)
            {
                for (var i = 0; i < draft.Styles.Count; i++)
                {
                    var style = draft.Styles[i];
                    if (style == null)
                    {
                        continue;
                    }

                    styles.Add(new BuildingStyleDefinition(
                        Normalize(style.StyleId),
                        Normalize(style.DisplayName),
                        style.Icon));
                    mappings.Add(new BuildingViewMapping(
                        1,
                        Normalize(style.StyleId),
                        style.Level1View));
                }
            }

            presentation.Configure(styles, mappings);
            return presentation;
        }

        private static BuildingFamilyDefinition CreateFamily(
            BuildingAuthoringDraft draft,
            BuildingModuleSetDefinition moduleSet,
            BuildingPresentationDefinition presentation)
        {
            var construction = new BuildingConstructionDefinition();
            var constructionCosts = new List<IReadOnlyList<BuildingCost>>();
            var constructionRewards = new List<IReadOnlyList<BuildingCost>>();
            for (var i = 0; i < draft.ConstructionTurns.Count; i++)
            {
                constructionCosts.Add(ConvertCosts(draft.ConstructionTurns[i].Costs));
                constructionRewards.Add(ConvertCosts(draft.ConstructionTurns[i].Rewards));
            }
            construction.Configure(constructionCosts, constructionRewards);

            var family = ScriptableObject.CreateInstance<BuildingFamilyDefinition>();
            family.ConfigureRuntime(null, construction, CreateLevels(draft), moduleSet, presentation);
            family.Definition.ConfigureIdentity(draft.FamilyId, draft.DisplayName, draft.FamilyId);

            var serializedFamily = new SerializedObject(family);
            serializedFamily.Update();
            var definition = serializedFamily.FindProperty("definition");
            definition.FindPropertyRelative("category").intValue = (int)draft.Category;
            definition.FindPropertyRelative("icon").objectReferenceValue = draft.Icon;
            definition.FindPropertyRelative("size").vector2IntValue = new Vector2Int(
                Mathf.Max(1, draft.Footprint.x),
                Mathf.Max(1, draft.Footprint.y));
            definition.FindPropertyRelative("ignoreTerrainRequirement").boolValue =
                draft.IgnoreTerrainRequirement;
            SetStringArray(
                definition.FindPropertyRelative("requiredTerrainKeys"),
                draft.IgnoreTerrainRequirement ? Array.Empty<string>() : draft.RequiredTerrainKeys);
            SetStringArray(
                definition.FindPropertyRelative("requiredAnyFootprintTerrainKeys"),
                draft.IgnoreTerrainRequirement
                    ? Array.Empty<string>()
                    : draft.RequiredAnyFootprintTerrainKeys);
            definition.FindPropertyRelative("movementResistance").intValue = draft.MovementResistance;
            SetCostArray(definition.FindPropertyRelative("placementCosts"), draft.PlacementCosts);
            definition.FindPropertyRelative("blueprintInitiallyLocked").boolValue =
                draft.BlueprintInitiallyLocked;
            definition.FindPropertyRelative("hideWhenBlueprintLocked").boolValue =
                draft.HideWhenBlueprintLocked;
            definition.FindPropertyRelative("buildMenuSortOrder").intValue = draft.BuildMenuSortOrder;
            definition.FindPropertyRelative("maxBuildCount").intValue = Mathf.Max(0, draft.MaxBuildCount);
            definition.FindPropertyRelative("buildLimitGroupId").stringValue = draft.FamilyId;
            definition.FindPropertyRelative("isDevelopmentCompleted").boolValue =
                draft.IsDevelopmentCompleted;
            serializedFamily.ApplyModifiedPropertiesWithoutUndo();
            family.Definition.Normalize();
            return family;
        }

        private static IReadOnlyList<BuildingLevelDefinition> CreateLevels(BuildingAuthoringDraft draft)
        {
            var levels = new List<BuildingLevelDefinition>();
            for (var level = 1; level <= Mathf.Max(1, draft.InitialLevelCount); level++)
            {
                var configurations = new List<BuildingLevelConfigurationBase>();
                if (draft.UsesWorkforceProduction)
                {
                    configurations.Add(new BuildingWorkforceLevelConfiguration(
                        draft.MaxWorkers,
                        draft.InitialWorkers,
                        draft.BaseJobAttraction,
                        draft.RecruitCost,
                        draft.AutoSubsidy,
                        draft.TargetStableWorkers,
                        draft.GoldItemDefinition));

                    if (draft.UsesMaintenance)
                    {
                        configurations.Add(new BuildingMaintenanceLevelConfiguration(
                            draft.MaintenanceItemDefinition,
                            draft.MaintenanceAmountPerTurn));
                    }

                    var tiers = new List<WorkerProductionTier>();
                    for (var i = 0; i < draft.ProductionTiers.Count; i++)
                    {
                        tiers.Add(new WorkerProductionTier(
                            draft.ProductionTiers[i].MinimumWorkers,
                            draft.ProductionTiers[i].Amount));
                    }

                    configurations.Add(new BuildingProductionLevelConfiguration(
                        draft.ProductionIntervalTurns,
                        new[]
                        {
                            new BuildingProductionOutputConfiguration(
                                draft.ProductionItem,
                                tiers)
                        }));
                }

                levels.Add(new BuildingLevelDefinition(
                    level,
                    level == 1,
                    Array.Empty<BuildingCost>(),
                    null,
                    configurations));
            }

            return levels;
        }

        private static BuildingBase CreateRuntimePrefab(
            string assetPath,
            BuildingAuthoringDraft draft,
            BuildingFamilyDefinition family)
        {
            var root = new GameObject($"{draft.AssetName}Runtime");
            try
            {
                var building = root.AddComponent<BuildingBase>();
                var collider = root.GetComponent<BoxCollider2D>();
                collider.size = new Vector2(
                    Mathf.Max(0.5f, draft.Footprint.x),
                    Mathf.Max(0.25f, draft.Footprint.y * 0.5f));

                var controller = root.AddComponent<BuildingPresentationController>();
                var viewRootObject = new GameObject("ViewRoot");
                viewRootObject.transform.SetParent(root.transform, false);
                var viewAdapter = viewRootObject.AddComponent<BuildingView>();

                var serializedController = new SerializedObject(controller);
                serializedController.FindProperty("viewRoot").objectReferenceValue = viewRootObject.transform;
                serializedController.FindProperty("viewAdapter").objectReferenceValue = viewAdapter;
                serializedController.ApplyModifiedPropertiesWithoutUndo();

                var serializedBuilding = new SerializedObject(building);
                serializedBuilding.FindProperty("familyDefinition").objectReferenceValue = family;
                serializedBuilding.FindProperty("isResourceProviderPoint").boolValue = draft.IsResourceProviderPoint;
                serializedBuilding.FindProperty("resourceProviderPriority").intValue = draft.ResourceProviderPriority;
                serializedBuilding.FindProperty("buildingActionPower").intValue = Mathf.Max(0, draft.BuildingActionPower);
                serializedBuilding.FindProperty("view").objectReferenceValue = viewAdapter;
                serializedBuilding.FindProperty("presentationController").objectReferenceValue = controller;
                serializedBuilding.ApplyModifiedPropertiesWithoutUndo();

                var savedRoot = PrefabUtility.SaveAsPrefabAsset(root, assetPath);
                var savedBuilding = savedRoot == null ? null : savedRoot.GetComponent<BuildingBase>();
                if (savedBuilding == null || savedBuilding.GetType() != typeof(BuildingBase))
                {
                    throw new InvalidOperationException($"Runtime Prefab 保存失败或不是统一 BuildingBase：{assetPath}");
                }

                return savedBuilding;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void ValidateTerrain(BuildingAuthoringDraft draft, ICollection<string> errors)
        {
            if (draft.IgnoreTerrainRequirement)
            {
                return;
            }

            var terrainKeys = new HashSet<string>(StringComparer.Ordinal);
            if (draft.RequiredTerrainKeys == null || draft.RequiredTerrainKeys.Count == 0)
            {
                errors.Add("未忽略地形时，至少需要一个地形 Key。");
                return;
            }

            for (var i = 0; i < draft.RequiredTerrainKeys.Count; i++)
            {
                var key = Normalize(draft.RequiredTerrainKeys[i]);
                if (string.IsNullOrEmpty(key))
                {
                    errors.Add($"地形 Key #{i + 1} 为空。");
                }
                else if (!terrainKeys.Add(key))
                {
                    errors.Add($"地形 Key 重复：{key}");
                }
            }

            var anyFootprintKeys = new HashSet<string>(StringComparer.Ordinal);
            if (draft.RequiredAnyFootprintTerrainKeys == null)
            {
                return;
            }

            for (var i = 0; i < draft.RequiredAnyFootprintTerrainKeys.Count; i++)
            {
                var key = Normalize(draft.RequiredAnyFootprintTerrainKeys[i]);
                if (string.IsNullOrEmpty(key))
                {
                    errors.Add($"占地内至少一格需要的 Key #{i + 1} 为空。");
                }
                else if (!anyFootprintKeys.Add(key))
                {
                    errors.Add($"占地内至少一格需要的 Key 重复：{key}");
                }
            }
        }

        private static void ValidateCosts(
            IReadOnlyList<BuildingCostDraft> costs,
            string label,
            ICollection<string> errors)
        {
            if (costs == null)
            {
                return;
            }

            var itemIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < costs.Count; i++)
            {
                var cost = costs[i];
                if (cost == null)
                {
                    errors.Add($"{label} 的费用项 #{i + 1} 为空对象。");
                    continue;
                }

                if (cost.Item == null && cost.Amount == 0)
                {
                    continue;
                }

                if (cost.Item == null || cost.Amount <= 0)
                {
                    errors.Add($"{label} 的费用项 #{i + 1} 必须同时配置物品和正数量。");
                }
                else if (!itemIds.Add(cost.Item.ItemId))
                {
                    errors.Add($"{label} 的物品重复：{cost.Item.ItemId}");
                }
            }
        }

        private static void ValidateStyles(BuildingAuthoringDraft draft, ICollection<string> errors)
        {
            if (draft.Styles == null)
            {
                return;
            }

            var styleIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < draft.Styles.Count; i++)
            {
                var style = draft.Styles[i];
                if (style == null)
                {
                    errors.Add($"视觉样式 #{i + 1} 为空对象。");
                    continue;
                }

                style.StyleId = Normalize(style.StyleId);
                if (!StyleIdPattern.IsMatch(style.StyleId))
                {
                    errors.Add($"StyleId 无效：{style.StyleId}。只能使用小写字母、数字和下划线。");
                }
                else if (!styleIds.Add(style.StyleId))
                {
                    errors.Add($"StyleId 重复：{style.StyleId}");
                }

                ValidateViewPrefab(style.Level1View, $"{style.StyleId} LV1 View", errors);
            }
        }

        private static void ValidateWorkforceProduction(
            BuildingAuthoringDraft draft,
            ICollection<string> errors)
        {
            if (draft.MaxWorkers <= 0)
            {
                errors.Add("岗位生产模板的最大工人必须大于 0。");
            }
            if (draft.InitialWorkers < 0 || draft.InitialWorkers > draft.MaxWorkers)
            {
                errors.Add("初始工人必须在 0～最大工人之间。");
            }
            if (draft.TargetStableWorkers < 0 || draft.TargetStableWorkers > draft.MaxWorkers)
            {
                errors.Add("稳定工人目标必须在 0～最大工人之间。");
            }
            if (draft.GoldItemDefinition == null)
            {
                errors.Add("岗位生产模板必须选择金币物品。");
            }
            if (draft.UsesMaintenance
                && draft.MaintenanceAmountPerTurn > 0
                && draft.MaintenanceItemDefinition == null)
            {
                errors.Add("岗位维护生产模板配置正维护费时必须选择维护费物品。");
            }
            if (draft.ProductionItem == null)
            {
                errors.Add("岗位生产模板必须选择产出物品。");
            }
            if (draft.ProductionIntervalTurns <= 0)
            {
                errors.Add("生产周期必须大于 0。");
            }
            if (draft.ProductionTiers == null || draft.ProductionTiers.Count == 0)
            {
                errors.Add("岗位生产模板至少需要一个工人数产量档位。");
                return;
            }

            var workerCounts = new HashSet<int>();
            for (var i = 0; i < draft.ProductionTiers.Count; i++)
            {
                var tier = draft.ProductionTiers[i];
                if (tier == null
                    || tier.MinimumWorkers <= 0
                    || tier.MinimumWorkers > draft.MaxWorkers
                    || tier.Amount <= 0)
                {
                    errors.Add($"生产档位 #{i + 1} 必须满足 1≤工人≤最大工人，且产量>0。");
                }
                else if (!workerCounts.Add(tier.MinimumWorkers))
                {
                    errors.Add($"生产档位工人数重复：{tier.MinimumWorkers}");
                }
            }
        }

        private static void ValidateAssetPaths(BuildingAuthoringDraft draft, ICollection<string> errors)
        {
            ValidateFolderPath(draft.FamilyFolder, "家族资产目录", errors);
            ValidateFolderPath(draft.ModuleFolder, "模块资产目录", errors);
            ValidateFolderPath(draft.PresentationFolder, "表现资产目录", errors);
            ValidateFolderPath(draft.RuntimePrefabFolder, "Runtime Prefab 目录", errors);

            var paths = new BuildingAuthoringPaths(draft);
            ValidateTargetDoesNotExist(paths.FamilyAssetPath, errors);
            ValidateTargetDoesNotExist(paths.ModuleAssetPath, errors);
            ValidateTargetDoesNotExist(paths.PresentationAssetPath, errors);
            ValidateTargetDoesNotExist(paths.RuntimePrefabPath, errors);
        }

        private static void ValidateFamilyIdUniqueness(string familyId, ICollection<string> errors)
        {
            var guids = AssetDatabase.FindAssets("t:BuildingFamilyDefinition", new[] { "Assets" });
            for (var i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                var family = AssetDatabase.LoadAssetAtPath<BuildingFamilyDefinition>(path);
                if (family != null && string.Equals(family.FamilyId, familyId, StringComparison.Ordinal))
                {
                    errors.Add($"FamilyId 已存在：{familyId}（{path}）");
                    return;
                }
            }
        }

        private static void ValidateViewPrefab(
            GameObject prefab,
            string label,
            ICollection<string> errors)
        {
            if (prefab == null)
            {
                return;
            }

            var path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrWhiteSpace(path)
                || PrefabUtility.GetPrefabAssetType(prefab) == PrefabAssetType.NotAPrefab)
            {
                errors.Add($"{label} 必须引用 Prefab 资产，不能引用场景对象。");
                return;
            }

            if (prefab.GetComponentInChildren<BuildingBase>(true) != null
                || prefab.GetComponentInChildren<BuildingPresentationController>(true) != null)
            {
                errors.Add($"{label} 含建筑运行时组件：{path}");
            }
            if (prefab.GetComponentInChildren<Collider>(true) != null
                || prefab.GetComponentInChildren<Collider2D>(true) != null)
            {
                errors.Add($"{label} 含 Collider：{path}");
            }
        }

        private static IReadOnlyList<BuildingCost> ConvertCosts(IReadOnlyList<BuildingCostDraft> drafts)
        {
            var result = new List<BuildingCost>();
            if (drafts == null)
            {
                return result;
            }

            for (var i = 0; i < drafts.Count; i++)
            {
                if (drafts[i]?.Item != null && drafts[i].Amount > 0)
                {
                    result.Add(new BuildingCost(drafts[i].Item, drafts[i].Amount));
                }
            }
            return result;
        }

        private static void SetCostArray(
            SerializedProperty property,
            IReadOnlyList<BuildingCostDraft> drafts)
        {
            var valid = new List<BuildingCostDraft>();
            if (drafts != null)
            {
                for (var i = 0; i < drafts.Count; i++)
                {
                    if (drafts[i]?.Item != null && drafts[i].Amount > 0)
                    {
                        valid.Add(drafts[i]);
                    }
                }
            }

            property.arraySize = valid.Count;
            for (var i = 0; i < valid.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("itemDefinition").objectReferenceValue = valid[i].Item;
                element.FindPropertyRelative("amount").intValue = valid[i].Amount;
            }
        }

        private static void SetStringArray(SerializedProperty property, IReadOnlyList<string> values)
        {
            property.arraySize = values?.Count ?? 0;
            for (var i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).stringValue = Normalize(values[i]);
            }
        }

        private static void EnsureAssetFolder(string folder)
        {
            folder = NormalizeAssetPath(folder).TrimEnd('/');
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            var parts = folder.Split('/');
            if (parts.Length == 0 || !string.Equals(parts[0], "Assets", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"资产目录必须位于 Assets 下：{folder}");
            }

            var current = "Assets";
            for (var i = 1; i < parts.Length; i++)
            {
                var next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void ValidateFolderPath(
            string folder,
            string label,
            ICollection<string> errors)
        {
            folder = NormalizeAssetPath(folder);
            if (!folder.StartsWith("Assets/", StringComparison.Ordinal) && folder != "Assets")
            {
                errors.Add($"{label} 必须位于 Assets 下：{folder}");
            }
            if (folder.Contains(".."))
            {
                errors.Add($"{label} 不能包含 '..'：{folder}");
            }
        }

        private static void ValidateTargetDoesNotExist(string assetPath, ICollection<string> errors)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null
                || File.Exists(ToAbsolutePath(assetPath)))
            {
                errors.Add($"目标资产已存在，窗口不会覆盖：{assetPath}");
            }
        }

        private static void RollbackCreatedAssets(IReadOnlyList<string> createdAssets)
        {
            for (var i = createdAssets.Count - 1; i >= 0; i--)
            {
                if (AssetDatabase.LoadMainAssetAtPath(createdAssets[i]) != null
                    || File.Exists(ToAbsolutePath(createdAssets[i])))
                {
                    AssetDatabase.DeleteAsset(createdAssets[i]);
                }
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static string ToAbsolutePath(string assetPath) =>
            Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), assetPath));

        private static string NormalizeAssetPath(string value) =>
            Normalize(value).Replace('\\', '/').TrimEnd('/');

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
