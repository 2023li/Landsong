using System;
using Landsong.GridSystem;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [CustomEditor(typeof(MapContentAuthoring))]
    public sealed class MapContentAuthoringEditor : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            var content = (MapContentAuthoring)target;
            using (new EditorGUI.DisabledScope(Application.isPlaying))
            {
                if (GUILayout.Button("初始化地图", GUILayout.Height(30))) Run(() => GameMapWorkflow.Initialize(content), "初始化完成，请保存场景。");
                if (GUILayout.Button("校验地图")) Run(() => GameMapWorkflow.Validate(content), "地图校验通过。");
                if (GUILayout.Button("烘焙地图", GUILayout.Height(34))) Run(() => GameMapWorkflow.Bake(content), "地图、建筑与菜单引用已同步并保存。");
                if (content.TwcConfiguration != null && GUILayout.Button("编辑 TWC 地形配置")) AssetDatabase.OpenAsset(content.TwcConfiguration);
            }
            EditorGUILayout.HelpBox("通过本场景制作地图。Data/Source 保存实际绘图成果，必须保留；Data/Generated 由烘焙维护。重复初始化只补齐资源，不重置地图。", MessageType.Info);
            base.OnInspectorGUI();
        }
        void Run(Action action, string success)
        {
            try { action(); Debug.Log(success, target); }
            catch (Exception error) { Debug.LogException(error, target); EditorUtility.DisplayDialog("地图操作未完成", error.Message, "确定"); }
        }
    }
}
