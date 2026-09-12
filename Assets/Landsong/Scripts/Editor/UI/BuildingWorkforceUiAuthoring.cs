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
    public static class BuildingWorkforceUiAuthoring
    {
        public static void ConfigureLayout()
        {
            const string path = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/UI_GamePanel_Building.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                using var rootLayout = UiPanelLayoutAuthoring.Preserve(contents.transform as RectTransform);
                var card = contents.GetComponent<UI_GamePanel_Building>().BuildingCard;
                var workforce = card.Block<UI_GamePanel_BuildingDetails_Block_岗位>();
                var parent = workforce.Increase.transform.parent;
                var group = parent.GetComponent<HorizontalLayoutGroup>();
                if (group == null || workforce.Decrease.transform.parent != parent || workforce.Budget.transform.parent != parent)
                    throw new InvalidOperationException("岗位预算控件不属于原有横向布局。");
                group.childControlWidth = group.childControlHeight = true;
                group.childForceExpandWidth = group.childForceExpandHeight = false;
                group.childAlignment = TextAnchor.MiddleCenter; group.spacing = 4;
                foreach (var button in new[] { workforce.Increase, workforce.Decrease })
                {
                    var rect = (RectTransform)button.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(36, 36);
                    var layout = button.GetComponent<LayoutElement>(); if (layout == null) layout = button.gameObject.AddComponent<LayoutElement>();
                    layout.minWidth = layout.preferredWidth = 36; layout.flexibleWidth = 0;
                    layout.minHeight = layout.preferredHeight = 36; layout.flexibleHeight = 0;
                    foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                    {
                        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                        text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
                        text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
                    }
                }
                var budget = workforce.Budget.GetComponent<LayoutElement>(); if (budget == null) budget = workforce.Budget.gameObject.AddComponent<LayoutElement>();
                budget.minWidth = 0; budget.preferredWidth = 80; budget.flexibleWidth = 1;
                budget.minHeight = budget.preferredHeight = 36; budget.flexibleHeight = 0;
                workforce.Budget.alignment = TextAlignmentOptions.Center;
                rootLayout.Dispose(); PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
    }
}
#endif
