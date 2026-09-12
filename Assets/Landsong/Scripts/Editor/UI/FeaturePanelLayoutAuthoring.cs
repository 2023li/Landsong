#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class FeaturePanelLayoutAuthoring
    {
        public const float HudFooterHeight = 56;
        public static void ReserveHudFooter()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后修订功能区边距。");
            var contents = PrefabUtility.LoadPrefabContents(ApplicationUiMigration.GamePath);
            try
            {
                UiPanelLayoutAuthoring.RequireStretchRoot(contents);
                var game = contents.GetComponent<UI_GamePanel>();
                if (game == null || game.FeatureRoot == null || game.FeatureRoot.parent != contents.transform)
                    throw new InvalidOperationException("普通功能区必须显式绑定 Game 根的直接子对象。");
                var feature = game.FeatureRoot;
                feature.anchorMin = Vector2.zero; feature.anchorMax = Vector2.one;
                feature.offsetMin = new Vector2(0, HudFooterHeight); feature.offsetMax = Vector2.zero;
                if (PrefabUtility.SaveAsPrefabAsset(contents, ApplicationUiMigration.GamePath) == null)
                    throw new InvalidOperationException("功能区底栏边距保存失败。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
