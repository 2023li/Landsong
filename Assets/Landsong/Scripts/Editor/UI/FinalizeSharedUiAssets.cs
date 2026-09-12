#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Presentation;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class FinalizeSharedUiAssets
    {
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后修订界面资产。");
            Edit<UI_StartPanel_GameStartPop>(ApplicationUiMigration.UiPath + "StartPanel/UI_StartPanel_GameStartPop.prefab", pop =>
            {
                pop.MapSelection.ClearOptions();
                pop.MapSelection.AddOptions(pop.Catalog.Maps.Select(map => map.DisplayName).ToList());
                pop.MapSelection.SetValueWithoutNotify(0); pop.MapSelection.RefreshShownValue();
                pop.DifficultySelection.ClearOptions(); pop.DifficultySelection.AddOptions(new List<string> { "简单", "普通", "困难" });
                pop.DifficultySelection.SetValueWithoutNotify(1); pop.DifficultySelection.RefreshShownValue();
                var map = pop.Catalog.Maps[0];
                pop.MapPreview.sprite = map.Thumbnail; pop.MapPreview.preserveAspect = true;
                pop.MapPreview.color = Color.white; pop.MapPreview.gameObject.SetActive(map.Thumbnail != null);
                NormalizeButtons(pop.gameObject);
            });
            Edit<UI_StartPanel>(ApplicationUiMigration.StartPath, menu => NormalizeButtons(menu.gameObject));
            Edit<UI_SettingPanel>(ApplicationUiMigration.SettingPath, setting =>
            {
                var content = setting.transform.Find("SettingsContent") as RectTransform;
                if (content == null) throw new InvalidOperationException("设置面板缺少已配置内容根。");
                RemoveLegacyFullscreenPlaceholder(setting, content);
                Anchors(content, new Vector2(.21f, .065f), new Vector2(.79f, .935f));
                NormalizeButtons(setting.gameObject);
                var footer = setting.ApplyButton.transform.parent.GetComponent<HorizontalLayoutGroup>();
                if (footer == null) throw new InvalidOperationException("设置底部操作区没有明确的横向布局。");
                footer.childControlWidth = footer.childControlHeight = true;
                footer.childForceExpandWidth = true; footer.childForceExpandHeight = false;
                footer.childAlignment = TextAnchor.MiddleCenter; footer.spacing = 16;
                footer.padding = new RectOffset(16, 16, 12, 12);
                SetFooter(setting.ApplyButton, "应用"); SetFooter(setting.DefaultsButton, "恢复默认"); SetFooter(setting.BackButton, "返回");
            });
            Edit<UI_SavePanel>(ApplicationUiMigration.SavePath, save =>
            {
                NormalizeButtons(save.gameObject);
                var card = save.Scroll.transform.parent;
                save.BackButton.transform.SetParent(card, false);
                Anchors((RectTransform)save.BackButton.transform, new Vector2(.84f, .935f), new Vector2(.975f, .99f));
                var title = card.Find("ArchiveTitle");
                if (title == null)
                {
                    var created = new GameObject("ArchiveTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
                    created.transform.SetParent(card, false); title = created.transform;
                }
                var label = title.GetComponent<TMP_Text>();
                label.font = save.RowTemplate.Label.font; label.text = "存档记录"; label.fontSize = 28;
                label.alignment = TextAlignmentOptions.MidlineLeft; label.color = new Color(.89f, .85f, .74f); label.raycastTarget = false;
                Anchors((RectTransform)title, new Vector2(.025f, .935f), new Vector2(.80f, .99f));
                Anchors((RectTransform)save.Scroll.transform, new Vector2(.025f, .05f), new Vector2(.975f, .915f));
            });
            AssetDatabase.SaveAssets();
            return "共享界面修订完成：开局选项及地图示例、设置旧全屏占位清理与底部按钮布局、存档标题与返回区域。";
        }
        static void RemoveLegacyFullscreenPlaceholder(UI_SettingPanel setting, RectTransform content)
        {
            // 旧 Toggle 是滚动区外的铺满占位；实际全屏入口已显式绑定在滚动内容中。
            var legacy = content.Find("Fullscreen");
            if (legacy == null) return;
            if (legacy.GetComponent<Toggle>() == null || setting.Fullscreen == null
                || setting.Fullscreen.transform == legacy || setting.Fullscreen.transform.IsChildOf(legacy))
                throw new InvalidOperationException("全屏占位与正式控件配置不符，不能清理。");
            foreach (var component in setting.GetComponentsInChildren<Component>(true))
            {
                // Transform 的父子关系会随销毁更新；其余序列化引用必须没有指向旧占位。
                if (component == null || component is Transform || component.transform == legacy
                    || component.transform.IsChildOf(legacy)) continue;
                using var serialized = new SerializedObject(component);
                var property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var target = property.objectReferenceValue;
                    var targetTransform = target is GameObject targetObject ? targetObject.transform
                        : target is Component targetComponent ? targetComponent.transform : null;
                    if (targetTransform != null && (targetTransform == legacy || targetTransform.IsChildOf(legacy)))
                        throw new InvalidOperationException("旧全屏占位仍被引用：" + component.name + "." + property.propertyPath);
                }
            }
            UnityEngine.Object.DestroyImmediate(legacy.gameObject);
        }
        static void Edit<T>(string path, Action<T> edit) where T : Component
        {
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                edit(contents.GetComponent<T>());
                ApplicationUiMigration.BindPresentation(contents);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        static void NormalizeButtons(GameObject root)
        {
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (text.transform.parent != button.transform) continue;
                    Anchors(text.rectTransform, Vector2.zero, Vector2.one);
                    text.rectTransform.offsetMin = new Vector2(16, 4); text.rectTransform.offsetMax = new Vector2(-16, -4);
                    text.alignment = TextAlignmentOptions.MidlineLeft;
                    text.textWrappingMode = TextWrappingModes.Normal;
                }
        }
        static void SetFooter(Button button, string caption)
        {
            var layout = button.GetComponent<LayoutElement>(); if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = 180; layout.flexibleWidth = 1; layout.preferredHeight = 64;
            var label = button.GetComponentsInChildren<TMP_Text>(true).Single();
            label.text = caption; label.alignment = TextAlignmentOptions.Center;
        }
        static void Anchors(RectTransform rect, Vector2 min, Vector2 max)
        { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
    }
}
#endif
