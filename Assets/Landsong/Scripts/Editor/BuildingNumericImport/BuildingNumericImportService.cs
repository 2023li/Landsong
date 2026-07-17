using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
using Landsong.EditorTools.Buildings;
using Landsong.InventorySystem;
using Landsong.TechnologySystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Buildings.NumericImport
{
    internal sealed class BuildingNumericImportSession
    {
        public BuildingNumericImportSession(
            string projectRelativePath,
            string absolutePath,
            BuildingNumericWorkbookData data,
            BuildingNumericImportReport report,
            Dictionary<string, BuildingFamilyDefinition> families,
            Dictionary<string, ItemDefinition> items,
            Dictionary<string, ItemGroupDefinition> itemGroups,
            Dictionary<string, TechnologyDefinition> technologies,
            Dictionary<string, TechnologyGlobalBuffDefinition> globalBuffs)
        {
            ProjectRelativePath = projectRelativePath;
            AbsolutePath = absolutePath;
            Data = data;
            Report = report;
            Families = families;
            Items = items;
            ItemGroups = itemGroups;
            Technologies = technologies;
            GlobalBuffs = globalBuffs;
        }

        public string ProjectRelativePath { get; }
        public string AbsolutePath { get; }
        public BuildingNumericWorkbookData Data { get; }
        public BuildingNumericImportReport Report { get; }
        public Dictionary<string, BuildingFamilyDefinition> Families { get; }
        public Dictionary<string, ItemDefinition> Items { get; }
        public Dictionary<string, ItemGroupDefinition> ItemGroups { get; }
        public Dictionary<string, TechnologyDefinition> Technologies { get; }
        public Dictionary<string, TechnologyGlobalBuffDefinition> GlobalBuffs { get; }
        public BuildingNumericImportChangePlan ChangePlan { get; internal set; } =
            new BuildingNumericImportChangePlan();
        public bool IsValid => Data != null && Report != null && !Report.HasErrors;
        public bool HasChanges => IsValid && ChangePlan.HasChanges;
    }

    internal sealed class BuildingSpatialEffectImportChange
    {
        public BuildingSpatialEffectImportChange(
            string familyId,
            string effectId,
            BuildingSpatialEffectDefinition definition)
        {
            FamilyId = familyId;
            EffectId = effectId;
            Definition = definition;
        }

        public string FamilyId { get; }
        public string EffectId { get; }
        public BuildingSpatialEffectDefinition Definition { get; }
    }

    internal sealed class TechnologyGlobalBuffImportChange
    {
        public TechnologyGlobalBuffImportChange(
            string buffId,
            TechnologyGlobalBuffDefinition definition)
        {
            BuffId = buffId;
            Definition = definition;
        }

        public string BuffId { get; }
        public TechnologyGlobalBuffDefinition Definition { get; }
    }

    internal sealed class BuildingNumericImportChangePlan
    {
        private readonly HashSet<string> familyAssetIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> presentationAssetIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> moduleSetAssetIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> runtimePrefabAssetIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> changedFamilyIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<UnityEngine.Object> changedAssets = new HashSet<UnityEngine.Object>();
        private readonly List<BuildingSpatialEffectImportChange> spatialEffectChanges =
            new List<BuildingSpatialEffectImportChange>();
        private readonly List<TechnologyGlobalBuffImportChange> globalBuffChanges =
            new List<TechnologyGlobalBuffImportChange>();
        private readonly List<string> messages = new List<string>();

        public bool HasChanges => changedAssets.Count > 0;
        public int ChangedAssetCount => changedAssets.Count;
        public int ChangedFamilyCount => changedFamilyIds.Count;
        public IReadOnlyList<string> Messages => messages;
        public IReadOnlyList<BuildingSpatialEffectImportChange> SpatialEffectChanges => spatialEffectChanges;
        public IReadOnlyList<TechnologyGlobalBuffImportChange> GlobalBuffChanges => globalBuffChanges;

        public bool ChangesFamilyAsset(string familyId) => familyAssetIds.Contains(familyId);
        public bool ChangesPresentationAsset(string familyId) =>
            presentationAssetIds.Contains(familyId);
        public bool ChangesModuleSetAsset(string familyId) => moduleSetAssetIds.Contains(familyId);
        public bool ChangesRuntimePrefabAsset(string familyId) => runtimePrefabAssetIds.Contains(familyId);

        public void AddFamilyAsset(string familyId, BuildingFamilyDefinition family, string summary)
        {
            if (!familyAssetIds.Add(familyId)) return;
            AddAsset(familyId, family);
            messages.Add($"{familyId}：{summary}（{AssetDatabase.GetAssetPath(family)}）");
        }

        public void AddPresentationAsset(
            string familyId,
            BuildingPresentationDefinition presentation,
            string summary)
        {
            if (presentation == null || !presentationAssetIds.Add(familyId)) return;
            AddAsset(familyId, presentation);
            messages.Add(
                $"{familyId}：{summary}（{AssetDatabase.GetAssetPath(presentation)}）");
        }

        public void AddModuleSetAsset(
            string familyId,
            BuildingModuleSetDefinition moduleSet,
            string summary)
        {
            if (moduleSet == null || !moduleSetAssetIds.Add(familyId)) return;
            AddAsset(familyId, moduleSet);
            messages.Add($"{familyId}：{summary}（{AssetDatabase.GetAssetPath(moduleSet)}）");
        }

        public void AddRuntimePrefabAsset(string familyId, BuildingBase runtimePrefab, string summary)
        {
            if (runtimePrefab == null || !runtimePrefabAssetIds.Add(familyId)) return;
            AddAsset(familyId, runtimePrefab.transform.root.gameObject);
            messages.Add($"{familyId}：{summary}（{AssetDatabase.GetAssetPath(runtimePrefab)}）");
        }

        public void AddSpatialEffectAsset(
            string familyId,
            string effectId,
            BuildingSpatialEffectDefinition definition)
        {
            if (definition == null || spatialEffectChanges.Any(change => change.Definition == definition)) return;
            spatialEffectChanges.Add(new BuildingSpatialEffectImportChange(familyId, effectId, definition));
            AddAsset(familyId, definition);
            messages.Add(
                $"{familyId}：更新空间效果 {effectId} 的数值（{AssetDatabase.GetAssetPath(definition)}）");
        }

        public void AddGlobalBuffAsset(
            string buffId,
            TechnologyGlobalBuffDefinition definition)
        {
            if (definition == null || globalBuffChanges.Any(change => change.Definition == definition)) return;
            globalBuffChanges.Add(new TechnologyGlobalBuffImportChange(buffId, definition));
            changedAssets.Add(definition);
            messages.Add($"global-buff:{buffId}：更新科技全局 Buff 数值（{AssetDatabase.GetAssetPath(definition)}）");
        }

        private void AddAsset(string familyId, UnityEngine.Object asset)
        {
            if (asset != null)
            {
                changedAssets.Add(asset);
            }

            if (!string.IsNullOrWhiteSpace(familyId))
            {
                changedFamilyIds.Add(familyId);
            }
        }
    }

    internal static class BuildingNumericImportService
    {
        public const string DefaultWorkbookProjectPath =
            "ConfigSource/Buildings/建筑数值策划表.xlsx";

        public static string ProjectRootPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        private const string ConditionNone = "none";
        private const string ConditionKeep = "keep";
        private const string TechnologyConditionPrefix = "technology.";

        private static readonly HashSet<string> ImportedConfigurationIds = new HashSet<string>(StringComparer.Ordinal)
        {
            "player_home",
            "inventory.capacity",
            "technology.points",
            "residential_housing",
            "workforce",
            "production",
            "fishing_hut",
            "maintenance",
            "operational_experience",
            "warehouse.operation"
        };

        private static readonly HashSet<string> BuildingCategoryNames = new HashSet<string>(
            Enum.GetNames(typeof(BuildingCategory))
                .Where(name => !string.Equals(name, nameof(BuildingCategory.None), StringComparison.Ordinal)),
            StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<Type, FieldInfo[]> SerializableFieldCache =
            new Dictionary<Type, FieldInfo[]>();

        public static BuildingNumericImportSession Analyze(string projectRelativePath)
        {
            var report = new BuildingNumericImportReport();
            if (!TryResolveWorkbookPath(
                    projectRelativePath,
                    out var normalizedProjectPath,
                    out var absolutePath,
                    out var pathError))
            {
                report.Error(pathError);
                return new BuildingNumericImportSession(
                    normalizedProjectPath,
                    string.Empty,
                    null,
                    report,
                    new Dictionary<string, BuildingFamilyDefinition>(),
                    new Dictionary<string, ItemDefinition>(),
                    new Dictionary<string, ItemGroupDefinition>(),
                    new Dictionary<string, TechnologyDefinition>(),
                    new Dictionary<string, TechnologyGlobalBuffDefinition>());
            }

            BuildingNumericWorkbookReader.TryRead(absolutePath, report, out var data);

            var families = FindAssetsByStableId<BuildingFamilyDefinition>(
                "t:BuildingFamilyDefinition",
                family => family == null ? string.Empty : family.FamilyId,
                "建筑家族",
                report);
            var items = FindAssetsByStableId<ItemDefinition>(
                "t:ItemDefinition",
                item => item == null ? string.Empty : item.ItemId,
                "物品",
                report);
            var itemGroups = FindAssetsByStableId<ItemGroupDefinition>(
                "t:ItemGroupDefinition",
                group => group == null ? string.Empty : group.GroupId,
                "物品组",
                report);
            var technologies = FindAssetsByStableId<TechnologyDefinition>(
                "t:TechnologyDefinition",
                technology => technology == null ? string.Empty : technology.TechnologyId,
                "科技",
                report);
            var globalBuffs = FindAssetsByStableId<TechnologyGlobalBuffDefinition>(
                "t:TechnologyGlobalBuffDefinition",
                definition => definition == null ? string.Empty : definition.BuffId,
                "全局 Buff",
                report);

            var session = new BuildingNumericImportSession(
                normalizedProjectPath,
                absolutePath,
                data,
                report,
                families,
                items,
                itemGroups,
                technologies,
                globalBuffs);

            if (data != null)
            {
                Validate(data, families, items, itemGroups, technologies, globalBuffs, report);
                if (!report.HasErrors)
                {
                    try
                    {
                        session.ChangePlan = BuildChangePlan(session);
                        foreach (var message in session.ChangePlan.Messages)
                        {
                            report.Change(message);
                        }
                    }
                    catch (Exception exception)
                    {
                        report.Error($"无法生成导入差异预览：{exception.Message}");
                    }
                }
            }

            return session;
        }

        public static bool TryResolveWorkbookPath(
            string projectRelativePath,
            out string normalizedProjectPath,
            out string absolutePath,
            out string error)
        {
            normalizedProjectPath = NormalizeProjectPath(projectRelativePath);
            absolutePath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(normalizedProjectPath))
            {
                error = "未指定建筑数值源表。";
                return false;
            }

            if (Path.IsPathRooted(normalizedProjectPath))
            {
                error = "建筑数值源必须填写相对于项目根目录的路径。";
                return false;
            }

            if (!normalizedProjectPath.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                error = "建筑数值源必须是 .xlsx 文件。";
                return false;
            }

            try
            {
                var configSourceRoot = Path.GetFullPath(Path.Combine(ProjectRootPath, "ConfigSource"));
                absolutePath = Path.GetFullPath(Path.Combine(
                    ProjectRootPath,
                    normalizedProjectPath.Replace('/', Path.DirectorySeparatorChar)));
                if (!IsPathInside(absolutePath, configSourceRoot))
                {
                    error = "正式建筑数值表必须位于项目根目录的 ConfigSource 文件夹中。";
                    absolutePath = string.Empty;
                    return false;
                }

                normalizedProjectPath = NormalizeProjectPath(
                    Path.GetRelativePath(ProjectRootPath, absolutePath));
                return true;
            }
            catch (Exception exception)
            {
                error = $"建筑数值源路径无效：{exception.Message}";
                absolutePath = string.Empty;
                return false;
            }
        }

        public static bool TryMakeProjectRelativeWorkbookPath(
            string selectedAbsolutePath,
            out string projectRelativePath,
            out string error)
        {
            projectRelativePath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(selectedAbsolutePath))
            {
                error = "未选择文件。";
                return false;
            }

            try
            {
                var absolutePath = Path.GetFullPath(selectedAbsolutePath);
                projectRelativePath = NormalizeProjectPath(Path.GetRelativePath(ProjectRootPath, absolutePath));
                return TryResolveWorkbookPath(projectRelativePath, out projectRelativePath, out _, out error);
            }
            catch (Exception exception)
            {
                error = $"无法解析所选文件路径：{exception.Message}";
                return false;
            }
        }

        public static bool Apply(BuildingNumericImportSession session)
        {
            if (session == null || !session.IsValid)
            {
                return false;
            }

            BuildingNumericImportChangePlan plan;
            try
            {
                plan = BuildChangePlan(session);
                session.ChangePlan = plan;
            }
            catch (Exception exception)
            {
                session.Report.Error($"执行导入前重新计算差异失败：{exception.Message}");
                Debug.LogException(exception);
                return false;
            }

            if (!plan.HasChanges)
            {
                return true;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("导入建筑数值表");
            try
            {
                foreach (var familyRow in session.Data.Families)
                {
                    var family = session.Families[familyRow.FamilyId];
                    if (plan.ChangesFamilyAsset(familyRow.FamilyId))
                    {
                        Undo.RecordObject(family, "导入建筑家族数值");
                        ApplyFamilyAsset(session, familyRow, family);
                    }

                    if (plan.ChangesPresentationAsset(familyRow.FamilyId))
                    {
                        Undo.RecordObject(family.Presentation, "同步建筑视图映射矩阵");
                        if (!BuildingPresentationMappingSynchronizer.TrySynchronize(
                                family,
                                out var synchronizationError)
                            && !string.IsNullOrWhiteSpace(synchronizationError))
                        {
                            throw new InvalidOperationException(
                                $"{familyRow.FamilyId} 无法同步视图映射矩阵：{synchronizationError}");
                        }
                    }

                    if (plan.ChangesModuleSetAsset(familyRow.FamilyId))
                    {
                        Undo.RecordObject(family.ModuleSet, "导入建筑模块数值");
                        ApplyManagedModuleDefaults(session, familyRow.FamilyId, family.ModuleSet);
                        family.ModuleSet.Normalize();
                        EditorUtility.SetDirty(family.ModuleSet);
                    }

                    if (plan.ChangesRuntimePrefabAsset(familyRow.FamilyId))
                    {
                        Undo.RecordObject(family.RuntimePrefab, "导入建筑 Prefab 数值");
                        family.RuntimePrefab.ConfigureNumericAuthoringData(
                            familyRow.IsResourceProviderPoint,
                            familyRow.ResourceProviderPriority,
                            familyRow.BuildingActionPower);
                        EditorUtility.SetDirty(family.RuntimePrefab);
                    }
                }

                foreach (var change in plan.SpatialEffectChanges)
                {
                    var row = session.Data.SpatialEffects.Single(value =>
                        value.FamilyId == change.FamilyId && value.EffectId == change.EffectId);
                    Undo.RecordObject(change.Definition, "导入建筑范围效果数值");
                    ApplySpatialEffectDefaults(row, change.Definition);
                    EditorUtility.SetDirty(change.Definition);
                }

                foreach (var change in plan.GlobalBuffChanges)
                {
                    Undo.RecordObject(change.Definition, "导入科技全局 Buff 数值");
                    var productionRow =
                        session.Data.TechnologyGlobalBuffs.SingleOrDefault(
                            value => value.BuffId == change.BuffId);
                    if (productionRow != null)
                    {
                        ApplyTechnologyGlobalBuff(
                            productionRow,
                            change.Definition,
                            session);
                    }
                    else
                    {
                        var inventoryRow =
                            session.Data.TechnologyInventoryLossBuffs.Single(
                                value => value.BuffId == change.BuffId);
                        ApplyTechnologyInventoryLossBuff(
                            inventoryRow,
                            change.Definition,
                            session);
                    }

                    EditorUtility.SetDirty(change.Definition);
                }

                Landsong.Editor.BuildingArchitectureValidator.Execute();
                SaveChangedAssets(session, plan);
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                var rollbackSucceeded = true;
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    SaveChangedAssets(session, plan);
                }
                catch (Exception rollbackException)
                {
                    rollbackSucceeded = false;
                    session.Report.Error($"导入失败后的自动回滚也发生异常：{rollbackException.Message}");
                    Debug.LogException(rollbackException);
                }
                session.Report.Error(rollbackSucceeded
                    ? $"导入执行失败，全部修改已回滚：{exception.Message}"
                    : $"导入执行失败且自动回滚未完整完成，请立即检查受影响资产：{exception.Message}");
                Debug.LogException(exception);
                return false;
            }
        }

        private static void SaveChangedAssets(
            BuildingNumericImportSession session,
            BuildingNumericImportChangePlan plan)
        {
            foreach (var familyRow in session.Data.Families)
            {
                var family = session.Families[familyRow.FamilyId];
                if (plan.ChangesFamilyAsset(familyRow.FamilyId))
                {
                    AssetDatabase.SaveAssetIfDirty(family);
                }

                if (plan.ChangesModuleSetAsset(familyRow.FamilyId) && family.ModuleSet != null)
                {
                    AssetDatabase.SaveAssetIfDirty(family.ModuleSet);
                }

                if (!plan.ChangesRuntimePrefabAsset(familyRow.FamilyId) || family.RuntimePrefab == null)
                {
                    continue;
                }

                var root = family.RuntimePrefab.transform.root.gameObject;
                if (PrefabUtility.IsPartOfPrefabAsset(root))
                {
                    PrefabUtility.SavePrefabAsset(root);
                }
            }

            foreach (var change in plan.SpatialEffectChanges)
            {
                if (change.Definition != null)
                {
                    AssetDatabase.SaveAssetIfDirty(change.Definition);
                }
            }


            foreach (var change in plan.GlobalBuffChanges)
            {
                if (change.Definition != null)
                {
                    AssetDatabase.SaveAssetIfDirty(change.Definition);
                }
            }
        }

        private static void ApplyFamilyAsset(
            BuildingNumericImportSession session,
            BuildingFamilyNumericRow row,
            BuildingFamilyDefinition family)
        {
            var placementCosts = session.Data.PlacementCosts
                .Where(cost => cost.FamilyId == row.FamilyId)
                .Select(cost => BuildCost(cost, session.Items))
                .ToArray();
            var terrainKeys = SplitStableKeys(row.TerrainKeys);
            ParseCategory(row.Category, out var category);
            family.Definition.ConfigureNumericData(
                row.DisplayName,
                category,
                new Vector2Int(row.SizeX, row.SizeY),
                row.IgnoreTerrain,
                terrainKeys,
                SplitStableKeys(row.AnyFootprintTerrainKeys),
                row.MovementResistance,
                placementCosts,
                BuildAutomaticCondition(row.BlueprintUnlockConditionId, session.Technologies),
                row.BlueprintInitiallyLocked,
                row.HideWhenBlueprintLocked,
                row.BuildMenuSortOrder,
                row.MaxBuildCount,
                row.BuildLimitGroupId,
                row.IsDevelopmentCompleted);

            var constructionTurns = new IReadOnlyList<BuildingCost>[row.ConstructionTurns];
            var constructionRewards = new IReadOnlyList<BuildingCost>[row.ConstructionTurns];
            for (var turn = 1; turn <= row.ConstructionTurns; turn++)
            {
                constructionTurns[turn - 1] = session.Data.ConstructionCosts
                    .Where(cost => cost.FamilyId == row.FamilyId && cost.LevelOrTurn == turn)
                    .Select(cost => BuildCost(cost, session.Items))
                    .ToArray();
                constructionRewards[turn - 1] = session.Data.ConstructionRewards
                    .Where(reward => reward.FamilyId == row.FamilyId && reward.LevelOrTurn == turn)
                    .Select(reward => BuildCost(reward, session.Items))
                    .ToArray();
            }

            var construction = new BuildingConstructionDefinition();
            construction.Configure(constructionTurns, constructionRewards);

            var existingLevels = family.Levels
                .Where(level => level != null)
                .ToDictionary(level => level.Level);
            var levels = session.Data.Levels
                .Where(level => level.FamilyId == row.FamilyId)
                .OrderBy(level => level.Level)
                .Select(level => BuildLevel(session, level, existingLevels))
                .ToArray();

            family.ConfigureImportedNumericData(construction, levels);
            EditorUtility.SetDirty(family);
        }

        private static BuildingLevelDefinition BuildLevel(
            BuildingNumericImportSession session,
            BuildingLevelNumericRow levelRow,
            IReadOnlyDictionary<int, BuildingLevelDefinition> existingLevels)
        {
            existingLevels.TryGetValue(levelRow.Level, out var existingLevel);
            var configurations = new List<BuildingLevelConfigurationBase>();
            if (existingLevel != null)
            {
                configurations.AddRange(existingLevel.Configurations
                    .Where(configuration => configuration != null
                                            && !ImportedConfigurationIds.Contains(configuration.ConfigurationId)));
            }

            configurations.AddRange(BuildImportedLevelConfigurations(session, levelRow, existingLevel));

            var upgradeCosts = session.Data.UpgradeCosts
                .Where(cost => cost.FamilyId == levelRow.FamilyId && cost.LevelOrTurn == levelRow.Level)
                .Select(cost => BuildCost(cost, session.Items))
                .ToArray();
            var condition = BuildCondition(levelRow.ConditionId, existingLevel, session.Technologies);
            return new BuildingLevelDefinition(
                levelRow.Level,
                levelRow.Configured,
                upgradeCosts,
                condition,
                configurations);
        }

        private static IReadOnlyList<BuildingLevelConfigurationBase> BuildImportedLevelConfigurations(
            BuildingNumericImportSession session,
            BuildingLevelNumericRow levelRow,
            BuildingLevelDefinition existingLevel)
        {
            var configurations = new List<BuildingLevelConfigurationBase>();

            var fixedPopulation = session.Data.FixedPopulation
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (fixedPopulation != null)
            {
                configurations.Add(new PlayerHomeLevelConfiguration(fixedPopulation.Population));
            }

            var inventory = session.Data.InventoryCapacity
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (inventory != null)
            {
                TryParseEnum(inventory.SlotType, out InventorySlotType slotType);
                configurations.Add(new BuildingInventoryLevelConfiguration(
                    inventory.Slots,
                    slotType));
            }

            var technology = session.Data.TechnologyPoints
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (technology != null)
            {
                configurations.Add(new BuildingTechnologyPointLevelConfiguration(technology.PointsPerTurn));
            }

            var residential = session.Data.Residential
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (residential != null)
            {
                var foodDefinition = session.Items[residential.FoodItemId];
                TryParseEnum(
                    residential.FoodSelectionPolicy,
                    out ItemRequirementSelectionPolicy foodSelectionPolicy);
                configurations.Add(new ResidentialHousingLevelConfiguration(
                    residential.InitialPopulation,
                    residential.MaxPopulation,
                    foodDefinition,
                    residential.GrowthIntervalTurns,
                    residential.FailureDecayThreshold,
                    session.Items[residential.TaxItemId],
                    residential.TaxIntervalTurns,
                    session.ItemGroups[residential.FoodGroupId],
                    foodSelectionPolicy,
                    residential.TargetDietVariety,
                    residential.MaxLifeQualityChangePerTurn,
                    residential.HighQualityGrowthThreshold));
            }

            var workforce = session.Data.Workforce
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (workforce != null)
            {
                configurations.Add(new BuildingWorkforceLevelConfiguration(
                    workforce.MaxWorkers,
                    workforce.InitialWorkers,
                    workforce.BaseAttraction,
                    workforce.RecruitCost,
                    workforce.AutoSubsidy,
                    workforce.TargetStableWorkers,
                    session.Items[workforce.GoldItemId]));
            }

            var maintenance = session.Data.Maintenance
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (maintenance != null)
            {
                configurations.Add(new BuildingMaintenanceLevelConfiguration(
                    session.Items[maintenance.ItemId],
                    maintenance.AmountPerTurn));
            }

            var operationalExperience = session.Data.OperationalExperience
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (operationalExperience != null)
            {
                configurations.Add(new BuildingOperationalExperienceLevelConfiguration(
                    operationalExperience.RequiredWorkers,
                    operationalExperience.ExperiencePerTurn,
                    operationalExperience.NextLevelExperience));
            }

            var productionRows = session.Data.Production
                .Where(value => IsLevel(value.FamilyId, value.Level, levelRow))
                .ToArray();
            if (productionRows.Length > 0)
            {
                var outputs = productionRows
                    .GroupBy(value => value.OutputItemId, StringComparer.Ordinal)
                    .Select(group => new BuildingProductionOutputConfiguration(
                        session.Items[group.Key],
                        group.OrderBy(value => value.MinimumWorkers)
                            .Select(value => new WorkerProductionTier(value.MinimumWorkers, value.AmountPerCycle))
                            .ToArray()))
                    .ToArray();
                configurations.Add(new BuildingProductionLevelConfiguration(productionRows[0].IntervalTurns, outputs));
            }

            var fishing = session.Data.FishingRare
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (fishing != null)
            {
                session.Items.TryGetValue(fishing.ItemId, out var rareItem);
                configurations.Add(new FishingHutLevelConfiguration(
                    fishing.Enabled,
                    rareItem,
                    fishing.MinimumWorkers,
                    fishing.ChancePercent,
                    fishing.Amount));
            }

            var warehouse = session.Data.Warehouses
                .SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, levelRow));
            if (warehouse != null)
            {
                TryParseEnum(
                    warehouse.BaseSlotType,
                    out InventorySlotType baseSlotType);
                TryParseEnum(
                    warehouse.BonusSlotType,
                    out InventorySlotType bonusSlotType);
                configurations.Add(new WarehouseLevelConfiguration(
                    warehouse.RequiredWorkers,
                    warehouse.ProvidedSlots,
                    baseSlotType,
                    session.Items[warehouse.MaintenanceItemId],
                    warehouse.MaintenancePerTurn,
                    warehouse.ExperienceWorkers,
                    warehouse.ExperiencePerTurn,
                    warehouse.NextLevelExperience,
                    warehouse.BonusWorkerThreshold,
                    warehouse.BonusSlots,
                    bonusSlotType,
                    warehouse.MaintenanceFailureAttractionPenalty));
            }

            return configurations;
        }

        private static void ApplyManagedModuleDefaults(
            BuildingNumericImportSession session,
            string familyId,
            BuildingModuleSetDefinition moduleSet)
        {
            var modules = moduleSet?.BuildingModules ?? Array.Empty<BuildingModuleBase>();
            var market = modules.OfType<BM_市场资源结算>().FirstOrDefault();
            if (market != null)
            {
                var row = session.Data.Markets.Single(value => value.FamilyId == familyId);
                market.ApplyConfiguration(session.Items[row.GoldItemId], row.IncomeRatio);
            }

            var tree = modules.OfType<BM_树木采集>().FirstOrDefault();
            if (tree != null)
            {
                var row = session.Data.Trees.Single(value => value.FamilyId == familyId);
                tree.ApplyConfiguration(
                    row.MinHealth,
                    row.MaxHealth,
                    row.DamagePerDoubleClick,
                    session.Items[row.WoodItemId],
                    row.WoodReward,
                    session.Items[row.SaplingItemId],
                    row.SaplingReward);
            }

            var finiteHarvest = modules.OfType<BM_有限次数采集>().FirstOrDefault();
            if (finiteHarvest != null)
            {
                var row = session.Data.FiniteHarvests.Single(value => value.FamilyId == familyId);
                finiteHarvest.ApplyConfiguration(
                    session.Items[row.RewardItemId],
                    row.RewardPerDoubleClick,
                    row.MaxSuccessfulHarvests);
            }

            var crop = modules.OfType<BuildingCropGrowthModule>().FirstOrDefault();
            if (crop != null)
            {
                var cropDefinitions = session.Data.Crops
                    .Where(value => value.FamilyId == familyId)
                    .OrderBy(value => value.CropId, StringComparer.Ordinal)
                    .Select(value => new BuildingCropAuthoringDefinition(
                        value.CropId,
                        value.DisplayName,
                        value.GrowTurns,
                        session.Data.CropPlantCosts
                            .Where(cost => cost.FamilyId == familyId && cost.CropId == value.CropId)
                            .Select(cost => new BuildingCost(session.Items[cost.ItemId], cost.Amount))
                            .ToArray(),
                        session.Data.CropHarvestRewards
                            .Where(reward => reward.FamilyId == familyId && reward.CropId == value.CropId)
                            .Select(reward => new BuildingCropHarvestAuthoringReward(
                                session.Items[reward.ItemId],
                                reward.MinAmount,
                                reward.MaxAmount))
                            .ToArray()))
                    .ToArray();
                var automaticHarvestCosts = session.Data.CropAutoHarvestCosts
                    .Where(cost => cost.FamilyId == familyId)
                    .Select(cost => BuildCost(cost, session.Items))
                    .ToArray();
                crop.ApplyAuthoringConfiguration(cropDefinitions, automaticHarvestCosts);
            }
        }

        private static void ApplySpatialEffectDefaults(
            SpatialEffectNumericRow row,
            BuildingSpatialEffectDefinition definition)
        {
            TryParseSpatialEffectKind(row.Kind, out var kind);
            TryParseSpatialTargetFilter(row.TargetFilter, out var targetFilter);
            TryParseSpatialStackingRule(row.StackingRule, out var stackingRule);
            definition.ConfigureNumericData(
                row.EffectId,
                row.DisplayName,
                kind,
                targetFilter,
                row.OperationalLevel,
                row.MinimumWorkers,
                row.Range,
                row.Value,
                stackingRule,
                row.IncludeSourceFootprint);
        }

        private static void ApplyTechnologyGlobalBuff(
            TechnologyGlobalBuffNumericRow row,
            TechnologyGlobalBuffDefinition definition,
            BuildingNumericImportSession session)
        {
            var effect = new TechnologyGlobalBuffEffect_BuildingProductionFlat();
            effect.Configure(
                session.Families[row.FamilyId],
                session.Items[row.OutputItemId],
                row.FlatBonus);
            definition.ConfigureNumericData(
                row.BuffId,
                row.DisplayName,
                definition.Icon,
                BuildAutomaticCondition(row.ConditionId, session.Technologies),
                new TechnologyGlobalBuffEffect[] { effect });
        }

        private static void ApplyTechnologyInventoryLossBuff(
            TechnologyInventoryLossBuffNumericRow row,
            TechnologyGlobalBuffDefinition definition,
            BuildingNumericImportSession session)
        {
            var effect =
                new TechnologyGlobalBuffEffect_InventoryLossReduction();
            effect.Configure(row.ReductionPercent);
            definition.ConfigureNumericData(
                row.BuffId,
                row.DisplayName,
                definition.Icon,
                BuildAutomaticCondition(
                    row.ConditionId,
                    session.Technologies),
                new TechnologyGlobalBuffEffect[] { effect });
        }

        private static GameCondition BuildCondition(
            string conditionId,
            BuildingLevelDefinition existingLevel,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies)
        {
            var normalized = NormalizeStableId(conditionId);
            if (string.Equals(normalized, ConditionNone, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (string.Equals(normalized, ConditionKeep, StringComparison.OrdinalIgnoreCase))
            {
                return existingLevel?.UpgradeCondition;
            }

            var technologyId = normalized.Substring(TechnologyConditionPrefix.Length);
            return new GameCondition_TechnologyUnlocked
            {
                TechnologyDefinition = technologies[technologyId]
            };
        }

        private static GameCondition BuildAutomaticCondition(
            string conditionId,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies)
        {
            var normalized = NormalizeStableId(conditionId);
            if (string.Equals(normalized, ConditionNone, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var technologyId = normalized.Substring(TechnologyConditionPrefix.Length);
            return new GameCondition_TechnologyUnlocked
            {
                TechnologyDefinition = technologies[technologyId]
            };
        }

        private static void Validate(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, ItemGroupDefinition> itemGroups,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies,
            IReadOnlyDictionary<string, TechnologyGlobalBuffDefinition> globalBuffs,
            BuildingNumericImportReport report)
        {
            ValidateUnique(data.Families, row => row.FamilyId, "FamilyId 重复", report);
            foreach (var row in data.Families)
            {
                if (!families.ContainsKey(row.FamilyId))
                {
                    report.Error($"未知 FamilyId：{row.FamilyId}。请先用建筑编辑器创建家族和模块结构。", row.Sheet, row.Row);
                }
                if (!ParseCategory(row.Category, out _)) report.Error($"未知建筑分类：{row.Category}", row.Sheet, row.Row);
                if (row.SizeX < 1 || row.SizeY < 1) report.Error("占地 SizeX/SizeY 必须大于 0。", row.Sheet, row.Row);
                if (!row.IgnoreTerrain && SplitStableKeys(row.TerrainKeys).Count == 0) report.Error("未忽略地形时，地形Keys 不能为空。", row.Sheet, row.Row);
                if (row.ConstructionTurns < 1) report.Error("施工回合必须至少为 1。", row.Sheet, row.Row);
                if (row.MaxBuildCount < 0) report.Error("数量上限不能小于 0。", row.Sheet, row.Row);
                if (row.BuildingActionPower < 0) report.Error("行动力预算不能小于 0。", row.Sheet, row.Row);
                if (row.HideWhenBlueprintLocked && !row.BlueprintInitiallyLocked)
                    report.Warning("已勾选锁定时隐藏，但蓝图并非初始锁定。", row.Sheet, row.Row);
                ValidateAutomaticCondition(
                    row.BlueprintUnlockConditionId,
                    "蓝图解锁ConditionId",
                    row,
                    technologies,
                    report);
            }

            foreach (var family in families)
            {
                if (data.Families.All(row => row.FamilyId != family.Key))
                {
                    report.Error($"正式表缺少现有建筑家族：{family.Key}");
                }
            }

            ValidateCosts(data.PlacementCosts, data, families, items, false, report);
            ValidateCosts(data.ConstructionCosts, data, families, items, true, report);
            ValidateCosts(data.ConstructionRewards, data, families, items, true, report);
            ValidateCosts(data.UpgradeCosts, data, families, items, true, report);
            ValidateUnique(data.Levels, row => LevelKey(row.FamilyId, row.Level), "FamilyId + Level 重复", report);
            ValidateLevels(data, families, technologies, report);

            ValidateLevelTable(data.FixedPopulation, row => row.FamilyId, row => row.Level, families, data, typeof(BM_固定人口), "固定人口", report);
            ValidateLevelTable(data.InventoryCapacity, row => row.FamilyId, row => row.Level, families, data, typeof(BM_库存格容量), "库存容量", report);
            ValidateLevelTable(data.TechnologyPoints, row => row.FamilyId, row => row.Level, families, data, typeof(BM_科技点产出), "科技点", report);
            ValidateLevelTable(data.Residential, row => row.FamilyId, row => row.Level, families, data, typeof(BM_居民运营), "住宅", report);
            ValidateLevelTable(data.Workforce, row => row.FamilyId, row => row.Level, families, data, typeof(BM_岗位运营), "岗位", report);
            ValidateLevelTable(data.FishingRare, row => row.FamilyId, row => row.Level, families, data, typeof(BM_稀有产出), "捕鱼稀有产出", report);
            ValidateLevelTable(data.Maintenance, row => row.FamilyId, row => row.Level, families, data, typeof(BM_维护费), "维护费", report);
            ValidateLevelTable(
                data.OperationalExperience,
                row => row.FamilyId,
                row => row.Level,
                families,
                data,
                typeof(BM_运营经验),
                "运营经验",
                report);
            ValidateLevelTable(data.Warehouses, row => row.FamilyId, row => row.Level, families, data, typeof(BM_仓库运营), "仓库运营", report);
            ValidateProduction(data, families, items, report);
            ValidateModuleRows(data, families, items, report);
            ValidateTechnologyGlobalBuffs(data, families, items, technologies, globalBuffs, report);

            foreach (var row in data.FixedPopulation) if (row.Population < 0) report.Error("提供人口不能小于 0。", row.Sheet, row.Row);
            foreach (var row in data.InventoryCapacity)
            {
                if (row.Slots < 0)
                {
                    report.Error("提供库存格不能小于 0。", row.Sheet, row.Row);
                }
                if (!TryParseEnum(row.SlotType, out InventorySlotType _))
                {
                    report.Error(
                        $"未知槽位类型：{row.SlotType}。可选值：{string.Join("、", Enum.GetNames(typeof(InventorySlotType)))}",
                        row.Sheet,
                        row.Row);
                }
            }
            foreach (var row in data.TechnologyPoints) if (row.PointsPerTurn < 0) report.Error("科技点不能小于 0。", row.Sheet, row.Row);
            foreach (var row in data.Residential)
            {
                RequireItem(row.FoodItemId, row, items, report);
                RequireItem(row.TaxItemId, row, items, report);
                if (!itemGroups.ContainsKey(row.FoodGroupId))
                {
                    report.Error($"未知 FoodGroupId：{row.FoodGroupId}", row.Sheet, row.Row);
                }
                if (!TryParseEnum(
                        row.FoodSelectionPolicy,
                        out ItemRequirementSelectionPolicy _))
                {
                    report.Error(
                        $"未知饮食选择策略：{row.FoodSelectionPolicy}。可选值：{string.Join("、", Enum.GetNames(typeof(ItemRequirementSelectionPolicy)))}",
                        row.Sheet,
                        row.Row);
                }
                if (row.InitialPopulation < 1 || row.MaxPopulation < row.InitialPopulation) report.Error("住宅人口范围非法。", row.Sheet, row.Row);
                if (row.GrowthIntervalTurns < 1 || row.FailureDecayThreshold < 1 || row.TaxIntervalTurns < 1) report.Error("住宅回合/阈值必须至少为 1。", row.Sheet, row.Row);
                if (row.TargetDietVariety < 1
                    || row.MaxLifeQualityChangePerTurn < 0f
                    || row.HighQualityGrowthThreshold < 0f
                    || row.HighQualityGrowthThreshold > 100f)
                {
                    report.Error("住宅饮食和生活质量参数非法。", row.Sheet, row.Row);
                }
            }
            foreach (var row in data.Workforce)
            {
                RequireItem(row.GoldItemId, row, items, report);
                if (row.MaxWorkers < 1 || row.InitialWorkers < 0 || row.InitialWorkers > row.MaxWorkers) report.Error("岗位工人数范围非法。", row.Sheet, row.Row);
                if (row.BaseAttraction < 0f || row.RecruitCost < 0 || row.TargetStableWorkers < 0 || row.TargetStableWorkers > row.MaxWorkers) report.Error("岗位吸引力、费用或稳定目标非法。", row.Sheet, row.Row);
            }
            foreach (var row in data.FishingRare)
            {
                if (row.Enabled || !string.IsNullOrWhiteSpace(row.ItemId)) RequireItem(row.ItemId, row, items, report);
                if (row.MinimumWorkers < 1 || row.ChancePercent < 0f || row.ChancePercent > 100f || row.Amount < 0) report.Error("捕鱼稀有产出参数非法。", row.Sheet, row.Row);
                var workforce = data.Workforce.SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, row.FamilyId, row.Level));
                if (workforce != null && row.MinimumWorkers > workforce.MaxWorkers) report.Error("稀有产出最低工人数超过岗位上限。", row.Sheet, row.Row);
            }
        }

        private static void ValidateLevels(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies,
            BuildingNumericImportReport report)
        {
            foreach (var group in data.Levels.GroupBy(row => row.FamilyId, StringComparer.Ordinal))
            {
                if (!families.ContainsKey(group.Key))
                {
                    foreach (var row in group) report.Error($"未知 FamilyId：{row.FamilyId}", row.Sheet, row.Row);
                    continue;
                }

                var levels = group.OrderBy(row => row.Level).ToArray();
                for (var index = 0; index < levels.Length; index++)
                {
                    var expected = index + 1;
                    if (levels[index].Level != expected) report.Error($"运营等级必须从 1 连续排列，期望 LV{expected}。", levels[index].Sheet, levels[index].Row);
                }

                if (levels.Length == 0 || levels[0].Level != 1 || !levels[0].Configured)
                    report.Error($"{group.Key} 必须包含开放的 LV1。");
            }

            foreach (var family in data.Families)
            {
                if (data.Levels.All(level => level.FamilyId != family.FamilyId)) report.Error($"{family.FamilyId} 缺少运营等级。", family.Sheet, family.Row);
            }

            foreach (var row in data.Levels)
            {
                if (row.Level < 1) report.Error("Level 必须至少为 1。", row.Sheet, row.Row);
                var condition = NormalizeStableId(row.ConditionId);
                if (condition.Equals(ConditionNone, StringComparison.OrdinalIgnoreCase)) continue;
                if (condition.Equals(ConditionKeep, StringComparison.OrdinalIgnoreCase))
                {
                    if (!families.TryGetValue(row.FamilyId, out var family) || !family.TryGetLevel(row.Level, out _))
                        report.Error("新等级不能使用 keep 条件，必须填写 none 或 technology.<科技ID>。", row.Sheet, row.Row);
                    continue;
                }
                if (!condition.StartsWith(TechnologyConditionPrefix, StringComparison.Ordinal))
                {
                    report.Error("ConditionId 只支持 none、keep 或 technology.<科技ID>。", row.Sheet, row.Row);
                    continue;
                }

                var technologyId = condition.Substring(TechnologyConditionPrefix.Length);
                if (!technologies.ContainsKey(technologyId)) report.Error($"找不到科技：{technologyId}", row.Sheet, row.Row);
            }
        }

        private static void ValidateCosts(
            IReadOnlyList<BuildingCostNumericRow> rows,
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, ItemDefinition> items,
            bool indexed,
            BuildingNumericImportReport report)
        {
            ValidateUnique(rows,
                row => indexed ? $"{row.FamilyId}\u001f{row.LevelOrTurn}\u001f{row.ItemId}" : $"{row.FamilyId}\u001f{row.ItemId}",
                "资源行键重复",
                report);
            foreach (var row in rows)
            {
                if (!families.ContainsKey(row.FamilyId)) report.Error($"未知 FamilyId：{row.FamilyId}", row.Sheet, row.Row);
                RequireItem(row.ItemId, row, items, report);
                if (row.Amount <= 0) report.Error("数量必须大于 0。", row.Sheet, row.Row);
                if (!indexed) continue;
                if (row.Sheet == "施工消耗" || row.Sheet == "施工产出")
                {
                    var family = data.Families.SingleOrDefault(value => value.FamilyId == row.FamilyId);
                    if (family != null && (row.LevelOrTurn < 1 || row.LevelOrTurn > family.ConstructionTurns))
                        report.Error("施工回合序号超出建筑家族表定义。", row.Sheet, row.Row);
                }
                else if (row.Sheet == "升级消耗")
                {
                    if (row.LevelOrTurn <= 1) report.Error("升级消耗只能写在目标 LV2 或更高等级。", row.Sheet, row.Row);
                    if (data.Levels.All(level => level.FamilyId != row.FamilyId || level.Level != row.LevelOrTurn))
                        report.Error("升级消耗引用了不存在的目标等级。", row.Sheet, row.Row);
                }
            }
        }

        private static void ValidateProduction(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, ItemDefinition> items,
            BuildingNumericImportReport report)
        {
            ValidateUnique(data.Production,
                row => $"{row.FamilyId}\u001f{row.Level}\u001f{row.OutputItemId}\u001f{row.MinimumWorkers}",
                "生产档位重复",
                report);
            ValidateLevelTable(data.Production, row => row.FamilyId, row => row.Level, families, data, typeof(BM_资源产出), "生产", report, false);
            foreach (var group in data.Production.GroupBy(row => LevelKey(row.FamilyId, row.Level), StringComparer.Ordinal))
            {
                if (group.Select(row => row.IntervalTurns).Distinct().Count() > 1)
                    foreach (var row in group) report.Error("同一 FamilyId + Level 的周期回合必须一致。", row.Sheet, row.Row);
            }
            foreach (var row in data.Production)
            {
                RequireItem(row.OutputItemId, row, items, report);
                if (row.MinimumWorkers < 1 || row.AmountPerCycle <= 0 || row.IntervalTurns < 1)
                    report.Error("生产阈值、产量和周期必须为正数。", row.Sheet, row.Row);
                var workforce = data.Workforce.SingleOrDefault(value => IsLevel(value.FamilyId, value.Level, row.FamilyId, row.Level));
                if (workforce == null) report.Error("生产等级缺少对应岗位配置。", row.Sheet, row.Row);
                else if (row.MinimumWorkers > workforce.MaxWorkers) report.Error("生产工人阈值超过岗位上限。", row.Sheet, row.Row);
            }
        }

        private static void ValidateModuleRows(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, ItemDefinition> items,
            BuildingNumericImportReport report)
        {
            ValidateUnique(data.Markets, row => row.FamilyId, "市场模块 FamilyId 重复", report);
            ValidateUnique(data.Trees, row => row.FamilyId, "树木模块 FamilyId 重复", report);
            ValidateUnique(
                data.FiniteHarvests,
                row => row.FamilyId,
                "有限采集模块 FamilyId 重复",
                report);
            ValidateUnique(
                data.SpatialEffects,
                row => $"{row.FamilyId}\u001f{row.EffectId}",
                "范围效果 FamilyId + EffectId 重复",
                report);
            foreach (var row in data.Markets)
            {
                ValidateModulePresence(row.FamilyId, row, families, typeof(BM_市场资源结算), report);
                RequireItem(row.GoldItemId, row, items, report);
                if (row.IncomeRatio < 0f) report.Error("价值结算比例不能小于 0。", row.Sheet, row.Row);
            }
            foreach (var row in data.Trees)
            {
                ValidateModulePresence(row.FamilyId, row, families, typeof(BM_树木采集), report);
                RequireItem(row.WoodItemId, row, items, report);
                RequireItem(row.SaplingItemId, row, items, report);
                if (row.MinHealth < 1 || row.MaxHealth < row.MinHealth || row.DamagePerDoubleClick < 1 || row.WoodReward < 0 || row.SaplingReward < 0)
                    report.Error("树木生命、伤害或奖励参数非法。", row.Sheet, row.Row);
            }
            foreach (var row in data.FiniteHarvests)
            {
                ValidateModulePresence(
                    row.FamilyId,
                    row,
                    families,
                    typeof(BM_有限次数采集),
                    report);
                RequireItem(row.RewardItemId, row, items, report);
                if (row.RewardPerDoubleClick < 1 || row.MaxSuccessfulHarvests < 1)
                {
                    report.Error("每次双击产出和最大成功采集次数必须大于 0。", row.Sheet, row.Row);
                }
            }
            foreach (var row in data.Maintenance)
            {
                RequireItem(row.ItemId, row, items, report);
                if (row.AmountPerTurn < 0) report.Error("维护费不能小于 0。", row.Sheet, row.Row);
            }
            foreach (var row in data.OperationalExperience)
            {
                if (row.RequiredWorkers < 0
                    || row.ExperiencePerTurn < 0
                    || row.NextLevelExperience < 0)
                {
                    report.Error("运营经验数值不能小于 0。", row.Sheet, row.Row);
                }

                var workforce = data.Workforce.SingleOrDefault(value =>
                    IsLevel(value.FamilyId, value.Level, row.FamilyId, row.Level));
                if (workforce != null && row.RequiredWorkers > workforce.MaxWorkers)
                {
                    report.Error("运营经验工人阈值不能超过该等级岗位上限。", row.Sheet, row.Row);
                }
            }
            foreach (var row in data.Warehouses)
            {
                RequireItem(row.MaintenanceItemId, row, items, report);
                if (!TryParseEnum(row.BaseSlotType, out InventorySlotType _))
                {
                    report.Error(
                        $"未知基础槽位类型：{row.BaseSlotType}",
                        row.Sheet,
                        row.Row);
                }
                if (!TryParseEnum(row.BonusSlotType, out InventorySlotType _))
                {
                    report.Error(
                        $"未知奖励槽位类型：{row.BonusSlotType}",
                        row.Sheet,
                        row.Row);
                }
                if (row.RequiredWorkers < 0
                    || row.ProvidedSlots < 0
                    || row.MaintenancePerTurn < 0
                    || row.ExperienceWorkers < 0
                    || row.ExperiencePerTurn < 0
                    || row.NextLevelExperience < 0
                    || row.BonusWorkerThreshold < 0
                    || row.BonusSlots < 0
                    || row.MaintenanceFailureAttractionPenalty < 0f)
                {
                    report.Error("仓库运营数值不能小于 0。", row.Sheet, row.Row);
                }

                var workforce = data.Workforce.SingleOrDefault(value =>
                    IsLevel(value.FamilyId, value.Level, row.FamilyId, row.Level));
                if (workforce != null
                    && (row.RequiredWorkers > workforce.MaxWorkers
                        || row.ExperienceWorkers > workforce.MaxWorkers
                        || row.BonusWorkerThreshold > workforce.MaxWorkers))
                {
                    report.Error("仓库工人阈值不能超过该等级岗位上限。", row.Sheet, row.Row);
                }
            }
            foreach (var row in data.SpatialEffects)
            {
                ValidateModulePresence(row.FamilyId, row, families, typeof(BM_空间效果源), report);
                if (!families.TryGetValue(row.FamilyId, out var family))
                {
                    continue;
                }

                var source = family.ModuleSet?.BuildingModules
                    .OfType<BM_空间效果源>()
                    .FirstOrDefault(module => module.IsEnabled);
                var definition = source?.Effects.FirstOrDefault(effect =>
                    effect != null && effect.EffectId == row.EffectId);
                if (definition == null)
                {
                    report.Error(
                        $"家族范围效果模块中找不到 EffectId：{row.EffectId}。必须先在 Unity 中创建效果资产并挂入 ModuleSet。",
                        row.Sheet,
                        row.Row);
                }

                var kindValid = TryParseSpatialEffectKind(row.Kind, out var kind);
                var targetValid = TryParseSpatialTargetFilter(row.TargetFilter, out var targetFilter);
                var stackingValid = TryParseSpatialStackingRule(row.StackingRule, out _);
                if (!kindValid)
                    report.Error("Kind 只支持 beauty、medical、security 或 production_percent。", row.Sheet, row.Row);
                if (!targetValid)
                    report.Error("TargetFilter 只支持 cell、any_building 或 farmland。", row.Sheet, row.Row);
                if (!stackingValid)
                    report.Error("StackingRule 只支持 additive、no_stack 或 highest_value。", row.Sheet, row.Row);
                if (row.OperationalLevel < 0 || row.MinimumWorkers < 0)
                    report.Error("生效Level 和最低工人不能小于 0。", row.Sheet, row.Row);
                if (row.Range < 0 || row.Value <= 0)
                    report.Error("范围效果半径不能小于 0，效果数值必须大于 0。", row.Sheet, row.Row);
                if (!row.IncludeSourceFootprint && row.Range < 1)
                    report.Error("不影响自身占地时，曼哈顿半径必须至少为 1。", row.Sheet, row.Row);
                if (kindValid && targetValid
                    && (kind == BuildingSpatialEffectKind.Beauty
                        || kind == BuildingSpatialEffectKind.Medical
                        || kind == BuildingSpatialEffectKind.Security)
                    && targetFilter != BuildingSpatialTargetFilter.Cell)
                    report.Error("美化、医疗和治安效果的 TargetFilter 必须为 cell。", row.Sheet, row.Row);
                if (kindValid && targetValid
                    && kind == BuildingSpatialEffectKind.ProductionPercent
                    && targetFilter == BuildingSpatialTargetFilter.Cell)
                    report.Error("生产百分比效果不能使用 cell 目标。", row.Sheet, row.Row);

                if (row.OperationalLevel > 0)
                {
                    var level = data.Levels.SingleOrDefault(value =>
                        IsLevel(value.FamilyId, value.Level, row.FamilyId, row.OperationalLevel));
                    if (level == null || !level.Configured)
                    {
                        report.Error("范围效果的生效Level 必须存在且已经开放。", row.Sheet, row.Row);
                    }

                    var workforce = data.Workforce.SingleOrDefault(value =>
                        IsLevel(value.FamilyId, value.Level, row.FamilyId, row.OperationalLevel));
                    if (row.MinimumWorkers > 0
                        && (workforce == null || row.MinimumWorkers > workforce.MaxWorkers))
                    {
                        report.Error("范围效果最低工人超过该等级岗位上限或缺少岗位配置。", row.Sheet, row.Row);
                    }
                }
            }

            foreach (var family in families.Values)
            {
                if (HasModule(family, typeof(BM_市场资源结算)) && data.Markets.Count(row => row.FamilyId == family.FamilyId) != 1)
                    report.Error($"{family.FamilyId} 的市场模块必须有且只有一行配置。");
                if (HasModule(family, typeof(BM_树木采集)) && data.Trees.Count(row => row.FamilyId == family.FamilyId) != 1)
                    report.Error($"{family.FamilyId} 的树木模块必须有且只有一行配置。");
                if (HasModule(family, typeof(BM_有限次数采集))
                    && data.FiniteHarvests.Count(row => row.FamilyId == family.FamilyId) != 1)
                {
                    report.Error($"{family.FamilyId} 的有限采集模块必须有且只有一行配置。");
                }
                var spatialEffectSource = family.ModuleSet?.BuildingModules
                    .OfType<BM_空间效果源>()
                    .FirstOrDefault(module => module.IsEnabled);
                if (spatialEffectSource != null)
                {
                    foreach (var effect in spatialEffectSource.Effects)
                    {
                        if (effect == null || string.IsNullOrWhiteSpace(effect.EffectId))
                        {
                            report.Error($"{family.FamilyId} 的范围效果模块包含空引用或无 EffectId 的效果资产。");
                            continue;
                        }

                        if (data.SpatialEffects.Count(row =>
                                row.FamilyId == family.FamilyId && row.EffectId == effect.EffectId) != 1)
                        {
                            report.Error(
                                $"{family.FamilyId} 的范围效果 {effect.EffectId} 必须有且只有一行配置。");
                        }
                    }
                }
            }

            ValidateUnique(data.Crops, row => $"{row.FamilyId}\u001f{row.CropId}", "作物 ID 重复", report);
            ValidateUnique(data.CropPlantCosts, row => $"{row.FamilyId}\u001f{row.CropId}\u001f{row.ItemId}", "作物种植消耗重复", report);
            ValidateUnique(data.CropHarvestRewards, row => $"{row.FamilyId}\u001f{row.CropId}\u001f{row.ItemId}", "作物收获产出重复", report);
            ValidateUnique(data.CropAutoHarvestCosts, row => $"{row.FamilyId}\u001f{row.ItemId}", "自动收获消耗重复", report);
            foreach (var row in data.Crops)
            {
                ValidateModulePresence(row.FamilyId, row, families, typeof(BuildingCropGrowthModule), report);
                if (row.GrowTurns < 1) report.Error("成熟回合必须至少为 1。", row.Sheet, row.Row);
            }
            foreach (var row in data.CropPlantCosts)
            {
                RequireItem(row.ItemId, row, items, report);
                if (row.Amount <= 0) report.Error("种植消耗必须大于 0。", row.Sheet, row.Row);
                if (data.Crops.All(crop => crop.FamilyId != row.FamilyId || crop.CropId != row.CropId)) report.Error("种植消耗引用了不存在的作物。", row.Sheet, row.Row);
            }
            foreach (var row in data.CropHarvestRewards)
            {
                RequireItem(row.ItemId, row, items, report);
                if (row.MinAmount < 0 || row.MaxAmount < row.MinAmount || row.MaxAmount <= 0) report.Error("作物收获数量范围非法。", row.Sheet, row.Row);
                if (data.Crops.All(crop => crop.FamilyId != row.FamilyId || crop.CropId != row.CropId)) report.Error("收获产出引用了不存在的作物。", row.Sheet, row.Row);
            }
            foreach (var row in data.CropAutoHarvestCosts)
            {
                ValidateModulePresence(row.FamilyId, row, families, typeof(BuildingCropGrowthModule), report);
                RequireItem(row.ItemId, row, items, report);
                if (row.Amount <= 0) report.Error("自动收获消耗必须大于 0。", row.Sheet, row.Row);
            }
        }

        private static void ValidateLevelTable<TRow>(
            IReadOnlyList<TRow> rows,
            Func<TRow, string> familyId,
            Func<TRow, int> level,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            BuildingNumericWorkbookData data,
            Type moduleType,
            string label,
            BuildingNumericImportReport report,
            bool requireExactlyOne = true)
            where TRow : BuildingNumericSourceRow
        {
            if (requireExactlyOne)
                ValidateUnique(rows, row => LevelKey(familyId(row), level(row)), $"{label}配置重复", report);
            foreach (var row in rows)
            {
                var id = familyId(row);
                ValidateModulePresence(id, row, families, moduleType, report);
                if (data.Levels.All(value => value.FamilyId != id || value.Level != level(row)))
                    report.Error($"{label}配置引用了不存在的运营等级。", row.Sheet, row.Row);
            }

            foreach (var family in families.Values)
            {
                if (!HasModule(family, moduleType)) continue;
                foreach (var definedLevel in data.Levels.Where(value => value.FamilyId == family.FamilyId))
                {
                    var count = rows.Count(row => familyId(row) == family.FamilyId && level(row) == definedLevel.Level);
                    if ((requireExactlyOne && count != 1) || (!requireExactlyOne && count < 1))
                        report.Error($"{family.FamilyId} LV{definedLevel.Level} 的{label}配置数量不正确。");
                }
            }
        }

        private static void ValidateModulePresence(
            string familyId,
            BuildingNumericSourceRow row,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            Type moduleType,
            BuildingNumericImportReport report)
        {
            if (!families.TryGetValue(familyId, out var family))
            {
                report.Error($"未知 FamilyId：{familyId}", row.Sheet, row.Row);
                return;
            }
            var module = family.ModuleSet?.BuildingModules.FirstOrDefault(value => value != null && moduleType.IsInstanceOfType(value));
            if (module == null) report.Error($"家族缺少模块 {moduleType.Name}。模块结构必须先在建筑编辑器中创建。", row.Sheet, row.Row);
            else if (!module.IsEnabled) report.Error($"模块 {moduleType.Name} 当前未启用。", row.Sheet, row.Row);
        }

        private static bool HasModule(BuildingFamilyDefinition family, Type type) =>
            family?.ModuleSet?.BuildingModules.Any(module => module != null && module.IsEnabled && type.IsInstanceOfType(module)) == true;

        private static void RequireItem(
            string itemId,
            BuildingNumericSourceRow row,
            IReadOnlyDictionary<string, ItemDefinition> items,
            BuildingNumericImportReport report)
        {
            if (string.IsNullOrWhiteSpace(itemId) || !items.ContainsKey(itemId))
                report.Error($"找不到 ItemDefinition：{itemId}", row.Sheet, row.Row);
        }

        private static BuildingNumericImportChangePlan BuildChangePlan(
            BuildingNumericImportSession session)
        {
            var plan = new BuildingNumericImportChangePlan();
            foreach (var row in session.Data.Families.OrderBy(value => value.FamilyId, StringComparer.Ordinal))
            {
                var family = session.Families[row.FamilyId];
                var changedFamilyScopes = new List<string>();
                if (!DoesDefinitionMatch(session, row, family.Definition))
                {
                    changedFamilyScopes.Add("公共数值");
                }

                if (!DoesConstructionMatch(session, row, family.Construction))
                {
                    changedFamilyScopes.Add("施工阶段");
                }

                if (!DoLevelsMatch(session, row.FamilyId, family.Levels))
                {
                    changedFamilyScopes.Add("运营等级");
                }

                if (changedFamilyScopes.Count > 0)
                {
                    plan.AddFamilyAsset(
                        row.FamilyId,
                        family,
                        $"更新{string.Join("、", changedFamilyScopes)}");
                }

                var targetLevels = session.Data.Levels
                    .Where(level => level.FamilyId == row.FamilyId)
                    .Select(level => level.Level)
                    .OrderBy(level => level)
                    .ToArray();
                if (BuildingPresentationMappingSynchronizer.NeedsSynchronization(
                        family.Presentation,
                        targetLevels,
                        out var mappingError))
                {
                    plan.AddPresentationAsset(
                        row.FamilyId,
                        family.Presentation,
                        "同步由运营等级与视觉样式生成的固定 ViewMapping 矩阵");
                }
                else if (!string.IsNullOrWhiteSpace(mappingError))
                {
                    throw new InvalidOperationException(
                        $"{row.FamilyId} 无法生成固定 ViewMapping 矩阵：{mappingError}");
                }

                if (family.ModuleSet != null && HasExcelManagedModuleDefaults(family))
                {
                    var desiredModuleSet = family.ModuleSet.CreateRuntimeClone();
                    try
                    {
                        ApplyManagedModuleDefaults(session, row.FamilyId, desiredModuleSet);
                        desiredModuleSet.Normalize();
                        if (!AreSerializedValuesEqual(
                                family.ModuleSet.BuildingModules,
                                desiredModuleSet.BuildingModules))
                        {
                            plan.AddModuleSetAsset(
                                row.FamilyId,
                                family.ModuleSet,
                                "更新 Excel 管理的模块默认值");
                        }
                    }
                    finally
                    {
                        UnityEngine.Object.DestroyImmediate(desiredModuleSet);
                    }
                }

                if (family.RuntimePrefab != null)
                {
                    var runtimeFields = GetChangedRuntimePrefabFields(row, family.RuntimePrefab);
                    if (runtimeFields.Count > 0)
                    {
                        plan.AddRuntimePrefabAsset(
                            row.FamilyId,
                            family.RuntimePrefab,
                            $"更新 Prefab 数值字段：{string.Join("、", runtimeFields)}");
                    }
                }

                var spatialEffectSource = family.ModuleSet?.BuildingModules
                    .OfType<BM_空间效果源>()
                    .FirstOrDefault();
                if (spatialEffectSource == null)
                {
                    continue;
                }

                foreach (var effectRow in session.Data.SpatialEffects
                             .Where(value => value.FamilyId == row.FamilyId)
                             .OrderBy(value => value.EffectId, StringComparer.Ordinal))
                {
                    var definition = spatialEffectSource.Effects.Single(value =>
                        value != null && value.EffectId == effectRow.EffectId);
                    if (!DoesSpatialEffectMatch(effectRow, definition))
                    {
                        plan.AddSpatialEffectAsset(row.FamilyId, effectRow.EffectId, definition);
                    }
                }
            }

            foreach (var row in session.Data.TechnologyGlobalBuffs
                         .OrderBy(value => value.BuffId, StringComparer.Ordinal))
            {
                var definition = session.GlobalBuffs[row.BuffId];
                if (!DoesTechnologyGlobalBuffMatch(row, definition, session))
                {
                    plan.AddGlobalBuffAsset(row.BuffId, definition);
                }
            }

            foreach (var row in session.Data.TechnologyInventoryLossBuffs
                         .OrderBy(value => value.BuffId, StringComparer.Ordinal))
            {
                var definition = session.GlobalBuffs[row.BuffId];
                if (!DoesTechnologyInventoryLossBuffMatch(
                        row,
                        definition,
                        session))
                {
                    plan.AddGlobalBuffAsset(row.BuffId, definition);
                }
            }

            return plan;
        }

        private static bool DoesTechnologyGlobalBuffMatch(
            TechnologyGlobalBuffNumericRow row,
            TechnologyGlobalBuffDefinition definition,
            BuildingNumericImportSession session)
        {
            if (definition == null
                || !string.Equals(definition.BuffId, row.BuffId, StringComparison.Ordinal)
                || !string.Equals(definition.DisplayName, row.DisplayName, StringComparison.Ordinal)
                || definition.ActivationCondition is not GameCondition_TechnologyUnlocked condition)
            {
                return false;
            }

            var technologyId = NormalizeStableId(row.ConditionId)
                .Substring(TechnologyConditionPrefix.Length);
            if (condition.TechnologyDefinition == null
                || !string.Equals(
                    condition.TechnologyDefinition.TechnologyId,
                    technologyId,
                    StringComparison.Ordinal)
                || definition.Effects.Count != 1
                || definition.Effects[0]
                    is not TechnologyGlobalBuffEffect_BuildingProductionFlat effect)
            {
                return false;
            }

            return ReferenceEquals(effect.TargetFamily, session.Families[row.FamilyId])
                   && ReferenceEquals(effect.ItemDefinition, session.Items[row.OutputItemId])
                   && effect.FlatBonus == row.FlatBonus;
        }

        private static bool DoesTechnologyInventoryLossBuffMatch(
            TechnologyInventoryLossBuffNumericRow row,
            TechnologyGlobalBuffDefinition definition,
            BuildingNumericImportSession session)
        {
            if (definition == null
                || !string.Equals(
                    definition.BuffId,
                    row.BuffId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    definition.DisplayName,
                    row.DisplayName,
                    StringComparison.Ordinal)
                || definition.ActivationCondition
                    is not GameCondition_TechnologyUnlocked condition)
            {
                return false;
            }

            var technologyId = NormalizeStableId(row.ConditionId)
                .Substring(TechnologyConditionPrefix.Length);
            if (condition.TechnologyDefinition == null
                || !string.Equals(
                    condition.TechnologyDefinition.TechnologyId,
                    technologyId,
                    StringComparison.Ordinal)
                || definition.Effects.Count != 1
                || definition.Effects[0]
                    is not TechnologyGlobalBuffEffect_InventoryLossReduction effect)
            {
                return false;
            }

            return Mathf.Approximately(
                effect.ReductionPercent,
                row.ReductionPercent);
        }

        private static void ValidateAutomaticCondition(
            string conditionId,
            string label,
            BuildingNumericSourceRow row,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies,
            BuildingNumericImportReport report)
        {
            var condition = NormalizeStableId(conditionId);
            if (condition.Equals(ConditionNone, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!condition.StartsWith(TechnologyConditionPrefix, StringComparison.OrdinalIgnoreCase))
            {
                report.Error($"{label} 只支持 none 或 technology.<科技ID>。", row.Sheet, row.Row);
                return;
            }

            var technologyId = condition.Substring(TechnologyConditionPrefix.Length);
            if (!technologies.ContainsKey(technologyId))
            {
                report.Error($"找不到科技：{technologyId}", row.Sheet, row.Row);
            }
        }

        private static void ValidateTechnologyGlobalBuffs(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies,
            IReadOnlyDictionary<string, TechnologyGlobalBuffDefinition> globalBuffs,
            BuildingNumericImportReport report)
        {
            ValidateUnique(data.TechnologyGlobalBuffs, row => row.BuffId, "BuffId 重复", report);
            ValidateUnique(
                data.TechnologyInventoryLossBuffs,
                row => row.BuffId,
                "BuffId 重复",
                report);
            var allBuffIds = new HashSet<string>(
                data.TechnologyGlobalBuffs.Select(row => row.BuffId),
                StringComparer.Ordinal);
            foreach (var row in data.TechnologyInventoryLossBuffs)
            {
                if (!allBuffIds.Add(row.BuffId))
                {
                    report.Error(
                        $"BuffId 在多个科技全局 Buff 表中重复：{row.BuffId}",
                        row.Sheet,
                        row.Row);
                }
            }

            foreach (var row in data.TechnologyGlobalBuffs)
            {
                if (!globalBuffs.ContainsKey(row.BuffId))
                    report.Error($"找不到全局 Buff 资产：{row.BuffId}", row.Sheet, row.Row);
                if (!families.ContainsKey(row.FamilyId))
                    report.Error($"未知 FamilyId：{row.FamilyId}", row.Sheet, row.Row);
                RequireItem(row.OutputItemId, row, items, report);
                ValidateAutomaticCondition(row.ConditionId, "ConditionId", row, technologies, report);
                if (row.FlatBonus <= 0)
                    report.Error("固定加成/次必须大于 0。", row.Sheet, row.Row);
            }

            foreach (var row in data.TechnologyInventoryLossBuffs)
            {
                if (!globalBuffs.ContainsKey(row.BuffId))
                {
                    report.Error(
                        $"找不到全局 Buff 资产：{row.BuffId}",
                        row.Sheet,
                        row.Row);
                }

                ValidateAutomaticCondition(
                    row.ConditionId,
                    "ConditionId",
                    row,
                    technologies,
                    report);
                if (row.ReductionPercent <= 0f
                    || row.ReductionPercent > 100f)
                {
                    report.Error(
                        "库存损耗降低%必须大于 0 且不超过 100。",
                        row.Sheet,
                        row.Row);
                }
            }

            foreach (var pair in globalBuffs)
            {
                if (!allBuffIds.Contains(pair.Key))
                {
                    report.Error($"正式表缺少现有全局 Buff：{pair.Key}");
                }
            }
        }

        private static bool DoesDefinitionMatch(
            BuildingNumericImportSession session,
            BuildingFamilyNumericRow row,
            BuildingDefinition current)
        {
            if (current == null)
            {
                return false;
            }

            ParseCategory(row.Category, out var category);
            var desired = new BuildingDefinition();
            desired.ConfigureIdentity(current.FamilyId, row.DisplayName, row.BuildLimitGroupId);
            desired.ConfigureNumericData(
                row.DisplayName,
                category,
                new Vector2Int(row.SizeX, row.SizeY),
                row.IgnoreTerrain,
                SplitStableKeys(row.TerrainKeys),
                SplitStableKeys(row.AnyFootprintTerrainKeys),
                row.MovementResistance,
                session.Data.PlacementCosts
                    .Where(cost => cost.FamilyId == row.FamilyId)
                    .Select(cost => BuildCost(cost, session.Items))
                    .ToArray(),
                BuildAutomaticCondition(row.BlueprintUnlockConditionId, session.Technologies),
                row.BlueprintInitiallyLocked,
                row.HideWhenBlueprintLocked,
                row.BuildMenuSortOrder,
                row.MaxBuildCount,
                row.BuildLimitGroupId,
                row.IsDevelopmentCompleted);

            return string.Equals(current.DisplayName, desired.DisplayName, StringComparison.Ordinal)
                   && current.Category == desired.Category
                   && current.Size == desired.Size
                   && current.MovementResistance == desired.MovementResistance
                   && AreStringsEqual(current.RequiredTerrainKeys, desired.RequiredTerrainKeys)
                   && AreStringsEqual(
                       current.RequiredAnyFootprintTerrainKeys,
                       desired.RequiredAnyFootprintTerrainKeys)
                   && AreCostsEqual(current.PlacementCosts, desired.PlacementCosts)
                   && AreSerializedValuesEqual(
                       current.AutomaticBlueprintUnlockCondition,
                       desired.AutomaticBlueprintUnlockCondition)
                   && current.BlueprintInitiallyLocked == desired.BlueprintInitiallyLocked
                   && current.HideWhenBlueprintLocked == desired.HideWhenBlueprintLocked
                   && current.BuildMenuSortOrder == desired.BuildMenuSortOrder
                   && current.MaxBuildCount == desired.MaxBuildCount
                   && string.Equals(current.BuildLimitGroupId, desired.BuildLimitGroupId, StringComparison.Ordinal)
                   && current.IsDevelopmentCompleted == desired.IsDevelopmentCompleted;
        }

        private static bool DoesConstructionMatch(
            BuildingNumericImportSession session,
            BuildingFamilyNumericRow row,
            BuildingConstructionDefinition current)
        {
            if (current == null || current.RequiredTurns != row.ConstructionTurns)
            {
                return false;
            }

            for (var turn = 1; turn <= row.ConstructionTurns; turn++)
            {
                var desiredCosts = session.Data.ConstructionCosts
                    .Where(cost => cost.FamilyId == row.FamilyId && cost.LevelOrTurn == turn)
                    .Select(cost => BuildCost(cost, session.Items))
                    .ToArray();
                if (!AreCostsEqual(current.GetCosts(turn - 1), desiredCosts))
                {
                    return false;
                }

                var desiredRewards = session.Data.ConstructionRewards
                    .Where(reward => reward.FamilyId == row.FamilyId && reward.LevelOrTurn == turn)
                    .Select(reward => BuildCost(reward, session.Items))
                    .ToArray();
                if (!AreCostsEqual(current.GetRewards(turn - 1), desiredRewards))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DoLevelsMatch(
            BuildingNumericImportSession session,
            string familyId,
            IReadOnlyList<BuildingLevelDefinition> currentLevels)
        {
            var rows = session.Data.Levels
                .Where(value => value.FamilyId == familyId)
                .OrderBy(value => value.Level)
                .ToArray();
            if (currentLevels == null || currentLevels.Count != rows.Length)
            {
                return false;
            }

            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var current = currentLevels[index];
                if (current == null
                    || current.Level != row.Level
                    || current.IsConfigured != (row.Level == 1 || row.Configured))
                {
                    return false;
                }

                var desiredUpgradeCosts = session.Data.UpgradeCosts
                    .Where(cost => cost.FamilyId == row.FamilyId && cost.LevelOrTurn == row.Level)
                    .Select(cost => BuildCost(cost, session.Items))
                    .ToArray();
                if (!AreCostsEqual(current.UpgradeCosts, desiredUpgradeCosts)
                    || !DoesConditionMatch(
                        row.ConditionId,
                        current.UpgradeCondition,
                        session.Technologies))
                {
                    return false;
                }

                var currentConfigurations = current.Configurations
                    .Where(configuration => configuration != null
                                            && ImportedConfigurationIds.Contains(configuration.ConfigurationId))
                    .ToArray();
                var desiredConfigurations = BuildImportedLevelConfigurations(session, row, current);
                if (currentConfigurations.Length != desiredConfigurations.Count)
                {
                    return false;
                }

                for (var configurationIndex = 0;
                     configurationIndex < currentConfigurations.Length;
                     configurationIndex++)
                {
                    if (!AreSerializedValuesEqual(
                            currentConfigurations[configurationIndex],
                            desiredConfigurations[configurationIndex]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool DoesConditionMatch(
            string conditionId,
            GameCondition current,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies)
        {
            var normalized = NormalizeStableId(conditionId);
            if (string.Equals(normalized, ConditionKeep, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(normalized, ConditionNone, StringComparison.OrdinalIgnoreCase))
            {
                return current == null;
            }

            var technologyId = normalized.Substring(TechnologyConditionPrefix.Length);
            return current is GameCondition_TechnologyUnlocked technologyCondition
                   && technologyCondition.TechnologyDefinition == technologies[technologyId];
        }

        private static bool HasExcelManagedModuleDefaults(BuildingFamilyDefinition family)
        {
            return HasModule(family, typeof(BM_市场资源结算))
                   || HasModule(family, typeof(BM_树木采集))
                   || HasModule(family, typeof(BM_有限次数采集))
                   || HasModule(family, typeof(BuildingCropGrowthModule));
        }

        private static IReadOnlyList<string> GetChangedRuntimePrefabFields(
            BuildingFamilyNumericRow row,
            BuildingBase runtimePrefab)
        {
            var fields = new List<string>(3);
            if (runtimePrefab.IsResourceProviderPoint != row.IsResourceProviderPoint)
            {
                fields.Add("资源点");
            }

            if (runtimePrefab.ResourceProviderPriority != row.ResourceProviderPriority)
            {
                fields.Add("优先级");
            }

            if (runtimePrefab.BuildingActionPower != Mathf.Max(0, row.BuildingActionPower))
            {
                fields.Add("行动力");
            }

            return fields;
        }

        private static bool DoesSpatialEffectMatch(
            SpatialEffectNumericRow row,
            BuildingSpatialEffectDefinition current)
        {
            TryParseSpatialEffectKind(row.Kind, out var kind);
            TryParseSpatialTargetFilter(row.TargetFilter, out var targetFilter);
            TryParseSpatialStackingRule(row.StackingRule, out var stackingRule);
            return current != null
                   && string.Equals(current.EffectId, row.EffectId, StringComparison.Ordinal)
                   && string.Equals(current.DisplayName, row.DisplayName, StringComparison.Ordinal)
                   && current.Kind == kind
                   && current.TargetFilter == targetFilter
                   && current.OperationalLevel == Mathf.Max(0, row.OperationalLevel)
                   && current.MinimumWorkers == Mathf.Max(0, row.MinimumWorkers)
                   && current.Range == Mathf.Max(0, row.Range)
                   && current.Value == Mathf.Max(0, row.Value)
                   && current.StackingRule == stackingRule
                   && current.IncludeSourceFootprint == row.IncludeSourceFootprint;
        }

        private static bool AreCostsEqual(
            IReadOnlyList<BuildingCost> current,
            IReadOnlyList<BuildingCost> desired)
        {
            if (ReferenceEquals(current, desired)) return true;
            if (current == null || desired == null || current.Count != desired.Count) return false;
            for (var i = 0; i < current.Count; i++)
            {
                if (current[i].ItemDefinition != desired[i].ItemDefinition
                    || current[i].Amount != desired[i].Amount)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreStringsEqual(
            IReadOnlyList<string> current,
            IReadOnlyList<string> desired)
        {
            if (ReferenceEquals(current, desired)) return true;
            if (current == null || desired == null || current.Count != desired.Count) return false;
            for (var i = 0; i < current.Count; i++)
            {
                if (!string.Equals(current[i], desired[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreSerializedValuesEqual(object current, object desired)
        {
            if (ReferenceEquals(current, desired)) return true;
            if (current == null || desired == null) return false;

            if (current is UnityEngine.Object currentObject)
            {
                return desired is UnityEngine.Object desiredObject && currentObject == desiredObject;
            }

            var currentType = current.GetType();
            if (currentType != desired.GetType()) return false;
            if (currentType.IsPrimitive
                || currentType.IsEnum
                || currentType == typeof(string)
                || currentType == typeof(decimal))
            {
                return current.Equals(desired);
            }

            if (current is IList currentList && desired is IList desiredList)
            {
                if (currentList.Count != desiredList.Count) return false;
                for (var i = 0; i < currentList.Count; i++)
                {
                    if (!AreSerializedValuesEqual(currentList[i], desiredList[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            foreach (var field in GetSerializableFields(currentType))
            {
                if (!AreSerializedValuesEqual(field.GetValue(current), field.GetValue(desired)))
                {
                    return false;
                }
            }

            return true;
        }

        private static IReadOnlyList<FieldInfo> GetSerializableFields(Type type)
        {
            if (SerializableFieldCache.TryGetValue(type, out var cachedFields))
            {
                return cachedFields;
            }

            var fields = new List<FieldInfo>();
            for (var currentType = type;
                 currentType != null && currentType != typeof(object);
                 currentType = currentType.BaseType)
            {
                foreach (var field in currentType.GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                             BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsInitOnly || field.IsNotSerialized)
                    {
                        continue;
                    }

                    if (field.IsPublic
                        || field.GetCustomAttribute<SerializeField>() != null
                        || field.GetCustomAttribute<SerializeReference>() != null)
                    {
                        fields.Add(field);
                    }
                }
            }

            cachedFields = fields.ToArray();
            SerializableFieldCache[type] = cachedFields;
            return cachedFields;
        }

        private static BuildingCost BuildCost(
            BuildingCostNumericRow row,
            IReadOnlyDictionary<string, ItemDefinition> items) =>
            new BuildingCost(items[row.ItemId], row.Amount);

        private static bool IsLevel(string familyId, int level, BuildingLevelNumericRow row) =>
            IsLevel(familyId, level, row.FamilyId, row.Level);

        private static bool IsLevel(string familyId, int level, string targetFamilyId, int targetLevel) =>
            familyId == targetFamilyId && level == targetLevel;

        private static bool TryParseEnum<T>(string value, out T result)
            where T : struct
        {
            return Enum.TryParse(value?.Trim(), true, out result)
                   && Enum.IsDefined(typeof(T), result);
        }

        private static string LevelKey(string familyId, int level) => $"{familyId}\u001f{level}";

        private static string NormalizeStableId(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string NormalizeProjectPath(string path) =>
            string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim().Replace('\\', '/');

        private static bool IsPathInside(string candidatePath, string parentPath)
        {
            var normalizedParent = parentPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                   + Path.DirectorySeparatorChar;
            return candidatePath.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<string> SplitStableKeys(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split(new[] { '|', ',', ';', '；' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(item => item.Trim())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

        private static bool ParseCategory(string value, out BuildingCategory category)
        {
            category = BuildingCategory.None;
            if (string.IsNullOrWhiteSpace(value)) return false;
            foreach (var token in value.Split(new[] { '|', ',', ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var normalizedToken = token.Trim();
                if (!BuildingCategoryNames.Contains(normalizedToken)
                    || !Enum.TryParse(normalizedToken, true, out BuildingCategory parsed)
                    || parsed == BuildingCategory.None)
                    return false;
                category |= parsed;
            }
            return category != BuildingCategory.None;
        }

        private static bool TryParseSpatialEffectKind(
            string value,
            out BuildingSpatialEffectKind kind)
        {
            switch (NormalizeStableId(value).ToLowerInvariant())
            {
                case "beauty":
                    kind = BuildingSpatialEffectKind.Beauty;
                    return true;
                case "medical":
                    kind = BuildingSpatialEffectKind.Medical;
                    return true;
                case "security":
                    kind = BuildingSpatialEffectKind.Security;
                    return true;
                case "production_percent":
                    kind = BuildingSpatialEffectKind.ProductionPercent;
                    return true;
                default:
                    kind = default;
                    return false;
            }
        }

        private static bool TryParseSpatialTargetFilter(
            string value,
            out BuildingSpatialTargetFilter targetFilter)
        {
            switch (NormalizeStableId(value).ToLowerInvariant())
            {
                case "cell":
                    targetFilter = BuildingSpatialTargetFilter.Cell;
                    return true;
                case "any_building":
                    targetFilter = BuildingSpatialTargetFilter.AnyBuilding;
                    return true;
                case "farmland":
                    targetFilter = BuildingSpatialTargetFilter.Farmland;
                    return true;
                default:
                    targetFilter = default;
                    return false;
            }
        }

        private static bool TryParseSpatialStackingRule(
            string value,
            out BuildingSpatialStackingRule stackingRule)
        {
            switch (NormalizeStableId(value).ToLowerInvariant())
            {
                case "additive":
                    stackingRule = BuildingSpatialStackingRule.Additive;
                    return true;
                case "no_stack":
                    stackingRule = BuildingSpatialStackingRule.NoStack;
                    return true;
                case "highest_value":
                    stackingRule = BuildingSpatialStackingRule.HighestValue;
                    return true;
                default:
                    stackingRule = default;
                    return false;
            }
        }

        private static Dictionary<string, T> FindAssetsByStableId<T>(
            string filter,
            Func<T, string> idSelector,
            string label,
            BuildingNumericImportReport report)
            where T : UnityEngine.Object
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<T>(path);
                var id = NormalizeStableId(idSelector(asset));
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.Warning($"{label}资产缺少稳定 ID：{path}");
                    continue;
                }
                if (result.ContainsKey(id))
                {
                    report.Error($"{label}稳定 ID 重复：{id}\n- {AssetDatabase.GetAssetPath(result[id])}\n- {path}");
                    continue;
                }
                result.Add(id, asset);
            }
            return result;
        }

        private static void ValidateUnique<TRow>(
            IReadOnlyList<TRow> rows,
            Func<TRow, string> keySelector,
            string message,
            BuildingNumericImportReport report)
            where TRow : BuildingNumericSourceRow
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = keySelector(row) ?? string.Empty;
                if (!seen.Add(key)) report.Error($"{message}：{key.Replace('\u001f', '/')}。", row.Sheet, row.Row);
            }
        }
    }
}
