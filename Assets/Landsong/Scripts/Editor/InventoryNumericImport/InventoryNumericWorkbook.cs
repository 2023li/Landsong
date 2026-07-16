using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Landsong.EditorTools.NumericImport;

namespace Landsong.EditorTools.Inventory.NumericImport
{
    internal enum InventoryNumericIssueSeverity
    {
        Warning,
        Error
    }

    internal sealed class InventoryNumericIssue
    {
        public InventoryNumericIssue(
            InventoryNumericIssueSeverity severity,
            string message,
            string sheet = "",
            int row = 0)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Sheet = sheet ?? string.Empty;
            Row = row;
        }

        public InventoryNumericIssueSeverity Severity { get; }
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

    internal sealed class InventoryNumericImportReport
    {
        private readonly List<InventoryNumericIssue> issues = new List<InventoryNumericIssue>();
        private readonly List<string> changes = new List<string>();

        public IReadOnlyList<InventoryNumericIssue> Issues => issues;
        public IReadOnlyList<string> Changes => changes;
        public int ErrorCount =>
            issues.Count(issue => issue.Severity == InventoryNumericIssueSeverity.Error);
        public int WarningCount =>
            issues.Count(issue => issue.Severity == InventoryNumericIssueSeverity.Warning);
        public bool HasErrors => ErrorCount > 0;

        public void Error(string message, string sheet = "", int row = 0) =>
            issues.Add(new InventoryNumericIssue(
                InventoryNumericIssueSeverity.Error,
                message,
                sheet,
                row));

        public void Warning(string message, string sheet = "", int row = 0) =>
            issues.Add(new InventoryNumericIssue(
                InventoryNumericIssueSeverity.Warning,
                message,
                sheet,
                row));

        public void Change(string message)
        {
            if (!string.IsNullOrWhiteSpace(message) && !changes.Contains(message))
            {
                changes.Add(message);
            }
        }
    }

    internal abstract class InventoryNumericSourceRow
    {
        protected InventoryNumericSourceRow(string sheet, int row)
        {
            Sheet = sheet;
            Row = row;
        }

        public string Sheet { get; }
        public int Row { get; }
    }

    internal sealed class ItemGroupNumericRow : InventoryNumericSourceRow
    {
        public ItemGroupNumericRow(string sheet, int row) : base(sheet, row) { }
        public string GroupId;
        public string DisplayName;
        public string Description;
        public string ParentGroupId;
        public string IconAssetPath;
    }

    internal sealed class ItemNumericRow : InventoryNumericSourceRow
    {
        public ItemNumericRow(string sheet, int row) : base(sheet, row) { }
        public string ItemId;
        public string DisplayName;
        public string Description;
        public string Category;
        public bool Stackable;
        public int MaxStackSize;
        public int BaseValue;
        public string AddressableKey;
        public string Tags;
        public float LossRatePerTurn;
        public string ItemGroupIds;
        public bool IsFood;
        public float NutritionValue;
        public float DietQuality;
        public string IconAssetPath;
    }

    internal sealed class SlotTypeNumericRow : InventoryNumericSourceRow
    {
        public SlotTypeNumericRow(string sheet, int row) : base(sheet, row) { }
        public string SlotType;
        public string DisplayName;
        public float BaseLossRateMultiplier;
        public int AutoStorePriority;
    }

    internal sealed class SlotTypeLossModifierNumericRow : InventoryNumericSourceRow
    {
        public SlotTypeLossModifierNumericRow(string sheet, int row) : base(sheet, row) { }
        public string SlotType;
        public string ItemGroupId;
        public float LossRateMultiplier;
    }

    internal sealed class InventoryNumericWorkbookData
    {
        public int SchemaVersion;
        public readonly List<ItemGroupNumericRow> ItemGroups = new List<ItemGroupNumericRow>();
        public readonly List<ItemNumericRow> Items = new List<ItemNumericRow>();
        public readonly List<SlotTypeNumericRow> SlotTypes = new List<SlotTypeNumericRow>();
        public readonly List<SlotTypeLossModifierNumericRow> SlotTypeLossModifiers =
            new List<SlotTypeLossModifierNumericRow>();
    }

    internal static class InventoryNumericWorkbookReader
    {
        public const int SupportedSchemaVersion = 3;
        private const int HeaderRow = 4;
        private const int FirstDataRow = 5;

        public static bool TryRead(
            string absolutePath,
            InventoryNumericImportReport report,
            out InventoryNumericWorkbookData data)
        {
            data = new InventoryNumericWorkbookData();
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                report.Error($"找不到库存系统数值表：{absolutePath}");
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

            if (!int.TryParse(
                    workbook.GetCell("说明", 4, 2),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out data.SchemaVersion))
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

            ReadItemGroups(workbook, data, report);
            ReadItems(workbook, data, report);
            ReadSlotTypes(workbook, data, report);
            ReadSlotTypeLossModifiers(workbook, data, report);
            return !report.HasErrors;
        }

        private static void ReadItemGroups(
            XlsxRawWorkbook workbook,
            InventoryNumericWorkbookData data,
            InventoryNumericImportReport report)
        {
            const string sheet = "物品组";
            foreach (var row in ReadRows(
                         workbook,
                         sheet,
                         report,
                         "GroupId",
                         "名称",
                         "描述",
                         "ParentGroupId",
                         "IconAssetPath"))
            {
                data.ItemGroups.Add(new ItemGroupNumericRow(sheet, row.Row)
                {
                    GroupId = Required(row, "GroupId", report),
                    DisplayName = Required(row, "名称", report),
                    Description = Optional(row, "描述"),
                    ParentGroupId = Optional(row, "ParentGroupId"),
                    IconAssetPath = Optional(row, "IconAssetPath")
                });
            }
        }

