#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public sealed class QuestEditorWindow : EditorWindow
    {
        GameCatalogAsset catalog;
        GameDefinitionAsset selected;
        Vector2 listScroll, detailScroll;
        string search = "", message = "";
        [MenuItem("Landsong/ECS/任务配置与校验")]
        public static void Open() => GetWindow<QuestEditorWindow>("ECS 任务");
        void OnEnable() => catalog = AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset");
        void OnGUI()
        {
            catalog = (GameCatalogAsset)EditorGUILayout.ObjectField("目录", catalog, typeof(GameCatalogAsset), false); if (catalog == null) return;
            search = EditorGUILayout.TextField("查找名称 / ID", search);
            EditorGUILayout.HelpBox("编辑态配置 → Baking → ECS。要求 Key 是持久进度身份，不要重写已有 Key；复制新要求时给它新 Key。QM007 为禁用草稿。运行中禁止编辑。", MessageType.Info);
            if (GUILayout.Button("校验全部任务")) { try { QuestContentValidation.Validate(catalog); message = "任务配置校验通过"; } catch (Exception e) { message = e.Message; } }
            if (message.Length > 0) EditorGUILayout.HelpBox(message, MessageType.Info);
            EditorGUILayout.BeginHorizontal(); listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.Width(260));
            foreach (var asset in catalog.Definitions.Where(a => a != null && a.Data.Kind == ContentKind.Quest))
            {
                var d = asset.Data; if (!(d.Name + d.Id).Contains(search, StringComparison.OrdinalIgnoreCase)) continue;
                if (GUILayout.Button(((d.Flags & 2) != 0 ? "[草稿] " : (d.Flags & 1) != 0 ? "[主线] " : "[随机] ") + d.Name + "\n" + d.Id)) { selected = asset; GUI.FocusControl(null); }
            }
            EditorGUILayout.EndScrollView(); detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            if (selected != null)
            {
                if (GUILayout.Button("在 Project 中定位定义")) { Selection.activeObject = selected; EditorGUIUtility.PingObject(selected); }
                var d = selected.Data;
                EditorGUILayout.LabelField("后续任务", string.Join("、", catalog.Content.Where(x => x.Kind == ContentKind.Quest && x.Rules.Any(r => r.Kind == RuleKind.Prerequisite && r.Target == d.Id)).Select(x => x.Name)), EditorStyles.wordWrappedLabel);
                using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    var serialized = new SerializedObject(selected); serialized.Update(); var data = serialized.FindProperty("Data");
                    foreach (var field in new[] { "Id", "Name", "Description", "Icon", "Duration", "Value", "Flags", "QuestIntensity", "QuestWeight", "ItemQuantityScale", "Rules" }) EditorGUILayout.PropertyField(data.FindPropertyRelative(field), true);
                    EditorGUILayout.HelpBox("来源强度只改变抽取概率。物品 Amount 填基础数量，ItemQuantityScale 在 Baking 时统一缩放物品要求、奖励、失败惩罚；蓝图/Buff/功能许可不缩放。", MessageType.Info);
                    serialized.ApplyModifiedProperties();
                    EditorGUILayout.HelpBox("Flags：0 随机、1 主线，额外 +2 禁用草稿。Value：随机任务类别。Duration：签约期限，0 无限。要求 Amount 为目标数；建筑 B 最低等级/C 仅完工；回合 B=1 为相对签约回合。Prerequisite 在领奖后满足。", MessageType.None);
                    if (GUILayout.Button("仅为缺失 Key 的要求生成稳定 ID"))
                    {
                        Undo.RecordObject(selected, "Assign quest requirement IDs"); foreach (var rule in d.Rules) if (QuestOps.Requirement(rule.Kind) && string.IsNullOrWhiteSpace(rule.Key)) rule.Key = Guid.NewGuid().ToString("N"); EditorUtility.SetDirty(selected);
                    }
                    if (GUILayout.Button("保存配置")) AssetDatabase.SaveAssets();
                }
            }
            EditorGUILayout.EndScrollView(); EditorGUILayout.EndHorizontal();
        }
    }
}
#endif
