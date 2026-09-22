using Landsong.GridSystem;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [CustomEditor(typeof(ProtrudingSlopeTilePreset))]
    public sealed class ProtrudingSlopePresetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox("突出式斜坡：左 + 中×N + 右。请使用 Landsong Slope 构建层；地形和高度仍由 Blueprint / Layer 决定。",MessageType.Info);
            serializedObject.Update();
            foreach(string name in new[]{"Left","Middle","Right"}) EditorGUILayout.PropertyField(serializedObject.FindProperty(name));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
