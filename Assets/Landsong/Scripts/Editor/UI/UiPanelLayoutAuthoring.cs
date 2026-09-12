#if UNITY_EDITOR
using System;
using Landsong.ECS.Editor;
using Moyo.Unity;
using UnityEditor;
using UnityEngine;

namespace Landsong.Editor.UI
{
    public static class UiPanelLayoutAuthoring
    {
        public static bool HasStretchRoot(GameObject root)
        {
            var rect = root != null ? root.transform as RectTransform : null;
            return rect != null && rect.anchorMin == Vector2.zero && rect.anchorMax == Vector2.one
                && rect.anchoredPosition3D == Vector3.zero && rect.sizeDelta == Vector2.zero
                && rect.localScale == Vector3.one && Quaternion.Angle(rect.localRotation, Quaternion.identity) < .001f;
        }

        public static void RequireStretchRoot(GameObject root)
        {
            if (!HasStretchRoot(root))
                throw new InvalidOperationException((root != null ? root.name : "根面板")
                    + " 根布局配置错误：需要拉伸锚点 0/1、位置与尺寸差 0、缩放 1、无旋转。请修复 Prefab，预览不代替配置。" );
        }

        public static string RepairAllRoots()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后修复面板根布局。");
            int count = 0;
            foreach (var path in new[] { ApplicationUiMigration.BootPath, ApplicationUiMigration.StartPath,
                ApplicationUiMigration.LoadingPath, ApplicationUiMigration.SettingPath, ApplicationUiMigration.SavePath,
                ApplicationUiMigration.ConfirmPath, ApplicationUiMigration.GamePath })
            {
                var contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    if (contents.GetComponent<UIPanelBase>() == null || contents.GetComponent<Canvas>() != null)
                        throw new InvalidOperationException("根布局修复只接受独立业务面板：" + path);
                    var rect = (RectTransform)contents.transform;
                    rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
                    rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = Vector2.zero;
                    rect.anchoredPosition3D = Vector3.zero; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
                    RequireStretchRoot(contents);
                    if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null) throw new InvalidOperationException("根布局保存失败：" + path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                RequireStretchRoot(AssetDatabase.LoadAssetAtPath<GameObject>(path)); count++;
            }
            AssetDatabase.SaveAssets();
            return "业务面板根布局已修复并重新读取验证：" + count + " 个。";
        }

        public static IDisposable Preserve(RectTransform rect) => new RectSnapshot(rect);
        sealed class RectSnapshot : IDisposable
        {
            readonly RectTransform rect;
            readonly Vector2 min, max, pivot, size;
            readonly Vector3 position, scale;
            readonly Quaternion rotation;
            public RectSnapshot(RectTransform target)
            {
                rect = target;
                if (rect == null) return;
                min = rect.anchorMin; max = rect.anchorMax; pivot = rect.pivot; size = rect.sizeDelta;
                position = rect.anchoredPosition3D; scale = rect.localScale; rotation = rect.localRotation;
            }
            public void Dispose()
            {
                if (rect == null) return;
                rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot; rect.sizeDelta = size;
                rect.anchoredPosition3D = position; rect.localScale = scale; rect.localRotation = rotation;
            }
        }
    }
}
#endif
