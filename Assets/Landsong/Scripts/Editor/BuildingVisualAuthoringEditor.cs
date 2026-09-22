using Landsong.Content;
#if UNITY_EDITOR
using Landsong.ECS.Authoring;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    [CustomEditor(typeof(BuildingVisualAuthoring))]
    public sealed class BuildingVisualAuthoringEditor : Sirenix.OdinInspector.Editor.OdinEditor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI(); var root = (BuildingVisualAuthoring)target;
            if (GUILayout.Button("检查建筑根与槽位结构"))
            {
                try { Landsong.EditorTools.BuildingAuthoringWorkflow.ValidateStructure(root.gameObject, root.Definition); Debug.Log("建筑根、锚点与模型归属检查通过。", root); }
                catch (System.Exception error) { Debug.LogError(error.Message, root); }
            }
            EditorGUILayout.HelpBox("每个等级/施工阶段/皮肤使用一个 Building Visual Slot，模型放在其 Content 下。所有槽位必须启用才能 Baking；下方预览只切换 Scene 可见性，不改玩法数据或启用状态。空槽回退到已有低等级模型。", MessageType.Info);
            if (root.Definition != null && GUILayout.Button("打开建筑数值定义")) Selection.activeObject = root.Definition;
            if (GUILayout.Button("补齐所有模型的 Baking 接线"))
            {
                foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true)) { var authoring = renderer.GetComponent<EntityVisualAuthoring>() ?? Undo.AddComponent<EntityVisualAuthoring>(renderer.gameObject); Undo.RecordObject(authoring, "Bind building renderer"); authoring.Owner = root.gameObject; EditorUtility.SetDirty(authoring); }
                Landsong.VisualSystem.BuildingPreviewBindingAuthoring.Rebind(root.gameObject, true);
            }
            if (GUILayout.Button("显示全部编辑节点")) SceneVisibilityManager.instance.Show(root.gameObject, true);
            foreach (var slot in root.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true))
            {
                EditorGUILayout.BeginHorizontal();
                var label = slot.Purpose + " LV" + slot.Level + " / " + slot.Step + " / " + slot.SkinId;
                if (GUILayout.Button(label)) { Selection.activeGameObject = slot.gameObject; EditorGUIUtility.PingObject(slot); }
                if (GUILayout.Button("预览", GUILayout.Width(50)))
                { SceneVisibilityManager.instance.Show(root.gameObject, true); foreach (var other in root.GetComponentsInChildren<BuildingVisualSlotAuthoring>(true)) if (other != slot) SceneVisibilityManager.instance.Hide(other.gameObject, true); SceneView.lastActiveSceneView?.FrameSelected(); }
                EditorGUILayout.EndHorizontal();
                if (!slot.gameObject.activeSelf) EditorGUILayout.HelpBox(slot.name + " 未启用：请启用后 Baking。", MessageType.Error);
                if (slot.GetComponentsInChildren<MeshRenderer>(true).Length == 0) EditorGUILayout.LabelField("  空槽：使用回退模型", EditorStyles.miniLabel);
            }
        }
    }
}
#endif
