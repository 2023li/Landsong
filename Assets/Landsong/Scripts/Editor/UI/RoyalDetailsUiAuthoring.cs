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
    public static class RoyalDetailsUiAuthoring
    {
        const string PrefabPath = "Assets/Landsong/UI/Prefabs/GamePanel/Views/UI_GamePanel_Royal.prefab";
        const string GamePath = "Assets/Landsong/UI/Prefabs/GamePanel/UI_GamePanel.prefab";
        public static void ConfigurePrefab()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后修复王室详情布局。");
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                using var rootLayout = UiPanelLayoutAuthoring.Preserve(contents.transform as RectTransform);
                var detail = contents.GetComponentInChildren<UI_GamePanel_RoyalPersonDetails>(true);
                if (detail == null) throw new InvalidOperationException("王室详情缺少显式绑定。");
                var tabs = detail.PersonTab.transform.parent as RectTransform;
                var tabLayout = tabs != null ? tabs.GetComponent<HorizontalLayoutGroup>() : null;
                var actions = detail.PersonContent.transform.Find("Person actions") as RectTransform;
                var actionLayout = actions != null ? actions.GetComponent<VerticalLayoutGroup>() : null;
                if (tabLayout == null || actionLayout == null || detail.OverviewTab.transform.parent != tabs
                    || !detail.Designate.transform.IsChildOf(actions) || !detail.Execute.transform.IsChildOf(actions)
                    || !detail.Marriage.transform.IsChildOf(actions) || !detail.Requests.transform.IsChildOf(actions))
                    throw new InvalidOperationException("王室详情页签/动作区没有绑定原有明确布局。");
                tabs.anchorMin = new Vector2(0, 1); tabs.anchorMax = Vector2.one; tabs.pivot = new Vector2(.5f, 1);
                tabs.anchoredPosition = Vector2.zero; tabs.sizeDelta = new Vector2(0, 44);
                ConfigureLayout(tabLayout, 4);
                ConfigureButton(detail.PersonTab); ConfigureButton(detail.OverviewTab);
                var content = (RectTransform)detail.PersonContent.transform;
                content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one;
                content.offsetMin = Vector2.zero; content.offsetMax = new Vector2(0, -48);
                actions.anchorMin = Vector2.zero; actions.anchorMax = Vector2.right; actions.pivot = new Vector2(.5f, 0);
                actions.anchoredPosition = new Vector2(0, 8); actions.sizeDelta = new Vector2(-16, 84);
                ConfigureLayout(actionLayout, 4);
                var firstRow = ActionRow(actions, "FirstActionRow"); var secondRow = ActionRow(actions, "SecondActionRow");
                detail.Designate.transform.SetParent(firstRow, false); detail.Execute.transform.SetParent(firstRow, false);
                detail.Marriage.transform.SetParent(secondRow, false); detail.Requests.transform.SetParent(secondRow, false);
                ConfigureButton(detail.Designate); ConfigureButton(detail.Execute); ConfigureButton(detail.Marriage); ConfigureButton(detail.Requests);
                var scroll = (RectTransform)detail.DetailScroll.transform;
                scroll.anchorMin = new Vector2(.02f, 0); scroll.anchorMax = new Vector2(.98f, .59f);
                scroll.offsetMin = new Vector2(0, 102); scroll.offsetMax = Vector2.zero;
                detail.DescriptionLayout.preferredHeight = 380;
                detail.gameObject.SetActive(true); detail.PersonContent.SetActive(true); detail.OverviewHost.gameObject.SetActive(false);
                ApplicationUiAuthoring.BindPresentation(contents);
                rootLayout.Dispose();
                if (PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath) == null) throw new InvalidOperationException("无法保存王室详情布局。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            BindPortraitCache();
            AssetDatabase.SaveAssets();
        }
        static void BindPortraitCache()
        {
            var contents = PrefabUtility.LoadPrefabContents(GamePath);
            try
            {
                var game = contents.GetComponent<UI_GamePanel>();
                var cache = contents.GetComponent<PortraitCache>();
                if (game == null || game.Court == null || cache == null)
                    throw new InvalidOperationException("游戏主预制体缺少王室面板或肖像缓存。");
                foreach (var binding in game.Court.GetComponentsInChildren<UI_Common_PortraitImageBinding>(true))
                    binding.Cache = cache;
                if (PrefabUtility.SaveAsPrefabAsset(contents, GamePath) == null)
                    throw new InvalidOperationException("无法保存王室肖像缓存引用。");
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        static void ConfigureLayout(HorizontalOrVerticalLayoutGroup group, float spacing)
        {
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            group.childAlignment = TextAnchor.MiddleCenter; group.spacing = spacing;
            group.padding = new RectOffset(4, 4, 4, 4);
        }
        static RectTransform ActionRow(RectTransform parent, string name)
        {
            var row = parent.Find(name) as RectTransform;
            if (row == null)
            {
                var created = new GameObject(name, typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
                created.transform.SetParent(parent, false); row = (RectTransform)created.transform;
            }
            var group = row.GetComponent<HorizontalLayoutGroup>();
            var layout = row.GetComponent<LayoutElement>();
            if (group == null || layout == null) throw new InvalidOperationException("王室动作行布局配置不完整。");
            ConfigureLayout(group, 4); group.padding = new RectOffset();
            layout.minHeight = 32; layout.preferredHeight = 36; layout.flexibleHeight = 0;
            layout.minWidth = 0; layout.flexibleWidth = 1;
            return row;
        }
        static void ConfigureButton(Button button)
        {
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(180, 36);
            var layout = button.GetComponent<LayoutElement>();
            if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 0; layout.preferredWidth = 140; layout.flexibleWidth = 1;
            layout.minHeight = 32; layout.preferredHeight = 36; layout.flexibleHeight = 0;
            foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
            {
                text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                text.rectTransform.offsetMin = new Vector2(4, 2); text.rectTransform.offsetMax = new Vector2(-4, -2);
                text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
                text.enableAutoSizing = true; text.fontSizeMin = Mathf.Min(12, text.fontSize); text.fontSizeMax = text.fontSize;
            }
        }
    }
}
#endif
