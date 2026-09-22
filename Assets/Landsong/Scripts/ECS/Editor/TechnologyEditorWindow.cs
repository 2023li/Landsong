#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    // Authoring browser, never a second runtime research store.
    public sealed class TechnologyEditorWindow : EditorWindow
    {
        TechnologyCatalogAsset catalog;
        TechnologyDefinitionAsset selected;
        Vector2 graphScroll, detailScroll;
        string search = "", validation = "";
        UnityEditor.Editor contentEditor;
        void OnDisable()
        {
            if (contentEditor != null)
                DestroyImmediate(contentEditor);
        }

        void OnEnable() => minSize = new Vector2(860, 480);
        [MenuItem("Landsong/ECS/Technology tree authoring")]
        public static void Open() => GetWindow<TechnologyEditorWindow>("ECS 科技配置");
        public static void Open(TechnologyDefinitionAsset asset)
        {
            var window = GetWindow<TechnologyEditorWindow>("ECS 科技配置");
            window.selected = asset;
        }

        void OnGUI()
        {
            ContentAuthoringContext.DrawContext();
            try { catalog = ContentAuthoringContext.Catalog<TechnologyCatalogAsset>(); }
            catch (Exception) { return; }
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                using (new EditorGUI.DisabledScope(true))
                    EditorGUILayout.ObjectField(catalog, typeof(TechnologyCatalogAsset), false);
                search = GUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(170));
                if (GUILayout.Button("校验", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    try
                    {
                        using var compiled = TechnologyCatalogBaking.Compile(ContentAuthoringContext.Content());
                        validation = "科技编译、费用、奖励和前置关系通过；显示同步与实际运行须按制作手册验收。";
                    }
                    catch (Exception error)
                    {
                        validation = error.Message;
                    }
                }

                if (GUILayout.Button("保存资源", EditorStyles.toolbarButton, GUILayout.Width(80)))
                    AssetDatabase.SaveAssets();
            }

            EditorGUILayout.HelpBox("修改模板会经 Baking 生效。坐标只影响科技树显示；ID、成本和奖励属于模拟配置。可重复配置控制重复研究，奖励仅首次发放。研究不在此窗口执行。" + (validation == "" ? "" : "\n" + validation), MessageType.Info);
            if (catalog == null)
                return;
            var nodes = catalog.Definitions.Where(d => d != null).ToArray();
            using (new EditorGUILayout.HorizontalScope())
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(graphScroll, GUILayout.Width(Mathf.Max(240, position.width * .6f))))
                {
                    graphScroll = scroll.scrollPosition;
                    var extent = new Vector2(300, 160);
                    foreach (var n in nodes)
                        extent = Vector2.Max(extent, n.TreePosition + new Vector2(230, 130));
                    var area = GUILayoutUtility.GetRect(extent.x, extent.y);
                    var previous = Handles.color;
                    Handles.BeginGUI();
                    foreach (var n in nodes)
                    {
                        var end = area.position + n.TreePosition + new Vector2(10, 40);
                        foreach (var rule in n.Prerequisites.TechnologyRequirements)
                        {
                            var parent = nodes.FirstOrDefault(p => p == rule?.Technology);
                            if (parent == null)
                                continue;
                            var start = area.position + parent.TreePosition + new Vector2(190, 40);
                            Handles.color = n == selected || parent == selected ? Color.cyan : Color.gray;
                            Handles.DrawAAPolyLine(2, start, new Vector2((start.x + end.x) / 2, start.y), new Vector2((start.x + end.x) / 2, end.y), end);
                        }
                    }

                    Handles.EndGUI();
                    Handles.color = previous;
                    foreach (var n in nodes)
                    {
                        var color = GUI.backgroundColor;
                        GUI.backgroundColor = n == selected ? Color.cyan : search.Length > 0 && n.Metadata.Name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 ? Color.gray : Color.white;
                        if (GUI.Button(new Rect(area.position + n.TreePosition + new Vector2(10, 10), new Vector2(180, 60)), n.Metadata.Name + "\n" + n.ResearchPointCost + " 研究点"))
                            selected = n;
                        GUI.backgroundColor = color;
                    }
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(detailScroll))
                {
                    detailScroll = scroll.scrollPosition;
                    if (selected == null)
                    {
                        EditorGUILayout.LabelField("选择节点查看模板与奖励来源");
                        return;
                    }

                    EditorGUILayout.ObjectField("当前模板", selected, typeof(TechnologyDefinitionAsset), false);
                    if (GUILayout.Button("在 Project / Inspector 中定位"))
                    {
                        Selection.activeObject = selected;
                        EditorGUIUtility.PingObject(selected);
                    }

                    EditorGUILayout.LabelField("前置 / 首次奖励引用（点击定位来源）", EditorStyles.boldLabel);
                    void Link(string label, UnityEngine.Object asset, string name, int quantity)
                    {
                        if (!GUILayout.Button(label + " · " + name + " ×" + quantity) || asset == null)
                            return;
                        if (asset is TechnologyDefinitionAsset technology)
                            selected = technology;
                        else
                        {
                            Selection.activeObject = asset;
                            EditorGUIUtility.PingObject(asset);
                        }
                    }

                    foreach (var entry in selected.Prerequisites.TechnologyRequirements)
                        if (entry != null)
                            Link("完成前置", entry.Technology, entry.Technology?.Metadata.Name, entry.Required);
                    var rewards = selected.Rewards;
                    foreach (var entry in rewards.Items)
                        if (entry != null)
                            Link("物品奖励", entry.Item, entry.Item?.Metadata.Name, entry.Quantity);
                    foreach (var entry in rewards.Blueprints)
                        if (entry != null)
                            Link("蓝图奖励", entry.Building, entry.Building?.Metadata.Name, entry.GrantedLevel);
                    foreach (var entry in rewards.Buffs)
                        if (entry != null)
                            Link("增益奖励", entry.Buff, entry.Buff?.Metadata.Name, entry.GrantedLevel);
                    foreach (var entry in rewards.Features)
                        if (entry != null)
                            Link("功能许可", entry.Feature, entry.Feature?.Metadata.Name, entry.GrantedLevel);
                    EditorGUILayout.HelpBox("在功能配置中编辑完成前置、奖励和情报。目标直接选择内容资产；执行顺序控制发奖先后，科技前置只接受科技。", MessageType.None);
                    using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                    {
                        UnityEditor.Editor.CreateCachedEditor(selected, typeof(TechnologyDefinitionInspector), ref contentEditor);
                        contentEditor.OnInspectorGUI();
                    }
                }
            }
        }
    }
}
#endif
