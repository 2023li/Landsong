using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.InventorySystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Inventory.NumericImport
{
    internal sealed class InventoryNumericImportSession
    {
        public InventoryNumericImportSession(
            string projectRelativePath,
            string absolutePath,
            DateTime sourceLastWriteUtc,
            InventoryNumericWorkbookData data,
            InventoryNumericImportReport report,
            Dictionary<string, ItemGroupDefinition> itemGroups,
            Dictionary<string, ItemDefinition> items,
            Dictionary<InventorySlotType, InventorySlotTypeDefinition> slotTypes,
            ItemCatalog itemCatalog,
            InventorySlotTypeCatalog slotTypeCatalog)
        {
            ProjectRelativePath = projectRelativePath;
            AbsolutePath = absolutePath;
            SourceLastWriteUtc = sourceLastWriteUtc;
            Data = data;
            Report = report;
            ItemGroups = itemGroups;
            Items = items;
            SlotTypes = slotTypes;
            ItemCatalog = itemCatalog;
            SlotTypeCatalog = slotTypeCatalog;
        }

        public string ProjectRelativePath { get; }
        public string AbsolutePath { get; }
        public DateTime SourceLastWriteUtc { get; }
        public InventoryNumericWorkbookData Data { get; }
        public InventoryNumericImportReport Report { get; }
        public Dictionary<string, ItemGroupDefinition> ItemGroups { get; }
        public Dictionary<string, ItemDefinition> Items { get; }
        public Dictionary<InventorySlotType, InventorySlotTypeDefinition> SlotTypes { get; }
        public ItemCatalog ItemCatalog { get; }
        public InventorySlotTypeCatalog SlotTypeCatalog { get; internal set; }
        public InventoryNumericImportChangePlan ChangePlan { get; internal set; } =
            new InventoryNumericImportChangePlan();
        public bool IsValid => Data != null && Report != null && !Report.HasErrors;
        public bool HasChanges => IsValid && ChangePlan.HasChanges;
    }

    internal sealed class InventoryNumericImportChangePlan
    {
        private readonly HashSet<string> groupIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> itemIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<InventorySlotType> slotTypes =
            new HashSet<InventorySlotType>();
        private readonly List<string> messages = new List<string>();
        private bool changesItemCatalog;
        private bool changesSlotTypeCatalog;

        public bool HasChanges =>
            groupIds.Count > 0
            || itemIds.Count > 0
            || slotTypes.Count > 0
            || changesItemCatalog
            || changesSlotTypeCatalog;
        public int ChangedAssetCount =>
            groupIds.Count
            + itemIds.Count
            + slotTypes.Count
            + (changesItemCatalog ? 1 : 0)
            + (changesSlotTypeCatalog ? 1 : 0);
        public int ChangedFamilyCount => 0;
        public IReadOnlyList<string> Messages => messages;
        public bool ChangesItemCatalog => changesItemCatalog;
        public bool ChangesSlotTypeCatalog => changesSlotTypeCatalog;

        public bool ChangesGroup(string id) => groupIds.Contains(id);
        public bool ChangesItem(string id) => itemIds.Contains(id);
        public bool ChangesSlotType(InventorySlotType type) => slotTypes.Contains(type);

        public void AddGroup(string id, ItemGroupDefinition current)
        {
            if (!groupIds.Add(id))
            {
                return;
            }

            messages.Add(current == null
                ? $"{id}：创建 ItemGroupDefinition"
                : $"{id}：更新物品组（{AssetDatabase.GetAssetPath(current)}）");
        }

        public void AddItem(string id, ItemDefinition current)
        {
            if (!itemIds.Add(id))
            {
                return;
            }

            messages.Add(current == null
                ? $"{id}：创建 ItemDefinition"
                : $"{id}：更新物品数值（{AssetDatabase.GetAssetPath(current)}）");
        }

        public void AddSlotType(
            InventorySlotType type,
            InventorySlotTypeDefinition current)
        {
            if (!slotTypes.Add(type))
            {
                return;
            }

            messages.Add(current == null
                ? $"{type}：创建 InventorySlotTypeDefinition"
                : $"{type}：更新槽位类型数值（{AssetDatabase.GetAssetPath(current)}）");
        }

        public void AddItemCatalog(ItemCatalog catalog)
        {
            if (changesItemCatalog)
            {
                return;
            }

            changesItemCatalog = true;
            messages.Add($"ItemCatalog：同步正式物品清单（{AssetDatabase.GetAssetPath(catalog)}）");
        }

        public void AddSlotTypeCatalog(InventorySlotTypeCatalog catalog)
        {
            if (changesSlotTypeCatalog)
            {
                return;
            }

            changesSlotTypeCatalog = true;
            messages.Add(catalog == null
                ? "InventorySlotTypeCatalog：创建并同步正式槽位类型清单"
                : $"InventorySlotTypeCatalog：同步正式槽位类型清单（{AssetDatabase.GetAssetPath(catalog)}）");
        }
    }

    internal static class InventoryNumericImportService
    {
        public const string DefaultWorkbookProjectPath =
            "ConfigSource/库存系统/库存系统数值表.xlsx";

        private const string ItemGroupAssetFolder =
            "Assets/Landsong/Objects/SO/ItemGroup";
        private const string ItemAssetFolder =
            "Assets/Landsong/Objects/SO/ItemDef";
        private const string SlotTypeAssetFolder =
            "Assets/Landsong/Objects/SO/InventorySlotType";
        public const string SlotTypeCatalogAssetPath =
            "Assets/Landsong/Objects/SO/InventorySlotTypeCatalog.asset";

        public static string ProjectRootPath =>
            Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static InventoryNumericImportSession Analyze(string projectRelativePath)
        {
            var report = new InventoryNumericImportReport();
            if (!TryResolveWorkbookPath(
                    projectRelativePath,
                    out var normalizedProjectPath,
                    out var absolutePath,
                    out var pathError))
            {
                report.Error(pathError);
                return EmptySession(normalizedProjectPath, report);
            }

            InventoryNumericWorkbookReader.TryRead(absolutePath, report, out var data);
            var groups = FindAssetsByStableId<ItemGroupDefinition, string>(
                "t:ItemGroupDefinition",
                value => value == null ? string.Empty : value.GroupId,
                "物品组",
                report,
                StringComparer.Ordinal);
            var items = FindAssetsByStableId<ItemDefinition, string>(
                "t:ItemDefinition",
                value => value == null ? string.Empty : value.ItemId,
                "物品",
                report,
                StringComparer.Ordinal);
            var slotTypes = FindAssetsByStableId<
                InventorySlotTypeDefinition,
                InventorySlotType>(
                "t:InventorySlotTypeDefinition",
                value => value == null ? default : value.SlotType,
                "槽位类型",
                report,
                EqualityComparer<InventorySlotType>.Default);
            var itemCatalog = FindUniqueAsset<ItemCatalog>(
                "t:ItemCatalog",
                "ItemCatalog",
                report,
                true);
            var slotTypeCatalog = FindUniqueAsset<InventorySlotTypeCatalog>(
                "t:InventorySlotTypeCatalog",
                "InventorySlotTypeCatalog",
                report,
                false);

            var session = new InventoryNumericImportSession(
                normalizedProjectPath,
                absolutePath,
                File.Exists(absolutePath)
                    ? File.GetLastWriteTimeUtc(absolutePath)
                    : DateTime.MinValue,
                data,
                report,
                groups,
                items,
                slotTypes,
                itemCatalog,
                slotTypeCatalog);

            if (data != null)
            {
                Validate(session);
                if (!report.HasErrors)
                {
                    session.ChangePlan = BuildChangePlan(session);
                    foreach (var message in session.ChangePlan.Messages)
                    {
                        report.Change(message);
                    }
                }
            }

            return session;
        }

        public static bool Apply(InventoryNumericImportSession session)
        {
            if (session == null || !session.IsValid)
            {
                return false;
            }

            if (!File.Exists(session.AbsolutePath)
                || File.GetLastWriteTimeUtc(session.AbsolutePath)
                != session.SourceLastWriteUtc)
            {
                Debug.LogError("库存数值表在校验后已发生变化，请重新读取并校验。");
                return false;
            }

            var current = Analyze(session.ProjectRelativePath);
            if (!current.IsValid)
            {
                Debug.LogError("库存数值表重新校验失败，未写入任何资产。");
                return false;
            }

            if (!current.HasChanges)
            {
                return true;
            }

            Undo.IncrementCurrentGroup();
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("导入库存系统数值表");

            try
            {
                EnsureAssetFolder(ItemGroupAssetFolder);
                EnsureAssetFolder(ItemAssetFolder);
                EnsureAssetFolder(SlotTypeAssetFolder);

                EnsureGroupAssets(current);
                EnsureItemAssets(current);
                EnsureSlotTypeAssets(current);
                EnsureSlotTypeCatalog(current);

                ApplyGroups(current);
                ApplyItems(current);
                ApplySlotTypes(current);
                ApplyCatalogs(current);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Undo.CollapseUndoOperations(undoGroup);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                try
                {
                    Undo.RevertAllDownToGroup(undoGroup);
                }
                catch (Exception rollbackException)
                {
                    Debug.LogException(rollbackException);
                }

                return false;
            }
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
                error = "未指定库存数值源表。";
                return false;
            }

            if (Path.IsPathRooted(normalizedProjectPath))
            {
                error = "库存数值源必须填写相对于项目根目录的路径。";
                return false;
            }

            absolutePath = Path.GetFullPath(
                Path.Combine(ProjectRootPath, normalizedProjectPath));
            var allowedRoot = Path.Combine(
                ProjectRootPath,
                "ConfigSource",
                "库存系统");
            if (!IsPathInside(absolutePath, allowedRoot))
            {
                error = "库存系统正式数值表必须位于 ConfigSource/库存系统 中。";
                return false;
            }

            if (!string.Equals(
                    Path.GetExtension(absolutePath),
                    ".xlsx",
                    StringComparison.OrdinalIgnoreCase))
            {
                error = "库存数值源必须是 .xlsx 文件。";
                return false;
            }

            return true;
        }

        public static bool TryMakeProjectRelativeWorkbookPath(
            string absolutePath,
            out string projectRelativePath,
            out string error)
        {
            projectRelativePath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(absolutePath))
            {
                error = "未选择文件。";
                return false;
            }

            var fullPath = Path.GetFullPath(absolutePath);
            var allowedRoot = Path.Combine(
                ProjectRootPath,
                "ConfigSource",
                "库存系统");
            if (!IsPathInside(fullPath, allowedRoot))
            {
                error = "只能选择 ConfigSource/库存系统 目录中的正式工作簿。";
                return false;
            }

            projectRelativePath = NormalizeProjectPath(
                fullPath.Substring(ProjectRootPath.Length));
            return true;
        }

        private static InventoryNumericImportSession EmptySession(
            string projectRelativePath,
            InventoryNumericImportReport report)
        {
            return new InventoryNumericImportSession(
                projectRelativePath,
                string.Empty,
                DateTime.MinValue,
                null,
                report,
                new Dictionary<string, ItemGroupDefinition>(),
                new Dictionary<string, ItemDefinition>(),
                new Dictionary<InventorySlotType, InventorySlotTypeDefinition>(),
                null,
                null);
        }

        private static void Validate(InventoryNumericImportSession session)
        {
            var data = session.Data;
            var report = session.Report;
            ValidateUnique(data.ItemGroups, row => row.GroupId, "GroupId 重复", report);
            ValidateUnique(data.Items, row => row.ItemId, "ItemId 重复", report);
            ValidateUnique(data.SlotTypes, row => row.SlotType, "SlotType 重复", report);
            ValidateUnique(
                data.SlotTypeLossModifiers,
                row => $"{row.SlotType}|{row.ItemGroupId}",
                "槽位分类修正的 SlotType + ItemGroupId 重复",
                report);

            var tableGroupIds = new HashSet<string>(
                data.ItemGroups.Select(row => row.GroupId),
                StringComparer.Ordinal);
            var availableGroupIds = new HashSet<string>(
                session.ItemGroups.Keys.Concat(tableGroupIds),
                StringComparer.Ordinal);
            var parsedSlotTypes = new HashSet<InventorySlotType>();

            foreach (var row in data.ItemGroups)
            {
                if (!string.IsNullOrWhiteSpace(row.ParentGroupId)
                    && !availableGroupIds.Contains(row.ParentGroupId))
                {
                    report.Error(
                        $"找不到 ParentGroupId：{row.ParentGroupId}",
                        row.Sheet,
                        row.Row);
                }

                if (string.Equals(
                        row.GroupId,
                        row.ParentGroupId,
                        StringComparison.Ordinal))
                {
                    report.Error("物品组不能把自己设为父组。", row.Sheet, row.Row);
                }

                ValidateOptionalAssetPath<Sprite>(
                    row.IconAssetPath,
                    "IconAssetPath",
                    row,
                    report);
            }

            ValidateGroupCycles(data.ItemGroups, report);

            foreach (var row in data.Items)
            {
                if (!TryParseEnum(row.Category, out ItemCategory _))
                {
                    report.Error(
                        $"未知物品分类：{row.Category}。可选值：{string.Join("、", Enum.GetNames(typeof(ItemCategory)))}",
                        row.Sheet,
                        row.Row);
                }
                if (row.MaxStackSize < 1)
                {
                    report.Error("堆叠上限必须至少为 1。", row.Sheet, row.Row);
                }
                if (!row.Stackable && row.MaxStackSize != 1)
                {
                    report.Warning("不可堆叠物品的堆叠上限会规范化为 1。", row.Sheet, row.Row);
                }
                if (row.BaseValue < 0)
                {
                    report.Error("基础价值不能小于 0。", row.Sheet, row.Row);
                }
                if (row.LossRatePerTurn < 0f || row.LossRatePerTurn > 1f)
                {
                    report.Error("每回合损耗率必须位于 0 到 1。", row.Sheet, row.Row);
                }
                if (row.NutritionValue < 0f)
                {
                    report.Error("营养值不能小于 0。", row.Sheet, row.Row);
                }
                if (row.DietQuality < 0f || row.DietQuality > 100f)
                {
                    report.Error("饮食质量必须位于 0 到 100。", row.Sheet, row.Row);
                }

                foreach (var groupId in SplitKeys(row.ItemGroupIds))
                {
                    if (!availableGroupIds.Contains(groupId))
                    {
                        report.Error(
                            $"找不到 ItemGroupId：{groupId}",
                            row.Sheet,
                            row.Row);
                    }
                }

                ValidateOptionalAssetPath<Sprite>(
                    row.IconAssetPath,
                    "IconAssetPath",
                    row,
                    report);
            }

            foreach (var row in data.SlotTypes)
            {
                if (!TryParseEnum(row.SlotType, out InventorySlotType slotType))
                {
                    report.Error(
                        $"未知 SlotType：{row.SlotType}。可选值：{string.Join("、", Enum.GetNames(typeof(InventorySlotType)))}",
                        row.Sheet,
                        row.Row);
                    continue;
                }

                parsedSlotTypes.Add(slotType);
                if (row.BaseLossRateMultiplier < 0f)
                {
                    report.Error("基础损耗倍率不能小于 0。", row.Sheet, row.Row);
                }
            }

            foreach (var slotType in Enum.GetValues(typeof(InventorySlotType))
                         .Cast<InventorySlotType>())
            {
                if (!parsedSlotTypes.Contains(slotType))
                {
                    report.Error($"槽位类型表缺少枚举值：{slotType}");
                }
            }

            foreach (var row in data.SlotTypeLossModifiers)
            {
                if (!TryParseEnum(row.SlotType, out InventorySlotType slotType)
                    || !parsedSlotTypes.Contains(slotType))
                {
                    report.Error(
                        $"槽位分类修正引用了未知 SlotType：{row.SlotType}",
                        row.Sheet,
                        row.Row);
                }
                if (!availableGroupIds.Contains(row.ItemGroupId))
                {
                    report.Error(
                        $"找不到 ItemGroupId：{row.ItemGroupId}",
                        row.Sheet,
                        row.Row);
                }
                if (row.LossRateMultiplier < 0f)
                {
                    report.Error("损耗倍率不能小于 0。", row.Sheet, row.Row);
                }
            }

            foreach (var groupId in session.ItemGroups.Keys)
            {
                if (!tableGroupIds.Contains(groupId))
                {
                    report.Error($"正式表缺少现有物品组：{groupId}");
                }
            }

            var tableItemIds = new HashSet<string>(
                data.Items.Select(row => row.ItemId),
                StringComparer.Ordinal);
            foreach (var itemId in session.Items.Keys)
            {
                if (!tableItemIds.Contains(itemId))
                {
                    report.Error($"正式表缺少现有物品：{itemId}");
                }
            }
        }

        private static InventoryNumericImportChangePlan BuildChangePlan(
            InventoryNumericImportSession session)
        {
            var plan = new InventoryNumericImportChangePlan();
            foreach (var row in session.Data.ItemGroups)
            {
                session.ItemGroups.TryGetValue(row.GroupId, out var current);
                if (!DoesGroupMatch(session, row, current))
                {
                    plan.AddGroup(row.GroupId, current);
                }
            }

            foreach (var row in session.Data.Items)
            {
                session.Items.TryGetValue(row.ItemId, out var current);
                if (!DoesItemMatch(session, row, current))
                {
                    plan.AddItem(row.ItemId, current);
                }
            }

            foreach (var row in session.Data.SlotTypes)
            {
                TryParseEnum(row.SlotType, out InventorySlotType slotType);
                session.SlotTypes.TryGetValue(slotType, out var current);
                if (!DoesSlotTypeMatch(session, row, current))
                {
                    plan.AddSlotType(slotType, current);
                }
            }

            if (!DoesItemCatalogMatch(session))
            {
                plan.AddItemCatalog(session.ItemCatalog);
            }
            if (!DoesSlotTypeCatalogMatch(session))
            {
                plan.AddSlotTypeCatalog(session.SlotTypeCatalog);
            }

            return plan;
        }

        private static bool DoesGroupMatch(
            InventoryNumericImportSession session,
            ItemGroupNumericRow row,
            ItemGroupDefinition current)
        {
            if (current == null)
            {
                return false;
            }

            ItemGroupDefinition expectedParent = null;
            if (!string.IsNullOrWhiteSpace(row.ParentGroupId)
                && !session.ItemGroups.TryGetValue(row.ParentGroupId, out expectedParent))
            {
                // 父组尚未创建时，当前资产一定不符合正式表，确保 Apply 阶段会回填父子关系。
                return false;
            }
            return string.Equals(current.GroupId, row.GroupId, StringComparison.Ordinal)
                   && string.Equals(
                       current.DisplayName,
                       row.DisplayName,
                       StringComparison.Ordinal)
                   && string.Equals(
                       current.Description ?? string.Empty,
                       row.Description ?? string.Empty,
                       StringComparison.Ordinal)
                   && ReferenceEquals(current.ParentGroup, expectedParent)
                   && ReferenceEquals(
                       current.Icon,
                       ResolveOptionalAsset(row.IconAssetPath, current.Icon));
        }

        private static bool DoesItemMatch(
            InventoryNumericImportSession session,
            ItemNumericRow row,
            ItemDefinition current)
        {
            if (current == null
                || !TryParseEnum(row.Category, out ItemCategory category))
            {
                return false;
            }

            var expectedGroups = SplitKeys(row.ItemGroupIds)
                .Select(groupId => session.ItemGroups.GetValueOrDefault(groupId))
                .ToArray();
            var currentGroupIds = current.ItemGroups
                .Select(group => group == null ? string.Empty : group.GroupId)
                .ToArray();
            var expectedGroupIds = expectedGroups
                .Select(group => group == null ? string.Empty : group.GroupId)
                .ToArray();
            return string.Equals(current.ItemId, row.ItemId, StringComparison.Ordinal)
                   && string.Equals(
                       current.DisplayName,
                       row.DisplayName,
                       StringComparison.Ordinal)
                   && string.Equals(
                       current.Description ?? string.Empty,
                       row.Description ?? string.Empty,
                       StringComparison.Ordinal)
                   && current.Category == category
                   && current.Stackable == row.Stackable
                   && current.MaxStackSize == (row.Stackable ? row.MaxStackSize : 1)
                   && current.BaseValue == row.BaseValue
                   && string.Equals(
                       current.AddressableKey ?? string.Empty,
                       row.AddressableKey ?? string.Empty,
                       StringComparison.Ordinal)
                   && AreStringsEqual(current.Tags, SplitKeys(row.Tags))
                   && Mathf.Approximately(current.LossRatePerTurn, row.LossRatePerTurn)
                   && AreStringsEqual(currentGroupIds, expectedGroupIds)
                   && current.FoodProfile.IsFood == row.IsFood
                   && Mathf.Approximately(
                       current.FoodProfile.NutritionValue,
                       row.NutritionValue)
                   && Mathf.Approximately(
                       current.FoodProfile.DietQuality,
                       row.DietQuality)
                   && ReferenceEquals(
                       current.Icon,
                       ResolveOptionalAsset(row.IconAssetPath, current.Icon));
        }

        private static bool DoesSlotTypeMatch(
            InventoryNumericImportSession session,
            SlotTypeNumericRow row,
            InventorySlotTypeDefinition current)
        {
            if (current == null
                || !TryParseEnum(row.SlotType, out InventorySlotType slotType))
            {
                return false;
            }

            return current.SlotType == slotType
                   && string.Equals(
                       current.DisplayName,
                       row.DisplayName,
                       StringComparison.Ordinal)
                   && Mathf.Approximately(
                       current.BaseLossRateMultiplier,
                       row.BaseLossRateMultiplier)
                   && AreLossModifiersEqual(
                       current.LossModifiers,
                       BuildLossModifiers(session, slotType));
        }

        private static bool DoesItemCatalogMatch(InventoryNumericImportSession session)
        {
            if (session.ItemCatalog == null
                || session.ItemCatalog.Definitions.Count != session.Data.Items.Count)
            {
                return false;
            }

            for (var i = 0; i < session.Data.Items.Count; i++)
            {
                if (!ReferenceEquals(
                        session.ItemCatalog.Definitions[i],
                        session.Items.GetValueOrDefault(session.Data.Items[i].ItemId)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool DoesSlotTypeCatalogMatch(
            InventoryNumericImportSession session)
        {
            if (session.SlotTypeCatalog == null
                || session.SlotTypeCatalog.Definitions.Count
                != session.Data.SlotTypes.Count)
            {
                return false;
            }

            for (var i = 0; i < session.Data.SlotTypes.Count; i++)
            {
                TryParseEnum(
                    session.Data.SlotTypes[i].SlotType,
                    out InventorySlotType slotType);
                if (!ReferenceEquals(
                        session.SlotTypeCatalog.Definitions[i],
                        session.SlotTypes.GetValueOrDefault(slotType)))
                {
                    return false;
                }
            }

            return true;
        }

        private static void EnsureGroupAssets(InventoryNumericImportSession session)
        {
            foreach (var row in session.Data.ItemGroups)
            {
                if (session.ItemGroups.ContainsKey(row.GroupId))
                {
                    continue;
                }

                var created = ScriptableObject.CreateInstance<ItemGroupDefinition>();
                created.name = $"ItemGroupDefinition_{row.DisplayName}";
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{ItemGroupAssetFolder}/ItemGroupDefinition_{SanitizeFileName(row.DisplayName)}.asset");
                AssetDatabase.CreateAsset(created, path);
                Undo.RegisterCreatedObjectUndo(created, "创建物品组资产");
                session.ItemGroups.Add(row.GroupId, created);
            }
        }

        private static void EnsureItemAssets(InventoryNumericImportSession session)
        {
            foreach (var row in session.Data.Items)
            {
                if (session.Items.ContainsKey(row.ItemId))
                {
                    continue;
                }

                var created = ScriptableObject.CreateInstance<ItemDefinition>();
                created.name = $"ItemDefinition_{row.DisplayName}";
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{ItemAssetFolder}/ItemDefinition_{SanitizeFileName(row.DisplayName)}.asset");
                AssetDatabase.CreateAsset(created, path);
                Undo.RegisterCreatedObjectUndo(created, "创建物品资产");
                session.Items.Add(row.ItemId, created);
            }
        }

        private static void EnsureSlotTypeAssets(InventoryNumericImportSession session)
        {
            foreach (var row in session.Data.SlotTypes)
            {
                TryParseEnum(row.SlotType, out InventorySlotType slotType);
                if (session.SlotTypes.ContainsKey(slotType))
                {
                    continue;
                }

                var created =
                    ScriptableObject.CreateInstance<InventorySlotTypeDefinition>();
                created.name = $"InventorySlotType_{slotType}";
                var path = AssetDatabase.GenerateUniqueAssetPath(
                    $"{SlotTypeAssetFolder}/InventorySlotType_{slotType}.asset");
                AssetDatabase.CreateAsset(created, path);
                Undo.RegisterCreatedObjectUndo(created, "创建库存槽位类型资产");
                session.SlotTypes.Add(slotType, created);
            }
        }

        private static void EnsureSlotTypeCatalog(InventoryNumericImportSession session)
        {
            if (session.SlotTypeCatalog != null)
            {
                return;
            }

            var created =
                ScriptableObject.CreateInstance<InventorySlotTypeCatalog>();
            created.name = "InventorySlotTypeCatalog";
            AssetDatabase.CreateAsset(created, SlotTypeCatalogAssetPath);
            Undo.RegisterCreatedObjectUndo(created, "创建库存槽位类型目录");
            session.SlotTypeCatalog = created;
        }

        private static void ApplyGroups(InventoryNumericImportSession session)
        {
            foreach (var row in session.Data.ItemGroups)
            {
                if (!session.ChangePlan.ChangesGroup(row.GroupId))
                {
                    continue;
                }

                var definition = session.ItemGroups[row.GroupId];
                Undo.RecordObject(definition, "导入物品组数值");
                definition.Configure(
                    row.GroupId,
                    row.DisplayName,
                    row.Description,
                    ResolveOptionalAsset(row.IconAssetPath, definition.Icon),
                    string.IsNullOrWhiteSpace(row.ParentGroupId)
                        ? null
                        : session.ItemGroups[row.ParentGroupId]);
                EditorUtility.SetDirty(definition);
            }
        }

        private static void ApplyItems(InventoryNumericImportSession session)
        {
            foreach (var row in session.Data.Items)
            {
                if (!session.ChangePlan.ChangesItem(row.ItemId))
                {
                    continue;
                }

                var definition = session.Items[row.ItemId];
                Undo.RecordObject(definition, "导入物品数值");
                TryParseEnum(row.Category, out ItemCategory category);
                definition.ConfigureNumericData(
                    ResolveOptionalAsset(row.IconAssetPath, definition.Icon),
                    row.ItemId,
                    row.DisplayName,
                    row.Description,
                    category,
                    row.Stackable,
                    row.MaxStackSize,
                    row.BaseValue,
                    row.AddressableKey,
                    SplitKeys(row.Tags),
                    row.LossRatePerTurn,
                    SplitKeys(row.ItemGroupIds)
                        .Select(groupId => session.ItemGroups[groupId]),
                    new ItemFoodProfile(
                        row.IsFood,
                        row.NutritionValue,
                        row.DietQuality));
                EditorUtility.SetDirty(definition);
            }
        }

        private static void ApplySlotTypes(InventoryNumericImportSession session)
        {
            foreach (var row in session.Data.SlotTypes)
            {
                TryParseEnum(row.SlotType, out InventorySlotType slotType);
                if (!session.ChangePlan.ChangesSlotType(slotType))
                {
                    continue;
                }

                var definition = session.SlotTypes[slotType];
                Undo.RecordObject(definition, "导入库存槽位类型数值");
                definition.Configure(
                    slotType,
                    row.DisplayName,
                    row.BaseLossRateMultiplier,
                    BuildLossModifiers(session, slotType));
                EditorUtility.SetDirty(definition);
            }
        }

        private static void ApplyCatalogs(InventoryNumericImportSession session)
        {
            if (session.ChangePlan.ChangesItemCatalog)
            {
                Undo.RecordObject(session.ItemCatalog, "同步物品目录");
                session.ItemCatalog.ConfigureDefinitions(
                    session.Data.Items.Select(row => session.Items[row.ItemId]));
                EditorUtility.SetDirty(session.ItemCatalog);
            }

            if (session.ChangePlan.ChangesSlotTypeCatalog)
            {
                Undo.RecordObject(session.SlotTypeCatalog, "同步库存槽位类型目录");
                session.SlotTypeCatalog.ConfigureDefinitions(
                    session.Data.SlotTypes.Select(row =>
                    {
                        TryParseEnum(row.SlotType, out InventorySlotType slotType);
                        return session.SlotTypes[slotType];
                    }));
                EditorUtility.SetDirty(session.SlotTypeCatalog);
            }
        }

        private static InventorySlotLossModifier[] BuildLossModifiers(
            InventoryNumericImportSession session,
            InventorySlotType slotType)
        {
            return session.Data.SlotTypeLossModifiers
                .Where(row =>
                    TryParseEnum(row.SlotType, out InventorySlotType rowSlotType)
                    && rowSlotType == slotType)
                .Select(row => new InventorySlotLossModifier(
                    session.ItemGroups[row.ItemGroupId],
                    row.LossRateMultiplier))
                .ToArray();
        }

        private static void ValidateGroupCycles(
            IReadOnlyList<ItemGroupNumericRow> rows,
            InventoryNumericImportReport report)
        {
            var parentById = rows.ToDictionary(
                row => row.GroupId,
                row => row.ParentGroupId,
                StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var visited = new HashSet<string>(StringComparer.Ordinal);
                var current = row.GroupId;
                while (!string.IsNullOrWhiteSpace(current)
                       && parentById.TryGetValue(current, out var parent))
                {
                    if (!visited.Add(current))
                    {
                        report.Error(
                            $"物品组父子关系形成循环：{row.GroupId}",
                            row.Sheet,
                            row.Row);
                        break;
                    }

                    current = parent;
                }
            }
        }

        private static T ResolveOptionalAsset<T>(string assetPath, T current)
            where T : UnityEngine.Object
        {
            var normalized = NormalizeAssetPath(assetPath);
            return string.IsNullOrWhiteSpace(normalized)
                ? current
                : AssetDatabase.LoadAssetAtPath<T>(normalized);
        }

        private static void ValidateOptionalAssetPath<T>(
            string assetPath,
            string label,
            InventoryNumericSourceRow row,
            InventoryNumericImportReport report)
            where T : UnityEngine.Object
        {
            var normalized = NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return;
            }

            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal)
                || AssetDatabase.LoadAssetAtPath<T>(normalized) == null)
            {
                report.Error(
                    $"{label} 找不到 {typeof(T).Name} 资产：{assetPath}",
                    row.Sheet,
                    row.Row);
            }
        }

        private static Dictionary<TKey, TAsset> FindAssetsByStableId<TAsset, TKey>(
            string filter,
            Func<TAsset, TKey> idSelector,
            string label,
            InventoryNumericImportReport report,
            IEqualityComparer<TKey> comparer)
            where TAsset : UnityEngine.Object
        {
            var result = new Dictionary<TKey, TAsset>(comparer);
            foreach (var guid in AssetDatabase.FindAssets(filter))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<TAsset>(path);
                if (asset == null)
                {
                    continue;
                }

                var id = idSelector(asset);
                if (result.TryGetValue(id, out var existing))
                {
                    report.Error(
                        $"{label}稳定 ID 重复：{id}\n- {AssetDatabase.GetAssetPath(existing)}\n- {path}");
                    continue;
                }

                result.Add(id, asset);
            }

            return result;
        }

        private static T FindUniqueAsset<T>(
            string filter,
            string label,
            InventoryNumericImportReport report,
            bool required)
            where T : UnityEngine.Object
        {
            var paths = AssetDatabase.FindAssets(filter)
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToArray();
            if (paths.Length == 0)
            {
                if (required)
                {
                    report.Error($"找不到 {label} 资产。");
                }
                return null;
            }

            if (paths.Length > 1)
            {
                report.Error($"{label} 资产必须唯一，当前找到 {paths.Length} 个。");
            }

            return AssetDatabase.LoadAssetAtPath<T>(paths[0]);
        }

        private static void ValidateUnique<TRow>(
            IEnumerable<TRow> rows,
            Func<TRow, string> keySelector,
            string message,
            InventoryNumericImportReport report)
            where TRow : InventoryNumericSourceRow
        {
            var seen = new Dictionary<string, TRow>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                var key = keySelector(row)?.Trim() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                if (seen.TryGetValue(key, out var previous))
                {
                    report.Error(
                        $"{message}：{key}，首次出现于 {previous.Sheet}!{previous.Row}",
                        row.Sheet,
                        row.Row);
                }
                else
                {
                    seen.Add(key, row);
                }
            }
        }

        private static bool TryParseEnum<T>(string value, out T result)
            where T : struct
        {
            return Enum.TryParse(value?.Trim(), true, out result)
                   && Enum.IsDefined(typeof(T), result);
        }

        private static bool AreStringsEqual(
            IReadOnlyList<string> left,
            IReadOnlyList<string> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool AreLossModifiersEqual(
            IReadOnlyList<InventorySlotLossModifier> left,
            IReadOnlyList<InventorySlotLossModifier> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (var i = 0; i < left.Count; i++)
            {
                var leftId = left[i].ItemGroup == null
                    ? string.Empty
                    : left[i].ItemGroup.GroupId;
                var rightId = right[i].ItemGroup == null
                    ? string.Empty
                    : right[i].ItemGroup.GroupId;
                if (!string.Equals(leftId, rightId, StringComparison.Ordinal)
                    || !Mathf.Approximately(
                        left[i].LossRateMultiplier,
                        right[i].LossRateMultiplier))
                {
                    return false;
                }
            }

            return true;
        }

        private static string[] SplitKeys(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Array.Empty<string>();
            }

            return value
                .Split(
                    new[] { ';', '；', ',', '，', '\n', '\r' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(item => item.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }

        private static string NormalizeProjectPath(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/').TrimStart('/');

        private static string NormalizeAssetPath(string value) =>
            string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');

        private static bool IsPathInside(string path, string directory)
        {
            var normalizedPath = Path.GetFullPath(path)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            var normalizedDirectory = Path.GetFullPath(directory)
                .TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
            return normalizedPath.StartsWith(
                normalizedDirectory + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        private static void EnsureAssetFolder(string folder)
        {
            var normalized = folder.Replace('\\', '/').TrimEnd('/');
            if (AssetDatabase.IsValidFolder(normalized))
            {
                return;
            }

            var segments = normalized.Split('/');
            var current = segments[0];
            for (var i = 1; i < segments.Length; i++)
            {
                var next = $"{current}/{segments[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[i]);
                }
                current = next;
            }
        }

        private static string SanitizeFileName(string value)
        {
            var result = string.IsNullOrWhiteSpace(value) ? "Unnamed" : value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                result = result.Replace(invalid, '_');
            }

            return result;
        }
    }
}
