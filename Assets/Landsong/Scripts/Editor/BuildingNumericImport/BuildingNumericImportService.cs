using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.BuildingSystem;
using Landsong.ConditionSystem;
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
            Dictionary<string, TechnologyDefinition> technologies)
        {
            ProjectRelativePath = projectRelativePath;
            AbsolutePath = absolutePath;
            Data = data;
            Report = report;
            Families = families;
            Items = items;
            Technologies = technologies;
        }

        public string ProjectRelativePath { get; }
        public string AbsolutePath { get; }
        public BuildingNumericWorkbookData Data { get; }
        public BuildingNumericImportReport Report { get; }
        public Dictionary<string, BuildingFamilyDefinition> Families { get; }
        public Dictionary<string, ItemDefinition> Items { get; }
        public Dictionary<string, TechnologyDefinition> Technologies { get; }
        public bool IsValid => Data != null && Report != null && !Report.HasErrors;
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
            "fishing_hut"
        };

        private static readonly HashSet<string> BuildingCategoryNames = new HashSet<string>(
            Enum.GetNames(typeof(BuildingCategory))
                .Where(name => !string.Equals(name, nameof(BuildingCategory.None), StringComparison.Ordinal)),
            StringComparer.OrdinalIgnoreCase);

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
                    new Dictionary<string, TechnologyDefinition>());
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
            var technologies = FindAssetsByStableId<TechnologyDefinition>(
                "t:TechnologyDefinition",
                technology => technology == null ? string.Empty : technology.TechnologyId,
                "科技",
                report);

            if (data != null)
            {
                Validate(data, families, items, technologies, report);
                BuildImpactPreview(data, families, report);
            }

            return new BuildingNumericImportSession(
                normalizedProjectPath,
                absolutePath,
                data,
                report,
                families,
                items,
                technologies);
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

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("导入建筑数值表");
            try
            {
                foreach (var familyRow in session.Data.Families)
                {
                    var family = session.Families[familyRow.FamilyId];
                    ApplyFamily(session, familyRow, family);
                }

                Landsong.Editor.BuildingArchitectureValidator.Execute();
                AssetDatabase.SaveAssets();

                SaveRuntimePrefabs(session.Families.Values);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                var rollbackSucceeded = true;
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                    SaveRuntimePrefabs(session.Families.Values);
                    AssetDatabase.SaveAssets();
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

        private static void SaveRuntimePrefabs(IEnumerable<BuildingFamilyDefinition> families)
        {
            foreach (var family in families)
            {
                if (family == null || family.RuntimePrefab == null)
                {
                    continue;
                }

                var root = family.RuntimePrefab.transform.root.gameObject;
                if (PrefabUtility.IsPartOfPrefabAsset(root))
                {
                    PrefabUtility.SavePrefabAsset(root);
                }
            }
        }

        private static void ApplyFamily(
            BuildingNumericImportSession session,
            BuildingFamilyNumericRow row,
            BuildingFamilyDefinition family)
        {
            Undo.RecordObject(family, "导入建筑家族数值");
            if (family.ModuleSet != null)
            {
                Undo.RecordObject(family.ModuleSet, "导入建筑模块数值");
            }

            if (family.RuntimePrefab != null)
            {
                Undo.RecordObject(family.RuntimePrefab, "导入建筑 Prefab 数值");
            }

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
                row.MovementResistance,
                placementCosts,
                row.BlueprintInitiallyLocked,
                row.HideWhenBlueprintLocked,
                row.BuildMenuSortOrder,
                row.MaxBuildCount,
                row.BuildLimitGroupId,
                row.IsDevelopmentCompleted);

            var constructionTurns = new IReadOnlyList<BuildingCost>[row.ConstructionTurns];
            for (var turn = 1; turn <= row.ConstructionTurns; turn++)
            {
                constructionTurns[turn - 1] = session.Data.ConstructionCosts
                    .Where(cost => cost.FamilyId == row.FamilyId && cost.LevelOrTurn == turn)
                    .Select(cost => BuildCost(cost, session.Items))
                    .ToArray();
            }

            var construction = new BuildingConstructionDefinition();
            construction.Configure(constructionTurns);

            var existingLevels = family.Levels
                .Where(level => level != null)
                .ToDictionary(level => level.Level);
            var levels = session.Data.Levels
                .Where(level => level.FamilyId == row.FamilyId)
                .OrderBy(level => level.Level)
                .Select(level => BuildLevel(session, family, level, existingLevels))
                .ToArray();

            family.ConfigureRuntime(
                family.RuntimePrefab,
                construction,
                levels,
                family.ModuleSet,
                family.Presentation);

            if (family.RuntimePrefab != null)
            {
                family.RuntimePrefab.ConfigureNumericAuthoringData(
                    row.IsResourceProviderPoint,
                    row.ResourceProviderPriority,
                    row.BuildingActionPower);
                EditorUtility.SetDirty(family.RuntimePrefab);
            }

            ApplyModuleDefaults(session, row.FamilyId, family);
            family.ModuleSet?.Normalize();
            EditorUtility.SetDirty(family);
            if (family.ModuleSet != null)
            {
                EditorUtility.SetDirty(family.ModuleSet);
            }
        }

        private static BuildingLevelDefinition BuildLevel(
            BuildingNumericImportSession session,
            BuildingFamilyDefinition family,
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
                configurations.Add(new BuildingInventoryLevelConfiguration(inventory.Slots));
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
                configurations.Add(new ResidentialHousingLevelConfiguration(
                    residential.InitialPopulation,
                    residential.MaxPopulation,
                    session.Items[residential.FoodItemId],
                    residential.GrowthIntervalTurns,
                    residential.FailureDecayThreshold,
                    session.Items[residential.TaxItemId],
                    residential.TaxIntervalTurns));
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

        private static void ApplyModuleDefaults(
            BuildingNumericImportSession session,
            string familyId,
            BuildingFamilyDefinition family)
        {
            var modules = family.ModuleSet?.BuildingModules ?? Array.Empty<BuildingModuleBase>();
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

        private static void Validate(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            IReadOnlyDictionary<string, ItemDefinition> items,
            IReadOnlyDictionary<string, TechnologyDefinition> technologies,
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
            ValidateCosts(data.UpgradeCosts, data, families, items, true, report);
            ValidateUnique(data.Levels, row => LevelKey(row.FamilyId, row.Level), "FamilyId + Level 重复", report);
            ValidateLevels(data, families, technologies, report);

            ValidateLevelTable(data.FixedPopulation, row => row.FamilyId, row => row.Level, families, data, typeof(BM_固定人口), "固定人口", report);
            ValidateLevelTable(data.InventoryCapacity, row => row.FamilyId, row => row.Level, families, data, typeof(BM_库存格容量), "库存容量", report);
            ValidateLevelTable(data.TechnologyPoints, row => row.FamilyId, row => row.Level, families, data, typeof(BM_科技点产出), "科技点", report);
            ValidateLevelTable(data.Residential, row => row.FamilyId, row => row.Level, families, data, typeof(BM_居民运营), "住宅", report);
            ValidateLevelTable(data.Workforce, row => row.FamilyId, row => row.Level, families, data, typeof(BM_岗位运营), "岗位", report);
            ValidateLevelTable(data.FishingRare, row => row.FamilyId, row => row.Level, families, data, typeof(BM_稀有产出), "捕鱼稀有产出", report);
            ValidateProduction(data, families, items, report);
            ValidateModuleRows(data, families, items, report);

            foreach (var row in data.FixedPopulation) if (row.Population < 0) report.Error("提供人口不能小于 0。", row.Sheet, row.Row);
            foreach (var row in data.InventoryCapacity) if (row.Slots < 0) report.Error("提供库存格不能小于 0。", row.Sheet, row.Row);
            foreach (var row in data.TechnologyPoints) if (row.PointsPerTurn < 0) report.Error("科技点不能小于 0。", row.Sheet, row.Row);
            foreach (var row in data.Residential)
            {
                RequireItem(row.FoodItemId, row, items, report);
                RequireItem(row.TaxItemId, row, items, report);
                if (row.InitialPopulation < 1 || row.MaxPopulation < row.InitialPopulation) report.Error("住宅人口范围非法。", row.Sheet, row.Row);
                if (row.GrowthIntervalTurns < 1 || row.FailureDecayThreshold < 1 || row.TaxIntervalTurns < 1) report.Error("住宅回合/阈值必须至少为 1。", row.Sheet, row.Row);
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
                "消耗键重复",
                report);
            foreach (var row in rows)
            {
                if (!families.ContainsKey(row.FamilyId)) report.Error($"未知 FamilyId：{row.FamilyId}", row.Sheet, row.Row);
                RequireItem(row.ItemId, row, items, report);
                if (row.Amount <= 0) report.Error("消耗数量必须大于 0。", row.Sheet, row.Row);
                if (!indexed) continue;
                if (row.Sheet == "施工消耗")
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

            foreach (var family in families.Values)
            {
                if (HasModule(family, typeof(BM_市场资源结算)) && data.Markets.Count(row => row.FamilyId == family.FamilyId) != 1)
                    report.Error($"{family.FamilyId} 的市场模块必须有且只有一行配置。");
                if (HasModule(family, typeof(BM_树木采集)) && data.Trees.Count(row => row.FamilyId == family.FamilyId) != 1)
                    report.Error($"{family.FamilyId} 的树木模块必须有且只有一行配置。");
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

        private static void BuildImpactPreview(
            BuildingNumericWorkbookData data,
            IReadOnlyDictionary<string, BuildingFamilyDefinition> families,
            BuildingNumericImportReport report)
        {
            foreach (var row in data.Families.OrderBy(value => value.FamilyId, StringComparer.Ordinal))
            {
                if (!families.TryGetValue(row.FamilyId, out var family)) continue;
                var levelCount = data.Levels.Count(level => level.FamilyId == row.FamilyId);
                var assetPath = AssetDatabase.GetAssetPath(family);
                report.Change($"{row.FamilyId}：覆盖公共数值、{row.ConstructionTurns} 个施工阶段、{levelCount} 个运营等级（{assetPath}）");
                if (family.ModuleSet != null && (HasModule(family, typeof(BM_市场资源结算)) || HasModule(family, typeof(BM_树木采集)) || HasModule(family, typeof(BuildingCropGrowthModule))))
                    report.Change($"{row.FamilyId}：覆盖 Excel 管理的模块默认值（{AssetDatabase.GetAssetPath(family.ModuleSet)}）");
                if (family.RuntimePrefab != null)
                    report.Change($"{row.FamilyId}：更新资源点/优先级/行动力三个 Prefab 数据字段（{AssetDatabase.GetAssetPath(family.RuntimePrefab)}）");
            }
        }

        private static BuildingCost BuildCost(
            BuildingCostNumericRow row,
            IReadOnlyDictionary<string, ItemDefinition> items) =>
            new BuildingCost(items[row.ItemId], row.Amount);

        private static bool IsLevel(string familyId, int level, BuildingLevelNumericRow row) =>
            IsLevel(familyId, level, row.FamilyId, row.Level);

        private static bool IsLevel(string familyId, int level, string targetFamilyId, int targetLevel) =>
            familyId == targetFamilyId && level == targetLevel;

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
