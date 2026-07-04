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
    private const string DefaultTechnologyFolder = "Assets/Landsong/Objects/SO/Technology";
    private const float GraphWidth = 1800f;
    private const float GraphHeight = 1000f;
    private const float NodeWidth = 190f;
    private const float NodeHeight = 82f;

    private TechnologyCatalog catalog;
    private TechnologyDefinition selectedDefinition;
    private TechnologyDefinition definitionToAdd;
    private string searchText = string.Empty;
    private string newTechnologyId = "新科技";
    private string technologyFolder = DefaultTechnologyFolder;
    private Vector2 listScroll;
    private Vector2 inspectorScroll;
    private Vector2 graphScroll;
    private TechnologyDefinition draggingDefinition;
    private TechnologyDefinition connectionSource;
    private GraphConnectionMode connectionMode;
    private Vector2 dragOffset;

    [MenuItem("Landsong/Technology/Technology Editor")]
    private static void Open()
    {
        var window = GetWindow<TechnologyEditorWindow>("Technology Editor");
        window.minSize = new Vector2(920f, 560f);
        window.Show();
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
        catalog = (TechnologyCatalog)EditorGUILayout.ObjectField("Catalog", catalog, typeof(TechnologyCatalog), false);

        if (GUILayout.Button("Create Catalog", GUILayout.Width(120f)))
        {
            CreateCatalogAsset();
        }

        if (catalog != null && GUILayout.Button("Ping", GUILayout.Width(60f)))
        {
            EditorGUIUtility.PingObject(catalog);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        technologyFolder = EditorGUILayout.TextField("Node Folder", technologyFolder);

        if (catalog != null && GUILayout.Button("Load Folder", GUILayout.Width(110f)))
        {
            catalog.EditorLoadDefinitionsFromFolder(technologyFolder);
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

            var label = $"{definition.DisplayName}  [{definition.SciencePointCost}]";
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
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("prerequisites"), true);
            EditorGUILayout.PropertyField(serializedDefinition.FindProperty("graphPosition"));

            if (serializedDefinition.ApplyModifiedProperties())
            {
                selectedDefinition.Normalize();
                EditorUtility.SetDirty(selectedDefinition);
                catalog.RebuildIndex();
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

        graphScroll = GUI.BeginScrollView(graphRect, graphScroll, new Rect(0f, 0f, GraphWidth, GraphHeight));
        DrawGraphConnections(definitions);
        DrawPendingConnection(definitions);

        for (var i = 0; i < definitions.Count; i++)
        {
            var definition = definitions[i];
            if (definition != null)
            {
                DrawGraphNode(definitions, definition, i);
            }
        }

        GUI.EndScrollView();
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
                var prerequisiteIndex = IndexOfDefinition(definitions, prerequisite);
                if (prerequisiteIndex < 0)
                {
                    continue;
                }

                var fromRect = GetNodeRect(prerequisite, prerequisiteIndex);
                var toRect = GetNodeRect(definition, i);
                DrawConnectionLine(
                    GetRectEdgePoint(fromRect, toRect.center),
                    GetRectEdgePoint(toRect, fromRect.center),
                    new Color(0.45f, 0.6f, 0.75f, 0.75f),
                    2f);
            }
        }

        Handles.EndGUI();
    }

    private void DrawPendingConnection(IReadOnlyList<TechnologyDefinition> definitions)
    {
        if (connectionMode == GraphConnectionMode.None || connectionSource == null || Event.current == null)
        {
            return;
        }

        var sourceIndex = IndexOfDefinition(definitions, connectionSource);
        if (sourceIndex < 0)
        {
            connectionSource = null;
            return;
        }

        var sourceRect = GetNodeRect(connectionSource, sourceIndex);
        var mousePosition = Event.current.mousePosition;

        Handles.BeginGUI();
        DrawConnectionLine(
            GetRectEdgePoint(sourceRect, mousePosition),
            mousePosition,
            connectionMode == GraphConnectionMode.AddPrerequisite
                ? new Color(0.35f, 0.9f, 0.55f, 0.9f)
                : new Color(1f, 0.45f, 0.25f, 0.9f),
            3f);
        Handles.EndGUI();
    }

    private void DrawGraphNode(IReadOnlyList<TechnologyDefinition> definitions, TechnologyDefinition definition, int index)
    {
        var nodeRect = GetNodeRect(definition, index);
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
        GUI.Label(costRect, $"{definition.SciencePointCost} 科技点", EditorStyles.miniBoldLabel);
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

        if (connectionMode == GraphConnectionMode.AddPrerequisite)
        {
            AddPrerequisite(definition, connectionSource);
        }
        else if (connectionMode == GraphConnectionMode.RemovePrerequisite)
        {
            RemovePrerequisite(definition, connectionSource);
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
        EnsureAssetFolder(DefaultCatalogFolder);
        var asset = CreateInstance<TechnologyCatalog>();
        var path = AssetDatabase.GenerateUniqueAssetPath($"{DefaultCatalogFolder}/TechnologyCatalog.asset");
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        catalog = asset;
        EditorGUIUtility.PingObject(catalog);
    }

    private void CreateTechnologyNode()
    {
        EnsureAssetFolder(technologyFolder);

        var normalizedId = NormalizeTechnologyId(newTechnologyId);
        if (string.IsNullOrEmpty(normalizedId))
        {
            normalizedId = "NewTechnology";
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

    private static Vector2 GetNodeCenter(TechnologyDefinition definition, int index)
    {
        var position = GetGraphPosition(definition, index);
        return new Vector2(position.x + NodeWidth * 0.5f, position.y + NodeHeight * 0.5f);
    }

    private static int IndexOfDefinition(IReadOnlyList<TechnologyDefinition> definitions, TechnologyDefinition definition)
    {
        if (definition == null)
        {
            return -1;
        }

        for (var i = 0; i < definitions.Count; i++)
        {
            if (definitions[i] == definition)
            {
                return i;
            }
        }

        return -1;
    }

    private static void SetGraphPosition(TechnologyDefinition definition, Vector2 position)
    {
        var serializedDefinition = new SerializedObject(definition);
        var graphPositionProperty = serializedDefinition.FindProperty("graphPosition");
        graphPositionProperty.vector2Value = new Vector2(
            Mathf.Clamp(position.x, 0f, GraphWidth - NodeWidth),
            Mathf.Clamp(position.y, 0f, GraphHeight - NodeHeight));
        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(definition);
    }

    private void AddPrerequisite(TechnologyDefinition targetDefinition, TechnologyDefinition prerequisiteDefinition)
    {
        if (targetDefinition == null || prerequisiteDefinition == null || targetDefinition == prerequisiteDefinition)
        {
            return;
        }

        if (DependsOn(prerequisiteDefinition, targetDefinition, new HashSet<TechnologyDefinition>()))
        {
            EditorUtility.DisplayDialog(
                "无法添加依赖",
                $"添加 {prerequisiteDefinition.DisplayName} -> {targetDefinition.DisplayName} 会形成循环依赖。",
                "确定");
            return;
        }

        var serializedDefinition = new SerializedObject(targetDefinition);
        var prerequisitesProperty = serializedDefinition.FindProperty("prerequisites");

        for (var i = 0; i < prerequisitesProperty.arraySize; i++)
        {
            if (prerequisitesProperty.GetArrayElementAtIndex(i).objectReferenceValue == prerequisiteDefinition)
            {
                return;
            }
        }

        prerequisitesProperty.InsertArrayElementAtIndex(prerequisitesProperty.arraySize);
        prerequisitesProperty
            .GetArrayElementAtIndex(prerequisitesProperty.arraySize - 1)
            .objectReferenceValue = prerequisiteDefinition;

        serializedDefinition.ApplyModifiedPropertiesWithoutUndo();
        targetDefinition.Normalize();
        EditorUtility.SetDirty(targetDefinition);
        AssetDatabase.SaveAssets();
    }

    private void RemovePrerequisite(TechnologyDefinition targetDefinition, TechnologyDefinition prerequisiteDefinition)
    {
        if (targetDefinition == null || prerequisiteDefinition == null)
        {
            return;
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
            AssetDatabase.SaveAssets();
            return;
        }
    }

    private void SetConnectionMode(GraphConnectionMode newMode)
    {
        connectionMode = newMode;
        connectionSource = null;
        draggingDefinition = null;
        Repaint();
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

    private static void DrawConnectionLine(Vector2 from, Vector2 to, Color color, float thickness)
    {
        Handles.color = color;
        Handles.DrawAAPolyLine(thickness, from, to);

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
        var safeName = value;
        for (var i = 0; i < invalidChars.Length; i++)
        {
            safeName = safeName.Replace(invalidChars[i], '_');
        }

        return string.IsNullOrWhiteSpace(safeName) ? "TechnologyDefinition" : safeName;
    }
}
