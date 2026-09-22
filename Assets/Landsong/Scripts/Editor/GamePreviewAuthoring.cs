#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    /// <summary>Idempotent authoring repair for the already migrated Game prefab. Does not read the legacy baseline.</summary>
    public static class GamePreviewAuthoring
    {
        const string ProfilesPath = "Assets/Landsong/Editor/UI/Profiles/";
        [MenuItem("Landsong/UI/刷新游戏预览配方与示例")]
        public static void RebindPreviewRecipes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("请退出运行模式后刷新预览。");
            T PreviewOwner<T>()
                where T : Component
            {
                var matches = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Landsong/UI/Prefabs/GamePanel" }).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(prefab => prefab.GetComponent<T>()).Where(component => component != null).ToArray();
                if (matches.Length != 1)
                    throw new InvalidOperationException(typeof(T).Name + " 的独立预览预制体必须且只能有一个。");
                return matches[0];
            }

            var hud = PreviewOwner<UI_GamePanel_Hud>();
            var building = PreviewOwner<UI_GamePanel_BuildingActionBar>();
            Recipe(hud, "GameHud", UIPreviewKind.GameHud, new[] { new UIPreviewTextBinding("turn", hud.Status), new UIPreviewTextBinding("message", hud.Message), new UIPreviewTextBinding("population", hud.Selection) }, Array.Empty<UIPreviewListBinding>());
            GameFeaturePreviewRecipes.ConfigureRecipes();
            var details = building.DetailsPanel;
            Recipe(building, "BuildingDetails", UIPreviewKind.BuildingDetails, new[] { new UIPreviewTextBinding("title", (TMP_Text)details.Name.placeholder, "建筑名称"), new UIPreviewTextBinding("level", details.Level), new UIPreviewTextBinding("production", details.Block<UI_GamePanel_BuildingDetails_Block_基础产出>().Label), new UIPreviewTextBinding("workers", details.Block<UI_GamePanel_BuildingDetails_Block_岗位>().Jobs), new UIPreviewTextBinding("experience", details.Experience), new UIPreviewTextBinding("position", details.Footer) }, Array.Empty<UIPreviewListBinding>());
            foreach (var name in new[]
            {
                "GameHud",
                "Technology",
                "Talent",
                "BuildingDetails"
            }

            )
            {
                var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(ProfilesPath + name + "Recipe.asset");
                recipe.ApplyToPrefab();
                AssetDatabase.SaveAssetIfDirty(recipe);
            }
        }

        static void Recipe(UIViewBase view, string name, UIPreviewKind kind, UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)
        {
            string path = ProfilesPath + name + "Recipe.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(path);
            if (recipe == null)
            {
                recipe = ScriptableObject.CreateInstance<UIPreviewRecipe>();
                AssetDatabase.CreateAsset(recipe, path);
            }

            recipe.Configure(view, UIPreviewBuilder.EnsureDefaultProfile(ProfilesPath + name + ".asset", kind), texts, lists);
            EditorUtility.SetDirty(recipe);
        }
    }
}
#endif
