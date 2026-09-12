#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    /// <summary>Authors the HUD navigation and lower controls for the supported 720p / 150% UI scale.</summary>
    public static class GameHudResponsiveLayout
    {
        const string HudPath = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_Hud.prefab";

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后修订 HUD 布局。");
            var panelRoots = UiPanelLayoutAuthoring.RepairAllRoots();
            FeaturePanelLayoutAuthoring.ReserveHudFooter();
            var root = PrefabUtility.LoadPrefabContents(HudPath);
            try
            {
                var hud = root.GetComponent<UI_GamePanel_Hud>();
                if (hud == null) throw new InvalidOperationException("HUD 预制体没有配置主控制器。");
                using var rootLayout = UiPanelLayoutAuthoring.Preserve(root.transform as RectTransform);
                // This is a one-time Editor authoring path from the verified prefab hierarchy, never a runtime lookup.
                var navigation = root.transform.Find("按钮组") as RectTransform;
                if (navigation == null) throw new InvalidOperationException("HUD 缺少现有按钮组布局。");
                var column = navigation.GetComponent<VerticalLayoutGroup>();
                if (column == null) throw new InvalidOperationException("HUD 按钮组缺少纵向布局。");
                navigation.anchorMin = new Vector2(0, .12f); navigation.anchorMax = new Vector2(0, .82f);
                navigation.pivot = new Vector2(0, .5f);
                navigation.offsetMin = new Vector2(12, 0); navigation.offsetMax = new Vector2(192, 0);
                var fitter = navigation.GetComponent<ContentSizeFitter>();
                if (fitter != null) fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = true; column.childForceExpandHeight = false;
                column.childAlignment = TextAnchor.UpperLeft; column.spacing = 6;
                column.padding = new RectOffset();
                for (var index = 0; index < navigation.childCount; index++)
                {
                    var child = navigation.GetChild(index);
                    var item = child.GetComponent<LayoutElement>();
                    if (item == null) item = child.gameObject.AddComponent<LayoutElement>();
                    item.minWidth = 160; item.preferredWidth = 180; item.flexibleWidth = 1;
                    bool research = child.GetComponent<UI_GamePanel_ResearchHud>() != null;
                    item.minHeight = research ? 64 : 28; item.preferredHeight = research ? 72 : 32; item.flexibleHeight = 0;
                }

                var selectionBar = hud.Selection.transform.parent as RectTransform;
                if (selectionBar == null || selectionBar.parent != root.transform)
                    throw new InvalidOperationException("HUD 选中信息必须属于当前底栏。");
                selectionBar.anchorMin = Vector2.zero; selectionBar.anchorMax = Vector2.right;
                selectionBar.pivot = new Vector2(.5f, 0); selectionBar.anchoredPosition = Vector2.zero;
                selectionBar.sizeDelta = new Vector2(0, 56);
                hud.Selection.rectTransform.anchorMin = Vector2.zero; hud.Selection.rectTransform.anchorMax = new Vector2(.72f, 1);
                hud.Selection.rectTransform.offsetMin = new Vector2(16, 6); hud.Selection.rectTransform.offsetMax = new Vector2(-16, -6);
                hud.Selection.alignment = TextAlignmentOptions.MidlineLeft;

                BottomRight((RectTransform)hud.Advance.transform, new Vector2(220, 42), new Vector2(16, 8));
                BottomRight((RectTransform)hud.MoonProgress.transform, new Vector2(300, 34), new Vector2(16, 66));
                hud.Advance.transform.SetAsLastSibling();
                hud.AdvanceLabel.alignment = TextAlignmentOptions.Center;
                hud.AdvanceLabel.textWrappingMode = TextWrappingModes.NoWrap;
                rootLayout.Dispose();
                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            return panelRoots + "HUD 已配置屏幕内导航列、独立底栏及无重叠的阶段/月亮控件；请重新渲染 1280×720、150% 缩放验收图。";
        }

        static void BottomRight(RectTransform rect, Vector2 size, Vector2 inset)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1, 0); rect.pivot = new Vector2(1, 0);
            rect.sizeDelta = size; rect.anchoredPosition = new Vector2(-inset.x, inset.y);
        }
    }
}
#endif
