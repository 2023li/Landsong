using System;
using System.Collections.Generic;
using Landsong.TechnologySystem;
using UnityEditor;
using UnityEngine;

public sealed class TechnologyEditorWindow : EditorWindow
{
    private enum GraphConnectionMode
    {
        None = 0,
        AddPrerequisite = 1,
        RemovePrerequisite = 2
    }

    private const string DefaultCatalogFolder = "Assets/Landsong/Objects/SO";
    private const string DefaultCatalogPath = "Assets/Landsong/Objects/SO/TechnologyCatalog.asset";
    private const string DefaultTechnologyFolder = "Assets/Landsong/Objects/SO/Technology";
    private const string CatalogEditorPrefsKey = "Landsong.TechnologyEditorWindow.CatalogPath";
    private const float MinGraphWidth = 3000f;
    private const float GraphHeight = 1000f;
    private const float GraphHorizontalExpansionPadding = 900f;
    private const float NodeWidth = 190f;
    private const float NodeHeight = 82f;
    private const string DefaultNewTechnologyId = "TN_R_L_";
    private const string TechnologyNodeIdPrefix = "TN";
    private const string TechnologyNodeIdFormatHint = "节点 ID 必须符合 TN_行_列_节点名称，例如 TN_3_1_启蒙。";

    private TechnologyCatalog catalog;
    private TechnologyDefinition selectedDefinition;
    private TechnologyDefinition definitionToAdd;
    private string searchText = string.Empty;
    private string newTechnologyId = DefaultNewTechnologyId;
    private string technologyFolder = DefaultTechnologyFolder;
    private Vector2 listScroll;
    private Vector2 inspectorScroll;
    private Vector2 graphScroll;
    private TechnologyDefinition draggingDefinition;
    private TechnologyDefinition connectionSource;
    private GraphConnectionMode connectionMode;
    private Vector2 dragOffset;
    private Rect graphVisibleRect;
    private float currentGraphContentWidth = MinGraphWidth;
    private bool hasUnsavedTechnologyTreeChanges;

    private readonly Dictionary<TechnologyDefinition, int> graphIndexByDefinition =
        new Dictionary<TechnologyDefinition, int>();

    private readonly Dictionary<TechnologyDefinition, Rect> graphRectsByDefinition =
        new Dictionary<TechnologyDefinition, Rect>();

    [MenuItem("Landsong/Technology/Technology Editor")]
    private static void Open()
    {
        var window = GetWindow<TechnologyEditorWindow>("Technology Editor");
        window.minSize = new Vector2(920f, 560f);
        window.Show();
    }

    private void OnEnable()
    {
        saveChangesMessage = "科技树有未保存的修改。";
        RestoreLastCatalog();
    }

    public override void SaveChanges()
    {
        if (!SaveTechnologyTree())
        {
            return;
        }

        base.SaveChanges();
    }

    public override void DiscardChanges()
    {
        SetUnsavedChanges(false);
        base.DiscardChanges();
    }

