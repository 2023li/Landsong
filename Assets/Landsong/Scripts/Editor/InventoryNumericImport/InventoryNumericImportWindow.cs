using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Inventory.NumericImport
{
    public sealed class InventoryNumericImportWindow : EditorWindow
    {
        [SerializeField] private string workbookProjectPath = string.Empty;
        [SerializeField] private Vector2 scroll;
        private InventoryNumericImportSession session;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("Landsong/Inventory/库存数值导表工具")]
        public static void Open()
        {
            var window = GetWindow<InventoryNumericImportWindow>("库存数值导表");
            window.minSize = new Vector2(760f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(760f, 560f);
            if (string.IsNullOrWhiteSpace(workbookProjectPath))
            {
                workbookProjectPath = InventoryNumericImportService.DefaultWorkbookProjectPath;
            }
        }

        private void OnGUI()
        {
            DrawHeader();
            DrawSource();
            DrawActions();

            if (!string.IsNullOrWhiteSpace(statusMessage))
            {
                EditorGUILayout.HelpBox(statusMessage, statusType);
            }

            DrawReport();
        }

        private static void DrawHeader()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("库存系统正式数值导表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "库存表只管理物品、物品组和槽位类型的库存规则。" +
                "建筑提供哪种槽位及数量由建筑数值表管理；槽位视觉由 UI 代码按槽位类型解析。" +
                "导入前会完成全表校验和差异预览，只写入真正变化的资产。",
                MessageType.Info);
        }

        private void DrawSource()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("正式源表", EditorStyles.boldLabel);
                var next = EditorGUILayout.TextField(
                    new GUIContent(
                        "项目相对路径",
                        "库存数值表必须位于项目根目录 ConfigSource/库存系统 中。"),
                    workbookProjectPath);
                if (!string.Equals(next, workbookProjectPath, StringComparison.Ordinal))
                {
                    SetWorkbookPath(next);
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("选择 XLSX", GUILayout.Width(100f)))
                    {
                        SelectWorkbook();
                    }

                    if (GUILayout.Button("恢复默认路径", GUILayout.Width(110f)))
                    {
                        SetWorkbookPath(InventoryNumericImportService.DefaultWorkbookProjectPath);
                    }
                }

                if (InventoryNumericImportService.TryResolveWorkbookPath(
                        workbookProjectPath,
                        out var normalizedPath,
                        out var absolutePath,
                        out var pathError))
                {
                    EditorGUILayout.LabelField("规范路径", normalizedPath);
                    EditorGUILayout.LabelField("磁盘路径", absolutePath);
                    EditorGUILayout.LabelField(
                        "磁盘修改时间",
                        File.Exists(absolutePath)
                            ? File.GetLastWriteTime(absolutePath).ToString("yyyy-MM-dd HH:mm:ss")
                            : "文件不存在");
                    if (!File.Exists(absolutePath))
                    {
                        EditorGUILayout.HelpBox(
                            "找不到源表，请确认工作簿已经保存到该路径。",
                            MessageType.Error);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(pathError, MessageType.Error);
                }
            }
        }

        private void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var canOpen = InventoryNumericImportService.TryResolveWorkbookPath(
                                  workbookProjectPath,
                                  out _,
                                  out var absolutePath,
                                  out _)
                              && File.Exists(absolutePath);
                using (new EditorGUI.DisabledScope(!canOpen))
                {
                    if (GUILayout.Button("打开 Excel", GUILayout.Height(28f)))
                    {
                        EditorUtility.OpenWithDefaultApp(absolutePath);
                    }
                }

                if (GUILayout.Button("重新读取并校验", GUILayout.Height(28f)))
                {
                    Analyze();
                }

                using (new EditorGUI.DisabledScope(session == null || !session.HasChanges))
                {
                    if (GUILayout.Button("执行导入", GUILayout.Height(28f)))
                    {
                        Apply();
                    }
                }
            }
        }

        private void DrawReport()
        {
            if (session?.Report == null)
            {
                EditorGUILayout.HelpBox(
                    "点击“重新读取并校验”查看导入影响和错误。",
                    MessageType.None);
                return;
            }

            var report = session.Report;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"校验结果：{report.ErrorCount} 个错误，{report.WarningCount} 个警告，" +
                $"{session.ChangePlan.ChangedAssetCount} 个资产存在实际变化",
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var issue in report.Issues)
            {
                EditorGUILayout.HelpBox(
                    issue.ToString(),
                    issue.Severity == InventoryNumericIssueSeverity.Error
                        ? MessageType.Error
                        : MessageType.Warning);
            }

            if (report.Changes.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("实际差异预览", EditorStyles.boldLabel);
                    foreach (var change in report.Changes)
                    {
                        EditorGUILayout.LabelField("• " + change, EditorStyles.wordWrappedLabel);
                    }
                }
            }
            else if (session.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "当前 Unity 资产已与库存系统数值表同步，无需导入。",
                    MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            session = InventoryNumericImportService.Analyze(workbookProjectPath);
            statusMessage = !session.IsValid
                ? $"校验未通过：{session.Report.ErrorCount} 个错误。未修改任何资产。"
                : session.HasChanges
                    ? $"全表校验通过：{session.ChangePlan.ChangedAssetCount} 个资产存在实际变化，可以执行导入。"
                    : "全表校验通过：当前资产已与工作簿同步。";
            statusType = session.IsValid ? MessageType.Info : MessageType.Error;
        }

        private void SelectWorkbook()
        {
            var defaultDirectory = Path.Combine(
                InventoryNumericImportService.ProjectRootPath,
                "ConfigSource",
                "库存系统");
            if (!Directory.Exists(defaultDirectory))
            {
                defaultDirectory = InventoryNumericImportService.ProjectRootPath;
            }

            var selectedPath = EditorUtility.OpenFilePanel(
                "选择库存系统数值源表",
                defaultDirectory,
                "xlsx");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (!InventoryNumericImportService.TryMakeProjectRelativeWorkbookPath(
                    selectedPath,
                    out var projectRelativePath,
                    out var error))
            {
                EditorUtility.DisplayDialog("无法选择源表", error, "确定");
                return;
            }

            SetWorkbookPath(projectRelativePath);
        }

        private void SetWorkbookPath(string value)
        {
            workbookProjectPath = string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Trim().Replace('\\', '/');
            session = null;
            statusMessage = string.Empty;
        }

        private void Apply()
        {
            if (session == null || !session.HasChanges)
            {
                return;
            }

            var changedAssetCount = session.ChangePlan.ChangedAssetCount;
            if (!EditorUtility.DisplayDialog(
                    "执行库存系统数值导入",
                    $"已完成全表校验和差异比较。\n" +
                    $"本次将更新 {changedAssetCount} 个库存资产，不会读取或改写建筑家族。\n" +
                    "操作支持一次 Undo。是否继续？",
                    "执行导入",
                    "取消"))
            {
                return;
            }

            if (InventoryNumericImportService.Apply(session))
            {
                Analyze();
                statusMessage = "库存系统数值导入完成，并已重新校验。";
                statusType = MessageType.Info;
            }
            else
            {
                statusMessage = "导入失败，请查看 Console；资产已尝试通过 Undo 回滚。";
                statusType = MessageType.Error;
            }
        }
    }
}
