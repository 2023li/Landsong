#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Presentation;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Landsong.EditorTools
{
    public static class NightFlowSetup
    {
        [MenuItem("Landsong/UI/预览 Boss 夜字幕 (Play)")]
        public static void PreviewBossNightCaption()
        {
            if (!UIManager.TryGetInstance(out var manager)
                || !manager.TryGetActivePanel<UI_GamePanel>(out var game))
                throw new InvalidOperationException("请先进入游戏局，再播放 Boss 夜字幕预览。");
            game.Hud.PreviewBossNightCaption();
        }

        [MenuItem("Landsong/UI/预览 Boss 夜字幕 (Play)", true)]
        static bool CanPreviewBossNightCaption()
            => EditorApplication.isPlaying && UIManager.TryGetInstance(out var manager)
                && manager.TryGetActivePanel<UI_GamePanel>(out _);

        [MenuItem("Landsong/昼夜/配置夜晚字幕")]
        public static string Configure()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请先退出运行模式。");
            const string hudPath = "Assets/Landsong/UI/Prefabs/GamePanel/Views/UI_GamePanel_Hud.prefab";
            var root = PrefabUtility.LoadPrefabContents(hudPath);
            try
            {
                var hud = root.GetComponent<UI_GamePanel_Hud>();
                var caption = hud.NightCaption;
                if (caption == null)
                {
                    var go = new GameObject("夜晚字幕", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                    go.transform.SetParent(root.transform, false);
                    caption = go.GetComponent<TextMeshProUGUI>();
                }

                var rect = caption.rectTransform;
                rect.anchorMin = new Vector2(.2f, .78f);
                rect.anchorMax = new Vector2(.85f, .92f);
                rect.offsetMin = rect.offsetMax = Vector2.zero;
                caption.font = hud.Message.font;
                caption.fontSharedMaterial = hud.Message.fontSharedMaterial;
                caption.fontSize = 32;
                caption.enableAutoSizing = true;
                caption.fontSizeMin = 18;
                caption.fontSizeMax = 32;
                caption.alignment = TextAlignmentOptions.Center;
                caption.color = Color.white;
                caption.raycastTarget = false;
                caption.textWrappingMode = TextWrappingModes.NoWrap;
                caption.text = "";
                caption.gameObject.SetActive(false);
                var binding = caption.GetComponent<UI_Common_TextBinding>();
                if (binding == null)
                    binding = caption.gameObject.AddComponent<UI_Common_TextBinding>();
                binding.Target = caption;
                hud.NightCaption = caption;
                hud.NightPresentation = AssetDatabase.LoadAssetAtPath<NightCaptionDefinition>("Assets/Landsong/ECSContent/Presentation/Night/LandsongNightCaptionEffects.asset");
                var localization = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(Landsong.ECS.Editor.LanguageContentTools.Path);
                var entries = localization.Text.ToList();
                foreach (var entry in Landsong.ECS.Editor.LanguageContentTools.NativeText().Where(e => e.Key.StartsWith("ui.ecs.night.", StringComparison.Ordinal)))
                    if (!entries.Any(e => e.Table == entry.Table && e.Key == entry.Key))
                        entries.Add(entry);
                localization.Text = entries.ToArray();
                EditorUtility.SetDirty(localization);
                AssetDatabase.SaveAssetIfDirty(localization);
                PrefabUtility.SaveAsPrefabAsset(root, hudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return "夜晚字幕已绑定；文案在每个夜晚 SO，字幕时间与动效在 LandsongNightCaptionEffects，流程秒数在 GameContentSet。";
        }
    }
}
#endif