        private static void ReadItems(
            XlsxRawWorkbook workbook,
            InventoryNumericWorkbookData data,
            InventoryNumericImportReport report)
        {
            const string sheet = "物品";
            foreach (var row in ReadRows(
                         workbook,
                         sheet,
                         report,
                         "ItemId",
                         "名称",
                         "描述",
                         "分类",
                         "可堆叠",
                         "堆叠上限",
                         "基础价值",
                         "AddressableKey",
                         "标签",
                         "每回合损耗率",
                         "ItemGroupIds",
                         "是否食物",
                         "营养值",
                         "饮食质量",
                         "IconAssetPath"))
            {
                data.Items.Add(new ItemNumericRow(sheet, row.Row)
                {
                    ItemId = Required(row, "ItemId", report),
                    DisplayName = Required(row, "名称", report),
                    Description = Optional(row, "描述"),
                    Category = Required(row, "分类", report),
                    Stackable = Boolean(row, "可堆叠", report),
                    MaxStackSize = Integer(row, "堆叠上限", report),
                    BaseValue = Integer(row, "基础价值", report),
                    AddressableKey = Optional(row, "AddressableKey"),
                    Tags = Optional(row, "标签"),
                    LossRatePerTurn = Float(row, "每回合损耗率", report),
                    ItemGroupIds = Optional(row, "ItemGroupIds"),
                    IsFood = Boolean(row, "是否食物", report),
                    NutritionValue = Float(row, "营养值", report),
                    DietQuality = Float(row, "饮食质量", report),
                    IconAssetPath = Optional(row, "IconAssetPath")
                });
            }
        }

        private static void ReadSlotTypes(
            XlsxRawWorkbook workbook,
            InventoryNumericWorkbookData data,
            InventoryNumericImportReport report)
        {
            const string sheet = "槽位类型";
            foreach (var row in ReadRows(
                         workbook,
                         sheet,
                         report,
                         "SlotType",
                         "名称",
                         "基础损耗倍率",
                         "自动存放优先级"))
            {
                data.SlotTypes.Add(new SlotTypeNumericRow(sheet, row.Row)
                {
                    SlotType = Required(row, "SlotType", report),
                    DisplayName = Required(row, "名称", report),
                    BaseLossRateMultiplier = Float(row, "基础损耗倍率", report),
                    AutoStorePriority = Integer(row, "自动存放优先级", report)
                });
            }
        }

        private static void ReadSlotTypeLossModifiers(
            XlsxRawWorkbook workbook,
            InventoryNumericWorkbookData data,
            InventoryNumericImportReport report)
        {
            const string sheet = "槽位分类修正";
            foreach (var row in ReadRows(
                         workbook,
                         sheet,
                         report,
                         "SlotType",
                         "ItemGroupId",
                         "损耗倍率"))
            {
                data.SlotTypeLossModifiers.Add(
                    new SlotTypeLossModifierNumericRow(sheet, row.Row)
                    {
                        SlotType = Required(row, "SlotType", report),
                        ItemGroupId = Required(row, "ItemGroupId", report),
                        LossRateMultiplier = Float(row, "损耗倍率", report)
                    });
            }
        }

        private static List<RawSheetRow> ReadRows(
            XlsxRawWorkbook workbook,
            string sheet,
            InventoryNumericImportReport report,
            params string[] requiredHeaders)
        {
            if (!workbook.TryGetSheet(sheet, out var rawSheet))
            {
                report.Error($"缺少工作表：{sheet}");
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

            if (requiredHeaders.Any(required => !headers.ContainsKey(required)))
            {
                return new List<RawSheetRow>();
            }

            var result = new List<RawSheetRow>();
            for (var rowIndex = FirstDataRow; rowIndex <= rawSheet.MaxRow; rowIndex++)
            {
                var values = new Dictionary<string, string>(StringComparer.Ordinal);
                var hasValue = false;
                foreach (var pair in headers)
                {
                    var value = rawSheet.GetCell(rowIndex, pair.Value).Trim();
                    values[pair.Key] = value;
                    hasValue |= !string.IsNullOrWhiteSpace(value);
                }

                if (hasValue)
                {
                    result.Add(new RawSheetRow(sheet, rowIndex, values));
                }
            }

            return result;
        }

        private static string Required(
            RawSheetRow row,
            string header,
            InventoryNumericImportReport report)
        {
            var value = Optional(row, header);
            if (string.IsNullOrWhiteSpace(value))
            {
                report.Error($"{header} 不能为空。", row.Sheet, row.Row);
            }

            return value;
        }

        private static string Optional(RawSheetRow row, string header) =>
            row.Values.TryGetValue(header, out var value) ? value.Trim() : string.Empty;

        private static int Integer(
            RawSheetRow row,
            string header,
            InventoryNumericImportReport report)
        {
            var value = Optional(row, header);
            if (int.TryParse(
                    value,
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var result))
            {
                return result;
            }

            if (double.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var number)
                && Math.Abs(number - Math.Round(number)) < 0.00001d)
            {
                return (int)Math.Round(number);
            }

            report.Error($"{header} 必须是整数，当前值：{value}", row.Sheet, row.Row);
            return 0;
        }

        private static float Float(
            RawSheetRow row,
            string header,
            InventoryNumericImportReport report)
        {
            var value = Optional(row, header);
            if (float.TryParse(
                    value,
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture,
                    out var result))
            {
                return result;
            }

            report.Error($"{header} 必须是数字，当前值：{value}", row.Sheet, row.Row);
            return 0f;
        }

        private static bool Boolean(
            RawSheetRow row,
            string header,
            InventoryNumericImportReport report)
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
            public RawSheetRow(
                string sheet,
                int row,
                Dictionary<string, string> values)
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
}
