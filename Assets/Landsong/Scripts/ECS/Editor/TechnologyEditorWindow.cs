#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // Authoring browser, never a second runtime research store.
    public sealed class TechnologyEditorWindow : EditorWindow
    {
        GameCatalogAsset catalog;
        GameDefinitionAsset selected;
        Vector2 graphScroll, detailScroll;
        string search = "", validation = "";
        void OnEnable() => minSize = new Vector2(860, 480);
        [MenuItem("Landsong/ECS/Technology tree authoring")]
        public static void Open() => GetWindow<TechnologyEditorWindow>("ECS 科技配置");
        public static void Open(GameDefinitionAsset asset) { var window = GetWindow<TechnologyEditorWindow>("ECS 科技配置"); window.selected = asset; }
        void OnGUI()
        {
            if (catalog == null) catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                catalog = (GameCatalogAsset)EditorGUILayout.ObjectField(catalog, typeof(GameCatalogAsset), false);
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(170));
                if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(50)))
                { try { using var blob = GameWorldAuthoring.BuildCatalog(catalog); validation = "内容及前置关系校验通过"; } catch (Exception error) { validation = error.Message; } }
                if (GUILayout.Button("保存资源", EditorStyles.toolbarButton, GUILayout.Width(80))) AssetDatabase.SaveAssets();
            }
            EditorGUILayout.HelpBox("修改模板会经 Baking 生效。坐标只影响科技树显示；ID、成本和奖励属于模拟配置。Flags: 0 不重复 / 1 可重复（仅首次发奖）。研究不在此窗口执行。" + (validation == "" ? "" : "\n" + validation), MessageType.Info);
            if (catalog == null) return;
            var nodes = catalog.Definitions.Where(d => d != null && d.Data.Kind == ContentKind.Technology).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(graphScroll, GUILayout.Width(Mathf.Max(240, position.width * .6f))))
                {
                    graphScroll = scroll.scrollPosition;
                    var extent = new Vector2(300, 160); foreach (var n in nodes) extent = Vector2.Max(extent, n.Data.TechnologyPosition + new Vector2(230, 130));
                    var area = GUILayoutUtility.GetRect(extent.x, extent.y); var previous = Handles.color;
                    Handles.BeginGUI();
                    foreach (var n in nodes)
                    {
                        var end = area.position + n.Data.TechnologyPosition + new Vector2(10, 40);
                        foreach (var rule in n.Data.Rules.Where(r => r.Kind == RuleKind.Prerequisite))
                        {
                            var parent = nodes.FirstOrDefault(p => p.Data.Id == rule.Target); if (parent == null) continue;
                            var start = area.position + parent.Data.TechnologyPosition + new Vector2(190, 40);
                            Handles.color = n == selected || parent == selected ? Color.cyan : Color.gray;
                            Handles.DrawAAPolyLine(2, start, new Vector2((start.x + end.x) / 2, start.y), new Vector2((start.x + end.x) / 2, end.y), end);
                        }
                    }
                    Handles.EndGUI(); Handles.color = previous;
                    foreach (var n in nodes)
                    {
                        var color = GUI.backgroundColor;
                        GUI.backgroundColor = n == selected ? Color.cyan : search.Length > 0 && n.Data.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 ? Color.gray : Color.white;
                        if (GUI.Button(new Rect(area.position + n.Data.TechnologyPosition + new Vector2(10, 10), new Vector2(180, 60)), n.Data.Name + "\n" + n.Data.Cost + " 研究点")) selected = n;
                        GUI.backgroundColor = color;
                    }
                }
                using (var scroll = new EditorGUILayout.ScrollViewScope(detailScroll))
                {
                    detailScroll = scroll.scrollPosition;
                    if (selected == null) { EditorGUILayout.LabelField("选择节点查看模板与奖励来源"); return; }
                    EditorGUILayout.ObjectField("当前模板", selected, typeof(GameDefinitionAsset), false);
                    if (GUILayout.Button("在 Project / Inspector 中定位")) { Selection.activeObject = selected; EditorGUIUtility.PingObject(selected); }
                    EditorGUILayout.LabelField("前置 / 首次奖励引用（点击定位来源）", EditorStyles.boldLabel);
                    foreach (var rule in selected.Data.Rules)
                    {
                        var target = catalog.Definitions.FirstOrDefault(d => d != null && d.Data.Id == rule.Target);
                        if (GUILayout.Button(rule.Kind + " · " + (target != null ? target.Data.Name : rule.Target) + " ×" + rule.Amount))
                        { if (target != null && target.Data.Kind == ContentKind.Technology) selected = target; else { Selection.activeObject = target; EditorGUIUtility.PingObject(target); } break; }
                    }
                    EditorGUILayout.HelpBox("Prerequisite：Target=科技 ID，Amount=1；RewardBlueprint：Target=建筑 ID，Amount=最高许可等级；RewardBuff/Feature：Target=对应 ID；RewardItem：Amount=数量。Level 均为 0，功能奖励不可指向 limit.* 限建分组。", MessageType.None);
                    var serialized = new SerializedObject(selected); serialized.Update(); var data = serialized.FindProperty("Data");
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        foreach (var field in new[] { "Name", "Description", "Icon", "Cost", "Flags", "HasTechnologyPosition", "TechnologyPosition", "Rules" }) EditorGUILayout.PropertyField(data.FindPropertyRelative(field), true);
                        serialized.ApplyModifiedProperties();
                    }
                }
            }
        }
    }
}
#endif
