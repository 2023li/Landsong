using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Buildings.NumericImport
{
    public sealed class BuildingNumericImportWindow : EditorWindow
    {
        [SerializeField] private string workbookProjectPath = string.Empty;
        [SerializeField] private Vector2 scroll;
        private BuildingNumericImportSession session;
        private string statusMessage = string.Empty;
        private MessageType statusType = MessageType.Info;

        [MenuItem("Landsong/Building/建筑数值导表工具")]
        public static void Open()
        {
            var window = GetWindow<BuildingNumericImportWindow>("建筑数值导表");
            window.minSize = new Vector2(760f, 560f);
            window.Show();
        }

        private void OnEnable()
        {
            minSize = new Vector2(760f, 560f);
            if (string.IsNullOrWhiteSpace(workbookProjectPath))
            {
                workbookProjectPath = BuildingNumericImportService.DefaultWorkbookProjectPath;
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
            EditorGUILayout.LabelField("建筑正式数值导表", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Excel 是策划源，Unity Family / LevelConfiguration / ModuleSet / Runtime Prefab 是生成后的运行时资产。" +
                "导入会先读取并校验整张工作簿；任一错误都会阻止全部写入。Prefab 只更新资源点、优先级和行动力三个数据字段，不改结构与表现。",
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
                        "正式源表必须位于项目根目录 ConfigSource 中，不进入 Unity AssetDatabase。"),
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
                        SetWorkbookPath(BuildingNumericImportService.DefaultWorkbookProjectPath);
                    }
                }

                if (BuildingNumericImportService.TryResolveWorkbookPath(
                        workbookProjectPath,
                        out var normalizedPath,
                        out var absolutePath,
                        out var pathError))
                {
                    EditorGUILayout.LabelField("规范路径", normalizedPath);
                    EditorGUILayout.LabelField("磁盘路径", absolutePath);
                    EditorGUILayout.LabelField("磁盘修改时间", File.Exists(absolutePath)
                        ? File.GetLastWriteTime(absolutePath).ToString("yyyy-MM-dd HH:mm:ss")
                        : "文件不存在");
                    if (!File.Exists(absolutePath))
                    {
                        EditorGUILayout.HelpBox("找不到源表；请确认文件已经保存到该路径。", MessageType.Error);
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
                var canOpen = BuildingNumericImportService.TryResolveWorkbookPath(
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

                using (new EditorGUI.DisabledScope(session == null || !session.IsValid))
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
                EditorGUILayout.HelpBox("点击“重新读取并校验”查看导入影响和错误。", MessageType.None);
                return;
            }

            var report = session.Report;
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                $"校验结果：{report.ErrorCount} 个错误，{report.WarningCount} 个警告，{report.Changes.Count} 项影响",
                EditorStyles.boldLabel);

            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var issue in report.Issues)
            {
                EditorGUILayout.HelpBox(
                    issue.ToString(),
                    issue.Severity == BuildingNumericIssueSeverity.Error ? MessageType.Error : MessageType.Warning);
            }

            if (report.Changes.Count > 0)
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("导入影响预览", EditorStyles.boldLabel);
                    foreach (var change in report.Changes)
                    {
                        EditorGUILayout.LabelField("• " + change, EditorStyles.wordWrappedLabel);
                    }
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void Analyze()
        {
            session = BuildingNumericImportService.Analyze(workbookProjectPath);
            statusMessage = session.IsValid
                ? "全表校验通过，可以执行导入。"
                : $"校验未通过：{session.Report.ErrorCount} 个错误。未修改任何资产。";
            statusType = session.IsValid ? MessageType.Info : MessageType.Error;
        }

        private void SelectWorkbook()
        {
            var defaultDirectory = Path.Combine(
                BuildingNumericImportService.ProjectRootPath,
                "ConfigSource",
                "Buildings");
            if (!Directory.Exists(defaultDirectory))
            {
                defaultDirectory = BuildingNumericImportService.ProjectRootPath;
            }

            var selectedPath = EditorUtility.OpenFilePanel("选择建筑数值源表", defaultDirectory, "xlsx");
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                return;
            }

            if (!BuildingNumericImportService.TryMakeProjectRelativeWorkbookPath(
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
            if (session == null || !session.IsValid)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "执行建筑数值导入",
                    $"将按 Excel 覆盖 {session.Data.Families.Count} 个建筑家族的正式数值。\n" +
                    "已通过全表校验，操作支持一次 Undo。是否继续？",
                    "执行导入",
                    "取消"))
            {
                return;
            }

            if (BuildingNumericImportService.Apply(session))
            {
                statusMessage = $"导入完成：已更新 {session.Data.Families.Count} 个建筑家族。";
                statusType = MessageType.Info;
                Analyze();
                BuildingAuthoringWindow.RepaintOpenWindow();
            }
            else
            {
                statusMessage = "导入失败。请查看错误与 Console；若自动回滚自身报错，需要立即检查影响预览中的资产。";
                statusType = MessageType.Error;
            }
        }
    }
}
