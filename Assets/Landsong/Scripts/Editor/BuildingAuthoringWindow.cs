using System;
using System.Collections.Generic;
using System.IO;
using Landsong.BuildingSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools.Buildings
{
    /// <summary>
    /// 建筑终态架构的唯一策划入口。负责创建完整资产组，并维护已经登记的建筑家族。
    /// </summary>
    public sealed class BuildingAuthoringWindow : EditorWindow
    {
        private const string CatalogPath = "Assets/Landsong/Objects/SO/BuildingCatalog.asset";
        private const string DocumentationRoot = "Document/建筑系统/";
        private const string AuthoringDocumentPath = DocumentationRoot + "建筑编辑器窗口规划与使用.md";
        private const string NumericDocumentPath = DocumentationRoot + "建筑数值策划表.md";
        private const string NumericWorkbookPath =
            "ConfigSource/Buildings/建筑数值策划表.xlsx";

        [SerializeField] private BuildingCatalog catalog;
        [SerializeField] private BuildingAuthoringDraft draft = new BuildingAuthoringDraft();
        [SerializeField] private BuildingFamilyDefinition selectedFamily;
        [SerializeField] private int selectedTab;
        [SerializeField] private string familySearch = string.Empty;
        [SerializeField] private bool showAdvancedPaths;
        [SerializeField] private bool showModuleEditor = true;
        [SerializeField] private bool showPresentationEditor = true;

        private Vector2 createScroll;
        private Vector2 familyListScroll;
        private Vector2 familyEditorScroll;
        private SerializedObject windowSerializedObject;
        private UnityEditor.Editor familyEditor;
        private UnityEditor.Editor moduleEditor;
        private UnityEditor.Editor presentationEditor;
        private readonly List<string> lastValidationErrors = new List<string>();
        private string transientMessage = string.Empty;
        private MessageType transientMessageType = MessageType.Info;

        [MenuItem("Landsong/Building/建筑编辑器")]
        public static void Open()
        {
            var window = GetWindow<BuildingAuthoringWindow>("Landsong 建筑");
            window.minSize = new Vector2(900f, 650f);
            window.Show();
        }

        internal static void RepaintOpenWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<BuildingAuthoringWindow>();
            for (var i = 0; i < windows.Length; i++)
            {
                windows[i].Repaint();
            }
        }

        private void OnEnable()
        {
            minSize = new Vector2(900f, 650f);
            draft ??= new BuildingAuthoringDraft();
            catalog ??= AssetDatabase.LoadAssetAtPath<BuildingCatalog>(CatalogPath);
            windowSerializedObject = new SerializedObject(this);
            EditorApplication.projectChanged += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= Repaint;
            DestroyCachedEditors();
        }

        private void OnGUI()
        {
            DrawHeader();

            selectedTab = GUILayout.Toolbar(
                selectedTab,
                new[] { "创建建筑", "编辑现有家族" },
                GUILayout.Height(28f));
            EditorGUILayout.Space(4f);

            if (selectedTab == 0)
            {
                DrawCreateTab();
            }
            else
            {
                DrawEditTab();
            }
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("建筑终态架构工作台", EditorStyles.boldLabel, GUILayout.Width(160f));
                GUILayout.Label(
                    new GUIContent(
                        "Catalog",
                        "正式建筑家族的唯一目录。终态架构要求使用标准 BuildingCatalog.asset。"),
                    EditorStyles.miniLabel,
                    GUILayout.Width(48f));
                var nextCatalog = (BuildingCatalog)EditorGUILayout.ObjectField(
                    catalog,
                    typeof(BuildingCatalog),
                    false,
                    GUILayout.MinWidth(220f));
                if (nextCatalog != catalog)
                {
                    catalog = nextCatalog;
                    selectedFamily = null;
                    DestroyCachedEditors();
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("保存资产", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    AssetDatabase.SaveAssets();
                    transientMessage = "已保存建筑资产。";
                    transientMessageType = MessageType.Info;
                }

                if (GUILayout.Button("架构校验", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    ExecuteArchitectureValidation();
                }

                if (GUILayout.Button("使用文档", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    OpenProjectDocument(AuthoringDocumentPath);
                }

                if (GUILayout.Button("数值文档", EditorStyles.toolbarButton, GUILayout.Width(70f)))
                {
                    OpenProjectDocument(NumericDocumentPath);
                }

                if (GUILayout.Button("数值表", EditorStyles.toolbarButton, GUILayout.Width(58f)))
                {
                    OpenProjectDocument(NumericWorkbookPath);
                }

                if (GUILayout.Button("导表", EditorStyles.toolbarButton, GUILayout.Width(46f)))
                {
                    NumericImport.BuildingNumericImportWindow.Open();
                }
            }

            if (catalog == null)
            {
                EditorGUILayout.HelpBox(
                    $"未找到标准 BuildingCatalog，请绑定：{CatalogPath}",
                    MessageType.Error);
            }
            else
            {
                var path = AssetDatabase.GetAssetPath(catalog);
                if (!string.Equals(path, CatalogPath, StringComparison.Ordinal))
                {
                    EditorGUILayout.HelpBox(
                        $"终态架构只允许登记到标准 Catalog：{CatalogPath}\n当前：{path}",
                        MessageType.Error);
                }
            }

            if (!string.IsNullOrWhiteSpace(transientMessage))
            {
                EditorGUILayout.HelpBox(transientMessage, transientMessageType);
            }
        }

        private void DrawCreateTab()
        {
            windowSerializedObject ??= new SerializedObject(this);
            windowSerializedObject.Update();
            var draftProperty = windowSerializedObject.FindProperty("draft");
            createScroll = EditorGUILayout.BeginScrollView(createScroll);

            DrawWorkflowSummary();
            DrawIdentitySection(draftProperty);
            DrawPlacementSection(draftProperty);
            DrawConstructionAndLevelSection(draftProperty);
            DrawPresentationSection(draftProperty);
            DrawWorkforceProductionSection(draftProperty);
            DrawAdvancedPathSection(draftProperty);

            windowSerializedObject.ApplyModifiedProperties();
            DrawPathPreview();
            DrawDraftValidationResult();
            DrawCreateActions();

            EditorGUILayout.EndScrollView();
        }

        private static void DrawWorkflowSummary()
        {
            EditorGUILayout.HelpBox(
                "一次创建：Family + ModuleSet + Presentation + 唯一 Runtime Prefab + Catalog 登记。" +
                "所有普通建筑直接使用统一 BuildingBase，不生成家族专属脚本，也没有编译续建阶段。" +
                "任何已存在的目标资产都不会被覆盖。",
                MessageType.Info);
        }

        private void DrawIdentitySection(SerializedProperty draftProperty)
        {
            BeginSection("1. 身份与模块模板");
            DrawProperty(draftProperty, "FamilyId");
            DrawProperty(draftProperty, "DisplayName");
            DrawProperty(draftProperty, "AssetName");
            DrawProperty(draftProperty, "ModuleTemplate");

            DrawProperty(draftProperty, "Category");
            DrawProperty(draftProperty, "Icon");
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("应用所选模板默认值", GUILayout.Width(180f)))
                {
                    windowSerializedObject.ApplyModifiedProperties();
                    draft.ApplyTemplateDefaults();
                    windowSerializedObject.Update();
                }
            }
            EndSection();
        }

        private static void DrawPlacementSection(SerializedProperty draftProperty)
        {
            BeginSection("2. 放置与地图规则");
            DrawProperty(draftProperty, "Footprint");
            var ignoreTerrain = DrawProperty(draftProperty, "IgnoreTerrainRequirement");
            if (!ignoreTerrain.boolValue)
            {
                DrawProperty(draftProperty, "RequiredTerrainKeys", true);
                DrawProperty(draftProperty, "RequiredAnyFootprintTerrainKeys", true);
            }

            DrawProperty(draftProperty, "MovementResistance");
            DrawProperty(draftProperty, "PlacementCosts", true);
            DrawProperty(draftProperty, "BlueprintInitiallyLocked");
            DrawProperty(draftProperty, "HideWhenBlueprintLocked");
            DrawProperty(draftProperty, "BuildMenuSortOrder");
            DrawProperty(draftProperty, "MaxBuildCount");
            DrawProperty(draftProperty, "IsDevelopmentCompleted");
            DrawProperty(draftProperty, "IsResourceProviderPoint");
            DrawProperty(draftProperty, "ResourceProviderPriority");
            DrawProperty(draftProperty, "BuildingActionPower");
            EndSection();
        }

        private static void DrawConstructionAndLevelSection(SerializedProperty draftProperty)
        {
            BeginSection("3. 施工与等级");
            var modeProperty = DrawProperty(draftProperty, "ConstructionViewMode");
            var mode = (BuildingConstructionViewMode)modeProperty.intValue;
            EditorGUILayout.HelpBox(
                mode == BuildingConstructionViewMode.Single
                    ? "施工是独立阶段。当前选择单一施工视图：整个施工期保持同一个 View；每回合仍可分别填写消耗与产出。运营等级始终从 LV1 开始。"
                    : "施工是独立阶段。当前选择逐回合施工视图：每个施工回合拥有独立 View；完成当前回合后切换到下一回合 View。运营等级始终从 LV1 开始。",
                MessageType.None);
            if (mode == BuildingConstructionViewMode.Single)
            {
                DrawProperty(draftProperty, "ConstructionViewPrefab");
            }

            DrawProperty(draftProperty, "ConstructionTurns", true);
            if (mode == BuildingConstructionViewMode.PerTurn)
            {
                DrawPerTurnConstructionViews(draftProperty);
            }

            DrawProperty(draftProperty, "InitialLevelCount");
            EndSection();
        }

        private static void DrawPerTurnConstructionViews(SerializedProperty draftProperty)
        {
            var turnsProperty = draftProperty.FindPropertyRelative("ConstructionTurns");
            var viewsProperty = draftProperty.FindPropertyRelative("ConstructionTurnViewPrefabs");
            if (turnsProperty == null || viewsProperty == null)
            {
                EditorGUILayout.HelpBox("创建草稿缺少逐回合施工视图字段。", MessageType.Error);
                return;
            }

            if (viewsProperty.arraySize != turnsProperty.arraySize)
            {
                viewsProperty.arraySize = turnsProperty.arraySize;
            }

            EditorGUILayout.Space(3f);
            EditorGUILayout.LabelField(
                new GUIContent(
                    "逐回合施工视图",
                    "列表与上方施工回合一一对应。允许暂时留空；缺失回合会显示统一占位表现。"),
                EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            for (var i = 0; i < viewsProperty.arraySize; i++)
            {
                EditorGUILayout.PropertyField(
                    viewsProperty.GetArrayElementAtIndex(i),
                    new GUIContent(
                        $"第 {i + 1} 回合 View Prefab",
                        $"进入第 {i + 1} 个施工回合时显示的独立纯表现 Prefab。"));
            }
            EditorGUI.indentLevel--;
        }

        private static void DrawPresentationSection(SerializedProperty draftProperty)
        {
            BeginSection("4. 表现资源（可暂时缺省）");
            EditorGUILayout.HelpBox(
                "这里只引用独立 View Prefab。不要把施工、LV1～LVN 美术节点塞入 Runtime Prefab。" +
                "资源紧张时可以留空，之后直接替换或追加 Presentation 映射。",
                MessageType.None);
            DrawProperty(draftProperty, "PlacementPreviewViewPrefab");
            DrawProperty(draftProperty, "DefaultOperationalViewPrefab");
            DrawProperty(draftProperty, "Styles", true);
            EndSection();
        }

        private void DrawWorkforceProductionSection(SerializedProperty draftProperty)
        {
            var templateProperty = draftProperty.FindPropertyRelative("ModuleTemplate");
            var template = (BuildingModuleTemplate)templateProperty.enumValueIndex;
            if (template != BuildingModuleTemplate.WorkforceProduction
                && template != BuildingModuleTemplate.WorkforceMaintenanceProduction)
            {
                return;
            }

            BeginSection("5. 岗位与生产模块");
            DrawProperty(draftProperty, "MaxWorkers");
            DrawProperty(draftProperty, "InitialWorkers");
            DrawProperty(draftProperty, "BaseJobAttraction");
            DrawProperty(draftProperty, "RecruitCost");
            DrawProperty(draftProperty, "AutoSubsidy");
            DrawProperty(draftProperty, "TargetStableWorkers");
            DrawProperty(draftProperty, "GoldItemDefinition");
            if (template == BuildingModuleTemplate.WorkforceMaintenanceProduction)
            {
                DrawProperty(draftProperty, "MaintenanceItemDefinition");
                DrawProperty(draftProperty, "MaintenanceAmountPerTurn");
            }
            DrawProperty(draftProperty, "ProductionIntervalTurns");
            DrawProperty(draftProperty, "ProductionItem");
            DrawProperty(draftProperty, "ProductionTiers", true);
            EndSection();
        }

        private void DrawAdvancedPathSection(SerializedProperty draftProperty)
        {
            showAdvancedPaths = EditorGUILayout.Foldout(showAdvancedPaths, "高级：输出目录", true);
            if (!showAdvancedPaths)
            {
                return;
            }

            BeginSection("输出目录");
            EditorGUILayout.HelpBox("通常不应修改；所有目录必须位于 Assets 下。", MessageType.Warning);
            DrawProperty(draftProperty, "FamilyFolder");
            DrawProperty(draftProperty, "ModuleFolder");
            DrawProperty(draftProperty, "PresentationFolder");
            DrawProperty(draftProperty, "RuntimePrefabFolder");
            EndSection();
        }

        private void DrawPathPreview()
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField("将创建的资产", EditorStyles.boldLabel);
            try
            {
                var paths = BuildingAuthoringService.GetTargetPaths(draft);
                DrawPath("Family", paths.FamilyAssetPath);
                DrawPath("ModuleSet", paths.ModuleAssetPath);
                DrawPath("Presentation", paths.PresentationAssetPath);
                DrawPath("Runtime Prefab", paths.RuntimePrefabPath);
            }
            catch (Exception exception)
            {
                EditorGUILayout.HelpBox(exception.Message, MessageType.Error);
            }
        }

        private void DrawDraftValidationResult()
        {
            if (lastValidationErrors.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "当前不能创建：\n• " + string.Join("\n• ", lastValidationErrors),
                MessageType.Error);
        }

        private void DrawCreateActions()
        {
            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("校验创建参数", GUILayout.Height(32f)))
                {
                    ValidateCurrentDraft();
                }

                using (new EditorGUI.DisabledScope(EditorApplication.isCompiling))
                {
                    if (GUILayout.Button("创建完整建筑资产组", GUILayout.Height(32f)))
                    {
                        CreateBuilding();
                    }
                }

                if (GUILayout.Button("重置草稿", GUILayout.Width(100f), GUILayout.Height(32f)))
                {
                    if (EditorUtility.DisplayDialog("重置创建草稿", "确认恢复默认创建参数？", "重置", "取消"))
                    {
                        draft.ResetToDefaults();
                        lastValidationErrors.Clear();
                        transientMessage = string.Empty;
                        windowSerializedObject.Update();
                    }
                }
            }
            EditorGUILayout.Space(12f);
        }

        private void DrawEditTab()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                DrawFamilyList();
                DrawSelectedFamilyEditor();
            }
        }

        private void DrawFamilyList()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(270f)))
            {
                EditorGUILayout.LabelField("Catalog 建筑家族", EditorStyles.boldLabel);
                familySearch = EditorGUILayout.TextField(
                    new GUIContent("搜索", "按 FamilyId 或建筑显示名称筛选 Catalog 中的家族。"),
                    familySearch);
                familyListScroll = EditorGUILayout.BeginScrollView(familyListScroll, EditorStyles.helpBox);
                if (catalog == null || catalog.Families.Count == 0)
                {
                    EditorGUILayout.HelpBox("Catalog 中没有建筑家族。", MessageType.Info);
                }
                else
                {
                    for (var i = 0; i < catalog.Families.Count; i++)
                    {
                        var family = catalog.Families[i];
                        if (family == null || !MatchesSearch(family, familySearch))
                        {
                            continue;
                        }

                        var label = string.IsNullOrWhiteSpace(family.Definition?.DisplayName)
                            ? family.FamilyId
                            : $"{family.Definition.DisplayName}  ({family.FamilyId})";
                        var style = family == selectedFamily
                            ? EditorStyles.miniButtonMid
                            : EditorStyles.miniButton;
                        if (GUILayout.Button(label, style, GUILayout.Height(25f)))
                        {
                            SelectFamily(family);
                        }
                    }
                }

                EditorGUILayout.EndScrollView();
                var manualSelection = (BuildingFamilyDefinition)EditorGUILayout.ObjectField(
                    new GUIContent(
                        "手动选择",
                        "可以选择尚未登记到当前 Catalog 的 Family，用于检查、编辑或重新登记。"),
                    selectedFamily,
                    typeof(BuildingFamilyDefinition),
                    false);
                if (manualSelection != selectedFamily)
                {
                    SelectFamily(manualSelection);
                }
            }
        }

        private void DrawSelectedFamilyEditor()
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (selectedFamily == null)
                {
                    EditorGUILayout.HelpBox(
                        "从左侧选择家族。这里直接编辑 Family、ModuleSet 和 Presentation；" +
                        "Runtime Prefab 只承载运行时组件，不承载等级美术。",
                        MessageType.Info);
                    return;
                }

                familyEditorScroll = EditorGUILayout.BeginScrollView(familyEditorScroll);
                EditorGUILayout.LabelField(
                    $"{selectedFamily.Definition.DisplayName}  /  {selectedFamily.FamilyId}",
                    EditorStyles.largeLabel);
                DrawSelectedFamilyActions();

                UnityEditor.Editor.CreateCachedEditor(selectedFamily, null, ref familyEditor);
                DrawInlineAssetInspector(
                    familyEditor,
                    "Family 数据",
                    "这里使用稳定的 Unity 序列化字段视图，避免 Odin 列表嵌套在窗口滚动区时产生异常高度。");

                EditorGUILayout.Space(6f);
                showModuleEditor = EditorGUILayout.Foldout(showModuleEditor, "模块集合（ModuleSet）", true);
                if (showModuleEditor)
                {
                    if (selectedFamily.ModuleSet == null)
                    {
                        EditorGUILayout.HelpBox("Family 未绑定 ModuleSet。", MessageType.Error);
                    }
                    else
                    {
                        UnityEditor.Editor.CreateCachedEditor(selectedFamily.ModuleSet, null, ref moduleEditor);
                        DrawInlineAssetInspector(
                            moduleEditor,
                            "模块模板与默认参数",
                            "ModuleSet 决定建筑实际拥有的模块；等级差异继续在 Family 的运营等级配置中填写。");
                    }
                }

                EditorGUILayout.Space(6f);
                showPresentationEditor = EditorGUILayout.Foldout(showPresentationEditor, "表现配置（Presentation）", true);
                if (showPresentationEditor)
                {
                    if (selectedFamily.Presentation == null)
                    {
                        EditorGUILayout.HelpBox("Family 未绑定 Presentation。", MessageType.Error);
                    }
                    else
                    {
                        UnityEditor.Editor.CreateCachedEditor(
                            selectedFamily.Presentation,
                            null,
                            ref presentationEditor);
                        DrawInlinePresentationInspector(
                            selectedFamily,
                            presentationEditor,
                            "表现映射",
                            "这里只配置施工、运营等级和样式对应的独立 View Prefab，不把等级美术塞进 Runtime Prefab。");
                    }
                }

                EditorGUILayout.EndScrollView();
            }
        }

        private static void DrawInlineAssetInspector(
            UnityEditor.Editor editor,
            string title,
            string tooltip)
        {
            if (editor == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.boldLabel);
            editor.DrawDefaultInspector();
        }

        private static void DrawInlinePresentationInspector(
            BuildingFamilyDefinition family,
            UnityEditor.Editor editor,
            string title,
            string tooltip)
        {
            if (editor == null)
            {
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(new GUIContent(title, tooltip), EditorStyles.boldLabel);

            var serializedObject = editor.serializedObject;
            serializedObject.UpdateIfRequiredOrScript();
            var constructionViewMode = DrawPresentationProperty(
                serializedObject,
                "constructionViewMode",
                "施工视图模式",
                "单一模式在整个施工阶段保持同一视图；逐回合模式按当前施工回合选择视图。使用逐回合模式时，缺失映射不会回退单一视图。");
            var mode = constructionViewMode == null
                ? BuildingConstructionViewMode.Single
                : (BuildingConstructionViewMode)constructionViewMode.intValue;
            if (mode == BuildingConstructionViewMode.Single)
            {
                DrawPresentationProperty(
                    serializedObject,
                    "constructionView",
                    "施工视图",
                    "整个施工阶段使用的同一个独立纯表现 View。");
            }
            else if (mode == BuildingConstructionViewMode.PerTurn)
            {
                DrawPresentationProperty(
                    serializedObject,
                    "constructionViewMappings",
                    "逐回合施工视图",
                    "按施工回合和可选样式 ID 选择独立视图；回合从 1 开始，缺失映射显示占位表现。");
            }
            else
            {
                EditorGUILayout.HelpBox($"不支持的施工视图模式值：{constructionViewMode?.intValue}", MessageType.Error);
            }
            DrawPresentationProperty(
                serializedObject,
                "placementPreviewView",
                "放置预览视图",
                "建造放置预览优先使用；留空时回退到当前样式的 LV1 运营视图。");
            DrawPresentationProperty(
                serializedObject,
                "defaultOperationalView",
                "默认运营视图",
                "没有匹配到等级或样式映射时使用的运营视图。");
            DrawPresentationProperty(
                serializedObject,
                "styles",
                "视觉样式",
                "玩家可选择的表现变体，例如不同树种。Style 不属于数值表；增删样式后，视图映射矩阵会按样式与运营等级自动重建。");
            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(editor.target);
            }

            SynchronizePresentationMappings(family, editor.target as BuildingPresentationDefinition);
            serializedObject.Update();
            DrawViewMappingMatrix(serializedObject);
            if (serializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(editor.target);
            }
        }

        private static void SynchronizePresentationMappings(
            BuildingFamilyDefinition family,
            BuildingPresentationDefinition presentation)
        {
            if (family == null || presentation == null)
            {
                return;
            }

            var levels = new List<int>();
            for (var i = 0; i < family.Levels.Count; i++)
            {
                if (family.Levels[i] != null)
                {
                    levels.Add(family.Levels[i].Level);
                }
            }

            if (!BuildingPresentationMappingSynchronizer.NeedsSynchronization(
                    presentation,
                    levels,
                    out var error))
            {
                if (!string.IsNullOrWhiteSpace(error))
                {
                    EditorGUILayout.HelpBox($"无法生成视图映射矩阵：{error}", MessageType.Error);
                }
                return;
            }

            Undo.RecordObject(presentation, "同步建筑视图映射矩阵");
            if (!BuildingPresentationMappingSynchronizer.TrySynchronize(
                    presentation,
                    levels,
                    out error)
                && !string.IsNullOrWhiteSpace(error))
            {
                EditorGUILayout.HelpBox($"无法生成视图映射矩阵：{error}", MessageType.Error);
            }
        }

        private static void DrawViewMappingMatrix(SerializedObject serializedObject)
        {
            var mappings = serializedObject.FindProperty("viewMappings");
            if (mappings == null)
            {
                EditorGUILayout.HelpBox("表现配置缺少序列化字段：viewMappings", MessageType.Error);
                return;
            }

            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(
                new GUIContent(
                    "视图映射",
                    "槽位由建筑数值表中的运营等级与视觉样式自动生成，不能手动添加、删除或修改键。"),
                EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                $"固定槽位共 {mappings.arraySize} 个。View 允许留空；缺少高等级美术时仍按运行时规则回退到同样式低等级或占位表现。",
                MessageType.None);

            for (var i = 0; i < mappings.arraySize; i++)
            {
                var mapping = mappings.GetArrayElementAtIndex(i);
                var level = mapping.FindPropertyRelative("level");
                var styleId = mapping.FindPropertyRelative("styleId");
                var view = mapping.FindPropertyRelative("view");
                if (level == null || styleId == null || view == null)
                {
                    EditorGUILayout.HelpBox($"视图映射槽位 #{i + 1} 的序列化结构无效。", MessageType.Error);
                    continue;
                }

                var slotName = string.IsNullOrWhiteSpace(styleId.stringValue)
                    ? $"默认样式 / LV{level.intValue}"
                    : $"{styleId.stringValue} / LV{level.intValue}";
                EditorGUILayout.PropertyField(
                    view,
                    new GUIContent(
                        slotName,
                        "该样式与运营等级对应的独立纯表现 View。等级和 StyleId 由矩阵自动维护。"),
                    true);
            }
        }

        private static SerializedProperty DrawPresentationProperty(
            SerializedObject serializedObject,
            string propertyName,
            string label,
            string tooltip)
        {
            var property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"表现配置缺少序列化字段：{propertyName}", MessageType.Error);
                return null;
            }

            EditorGUILayout.PropertyField(
                property,
                new GUIContent(label, tooltip),
                true);
            return property;
        }

        private void DrawSelectedFamilyActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("保存修改"))
                {
                    AssetDatabase.SaveAssets();
                    transientMessage = $"已保存 {selectedFamily.FamilyId}。";
                    transientMessageType = MessageType.Info;
                }

                if (GUILayout.Button("登记到 Catalog"))
                {
                    BuildingAuthoringService.RegisterFamily(catalog, selectedFamily);
                    transientMessage = $"已登记 {selectedFamily.FamilyId}。";
                    transientMessageType = MessageType.Info;
                }

                using (new EditorGUI.DisabledScope(selectedFamily.RuntimePrefab == null))
                {
                    if (GUILayout.Button("打开 Runtime Prefab"))
                    {
                        AssetDatabase.OpenAsset(selectedFamily.RuntimePrefab.gameObject);
                    }
                }

                if (GUILayout.Button("定位资产"))
                {
                    Selection.activeObject = selectedFamily;
                    EditorGUIUtility.PingObject(selectedFamily);
                }

                if (GUILayout.Button("架构校验"))
                {
                    ExecuteArchitectureValidation();
                }
            }
        }

        private void ValidateCurrentDraft()
        {
            lastValidationErrors.Clear();
            lastValidationErrors.AddRange(BuildingAuthoringService.ValidateDraft(draft, catalog));
            if (lastValidationErrors.Count == 0)
            {
                transientMessage = "创建参数校验通过。";
                transientMessageType = MessageType.Info;
            }
            else
            {
                transientMessage = $"发现 {lastValidationErrors.Count} 个创建问题。";
                transientMessageType = MessageType.Error;
            }
        }

        private void CreateBuilding()
        {
            ValidateCurrentDraft();
            if (lastValidationErrors.Count > 0)
            {
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(
                "创建建筑资产组",
                $"将创建建筑 {draft.DisplayName}（{draft.FamilyId}）。\n" +
                "将直接使用统一 BuildingBase 创建 Runtime Prefab，并同步登记 Catalog。",
                "开始创建",
                "取消");
            if (!confirmed)
            {
                return;
            }

            if (BuildingAuthoringService.BeginCreation(draft, catalog))
            {
                transientMessage = "完整建筑资产组创建完成；未生成建筑脚本。";
                transientMessageType = MessageType.Info;
                lastValidationErrors.Clear();
            }
            else
            {
                transientMessage = "建筑创建失败，详情见 Console；已创建的前置资产已自动回滚。";
                transientMessageType = MessageType.Error;
            }
        }

        private void ExecuteArchitectureValidation()
        {
            try
            {
                Landsong.Editor.BuildingArchitectureValidator.Execute();
                transientMessage = "建筑终态架构校验通过。";
                transientMessageType = MessageType.Info;
            }
            catch (Exception exception)
            {
                transientMessage = "建筑终态架构校验失败，详情见 Console。";
                transientMessageType = MessageType.Error;
                Debug.LogException(exception);
            }
        }

        private void SelectFamily(BuildingFamilyDefinition family)
        {
            if (selectedFamily == family)
            {
                return;
            }

            selectedFamily = family;
            DestroyCachedEditors();
        }

        private void DestroyCachedEditors()
        {
            DestroyImmediate(familyEditor);
            DestroyImmediate(moduleEditor);
            DestroyImmediate(presentationEditor);
            familyEditor = null;
            moduleEditor = null;
            presentationEditor = null;
        }

        private static bool MatchesSearch(BuildingFamilyDefinition family, string search)
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return true;
            }

            return family.FamilyId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0
                   || (family.Definition != null
                       && family.Definition.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static SerializedProperty DrawProperty(
            SerializedProperty parent,
            string relativeName,
            bool includeChildren = false)
        {
            var property = parent.FindPropertyRelative(relativeName);
            if (property == null)
            {
                EditorGUILayout.HelpBox($"找不到窗口字段：{relativeName}", MessageType.Error);
                return null;
            }

            EditorGUILayout.PropertyField(property, includeChildren);
            return property;
        }

        private static void BeginSection(string title)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        }

        private static void EndSection()
        {
            EditorGUILayout.EndVertical();
        }

        private static void DrawPath(string label, string path)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.PrefixLabel(label);
                EditorGUILayout.SelectableLabel(path ?? string.Empty, EditorStyles.textField, GUILayout.Height(18f));
            }
        }

        private static void OpenProjectDocument(string relativePath)
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var absolutePath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
            if (!File.Exists(absolutePath))
            {
                EditorUtility.DisplayDialog("文档不存在", absolutePath, "确定");
                return;
            }

            EditorUtility.OpenWithDefaultApp(absolutePath);
        }
    }
}
