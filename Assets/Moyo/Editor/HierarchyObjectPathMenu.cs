#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

namespace Moyo.Unity
{
    internal static class HierarchyObjectPathMenu
    {
        private const string MenuPath = "GameObject/MOYO_复制对象路径";

        [MenuItem(MenuPath, false, 0)]
        private static void CopyObjectPath(MenuCommand menuCommand)
        {
            var target = menuCommand.context as GameObject ?? Selection.activeGameObject;

            if (target == null)
            {
                return;
            }

            var targetTransform = target.transform;
            var relativePath = AnimationUtility.CalculateTransformPath(targetTransform, targetTransform.root);

            EditorGUIUtility.systemCopyBuffer = relativePath;

            if (string.IsNullOrEmpty(relativePath))
            {
                Debug.Log("已复制根物体的相对路径（空字符串）。", target);
                return;
            }

            Debug.Log($"已复制对象路径：{relativePath}", target);
        }

        [MenuItem(MenuPath, true)]
        private static bool ValidateCopyObjectPath()
        {
            return Selection.activeGameObject != null;
        }
    }
}

#endif
