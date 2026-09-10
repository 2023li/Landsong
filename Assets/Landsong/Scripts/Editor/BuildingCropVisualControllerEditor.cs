using Landsong.VisualSystem;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    [CustomEditor(typeof(BuildingCropVisualController))]
    public sealed class BuildingCropVisualControllerEditor : OdinEditor
    {
        private static readonly Color PointColor = new Color(0.3f, 1f, 0.45f, 0.85f);

        public override void OnInspectorGUI()
        {
            var controller = (BuildingCropVisualController)target;
            EditorGUILayout.HelpBox(
                "CropVisuals 的直接子对象就是固定种植点。每个空节点只挂 BuildingCropPlantPoint，" +
                "其 Transform 直接决定作物的位置、旋转和统一缩放。可在 Hierarchy 中复制、多选、" +
                "吸附和对齐；同级顺序决定低密度作物选取点位的顺序。",
                MessageType.Info);

            if (controller.EditablePlantPointCount == 0 && controller.HasLegacyPlantPoints)
            {
                EditorGUILayout.HelpBox(
                    $"检测到 {controller.PlantPointCount} 个旧固定点。先转换为空子对象即可直接编辑。",
                    MessageType.Warning);
                if (GUILayout.Button("将旧固定点转换为空子对象"))
                {
                    var createdCount = controller.CreateEditorPlantPointChildrenFromLegacy();
                    SaveControllerChange(controller);
                    Debug.Log($"已生成 {createdCount} 个可编辑的农田空点位。", controller);
                }
            }

            EditorGUILayout.BeginHorizontal();
            var requiresMigration = controller.EditablePlantPointCount == 0
                                    && controller.HasLegacyPlantPoints;
            using (new EditorGUI.DisabledScope(requiresMigration))
            {
                if (GUILayout.Button("新增空点位"))
                {
                    var createdPoint = controller.AddEditorPlantPoint();
                    SaveControllerChange(controller);
                    if (createdPoint != null)
                    {
                        Selection.activeGameObject = createdPoint.gameObject;
                    }
                }
            }

            using (new EditorGUI.DisabledScope(controller.EditablePlantPointCount == 0))
            {
                if (GUILayout.Button("选择全部点位"))
                {
                    var points = controller.GetEditorPlantPoints();
                    var pointObjects = new Object[points.Length];
                    for (var i = 0; i < points.Length; i++)
                    {
                        pointObjects[i] = points[i].gameObject;
                    }

                    Selection.objects = pointObjects;
                }
            }

            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                $"可编辑空点位：{controller.EditablePlantPointCount}",
                EditorStyles.miniLabel);

            base.OnInspectorGUI();
            if (GUILayout.Button("同步点位与生长阶段到 ECS 建筑表现"))
            {
                var root = controller.GetComponentInParent<Landsong.ECS.Authoring.BuildingVisualAuthoring>();
                if (root == null) Debug.LogError("请在 ECS 建筑 Prefab 内编辑作物点位。", controller);
                else { var count = LS_EcsCropVisualCompiler.Compile(controller, root.gameObject); SaveControllerChange(controller); Debug.Log("已同步 " + count + " 个作物阶段渲染节点，保存 Prefab 后重新 Baking 生效。", controller); }
            }
        }

        private void OnSceneGUI()
        {
            if (!(target is BuildingCropVisualController controller))
            {
                return;
            }

            var points = controller.GetEditorPlantPoints();
            using (new Handles.DrawingScope(controller.transform.localToWorldMatrix))
            {
                for (var i = 0; i < points.Length; i++)
                {
                    var point = points[i];
                    var position = point.LocalPosition;
                    Handles.color = PointColor;
                    var buttonSize = HandleUtility.GetHandleSize(point.transform.position) * 0.055f;
                    if (Handles.Button(
                            position,
                            point.LocalRotation,
                            buttonSize,
                            buttonSize,
                            Handles.DotHandleCap))
                    {
                        Selection.activeGameObject = point.gameObject;
                        return;
                    }

                    Handles.Label(position + Vector3.up * 0.1f, point.name);
                }
            }
        }

        private static void SaveControllerChange(BuildingCropVisualController controller)
        {
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            SceneView.RepaintAll();
        }
    }
}
