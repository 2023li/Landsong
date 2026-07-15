using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Landsong.EditorTools.Buildings.NumericImport
{
    internal enum BuildingNumericIssueSeverity
    {
        Warning,
        Error
    }

    internal sealed class BuildingNumericIssue
    {
        public BuildingNumericIssue(
            BuildingNumericIssueSeverity severity,
            string message,
            string sheet = "",
            int row = 0)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Sheet = sheet ?? string.Empty;
            Row = row;
        }

        public BuildingNumericIssueSeverity Severity { get; }
        public string Message { get; }
        public string Sheet { get; }
        public int Row { get; }

        public override string ToString()
        {
            var location = string.IsNullOrWhiteSpace(Sheet)
                ? string.Empty
                : Row > 0 ? $"[{Sheet}!{Row}] " : $"[{Sheet}] ";
            return $"{location}{Message}";
        }
    }

    internal sealed class BuildingNumericImportReport
    {
        private readonly List<BuildingNumericIssue> issues = new List<BuildingNumericIssue>();
        private readonly List<string> changes = new List<string>();

        public IReadOnlyList<BuildingNumericIssue> Issues => issues;
        public IReadOnlyList<string> Changes => changes;
        public int ErrorCount => issues.Count(issue => issue.Severity == BuildingNumericIssueSeverity.Error);
        public int WarningCount => issues.Count(issue => issue.Severity == BuildingNumericIssueSeverity.Warning);
        public bool HasErrors => ErrorCount > 0;

        public void Error(string message, string sheet = "", int row = 0) =>
            issues.Add(new BuildingNumericIssue(BuildingNumericIssueSeverity.Error, message, sheet, row));

        public void Warning(string message, string sheet = "", int row = 0) =>
            issues.Add(new BuildingNumericIssue(BuildingNumericIssueSeverity.Warning, message, sheet, row));

        public void Change(string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && !changes.Contains(message))
            {
                changes.Add(message);
            }
        }
    }

    internal abstract class BuildingNumericSourceRow
    {
        protected BuildingNumericSourceRow(string sheet, int row)
        {
            Sheet = sheet;
            Row = row;
        }

        public string Sheet { get; }
        public int Row { get; }
    }

    internal sealed class BuildingFamilyNumericRow : BuildingNumericSourceRow
    {
        public BuildingFamilyNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public string DisplayName;
        public string Category;
        public int SizeX;
        public int SizeY;
        public bool IgnoreTerrain;
        public string TerrainKeys;
        public int MovementResistance;
        public int ConstructionTurns;
        public int MaxBuildCount;
        public string BuildLimitGroupId;
        public bool BlueprintInitiallyLocked;
        public bool HideWhenBlueprintLocked;
        public int BuildMenuSortOrder;
        public bool IsDevelopmentCompleted;
        public bool IsResourceProviderPoint;
        public int ResourceProviderPriority;
        public int BuildingActionPower;
        public string Notes;
    }

    internal sealed class BuildingCostNumericRow : BuildingNumericSourceRow
    {
        public BuildingCostNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int LevelOrTurn;
        public string ItemId;
        public int Amount;
    }

    internal sealed class BuildingLevelNumericRow : BuildingNumericSourceRow
    {
        public BuildingLevelNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public bool Configured;
        public string ConditionId;
    }

    internal sealed class FixedPopulationNumericRow : BuildingNumericSourceRow
    {
        public FixedPopulationNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public int Population;
    }

    internal sealed class InventoryCapacityNumericRow : BuildingNumericSourceRow
    {
        public InventoryCapacityNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public int Slots;
    }

    internal sealed class TechnologyPointsNumericRow : BuildingNumericSourceRow
    {
        public TechnologyPointsNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public int PointsPerTurn;
    }

    internal sealed class ResidentialNumericRow : BuildingNumericSourceRow
    {
        public ResidentialNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public int InitialPopulation;
        public int MaxPopulation;
        public string FoodItemId;
        public int GrowthIntervalTurns;
        public int FailureDecayThreshold;
        public string TaxItemId;
        public int TaxIntervalTurns;
    }

    internal sealed class WorkforceNumericRow : BuildingNumericSourceRow
    {
        public WorkforceNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public int MaxWorkers;
        public int InitialWorkers;
        public float BaseAttraction;
        public int RecruitCost;
        public bool AutoSubsidy;
        public int TargetStableWorkers;
        public string GoldItemId;
    }

    internal sealed class ProductionNumericRow : BuildingNumericSourceRow
    {
        public ProductionNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public string OutputItemId;
        public int MinimumWorkers;
        public int AmountPerCycle;
        public int IntervalTurns;
    }

    internal sealed class FishingRareNumericRow : BuildingNumericSourceRow
    {
        public FishingRareNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int Level;
        public bool Enabled;
        public string ItemId;
        public int MinimumWorkers;
        public float ChancePercent;
        public int Amount;
    }

    internal sealed class MarketNumericRow : BuildingNumericSourceRow
    {
        public MarketNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public string GoldItemId;
        public float IncomeRatio;
    }

    internal sealed class TreeNumericRow : BuildingNumericSourceRow
    {
        public TreeNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public int MinHealth;
        public int MaxHealth;
        public int DamagePerDoubleClick;
        public string WoodItemId;
        public int WoodReward;
        public string SaplingItemId;
        public int SaplingReward;
    }

    internal sealed class CropNumericRow : BuildingNumericSourceRow
    {
        public CropNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public string CropId;
        public string DisplayName;
        public int GrowTurns;
    }

    internal sealed class CropRewardNumericRow : BuildingNumericSourceRow
    {
        public CropRewardNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public string CropId;
        public string ItemId;
        public int MinAmount;
        public int MaxAmount;
    }

    internal sealed class CropCostNumericRow : BuildingNumericSourceRow
    {
        public CropCostNumericRow(string sheet, int row) : base(sheet, row) { }
        public string FamilyId;
        public string CropId;
        public string ItemId;
        public int Amount;
    }

    internal sealed class BuildingNumericWorkbookData
    {
        public int SchemaVersion;
        public readonly List<BuildingFamilyNumericRow> Families = new List<BuildingFamilyNumericRow>();
        public readonly List<BuildingCostNumericRow> PlacementCosts = new List<BuildingCostNumericRow>();
        public readonly List<BuildingCostNumericRow> ConstructionCosts = new List<BuildingCostNumericRow>();
        public readonly List<BuildingLevelNumericRow> Levels = new List<BuildingLevelNumericRow>();
        public readonly List<BuildingCostNumericRow> UpgradeCosts = new List<BuildingCostNumericRow>();
        public readonly List<FixedPopulationNumericRow> FixedPopulation = new List<FixedPopulationNumericRow>();
        public readonly List<InventoryCapacityNumericRow> InventoryCapacity = new List<InventoryCapacityNumericRow>();
        public readonly List<TechnologyPointsNumericRow> TechnologyPoints = new List<TechnologyPointsNumericRow>();
        public readonly List<ResidentialNumericRow> Residential = new List<ResidentialNumericRow>();
        public readonly List<WorkforceNumericRow> Workforce = new List<WorkforceNumericRow>();
        public readonly List<ProductionNumericRow> Production = new List<ProductionNumericRow>();
        public readonly List<FishingRareNumericRow> FishingRare = new List<FishingRareNumericRow>();
        public readonly List<MarketNumericRow> Markets = new List<MarketNumericRow>();
        public readonly List<TreeNumericRow> Trees = new List<TreeNumericRow>();
        public readonly List<CropNumericRow> Crops = new List<CropNumericRow>();
        public readonly List<CropCostNumericRow> CropPlantCosts = new List<CropCostNumericRow>();
        public readonly List<CropRewardNumericRow> CropHarvestRewards = new List<CropRewardNumericRow>();
        public readonly List<BuildingCostNumericRow> CropAutoHarvestCosts = new List<BuildingCostNumericRow>();
    }

    internal static class BuildingNumericWorkbookReader
    {
        public const int SupportedSchemaVersion = 2;
        private const int HeaderRow = 4;
        private const int FirstDataRow = 5;

        public static bool TryRead(
            string absolutePath,
            BuildingNumericImportReport report,
            out BuildingNumericWorkbookData data)
        {
            data = new BuildingNumericWorkbookData();
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                report.Error($"找不到建筑数值表：{absolutePath}");
                return false;
            }

            XlsxRawWorkbook workbook;
            try
            {
                workbook = XlsxRawWorkbook.Load(absolutePath);
            }
            catch (IOException exception)
            {
                report.Error($"无法读取 XLSX。请先保存并关闭 Excel，再重新校验：{exception.Message}");
                return false;
            }
            catch (Exception exception)
            {
                report.Error($"XLSX 读取失败：{exception.Message}");
                return false;
            }

            if (!int.TryParse(workbook.GetCell("说明", 4, 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out data.SchemaVersion))
            {
                report.Error("说明!B4 必须填写整数 SchemaVersion。", "说明", 4);
            }
            else if (data.SchemaVersion != SupportedSchemaVersion)
            {
                report.Error(
                    $"不支持 SchemaVersion {data.SchemaVersion}，当前导表器只支持 {SupportedSchemaVersion}。",
                    "说明",
                    4);
            }

            ReadFamilies(workbook, data, report);
            ReadSimpleCosts(workbook, "放置消耗", false, "", data.PlacementCosts, report);
            ReadSimpleCosts(workbook, "施工消耗", true, "回合序号", data.ConstructionCosts, report);
            ReadLevels(workbook, data, report);
            ReadSimpleCosts(workbook, "升级消耗", true, "目标Level", data.UpgradeCosts, report);
            ReadFixedPopulation(workbook, data, report);
            ReadInventoryCapacity(workbook, data, report);
            ReadTechnologyPoints(workbook, data, report);
            ReadResidential(workbook, data, report);
            ReadWorkforce(workbook, data, report);
            ReadProduction(workbook, data, report);
            ReadFishingRare(workbook, data, report);
            ReadMarkets(workbook, data, report);
            ReadTrees(workbook, data, report);
            ReadCrops(workbook, data, report);
            ReadCropPlantCosts(workbook, data, report);
            ReadCropRewards(workbook, data, report);
            ReadSimpleCosts(workbook, "作物_自动收获", false, "", data.CropAutoHarvestCosts, report);
            return !report.HasErrors;
        }

        private static void ReadFamilies(
            XlsxRawWorkbook workbook,
            BuildingNumericWorkbookData data,
            BuildingNumericImportReport report)
        {
            const string sheet = "建筑家族";
            var rows = ReadRows(workbook, sheet, report,
                "FamilyId", "名称", "分类", "SizeX", "SizeY", "忽略地形", "地形Keys", "移动阻力",
                "施工回合", "数量上限", "数量限制分组ID", "蓝图初始锁定", "锁定时隐藏", "菜单排序",
                "开发完成", "资源提供点", "资源提供优先级", "行动力预算", "说明");
            foreach (var row in rows)
            {
                var result = new BuildingFamilyNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report),
                    DisplayName = Required(row, "名称", report),
                    Category = Required(row, "分类", report),
                    SizeX = Integer(row, "SizeX", report),
                    SizeY = Integer(row, "SizeY", report),
                    IgnoreTerrain = Boolean(row, "忽略地形", report),
                    TerrainKeys = Optional(row, "地形Keys"),
                    MovementResistance = Integer(row, "移动阻力", report),
                    ConstructionTurns = Integer(row, "施工回合", report),
                    MaxBuildCount = Integer(row, "数量上限", report),
                    BuildLimitGroupId = Optional(row, "数量限制分组ID"),
                    BlueprintInitiallyLocked = Boolean(row, "蓝图初始锁定", report),
                    HideWhenBlueprintLocked = Boolean(row, "锁定时隐藏", report),
                    BuildMenuSortOrder = Integer(row, "菜单排序", report),
                    IsDevelopmentCompleted = Boolean(row, "开发完成", report),
                    IsResourceProviderPoint = Boolean(row, "资源提供点", report),
                    ResourceProviderPriority = Integer(row, "资源提供优先级", report),
                    BuildingActionPower = Integer(row, "行动力预算", report),
                    Notes = Optional(row, "说明")
                };
                data.Families.Add(result);
            }
        }

        private static void ReadSimpleCosts(
            XlsxRawWorkbook workbook,
            string sheet,
            bool hasIndex,
            string indexHeader,
            List<BuildingCostNumericRow> target,
            BuildingNumericImportReport report)
        {
            var headers = hasIndex
                ? new[] { "FamilyId", indexHeader, "ItemId", "数量" }
                : new[] { "FamilyId", "ItemId", "数量" };
            foreach (var row in ReadRows(workbook, sheet, report, headers))
            {
                target.Add(new BuildingCostNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report),
                    LevelOrTurn = hasIndex ? Integer(row, indexHeader, report) : 0,
                    ItemId = Required(row, "ItemId", report),
                    Amount = Integer(row, "数量", report)
                });
            }
        }

        private static void ReadLevels(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "运营等级";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "Level", "开放", "ConditionId"))
            {
                data.Levels.Add(new BuildingLevelNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report),
                    Level = Integer(row, "Level", report),
                    Configured = Boolean(row, "开放", report),
                    ConditionId = Required(row, "ConditionId", report)
                });
            }
        }

        private static void ReadFixedPopulation(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_固定人口";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "Level", "提供人口"))
            {
                data.FixedPopulation.Add(new FixedPopulationNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    Population = Integer(row, "提供人口", report)
                });
            }
        }

        private static void ReadInventoryCapacity(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_库存容量";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "Level", "提供库存格"))
            {
                data.InventoryCapacity.Add(new InventoryCapacityNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    Slots = Integer(row, "提供库存格", report)
                });
            }
        }

        private static void ReadTechnologyPoints(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_科技点";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "Level", "科技点/回合"))
            {
                data.TechnologyPoints.Add(new TechnologyPointsNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    PointsPerTurn = Integer(row, "科技点/回合", report)
                });
            }
        }

        private static void ReadResidential(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_住宅";
            foreach (var row in ReadRows(workbook, sheet, report,
                         "FamilyId", "Level", "初始人口", "人口上限", "FoodItemId", "增长回合",
                         "失败衰减阈值", "TaxItemId", "税收回合"))
            {
                data.Residential.Add(new ResidentialNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    InitialPopulation = Integer(row, "初始人口", report), MaxPopulation = Integer(row, "人口上限", report),
                    FoodItemId = Required(row, "FoodItemId", report), GrowthIntervalTurns = Integer(row, "增长回合", report),
                    FailureDecayThreshold = Integer(row, "失败衰减阈值", report), TaxItemId = Required(row, "TaxItemId", report),
                    TaxIntervalTurns = Integer(row, "税收回合", report)
                });
            }
        }

        private static void ReadWorkforce(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_岗位";
            foreach (var row in ReadRows(workbook, sheet, report,
                         "FamilyId", "Level", "最大工人", "初始工人", "基础吸引力", "招工金币",
                         "自动补贴", "稳定目标工人", "GoldItemId"))
            {
                data.Workforce.Add(new WorkforceNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    MaxWorkers = Integer(row, "最大工人", report), InitialWorkers = Integer(row, "初始工人", report),
                    BaseAttraction = Float(row, "基础吸引力", report), RecruitCost = Integer(row, "招工金币", report),
                    AutoSubsidy = Boolean(row, "自动补贴", report), TargetStableWorkers = Integer(row, "稳定目标工人", report),
                    GoldItemId = Required(row, "GoldItemId", report)
                });
            }
        }

        private static void ReadProduction(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_生产";
            foreach (var row in ReadRows(workbook, sheet, report,
                         "FamilyId", "Level", "OutputItemId", "工人阈值", "产量/周期", "周期回合"))
            {
                data.Production.Add(new ProductionNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    OutputItemId = Required(row, "OutputItemId", report), MinimumWorkers = Integer(row, "工人阈值", report),
                    AmountPerCycle = Integer(row, "产量/周期", report), IntervalTurns = Integer(row, "周期回合", report)
                });
            }
        }

        private static void ReadFishingRare(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "等级_捕鱼稀有";
            foreach (var row in ReadRows(workbook, sheet, report,
                         "FamilyId", "Level", "启用", "ItemId", "最低工人", "概率%", "数量"))
            {
                data.FishingRare.Add(new FishingRareNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), Level = Integer(row, "Level", report),
                    Enabled = Boolean(row, "启用", report), ItemId = Optional(row, "ItemId"),
                    MinimumWorkers = Integer(row, "最低工人", report), ChancePercent = Float(row, "概率%", report),
                    Amount = Integer(row, "数量", report)
                });
            }
        }

        private static void ReadMarkets(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "模块_市场";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "GoldItemId", "价值结算比例"))
            {
                data.Markets.Add(new MarketNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), GoldItemId = Required(row, "GoldItemId", report),
                    IncomeRatio = Float(row, "价值结算比例", report)
                });
            }
        }

        private static void ReadTrees(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "模块_树木";
            foreach (var row in ReadRows(workbook, sheet, report,
                         "FamilyId", "最低生命", "最高生命", "双击伤害", "WoodItemId", "原木奖励", "SaplingItemId", "树苗奖励"))
            {
                data.Trees.Add(new TreeNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), MinHealth = Integer(row, "最低生命", report),
                    MaxHealth = Integer(row, "最高生命", report), DamagePerDoubleClick = Integer(row, "双击伤害", report),
                    WoodItemId = Required(row, "WoodItemId", report), WoodReward = Integer(row, "原木奖励", report),
                    SaplingItemId = Required(row, "SaplingItemId", report), SaplingReward = Integer(row, "树苗奖励", report)
                });
            }
        }

        private static void ReadCrops(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "模块_作物";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "CropId", "显示名", "成熟回合"))
            {
                data.Crops.Add(new CropNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), CropId = Required(row, "CropId", report),
                    DisplayName = Required(row, "显示名", report), GrowTurns = Integer(row, "成熟回合", report)
                });
            }
        }

        private static void ReadCropPlantCosts(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "作物_种植消耗";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "CropId", "ItemId", "数量"))
            {
                data.CropPlantCosts.Add(new CropCostNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), CropId = Required(row, "CropId", report),
                    ItemId = Required(row, "ItemId", report), Amount = Integer(row, "数量", report)
                });
            }
        }

        private static void ReadCropRewards(XlsxRawWorkbook workbook, BuildingNumericWorkbookData data, BuildingNumericImportReport report)
        {
            const string sheet = "作物_收获产出";
            foreach (var row in ReadRows(workbook, sheet, report, "FamilyId", "CropId", "ItemId", "最小数量", "最大数量"))
            {
                data.CropHarvestRewards.Add(new CropRewardNumericRow(sheet, row.Row)
                {
                    FamilyId = Required(row, "FamilyId", report), CropId = Required(row, "CropId", report),
                    ItemId = Required(row, "ItemId", report), MinAmount = Integer(row, "最小数量", report),
                    MaxAmount = Integer(row, "最大数量", report)
                });
            }
        }

        private static List<RawSheetRow> ReadRows(
            XlsxRawWorkbook workbook,
            string sheet,
            BuildingNumericImportReport report,
            params string[] requiredHeaders)
        {
            if (!workbook.TryGetSheet(sheet, out var rawSheet))
            {
                report.Error("缺少必需工作表。", sheet);
                return new List<RawSheetRow>();
            }

            var headers = rawSheet.GetHeaders(HeaderRow);
            foreach (var required in requiredHeaders)
            {
                if (!headers.ContainsKey(required))
                {
                    report.Error($"缺少列：{required}", sheet, HeaderRow);
                }
            }

            if (report.HasErrors && requiredHeaders.Any(required => !headers.ContainsKey(required)))
            {
                return new List<RawSheetRow>();
            }

            var result = new List<RawSheetRow>();
            for (var rowIndex = FirstDataRow; rowIndex <= rawSheet.MaxRow; rowIndex++)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var hasAnyValue = false;
                foreach (var pair in headers)
                {
                    var value = rawSheet.GetCell(rowIndex, pair.Value).Trim();
                    values[pair.Key] = value;
                    hasAnyValue |= !string.IsNullOrWhiteSpace(value);
                }

                if (hasAnyValue)
                {
                    result.Add(new RawSheetRow(sheet, rowIndex, values));
                }
            }

            return result;
        }

        private static string Required(RawSheetRow row, string header, BuildingNumericImportReport report)
        {
            var value = Optional(row, header);
            if (string.IsNullOrWhiteSpace(value))
            {
                report.Error($"{header} 不能为空。", row.Sheet, row.Row);
            }

            return value;
        }

        private static string Optional(RawSheetRow row, string header) =>
            row.Values.TryGetValue(header, out var value) ? value?.Trim() ?? string.Empty : string.Empty;

        private static int Integer(RawSheetRow row, string header, BuildingNumericImportReport report)
        {
            var value = Optional(row, header);
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
                && Math.Abs(number - Math.Round(number)) < 0.000001
                && number >= int.MinValue
                && number <= int.MaxValue)
            {
                return (int)Math.Round(number);
            }

            report.Error($"{header} 必须是整数，当前值：{value}", row.Sheet, row.Row);
            return 0;
        }

        private static float Float(RawSheetRow row, string header, BuildingNumericImportReport report)
        {
            var value = Optional(row, header);
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }

            report.Error($"{header} 必须是数字，当前值：{value}", row.Sheet, row.Row);
            return 0f;
        }

        private static bool Boolean(RawSheetRow row, string header, BuildingNumericImportReport report)
        {
            var value = Optional(row, header);
            if (string.Equals(value, "是", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase)
                || value == "1")
            {
                return true;
            }

            if (string.Equals(value, "否", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "false", StringComparison.OrdinalIgnoreCase)
                || value == "0")
            {
                return false;
            }

            report.Error($"{header} 必须填写 是/否，当前值：{value}", row.Sheet, row.Row);
            return false;
        }

        private sealed class RawSheetRow
        {
            public RawSheetRow(string sheet, int row, Dictionary<string, string> values)
            {
                Sheet = sheet;
                Row = row;
                Values = values;
            }

            public string Sheet { get; }
            public int Row { get; }
            public Dictionary<string, string> Values { get; }
        }
    }

    internal sealed class XlsxRawWorkbook
    {
        private static readonly XNamespace SpreadsheetNamespace =
            "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNamespace =
            "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNamespace =
            "http://schemas.openxmlformats.org/package/2006/relationships";

        private readonly Dictionary<string, XlsxRawSheet> sheets =
            new Dictionary<string, XlsxRawSheet>(StringComparer.Ordinal);

        public static XlsxRawWorkbook Load(string path)
        {
            using var file = File.OpenRead(path);
            using var archive = new ZipArchive(file, ZipArchiveMode.Read, false);
            var sharedStrings = ReadSharedStrings(archive);
            var workbookDocument = LoadXml(archive, "xl/workbook.xml");
            var relationsDocument = LoadXml(archive, "xl/_rels/workbook.xml.rels");
            var targets = relationsDocument.Root?
                .Elements(PackageRelationshipNamespace + "Relationship")
                .Where(element => element.Attribute("Id") != null && element.Attribute("Target") != null)
                .ToDictionary(
                    element => element.Attribute("Id")?.Value ?? string.Empty,
                    element => NormalizeEntryPath("xl/", element.Attribute("Target")?.Value ?? string.Empty),
                    StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);

            var result = new XlsxRawWorkbook();
            foreach (var element in workbookDocument.Descendants(SpreadsheetNamespace + "sheet"))
            {
                var name = element.Attribute("name")?.Value ?? string.Empty;
                var relationId = element.Attribute(RelationshipNamespace + "id")?.Value ?? string.Empty;
                if (string.IsNullOrWhiteSpace(name)
                    || !targets.TryGetValue(relationId, out var target)
                    || archive.GetEntry(target) == null)
                {
                    continue;
                }

                result.sheets[name] = ReadSheet(archive, target, sharedStrings);
            }

            return result;
        }

        public bool TryGetSheet(string name, out XlsxRawSheet sheet) => sheets.TryGetValue(name, out sheet);

        public string GetCell(string sheet, int row, int column) =>
            sheets.TryGetValue(sheet, out var value) ? value.GetCell(row, column) : string.Empty;

        private static XlsxRawSheet ReadSheet(
            ZipArchive archive,
            string entryName,
            IReadOnlyList<string> sharedStrings)
        {
            var document = LoadXml(archive, entryName);
            var sheet = new XlsxRawSheet();
            foreach (var rowElement in document.Descendants(SpreadsheetNamespace + "row"))
            {
                var rowNumber = ParseInt(rowElement.Attribute("r")?.Value);
                if (rowNumber <= 0)
                {
                    continue;
                }

                foreach (var cell in rowElement.Elements(SpreadsheetNamespace + "c"))
                {
                    var reference = cell.Attribute("r")?.Value ?? string.Empty;
                    var column = ParseColumn(reference);
                    if (column <= 0)
                    {
                        continue;
                    }

                    var type = cell.Attribute("t")?.Value ?? string.Empty;
                    string value;
                    if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
                    {
                        value = string.Concat(cell.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value));
                    }
                    else
                    {
                        value = cell.Element(SpreadsheetNamespace + "v")?.Value ?? string.Empty;
                        if (string.Equals(type, "s", StringComparison.Ordinal)
                            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var stringIndex)
                            && stringIndex >= 0
                            && stringIndex < sharedStrings.Count)
                        {
                            value = sharedStrings[stringIndex];
                        }
                        else if (string.Equals(type, "b", StringComparison.Ordinal))
                        {
                            value = value == "1" ? "TRUE" : "FALSE";
                        }
                    }

                    sheet.SetCell(rowNumber, column, value);
                }
            }

            return sheet;
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            using var stream = entry.Open();
            var document = XDocument.Load(stream);
            return document.Descendants(SpreadsheetNamespace + "si")
                .Select(item => string.Concat(item.Descendants(SpreadsheetNamespace + "t").Select(text => text.Value)))
                .ToList();
        }

        private static XDocument LoadXml(ZipArchive archive, string entryName)
        {
            var entry = archive.GetEntry(entryName)
                        ?? throw new InvalidDataException($"XLSX 缺少条目：{entryName}");
            using var stream = entry.Open();
            return XDocument.Load(stream);
        }

        private static string NormalizeEntryPath(string basePath, string target)
        {
            var segments = (target.StartsWith("/", StringComparison.Ordinal)
                    ? target.TrimStart('/')
                    : basePath + target)
                .Replace('\\', '/')
                .Split('/');
            var stack = new List<string>();
            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment) || segment == ".")
                {
                    continue;
                }

                if (segment == "..")
                {
                    if (stack.Count > 0) stack.RemoveAt(stack.Count - 1);
                    continue;
                }

                stack.Add(segment);
            }

            return string.Join("/", stack);
        }

        private static int ParseColumn(string reference)
        {
            var result = 0;
            for (var i = 0; i < reference.Length && char.IsLetter(reference[i]); i++)
            {
                result = result * 26 + char.ToUpperInvariant(reference[i]) - 'A' + 1;
            }

            return result;
        }

        private static int ParseInt(string value) =>
            int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : 0;
    }

    internal sealed class XlsxRawSheet
    {
        private readonly Dictionary<long, string> cells = new Dictionary<long, string>();
        public int MaxRow { get; private set; }

        public void SetCell(int row, int column, string value)
        {
            cells[Key(row, column)] = value ?? string.Empty;
            MaxRow = Math.Max(MaxRow, row);
        }

        public string GetCell(int row, int column) =>
            cells.TryGetValue(Key(row, column), out var value) ? value ?? string.Empty : string.Empty;

        public Dictionary<string, int> GetHeaders(int row)
        {
            var result = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var column = 1; column <= 256; column++)
            {
                var value = GetCell(row, column).Trim();
                if (!string.IsNullOrWhiteSpace(value) && !result.ContainsKey(value))
                {
                    result.Add(value, column);
                }
            }

            return result;
        }

        private static long Key(int row, int column) => ((long)row << 32) | (uint)column;
    }
}
