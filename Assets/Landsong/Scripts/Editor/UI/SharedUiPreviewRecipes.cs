#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    /// <summary>Authors persistent preview recipes for the already generated shared panels.</summary>
    public static class SharedUiPreviewRecipes
    {
        const string Profiles = "Assets/Landsong/Editor/UI/Profiles/";

        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请先退出运行模式。");
            var start = Load<UI_StartPanel>(ApplicationUiMigration.StartPath);
            var pop = Load<UI_StartPanel_GameStartPop>(ApplicationUiMigration.UiPath + "StartPanel/UI_StartPanel_GameStartPop.prefab");
            var setting = Load<UI_SettingPanel>(ApplicationUiMigration.SettingPath);
            var save = Load<UI_SavePanel>(ApplicationUiMigration.SavePath);
            var menuProfile = UIPreviewBuilder.EnsureDefaultProfile(Profiles + "Menu.asset", UIPreviewKind.Menu);
            Configure("StartRecipe", start, menuProfile,
                new[] { new UIPreviewTextBinding("continue", start.Status) }, Array.Empty<UIPreviewListBinding>());
            Configure("GameStartPopRecipe", pop, menuProfile,
                new[] { new UIPreviewTextBinding("mapDescription", pop.MapInfo) }, Array.Empty<UIPreviewListBinding>());
            Configure("SettingRecipe", setting, UIPreviewBuilder.EnsureDefaultProfile(Profiles + "Setting.asset", UIPreviewKind.Setting),
                new[] { new UIPreviewTextBinding("status", setting.Status), new UIPreviewTextBinding("language", setting.LanguageLabel), new UIPreviewTextBinding("resolution", setting.ResolutionLabel) }, Array.Empty<UIPreviewListBinding>());
            Configure("SaveRecipe", save, UIPreviewBuilder.EnsureDefaultProfile(Profiles + "Save.asset", UIPreviewKind.Save),
                Array.Empty<UIPreviewTextBinding>(), new[] { new UIPreviewListBinding("slots", save.Rows, (RectTransform)save.RowTemplate.transform, new TMP_Text[] { save.RowTemplate.Label }) });
            AssetDatabase.SaveAssets();
            return "Start、开局弹窗、Setting、Save 的四份编辑预览配方已配置。";
        }

        static T Load<T>(string path) where T : UIViewBase
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("共享界面资产不存在：" + path);
            var view = prefab.GetComponent<T>();
            if (view == null) throw new InvalidOperationException("共享界面缺少目标脚本：" + path);
            return view;
        }

        static void Configure(string name, UIViewBase view, UIPreviewProfile profile, UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)
        {
            var path = Profiles + name + ".asset";
            var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(path);
            if (recipe == null) { recipe = ScriptableObject.CreateInstance<UIPreviewRecipe>(); AssetDatabase.CreateAsset(recipe, path); }
            recipe.Configure(view, profile, texts, lists);
            EditorUtility.SetDirty(recipe);
        }
    }
}
#endif
