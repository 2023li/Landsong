#if UNITY_EDITOR
using System;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class GameFeaturePreviewRecipes
    {
        const string Views = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/";
        const string Profiles = "Assets/Landsong/Editor/UI/Profiles/";
        const string TemplatePath = "EditorPreviewTemplates/TechnologyDetailTemplate";
        public static string Run()
        {
            ConfigureRecipes();
            foreach (var name in new[] { "Technology", "Talent", "RoyalDetails", "Marriage", "SoldierDetails" })
                AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(Profiles + name + "Recipe.asset").ApplyToPrefab();
            AssetDatabase.SaveAssets();
            return "科技、人才、王室、婚姻与士兵详情示例，以及弹窗动作布局已配置。";
        }
        public static void ConfigureRecipes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后制作预览。");
            AuthorDetailTemplate();
            var technology = AssetDatabase.LoadAssetAtPath<GameObject>(Views + "UI_GamePanel_Technology.prefab").GetComponent<UI_GamePanel_Technology>();
            var tree = technology.TechnologyTree;
            var template = (RectTransform)technology.transform.Find(TemplatePath);
            var templateLabel = template.GetComponentInChildren<TMP_Text>(true);
            Configure("Technology", technology, UIPreviewKind.Technology,
                new[] { new UIPreviewTextBinding("title", tree.Header) },
                new[]
                {
                    new UIPreviewListBinding("nodes", tree.GraphScroll.content, (RectTransform)tree.NodeTemplate.transform,
                        new TMP_Text[] { tree.NodeTemplate.Label }, new[] { 0 }, UIPreviewListLayout.GraphGrid(), new[] { "{0}\n{1}\n{2}" }),
                    new UIPreviewListBinding("details", tree.DetailRows, template, new[] { templateLabel }, new[] { 0 },
                        textKeys: new[] { "selected", "description", "status", "progress", "cost" }),
                    new UIPreviewListBinding("queue", tree.DetailRows, template, new[] { templateLabel }, new[] { 0 },
                        textFormats: new[] { "{0}\n{1}" })
                });
            var talent = AssetDatabase.LoadAssetAtPath<GameObject>(Views + "UI_GamePanel_Talent.prefab").GetComponent<UI_GamePanel_Talent>();
            Configure("Talent", talent, UIPreviewKind.Talent,
                new[] { new UIPreviewTextBinding("title", talent.CourtGraph.Header) },
                new[] { new UIPreviewListBinding("people", talent.CourtGraph.NodesRoot, (RectTransform)talent.CourtGraph.NodeTemplate.transform,
                    new TMP_Text[] { talent.CourtGraph.NodeTemplate.Label }, new[] { 0 }, UIPreviewListLayout.GraphGrid(), new[] { "{0}\n{1} · {2}\n{3}" }) });
            RoyalDetailsUiAuthoring.ConfigureRecipe();
            BuildingWorkforceUiAuthoring.ConfigureLayout();
            GameModalUiAuthoring.Configure();
            AssetDatabase.SaveAssets();
        }
        static void AuthorDetailTemplate()
        {
            var path = Views + "UI_GamePanel_Technology.prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                using var rootLayout = UiPanelLayoutAuthoring.Preserve(contents.transform as RectTransform);
                var owner = contents.GetComponent<UI_GamePanel_Technology>();
                if (owner.transform.Find(TemplatePath) == null)
                {
                    var root = owner.transform.Find("EditorPreviewTemplates");
                    if (root == null)
                    {
                        var created = new GameObject("EditorPreviewTemplates", typeof(RectTransform));
                        created.transform.SetParent(owner.transform, false); created.tag = "EditorOnly"; created.SetActive(false); root = created.transform;
                    }
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Landsong/Objects/Prefabs/UI/GamePanel/Items/UI_GamePanel_Row.prefab").GetComponent<UI_GamePanel_Row>();
                    var item = new GameObject("TechnologyDetailTemplate", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                    item.transform.SetParent(root, false); item.tag = "EditorOnly";
                    var background = item.GetComponent<Image>(); background.color = source.Select.targetGraphic.color; background.raycastTarget = false;
                    var layout = item.GetComponent<LayoutElement>(); layout.minHeight = 42; layout.preferredHeight = 90;
                    var textObject = new GameObject("DetailText", typeof(RectTransform), typeof(TextMeshProUGUI)); textObject.transform.SetParent(item.transform, false);
                    var label = textObject.GetComponent<TextMeshProUGUI>();
                    label.font = source.Label.font; label.fontSize = source.Label.fontSize; label.color = source.Label.color;
                    label.alignment = TextAlignmentOptions.MidlineLeft; label.raycastTarget = false; label.textWrappingMode = TextWrappingModes.Normal;
                    label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
                    label.rectTransform.offsetMin = new Vector2(12, 6); label.rectTransform.offsetMax = new Vector2(-12, -6);
                    item.SetActive(false);
                }
                var column = owner.TechnologyTree.DetailRows.GetComponent<VerticalLayoutGroup>();
                if (column == null) throw new InvalidOperationException("科技详情缺少原有纵向条目布局。");
                column.childControlWidth = column.childControlHeight = true;
                column.childForceExpandWidth = true; column.childForceExpandHeight = false;
                ApplicationUiMigration.BindPresentation(contents);
                rootLayout.Dispose();
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        static void Configure(string name, UIViewBase owner, UIPreviewKind kind, UIPreviewTextBinding[] texts, UIPreviewListBinding[] lists)
        {
            var path = Profiles + name + "Recipe.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(path);
            if (recipe == null) { recipe = ScriptableObject.CreateInstance<UIPreviewRecipe>(); AssetDatabase.CreateAsset(recipe, path); }
            recipe.Configure(owner, UIPreviewBuilder.EnsureDefaultProfile(Profiles + name + ".asset", kind), texts, lists);
            EditorUtility.SetDirty(recipe);
        }
    }
}
#endif
