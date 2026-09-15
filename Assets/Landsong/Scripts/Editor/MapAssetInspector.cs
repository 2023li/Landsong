using Landsong.ECS.Authoring;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(MapAsset))]
    public sealed class MapAssetInspector : OdinEditor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("这是生成的运行地图，请打开制图源场景编辑并烘焙。", MessageType.Info);
            if (GUILayout.Button("打开制图源场景")) AssetDatabase.OpenAsset(AssetDatabase.LoadAssetAtPath<SceneAsset>(EcsMapIncrementalImport.SourceScene((MapAsset)target)));
            using (new EditorGUI.DisabledScope(true)) base.OnInspectorGUI();
        }
    }
}
