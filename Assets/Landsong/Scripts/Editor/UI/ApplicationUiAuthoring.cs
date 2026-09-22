#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Landsong.ECS.Presentation;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using Landsong.Editor.UI;

namespace Landsong.ECS.Editor
{
    // Explicit, one-shot authoring migration. Runtime code never uses these discovery helpers.
    public static class ApplicationUiAuthoring
    {
        public const string UiPath = "Assets/Landsong/UI/Prefabs/";
        public const string RootPath = UiPath + "Bootstrap/UI_Root.prefab";
        public const string StartPath = UiPath + "StartPanel/UI_StartPanel.prefab";
        public const string LoadingPath = UiPath + "LoadingPanel/UI_LoadingPanel.prefab";
        public const string BootPath = UiPath + "BootPanel/UI_BootPanel.prefab";
        public const string SettingPath = UiPath + "SettingPanel/UI_SettingPanel.prefab";
        public const string SavePath = UiPath + "SavePanel/UI_SavePanel.prefab";
        public const string ConfirmPath = UiPath + "ConfirmPanel/UI_ConfirmPanel.prefab";
        public const string GamePath = UiPath + "GamePanel/UI_GamePanel.prefab";
        public static void Configure(UIPanelBase panel)
        {
            var group = panel.GetComponent<CanvasGroup>(); if (group == null) group = panel.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 1; group.interactable = group.blocksRaycasts = true;
            panel.ConfigurePanel(group); panel.gameObject.SetActive(true);
        }
        public static void StripCanvas(GameObject root)
        {
            foreach (var component in root.GetComponentsInChildren<GraphicRaycaster>(true)) Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<CanvasScaler>(true)) Object.DestroyImmediate(component);
            foreach (var component in root.GetComponentsInChildren<Canvas>(true)) Object.DestroyImmediate(component);
        }
        public static void BindPresentation(GameObject root)
        {
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            { var binding = input.GetComponent<UI_Common_InputFocusBinding>(); if (binding == null) binding = input.gameObject.AddComponent<UI_Common_InputFocusBinding>(); binding.Target = input; }
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                var input = text.GetComponentInParent<TMP_InputField>(true);
                if (input != null && input.textComponent == text) { var old = text.GetComponent<UI_Common_TextBinding>(); if (old != null) Object.DestroyImmediate(old); continue; }
                var binding = text.GetComponent<UI_Common_TextBinding>(); if (binding == null) binding = text.gameObject.AddComponent<UI_Common_TextBinding>(); binding.Target = text; binding.ButtonLabel = text.GetComponentInParent<Button>(true) != null;
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            { var binding = button.GetComponent<UI_Common_Click>(); if (binding == null) binding = button.gameObject.AddComponent<UI_Common_Click>(); binding.Target = button; }
        }
    }
}
#endif
