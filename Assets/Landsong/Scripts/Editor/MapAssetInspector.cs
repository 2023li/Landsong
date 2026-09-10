#if UNITY_EDITOR
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(MapAsset))]
    public sealed class MapAssetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var guid = serializedObject.FindProperty("TwcSourceSceneGuid");
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(AssetDatabase.GUIDToAssetPath(guid.stringValue));
            EditorGUI.BeginChangeCheck();
            scene = (SceneAsset)EditorGUILayout.ObjectField("TWC 制图源场景", scene, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck()) guid.stringValue = scene == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(scene));
            DrawPropertiesExcluding(serializedObject, "m_Script", "TwcSourceSceneGuid");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("运行时只加载 Entity SubScene。TWC 源场景仅用于地形导入，不参与运行时加载。", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("从 TWC 更新地形（保留初始建筑）") &&
                    EditorUtility.DisplayDialog("更新 ECS 地形", "只更新此地图的逻辑地形和导入网格。请先烘焙并保存 TWC 制图源场景。", "更新", "取消"))
                    EcsMapIncrementalImport.Import((MapAsset)target);
        }
    }
}
#endif