    private void OnGUI()
    {
        DrawToolbar();

        if (catalog == null)
        {
            EditorGUILayout.HelpBox("选择或创建 TechnologyCatalog 后再编辑科技节点。", MessageType.Info);
            return;
        }

        EditorGUILayout.Space(6f);
        EditorGUILayout.BeginHorizontal();
        DrawNodeList();
        DrawInspectorPanel();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        DrawGraph(catalog.Definitions);
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginChangeCheck();
        var selectedCatalog = (TechnologyCatalog)EditorGUILayout.ObjectField(
            "Catalog",
            catalog,
            typeof(TechnologyCatalog),
            false);
        if (EditorGUI.EndChangeCheck())
        {
            if (ConfirmSaveOrDiscardUnsavedChanges())
            {
                SetCatalog(selectedCatalog);
            }
        }

        if (GUILayout.Button("Create Catalog", GUILayout.Width(120f)))
        {
            CreateCatalogAsset();
        }

        if (catalog != null && GUILayout.Button("Ping", GUILayout.Width(60f)))
        {
            EditorGUIUtility.PingObject(catalog);
        }

        using (new EditorGUI.DisabledScope(catalog == null))
        {
            if (GUILayout.Button("保存", GUILayout.Width(70f)))
            {
                SaveTechnologyTree();
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        technologyFolder = EditorGUILayout.TextField("Node Folder", technologyFolder);

        if (catalog != null && GUILayout.Button("Load Folder", GUILayout.Width(110f)))
        {
            catalog.EditorLoadDefinitionsFromFolder(technologyFolder);
            MarkUnsavedChanges();
            Repaint();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        newTechnologyId = EditorGUILayout.TextField("New Node Id", newTechnologyId);

        if (catalog != null && GUILayout.Button("Create Node", GUILayout.Width(110f)))
        {
            CreateTechnologyNode();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        definitionToAdd = (TechnologyDefinition)EditorGUILayout.ObjectField(
            "Add Existing",
            definitionToAdd,
            typeof(TechnologyDefinition),
            false);

        using (new EditorGUI.DisabledScope(catalog == null || definitionToAdd == null))
        {
            if (GUILayout.Button("Add", GUILayout.Width(60f)))
            {
                catalog.EditorAddDefinition(definitionToAdd);
                selectedDefinition = definitionToAdd;
                definitionToAdd = null;
                MarkUnsavedChanges();
                Repaint();
            }
        }

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
    }

    private void DrawNodeList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(260f));
        EditorGUILayout.LabelField("科技节点", EditorStyles.boldLabel);
        searchText = EditorGUILayout.TextField("Search", searchText);

        listScroll = EditorGUILayout.BeginScrollView(listScroll, EditorStyles.helpBox, GUILayout.Height(260f));
        var definitions = catalog.Definitions;
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null || !MatchesSearch(definition))
            {
                continue;
            }

            var repeatLabel = definition.AllowRepeatResearch ? " 可重复" : string.Empty;
            var label = $"{definition.DisplayName}  [需求 {definition.SciencePointCost}{repeatLabel}]";
            var wasSelected = selectedDefinition == definition;
            if (GUILayout.Toggle(wasSelected, label, "Button") && !wasSelected)
            {
                selectedDefinition = definition;
                GUI.FocusControl(null);
            }
        }

        EditorGUILayout.EndScrollView();

        using (new EditorGUI.DisabledScope(selectedDefinition == null))
        {
            if (GUILayout.Button("Remove Selected From Catalog"))
            {
                catalog.EditorRemoveDefinition(selectedDefinition);
                selectedDefinition = null;
                MarkUnsavedChanges();
                Repaint();
            }
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawInspectorPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(360f));
        EditorGUILayout.LabelField("节点数据", EditorStyles.boldLabel);

        inspectorScroll = EditorGUILayout.BeginScrollView(inspectorScroll, EditorStyles.helpBox, GUILayout.Height(260f));
        if (selectedDefinition == null)
        {
            EditorGUILayout.HelpBox("选择一个科技节点查看和编辑数据。", MessageType.Info);
        }
        else
        {
            var serializedDefinition = new SerializedObject(selectedDefinition);
            serializedDefinition.Update();

            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("icon"));
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("technologyId"));
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("displayName"));
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("description"));
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("sciencePointCost"));
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("allowRepeatResearch"));
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("prerequisites"), true);
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("completionEffects"), true);
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("graphPosition"));

            if (serializedDefinition.ApplyModifiedProperties())
            {
                selectedDefinition.Normalize();
                EditorUtility.SetDirty(selectedDefinition);
                catalog.RebuildIndex();
                MarkUnsavedChanges();
            }

            EditorGUILayout.Space(6f);
            if (GUILayout.Button("Ping Node Asset"))
            {
                EditorGUIUtility.PingObject(selectedDefinition);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawGraph(IReadOnlyList<TechnologyDefinition> definitions)
    {
        EditorGUILayout.LabelField("科技图", EditorStyles.boldLabel);
        DrawGraphConnectionToolbar();
        var graphRect = EditorGUILayout.GetControlRect(false, 420f, GUILayout.ExpandWidth(true));
        GUI.Box(graphRect, GUIContent.none);

        BuildGraphCache(definitions);
        currentGraphContentWidth = CalculateGraphContentWidth(graphRect.width);
        graphScroll = GUI.BeginScrollView(
            graphRect,
            graphScroll,
            new Rect(0f, 0f, currentGraphContentWidth, GraphHeight));
        graphVisibleRect = new Rect(graphScroll.x, graphScroll.y, graphRect.width, graphRect.height);

        if (Event.current.type == EventType.Repaint)
        {
            DrawGraphConnections(definitions);
            DrawPendingConnection();
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition != null
                && TryGetGraphNodeRect(definition, out var nodeRect)
                && IsNodeVisible(nodeRect))
            {
                DrawGraphNode(definition, nodeRect);
            }
        }

        GUI.EndScrollView();
        HandleGraphMouseMove();
    }

    private void DrawGraphConnectionToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        var addModeEnabled = GUILayout.Toggle(
            connectionMode == GraphConnectionMode.AddPrerequisite,
            "添加依赖连线",
            EditorStyles.toolbarButton,
            GUILayout.Width(100f));
        if (addModeEnabled != (connectionMode == GraphConnectionMode.AddPrerequisite))
        {
            SetConnectionMode(addModeEnabled ? GraphConnectionMode.AddPrerequisite : GraphConnectionMode.None);
        }

        var removeModeEnabled = GUILayout.Toggle(
            connectionMode == GraphConnectionMode.RemovePrerequisite,
            "删除依赖连线",
            EditorStyles.toolbarButton,
            GUILayout.Width(100f));
        if (removeModeEnabled != (connectionMode == GraphConnectionMode.RemovePrerequisite))
        {
            SetConnectionMode(removeModeEnabled ? GraphConnectionMode.RemovePrerequisite : GraphConnectionMode.None);
        }

        GUILayout.Space(8f);
        GUILayout.Label(GetConnectionModeHint(), EditorStyles.miniLabel);

        GUILayout.FlexibleSpace();
        using (new EditorGUI.DisabledScope(connectionMode == GraphConnectionMode.None && connectionSource == null))
        {
            if (GUILayout.Button("取消连线", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                SetConnectionMode(GraphConnectionMode.None);
            }
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawGraphConnections(IReadOnlyList<TechnologyDefinition> definitions)
    {
        Handles.BeginGUI();

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            var prerequisites = definition.Prerequisites;
            for (var j = 0; j < prerequisites.Count; j++)
            {
                var prerequisite = prerequisites[j];
                if (!TryGetGraphNodeRect(prerequisite, out var fromRect)
                    || !TryGetGraphNodeRect(definition, out var toRect)
                    || !IsConnectionVisible(fromRect, toRect))
                {
                    continue;
                }

                var highlight = selectedDefinition == definition
                                || selectedDefinition == prerequisite
                                || connectionSource == definition
                                || connectionSource == prerequisite;
                DrawConnectionLine(
                    GetRectEdgePoint(fromRect, toRect.center),
                    GetRectEdgePoint(toRect, fromRect.center),
                    highlight ? new Color(0.55f, 0.8f, 1f, 0.95f) : new Color(0.45f, 0.6f, 0.75f, 0.65f),
                    highlight ? 2.5f : 1.5f,
                    highlight);
            }
        }

        Handles.EndGUI();
    }

    private void DrawPendingConnection()
    {
        if (connectionMode == GraphConnectionMode.None || connectionSource == null || Event.current == null)
        {
            return;
        }

        if (!TryGetGraphNodeRect(connectionSource, out var sourceRect))
        {
            connectionSource = null;
            return;
        }

        var mousePosition = Event.current.mousePosition;

        Handles.BeginGUI();
        DrawConnectionLine(
            GetRectEdgePoint(sourceRect, mousePosition),
            mousePosition,
            connectionMode == GraphConnectionMode.AddPrerequisite
                ? new Color(0.35f, 0.9f, 0.55f, 0.9f)
                : new Color(1f, 0.45f, 0.25f, 0.9f),
            3f,
            true);
        Handles.EndGUI();
    }

    private void DrawGraphNode(TechnologyDefinition definition, Rect nodeRect)
    {
        HandleNodeInput(definition, nodeRect);

        var selected = selectedDefinition == definition;
        var isConnectionSource = connectionSource == definition;
        EditorGUI.DrawRect(
            nodeRect,
            isConnectionSource
                ? new Color(0.32f, 0.34f, 0.14f, 1f)
                : selected
                    ? new Color(0.18f, 0.32f, 0.48f, 1f)
                    : new Color(0.16f, 0.16f, 0.16f, 1f));
        GUI.Box(nodeRect, GUIContent.none);

        var titleRect = new Rect(nodeRect.x + 8f, nodeRect.y + 8f, nodeRect.width - 16f, 20f);
        GUI.Label(titleRect, definition.DisplayName, EditorStyles.boldLabel);

        var idRect = new Rect(nodeRect.x + 8f, nodeRect.y + 30f, nodeRect.width - 16f, 18f);
        GUI.Label(idRect, definition.TechnologyId, EditorStyles.miniLabel);

        var costRect = new Rect(nodeRect.x + 8f, nodeRect.y + 52f, nodeRect.width - 16f, 18f);
        var repeatLabel = definition.AllowRepeatResearch ? " / 可重复" : string.Empty;
        GUI.Label(costRect, $"需求 {definition.SciencePointCost} 科技点{repeatLabel}", EditorStyles.miniBoldLabel);
    }

    private void HandleNodeInput(TechnologyDefinition definition, Rect nodeRect)
    {
        var current = Event.current;
        if (current == null)
        {
            return;
        }

        if (current.type == EventType.MouseDown && current.button == 0 && nodeRect.Contains(current.mousePosition))
        {
            selectedDefinition = definition;

            if (connectionMode != GraphConnectionMode.None)
            {
                HandleConnectionNodeClicked(definition);
                current.Use();
                return;
            }

            draggingDefinition = definition;
            dragOffset = current.mousePosition - nodeRect.position;
            current.Use();
        }
        else if (current.type == EventType.MouseDrag && draggingDefinition == definition)
        {
            SetGraphPosition(definition, current.mousePosition - dragOffset);
            current.Use();
            Repaint();
        }
        else if (current.type == EventType.MouseUp && draggingDefinition == definition)
        {
            draggingDefinition = null;
            current.Use();
        }
    }

    private void HandleConnectionNodeClicked(TechnologyDefinition definition)
    {
        if (definition == null)
        {
            return;
        }

        if (connectionSource == null)
        {
            connectionSource = definition;
            Repaint();
            return;
        }

        if (connectionSource == definition)
        {
            connectionSource = null;
            Repaint();
            return;
        }

        var completedConnection = false;
        if (connectionMode == GraphConnectionMode.AddPrerequisite)
        {
            completedConnection = AddPrerequisite(definition, connectionSource);
        }
        else if (connectionMode == GraphConnectionMode.RemovePrerequisite)
        {
            completedConnection = RemovePrerequisite(definition, connectionSource);
        }

        if (completedConnection)
        {
            SetConnectionMode(GraphConnectionMode.None);
            return;
        }

        connectionSource = null;
        Repaint();
    }

    private bool MatchesSearch(TechnologyDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return true;
        }

        var keyword = searchText.Trim();
        return definition.TechnologyId.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
               || definition.DisplayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void CreateCatalogAsset()
    {
        if (!ConfirmSaveOrDiscardUnsavedChanges())
        {
            return;
        }

        EnsureAssetFolder(DefaultCatalogFolder);
        var asset = CreateInstance<TechnologyCatalog>();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultCatalogFolder}/TechnologyCatalog.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        SetCatalog(asset);
        EditorGUIUtility.PingObject(catalog);
    }

    private void SetCatalog(TechnologyCatalog newCatalog)
    {
        if (catalog == newCatalog)
        {
            return;
        }

        catalog = newCatalog;
        selectedDefinition = null;
        connectionSource = null;
        connectionMode = GraphConnectionMode.None;
        draggingDefinition = null;
        wantsMouseMove = false;

        if (catalog == null)
        {
            EditorPrefs.DeleteKey(CatalogEditorPrefsKey);
            SetUnsavedChanges(false);
            return;
        }

        var catalogPath = AssetDatabase.GetAssetPath(catalog);
        if (!string.IsNullOrWhiteSpace(catalogPath))
        {
            EditorPrefs.SetString(CatalogEditorPrefsKey, catalogPath);
        }

        SetUnsavedChanges(false);
    }

    private void MarkUnsavedChanges()
    {
        SetUnsavedChanges(true);
    }

    private void SetUnsavedChanges(bool unsaved)
    {
        hasUnsavedTechnologyTreeChanges = unsaved;
        hasUnsavedChanges = unsaved;
        saveChangesMessage = "科技树有未保存的修改。";
    }

    private bool ConfirmSaveOrDiscardUnsavedChanges()
    {
        if (!hasUnsavedTechnologyTreeChanges)
        {
            return true;
        }

        var choice = EditorUtility.DisplayDialogComplex(
            "科技树未保存",
            "科技树有未保存的修改。是否先保存？",
            "保存",
            "取消",
            "不保存");

        if (choice == 0)
        {
            return SaveTechnologyTree();
        }

        if (choice == 1)
        {
            return false;
        }

        SetUnsavedChanges(false);
        return true;
    }

    private void RestoreLastCatalog()
    {
        if (catalog != null)
        {
            return;
        }

        var savedPath = EditorPrefs.GetString(CatalogEditorPrefsKey, DefaultCatalogPath);
        var restoredCatalog = AssetDatabase.LoadAssetAtPath<TechnologyCatalog>(savedPath);
        if (restoredCatalog == null)
        {
            restoredCatalog = AssetDatabase.LoadAssetAtPath<TechnologyCatalog>(DefaultCatalogPath);
        }

        if (restoredCatalog == null)
        {
            restoredCatalog = FindFirstCatalogAsset();
        }

        SetCatalog(restoredCatalog);
    }

    private static TechnologyCatalog FindFirstCatalogAsset()
    {
        var guids = AssetDatabase.FindAssets("t:TechnologyCatalog", new[] { DefaultCatalogFolder });
        if (guids == null || guids.Length == 0)
        {
            return null;
        }

        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<TechnologyCatalog>(path);
    }

    private bool SaveTechnologyTree()
    {
        if (catalog == null)
        {
            return false;
        }

        if (!ValidateTechnologyNodeIds(catalog.Definitions, out var errorMessage, out var invalidDefinition))
        {
            selectedDefinition = invalidDefinition;
            EditorUtility.DisplayDialog("科技树保存失败", errorMessage, "确定");
            Repaint();
            return false;
        }

        catalog.RebuildIndex();
        EditorUtility.SetDirty(catalog);

        var renamedCount = RenameTechnologyDefinitionAssets(catalog.Definitions);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        SetUnsavedChanges(false);
        Debug.Log($"科技树已保存。同步重命名科技节点 SO：{renamedCount} 个。", this);
        return true;
    }

    private void CreateTechnologyNode()
    {
        EnsureAssetFolder(technologyFolder);

        var normalizedId = NormalizeTechnologyId(newTechnologyId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            normalizedId = DefaultNewTechnologyId;
        }

        var asset = CreateInstance<TechnologyDefinition>();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{technologyFolder}/{MakeSafeFileName(normalizedId)}.asset");
        AssetDatabase.CreateAsset(asset, path);

        var serializedDefinition = new SerializedObject(asset);
        serializedDefinition.FindProperty("technologyId").stringValue = normalizedId;
        serializedDefinition.FindProperty("displayName").stringValue = normalizedId;
        serializedDefinition.FindProperty("sciencePointCost").intValue = 1;
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        asset.Normalize();

        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        catalog.EditorAddDefinition(asset);
        selectedDefinition = asset;
        newTechnologyId = DefaultNewTechnologyId;
        MarkUnsavedChanges();
        EditorGUIUtility.PingObject(asset);
    }

    private static Vector2 GetGraphPosition(TechnologyDefinition definition, int index)
    {
        var position = definition.GraphPosition;
        if (position == Vector2.zero)
        {
            position = new Vector2(40f + index % 5 * 250f, 40f + index / 5 * 130f);
        }

        return position;
    }

    private static Rect GetNodeRect(TechnologyDefinition definition, int index)
    {
        var position = GetGraphPosition(definition, index);
        return new Rect(position.x, position.y, NodeWidth, NodeHeight);
    }

    private void BuildGraphCache(IReadOnlyList<TechnologyDefinition> definitions)
    {
        graphIndexByDefinition.Clear();
        graphRectsByDefinition.Clear();

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null || graphIndexByDefinition.ContainsKey(definition))
            {
                continue;
            }

            graphIndexByDefinition.Add(definition, i);
            graphRectsByDefinition.Add(definition, GetNodeRect(definition, i));
        }
    }

    private bool TryGetGraphNodeRect(TechnologyDefinition definition, out Rect nodeRect)
    {
        nodeRect = default;
        return definition != null && graphRectsByDefinition.TryGetValue(definition, out nodeRect);
    }

    private float CalculateGraphContentWidth(float visibleWidth)
    {
        var width = Mathf.Max(MinGraphWidth, visibleWidth);
        width = Mathf.Max(width, graphScroll.x + visibleWidth + GraphHorizontalExpansionPadding);

        foreach (var nodeRect in graphRectsByDefinition.Values)
        {
            width = Mathf.Max(width, nodeRect.xMax + GraphHorizontalExpansionPadding);
        }

        return width;
    }

    private bool IsNodeVisible(Rect nodeRect)
    {
        return ExpandRect(graphVisibleRect, 96f).Overlaps(nodeRect);
    }

    private bool IsConnectionVisible(Rect fromRect, Rect toRect)
    {
        var connectionBounds = Rect.MinMaxRect(
            Mathf.Min(fromRect.xMin, toRect.xMin),
            Mathf.Min(fromRect.yMin, toRect.yMin),
            Mathf.Max(fromRect.xMax, toRect.xMax),
            Mathf.Max(fromRect.yMax, toRect.yMax));
        return ExpandRect(graphVisibleRect, 160f).Overlaps(connectionBounds);
    }

    private static Rect ExpandRect(Rect rect, float padding)
    {
        rect.xMin -= padding;
        rect.xMax += padding;
        rect.yMin -= padding;
        rect.yMax += padding;
        return rect;
    }

    private void SetGraphPosition(TechnologyDefinition definition, Vector2 position)
    {
        var serializedDefinition = new SerializedObject(definition);
        var graphPositionProperty = serializedDefinition.FindProperty("graphPosition");
        var maxX = Mathf.Max(0f, currentGraphContentWidth - NodeWidth);
        graphPositionProperty.vector2Value = new Vector2(
            Mathf.Clamp(position.x, 0f, maxX),
            Mathf.Clamp(position.y, 0f, GraphHeight - NodeHeight));
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
        MarkUnsavedChanges();
    }

    private bool AddPrerequisite(TechnologyDefinition targetDefinition, TechnologyDefinition prerequisiteDefinition)
    {
        if (targetDefinition == null || prerequisiteDefinition == null || targetDefinition == prerequisiteDefinition)
        {
            return false;
        }

        if (DependsOn(prerequisiteDefinition, targetDefinition, new HashSet<TechnologyDefinition>()))
        {
            EditorUtility.DisplayDialog(
                "无法添加依赖",
                $"添加 {prerequisiteDefinition.DisplayName} -> {targetDefinition.DisplayName} 会形成循环依赖。",
                "确定");
            return false;
        }

        var serializedDefinition = new SerializedObject(targetDefinition);
        var prerequisitesProperty = serializedDefinition.FindProperty("prerequisites");

        for (var i = 0; i < prerequisitesProperty.arraySize; i++)
        {
            if (prerequisitesProperty.GetArrayElementAtIndex(i).objectReferenceValue == prerequisiteDefinition)
            {
                return false;
            }
        }

        prerequisitesProperty.InsertArrayElementAtIndex(prerequisitesProperty.arraySize);
        prerequisitesProperty
            .GetArrayElementAtIndex(prerequisitesProperty.arraySize - 1)
            .objectReferenceValue = prerequisiteDefinition;

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        targetDefinition.Normalize();
        EditorUtility.SetDirty(targetDefinition);
        MarkUnsavedChanges();
        return true;
    }

    private bool RemovePrerequisite(TechnologyDefinition targetDefinition, TechnologyDefinition prerequisiteDefinition)
    {
        if (targetDefinition == null || prerequisiteDefinition == null)
        {
            return false;
        }

        var serializedDefinition = new SerializedObject(targetDefinition);
        var prerequisitesProperty = serializedDefinition.FindProperty("prerequisites");

        for (var i = 0; i < prerequisitesProperty.arraySize; i++)
        {
            if (prerequisitesProperty.GetArrayElementAtIndex(i).objectReferenceValue != prerequisiteDefinition)
            {
                continue;
            }

            var originalSize = prerequisitesProperty.arraySize;
            prerequisitesProperty.DeleteArrayElementAtIndex(i);
            if (prerequisitesProperty.arraySize == originalSize)
            {
                prerequisitesProperty.DeleteArrayElementAtIndex(i);
            }

            serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
            targetDefinition.Normalize();
            EditorUtility.SetDirty(targetDefinition);
            MarkUnsavedChanges();
            return true;
        }

        return false;
    }

    private static bool ValidateTechnologyNodeIds(
        IReadOnlyList<TechnologyDefinition> definitions,
        out string errorMessage,
        out TechnologyDefinition invalidDefinition)
    {
        errorMessage = string.Empty;
        invalidDefinition = null;

        if (definitions == null)
        {
            return true;
        }

        var definitionsById = new Dictionary<string, TechnologyDefinition>(StringComparer.Ordinal);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            var technologyId = NormalizeTechnologyId(definition.TechnologyId);
            if (string.IsNullOrEmpty(technologyId))
            {
                invalidDefinition = definition;
                errorMessage = $"科技节点“{definition.DisplayName}”的节点 ID 为空。";
                return false;
            }

            if (!ValidateTechnologyNodeIdFormat(technologyId, out var idFormatError))
            {
                invalidDefinition = definition;
                errorMessage = $"科技节点“{definition.DisplayName}”的节点 ID 不合法：{technologyId}\n{idFormatError}";
                return false;
            }

            if (definitionsById.TryGetValue(technologyId, out var existingDefinition))
            {
                invalidDefinition = definition;
                errorMessage =
                    $"节点 ID 重复：{technologyId}\n"
                    + $"已有节点：{existingDefinition.DisplayName}\n"
                    + $"重复节点：{definition.DisplayName}";
                return false;
            }

            definitionsById.Add(technologyId, definition);
        }

        return true;
    }

    private static bool ValidateTechnologyNodeIdFormat(string technologyId, out string errorMessage)
    {
        errorMessage = string.Empty;
        if (string.IsNullOrWhiteSpace(technologyId))
        {
            errorMessage = TechnologyNodeIdFormatHint;
            return false;
        }

        var parts = technologyId.Trim().Split('_');
        if (parts.Length < 4)
        {
            errorMessage = TechnologyNodeIdFormatHint;
            return false;
        }

        if (!string.Equals(parts[0], TechnologyNodeIdPrefix, StringComparison.Ordinal))
        {
            errorMessage = $"节点 ID 必须以 {TechnologyNodeIdPrefix}_ 开头。{TechnologyNodeIdFormatHint}";
            return false;
        }

        if (!int.TryParse(parts[1], out var row)
            || row < TechnologyNodeId.MinimumRow
            || row > TechnologyNodeId.MaximumRow)
        {
            errorMessage = $"节点 ID 的行号必须是 {TechnologyNodeId.MinimumRow}-{TechnologyNodeId.MaximumRow}。";
            return false;
        }

        if (!int.TryParse(parts[2], out var column) || column <= 0)
        {
            errorMessage = "节点 ID 的列号必须是大于 0 的整数。";
            return false;
        }

        var nodeName = string.Join("_", parts, 3, parts.Length - 3).Trim();
        if (string.IsNullOrWhiteSpace(nodeName))
        {
            errorMessage = "节点 ID 的节点名称不能为空。";
            return false;
        }

        return true;
    }

    private static int RenameTechnologyDefinitionAssets(IReadOnlyList<TechnologyDefinition> definitions)
    {
        if (definitions == null)
        {
            return 0;
        }

        var renamedCount = 0;
        var reservedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition == null)
            {
                continue;
            }

            definition.Normalize();
            EditorUtility.SetDirty(definition);

            var currentPath = AssetDatabase.GetAssetPath(definition);
            if (string.IsNullOrWhiteSpace(currentPath))
            {
                continue;
            }

            var desiredName = MakeSafeFileName(definition.TechnologyId);
            var finalName = GetAvailableAssetName(currentPath, desiredName, reservedPaths);
            var targetPath = BuildAssetPathWithFileName(currentPath, finalName);
            reservedPaths.Add(targetPath);

            if (string.Equals(
                    System.IO.Path.GetFileNameWithoutExtension(currentPath),
                    finalName,
                    StringComparison.Ordinal))
            {
                definition.name = finalName;
                continue;
            }

            var renameError = AssetDatabase.RenameAsset(currentPath, finalName);
            if (!string.IsNullOrEmpty(renameError))
            {
                Debug.LogWarning($"重命名科技节点 SO 失败：{currentPath} -> {finalName}\n{renameError}");
                continue;
            }

            definition.name = finalName;
            renamedCount++;
        }

        return renamedCount;
    }

    private static string GetAvailableAssetName(
        string currentPath,
        string desiredName,
        HashSet<string> reservedPaths)
    {
        var safeDesiredName = MakeSafeFileName(desiredName);
        for (var i = 0; i < 1000; i++)
        {
            var candidateName = i == 0 ? safeDesiredName : $"{safeDesiredName}_{i + 1}";
            var candidatePath = BuildAssetPathWithFileName(currentPath, candidateName);
            if (reservedPaths != null && reservedPaths.Contains(candidatePath))
            {
                continue;
            }

            var existingAsset = AssetDatabase.LoadMainAssetAtPath(candidatePath);
            if (existingAsset == null
                || string.Equals(
                    NormalizeAssetPath(AssetDatabase.GetAssetPath(existingAsset)),
                    NormalizeAssetPath(currentPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                return candidateName;
            }
        }

        return $"{safeDesiredName}_{Guid.NewGuid():N}";
    }

    private static string BuildAssetPathWithFileName(string currentPath, string fileNameWithoutExtension)
    {
        var directory = NormalizeAssetPath(System.IO.Path.GetDirectoryName(currentPath));
        var extension = System.IO.Path.GetExtension(currentPath);
        return string.IsNullOrWhiteSpace(directory)
            ? $"{fileNameWithoutExtension}{extension}"
            : $"{directory}/{fileNameWithoutExtension}{extension}";
    }

    private void SetConnectionMode(GraphConnectionMode newMode)
    {
        connectionMode = newMode;
        connectionSource = null;
        draggingDefinition = null;
        wantsMouseMove = connectionMode != GraphConnectionMode.None;
        Repaint();
    }

    private void HandleGraphMouseMove()
    {
        if (connectionMode == GraphConnectionMode.None || connectionSource == null || Event.current == null)
        {
            return;
        }

        if (Event.current.type == EventType.MouseMove)
        {
            Repaint();
        }
    }

    private string GetConnectionModeHint()
    {
        if (connectionMode == GraphConnectionMode.AddPrerequisite)
        {
            return connectionSource == null
                ? "添加：先点击前置科技"
                : $"添加：再点击目标科技（前置：{connectionSource.DisplayName}）";
        }

        if (connectionMode == GraphConnectionMode.RemovePrerequisite)
        {
            return connectionSource == null
                ? "删除：先点击前置科技"
                : $"删除：再点击目标科技（前置：{connectionSource.DisplayName}）";
        }

        return "普通模式：点击选择，拖动移动节点";
    }

    private static bool DependsOn(
        TechnologyDefinition definition,
        TechnologyDefinition possiblePrerequisite,
        HashSet<TechnologyDefinition> visited)
    {
        if (definition == null || possiblePrerequisite == null || !visited.Add(definition))
        {
            return false;
        }

        var prerequisites = definition.Prerequisites;
        for (var i = 0; i < prerequisites.Count; i++)
        {
            var prerequisite = prerequisites[i];
            if (prerequisite == possiblePrerequisite || DependsOn(prerequisite, possiblePrerequisite, visited))
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawConnectionLine(Vector2 from, Vector2 to, Color color, float thickness, bool drawArrow)
    {
        Handles.color = color;
        Handles.DrawAAPolyLine(thickness, from, to);

        if (!drawArrow)
        {
            return;
        }

        var direction = to - from;
        if (direction.sqrMagnitude < 0.01f)
        {
            return;
        }

        direction.Normalize();
        var perpendicular = new Vector2(-direction.y, direction.x);
        var arrowTip = to;
        var arrowBack = to - direction * 12f;
        Handles.DrawAAConvexPolygon(
            arrowTip,
            arrowBack + perpendicular * 5f,
            arrowBack - perpendicular * 5f);
    }

    private static Vector2 GetRectEdgePoint(Rect rect, Vector2 target)
    {
        var center = rect.center;
        var direction = target - center;
        if (direction.sqrMagnitude < 0.01f)
        {
            return center;
        }

        direction.Normalize();
        var scaleX = Mathf.Abs(direction.x) < 0.001f
            ? float.PositiveInfinity
            : rect.width * 0.5f / Mathf.Abs(direction.x);
        var scaleY = Mathf.Abs(direction.y) < 0.001f
            ? float.PositiveInfinity
            : rect.height * 0.5f / Mathf.Abs(direction.y);

        return center + direction * Mathf.Min(scaleX, scaleY);
    }

    private static void EnsureAssetFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || AssetDatabase.IsValidFolder(folderPath))
        {
            return;
        }

        var parts = folderPath.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets")
        {
            throw new InvalidOperationException($"Asset folder must be under Assets: {folderPath}");
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

    private static string NormalizeTechnologyId(string technologyId)
    {
        return string.IsNullOrWhiteSpace(technologyId) ? string.Empty : technologyId.Trim();
    }

    private static string MakeSafeFileName(string value)
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        var safeName = value ?? string.Empty;
        for (var i = 0; i < invalidChars.Length; i++)
        {
            safeName = safeName.Replace(invalidChars[i], '_');
        }

        return string.IsNullOrWhiteSpace(safeName) ? "TechnologyDefinition" : safeName;
    }

    private static string NormalizeAssetPath(string assetPath)
    {
        return string.IsNullOrWhiteSpace(assetPath)
            ? string.Empty
            : assetPath.Replace('\\', '/');
    }
}
