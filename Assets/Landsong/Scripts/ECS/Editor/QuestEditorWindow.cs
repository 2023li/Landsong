#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using Landsong.ECS.Authoring.Definitions;
using Landsong.ECS.Definitions;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public sealed class QuestEditorWindow : EditorWindow
    {
        QuestCatalogAsset catalog;
        QuestDefinitionAsset selected;
        Vector2 listScroll, detailScroll;
        string search = "", message = "";
        UnityEditor.Editor contentEditor;
        void OnDisable()
        {
            if (contentEditor != null)
                DestroyImmediate(contentEditor);
        }

        [MenuItem("Landsong/ECS/任务配置与校验")]
        public static void Open() => GetWindow<QuestEditorWindow>("ECS 任务");
        void OnEnable() => catalog = AssetDatabase.LoadAssetAtPath<QuestCatalogAsset>("Assets/Landsong/ECSContent/Catalogs/Source/QuestCatalog.asset");
        void OnGUI()
        {
            catalog = (QuestCatalogAsset)EditorGUILayout.ObjectField("目录", catalog, typeof(QuestCatalogAsset), false);
            if (catalog == null)
                return;
            search = EditorGUILayout.TextField("查找名称 / ID", search);
            EditorGUILayout.HelpBox("编辑态配置 → Baking → ECS。要求 Key 是持久进度身份，不要重写已有 Key；复制新要求时给它新 Key。QM007 为禁用草稿。运行中禁止编辑。", MessageType.Info);
            if (GUILayout.Button("校验全部任务"))
            {
                try
                {
                    QuestCatalogValidation.Validate(catalog);
                    message = "任务配置校验通过";
                }
                catch (Exception e)
                {
                    message = e.Message;
                }
            }

            if (message.Length > 0)
                EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.BeginHorizontal();
            listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(260));
            foreach (var asset in catalog.Definitions.Where(a => a != null))
            {
                var d = asset;
                if (!(d.Metadata.Name + d.Metadata.Id).Contains(search, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (GUILayout.Button(((d.Behavior & QuestBehaviorFlags.Draft) != 0 ? "[草稿] " : (d.Behavior & QuestBehaviorFlags.Mainline) != 0 ? "[主线] " : "[随机] ") + d.Metadata.Name + "\n" + d.Metadata.Id))
                {
                    selected = asset;
                    GUI.FocusControl(null);
                }
            }

            EditorGUILayout.EndScrollView();
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            if (selected != null)
            {
                if (GUILayout.Button("在 Project 中定位定义"))
                {
                    Selection.activeObject = selected;
                    EditorGUIUtility.PingObject(selected);
                }

                var d = selected;
                EditorGUILayout.LabelField("后续任务", string.Join("、", catalog.Definitions.Where(x => x != null && x.Prerequisites.QuestRequirements.Any(r => r?.Quest == selected)).Select(x => x.Metadata.Name)), EditorStyles.wordWrappedLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    UnityEditor.Editor.CreateCachedEditor(selected, typeof(QuestDefinitionInspector), ref contentEditor);
                    contentEditor.OnInspectorGUI();
                    EditorGUILayout.HelpBox("任务目标保存稳定进度标识。物品填写基础数量，物品数量倍率在 Baking 时统一缩放；目标和奖励通过独立模块配置。", MessageType.Info);
                    if (GUILayout.Button("仅为缺失 Key 的要求生成稳定 ID"))
                    {
                        Undo.RecordObject(selected, "Assign quest requirement IDs");
                        var serialized = new SerializedObject(selected);
                        var objectives = serialized.FindProperty(nameof(QuestDefinitionAsset.Objectives));
                        var end = objectives.GetEndProperty();
                        while (objectives.Next(true) && !SerializedProperty.EqualContents(objectives, end))
                            if (objectives.name == "Key" && objectives.propertyType == SerializedPropertyType.String && string.IsNullOrWhiteSpace(objectives.stringValue))
                                objectives.stringValue = Guid.NewGuid().ToString("N");
                        serialized.ApplyModifiedProperties();
                        EditorUtility.SetDirty(selected);
                    }

                    if (GUILayout.Button("保存配置"))
                        AssetDatabase.SaveAssets();
                }
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
