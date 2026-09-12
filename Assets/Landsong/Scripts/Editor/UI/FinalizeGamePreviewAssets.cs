#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    /// <summary>Repairs the confirmed zero-width button layouts and visible Game preview captions.</summary>
    public static class FinalizeGamePreviewAssets
    {
        const string Views = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/";

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出运行模式。");
            Edit("HUD", root =>
            {
                var hud = root.GetComponent<UI_GamePanel_Hud>();
                var navigation = root.GetComponentsInChildren<UI_GamePanel_Navigation>(true).Single();
                FixButtonGroup(navigation.Back, navigation.History);
                hud.Message.gameObject.SetActive(true);
            });
            foreach (var panel in new[] { "Talent", "Royal", "Policy" })
                Edit(panel, root =>
                {
                    var court = root.GetComponent<UI_GamePanel_CourtGraph>();
                    FixButtonGroup(court.ZoomOutButton, court.ZoomInButton, court.CloseButton);
                });
            Edit("BuildingDetails", root =>
            {
                var card = root.GetComponent<UI_GamePanel_Building>().BuildingCard;
                var style = card.Style.GetComponentsInChildren<TMP_Text>(true).Single();
                style.text = "外观";
                EnsureNamePlaceholder(card.Name).gameObject.SetActive(true);
            });
            FinalizeGameMigration.RebindPreviewRecipes();
            AssetDatabase.SaveAssets();
            return "Game 导航及人才/王室/政策按钮宽度已修复；HUD 反馈与建筑名称、位置示例已补齐。";
        }

        static void FixButtonGroup(params Button[] buttons)
        {
            var parent = buttons[0].transform.parent;
            if (buttons.Any(button => button == null || button.transform.parent != parent))
                throw new InvalidOperationException("待修复的按钮必须属于同一个已配置布局。");
            var layout = parent.GetComponent<HorizontalLayoutGroup>();
            if (layout == null) throw new InvalidOperationException("按钮容器缺少横向布局配置。");
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleCenter; layout.spacing = 8;
            foreach (var button in buttons)
            {
                var element = button.GetComponent<LayoutElement>();
                if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 64; element.preferredWidth = 112; element.flexibleWidth = 1;
                element.minHeight = 30; element.preferredHeight = 36;
                foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (label.transform.parent != button.transform) continue;
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(8, 2); label.rectTransform.offsetMax = new Vector2(-8, -2);
                    label.alignment = TextAlignmentOptions.Center;
                    label.textWrappingMode = TextWrappingModes.NoWrap;
                }
            }
        }

        static TMP_Text EnsureNamePlaceholder(TMP_InputField input)
        {
            if (input.placeholder is TMP_Text existing) return existing;
            if (input.placeholder != null) throw new InvalidOperationException("建筑名称占位控件必须是 TMP 文字。");
            if (input.textViewport == null || input.textComponent == null)
                throw new InvalidOperationException("建筑名称输入框缺少明确的文字区域配置。");
            var created = new GameObject("BuildingNamePlaceholder", typeof(RectTransform), typeof(TextMeshProUGUI));
            created.transform.SetParent(input.textViewport, false);
            var label = created.GetComponent<TextMeshProUGUI>();
            var source = input.textComponent;
            label.font = source.font; label.fontSize = source.fontSize; label.fontStyle = source.fontStyle;
            label.alignment = source.alignment; label.color = source.color; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.NoWrap; label.text = "建筑名称";
            var rect = label.rectTransform;
            rect.anchorMin = source.rectTransform.anchorMin; rect.anchorMax = source.rectTransform.anchorMax;
            rect.pivot = source.rectTransform.pivot; rect.sizeDelta = source.rectTransform.sizeDelta;
            rect.anchoredPosition = source.rectTransform.anchoredPosition;
            input.placeholder = label;
            return label;
        }

        static void Edit(string name, Action<GameObject> edit)
        {
            var path = Views + "UI_GamePanel_" + name + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try { edit(root); ApplicationUiMigration.BindPresentation(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
#endif
