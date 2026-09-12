#if UNITY_EDITOR
using System;
using System.Linq;
using Landsong.ECS.Presentation;
using Landsong.ECS.Authoring;
using Landsong.Editor.UI;
using Moyo.Unity;
using TMPro;
using UnityEditor;
using Unity.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Editor
{
    public static class GameModalUiAuthoring
    {
        const string Views = "Assets/Landsong/Objects/Prefabs/UI/GamePanel/Views/";
        const string Profiles = "Assets/Landsong/Editor/UI/Profiles/";
        public static void Configure()
        {
            Edit("Marriage", contents =>
            {
                var panel = contents.GetComponent<UI_GamePanel_Marriage>().MarriagePanel;
                Actions(panel.Approve, panel.Refuse, panel.Close);
            });
            Edit("Portrait", contents =>
            {
                var panel = contents.GetComponent<UI_GamePanel_Portrait>().PortraitPanel;
                Actions(panel.Confirm, panel.Close);
            });
            Edit("Soldier", contents =>
            {
                var panel = contents.GetComponent<UI_GamePanel_Soldier>().SoldierDetailsPanel;
                var binding = panel.Portrait.GetComponent<UI_Common_PortraitImageBinding>();
                if (binding == null) binding = panel.Portrait.gameObject.AddComponent<UI_Common_PortraitImageBinding>();
                binding.Target = panel.Portrait; panel.PortraitBinding = binding;
                panel.Portrait.preserveAspect = true; panel.Portrait.raycastTarget = false;
                PortraitSample(contents.GetComponent<UI_GamePanel_Soldier>(), panel.Portrait);
            });
            var gameContents = PrefabUtility.LoadPrefabContents(ApplicationUiMigration.GamePath);
            try
            {
                UiPanelLayoutAuthoring.RequireStretchRoot(gameContents);
                var soldierInstance = gameContents.GetComponentInChildren<UI_GamePanel_Soldier>(true);
                var binding = soldierInstance.SoldierDetailsPanel.PortraitBinding;
                binding.Cache = gameContents.GetComponentsInChildren<PortraitCache>(true).Single();
                binding.ValidateConfiguration();
                PrefabUtility.RecordPrefabInstancePropertyModifications(binding);
                PrefabUtility.SaveAsPrefabAsset(gameContents, ApplicationUiMigration.GamePath);
            }
            finally { PrefabUtility.UnloadPrefabContents(gameContents); }
            var marriage = AssetDatabase.LoadAssetAtPath<GameObject>(Views + "UI_GamePanel_Marriage.prefab").GetComponent<UI_GamePanel_Marriage>();
            var p = marriage.MarriagePanel;
            Recipe("Marriage", marriage, UIPreviewKind.Marriage, new[] {
                new UIPreviewTextBinding("title", p.Title), new UIPreviewTextBinding("hint", p.Hint),
                new UIPreviewTextBinding("person", p.Person.Details), new UIPreviewTextBinding("mate", p.Mate.Details),
                new UIPreviewTextBinding("approve", p.ApproveLabel), new UIPreviewTextBinding("refuse", p.RefuseLabel),
                new UIPreviewTextBinding("close", p.CloseLabel) });
            var soldier = AssetDatabase.LoadAssetAtPath<GameObject>(Views + "UI_GamePanel_Soldier.prefab").GetComponent<UI_GamePanel_Soldier>();
            var s = soldier.SoldierDetailsPanel;
            Recipe("SoldierDetails", soldier, UIPreviewKind.SoldierDetails, new[] {
                new UIPreviewTextBinding("name", s.Name.placeholder as TMP_Text, "士兵姓名"), new UIPreviewTextBinding("age", s.Age),
                new UIPreviewTextBinding("stats", s.Stats), new UIPreviewTextBinding("abilities", s.Abilities) });
        }
        static void Edit(string name, Action<GameObject> apply)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后修复弹窗布局。");
            var path = Views + "UI_GamePanel_" + name + ".prefab";
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                using var rootLayout = UiPanelLayoutAuthoring.Preserve(contents.transform as RectTransform);
                apply(contents);
                rootLayout.Dispose();
                if (PrefabUtility.SaveAsPrefabAsset(contents, path) == null) throw new InvalidOperationException("无法保存弹窗配置：" + name);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }
        static void Actions(params Button[] buttons)
        {
            var parent = buttons[0].transform.parent as RectTransform;
            var group = parent != null ? parent.GetComponent<HorizontalLayoutGroup>() : null;
            if (group == null || buttons.Any(button => button == null || button.transform.parent != parent))
                throw new InvalidOperationException("弹窗动作按钮必须绑定原有同一横向布局。");
            parent.anchorMin = new Vector2(.06f, 0); parent.anchorMax = new Vector2(.94f, 0);
            parent.pivot = new Vector2(.5f, 0); parent.anchoredPosition = new Vector2(0, 12); parent.sizeDelta = new Vector2(0, 42);
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = true; group.childForceExpandHeight = false;
            group.spacing = 8; group.padding = new RectOffset(); group.childAlignment = TextAnchor.MiddleCenter;
            foreach (var button in buttons)
            {
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(160, 42);
                var element = button.GetComponent<LayoutElement>();
                if (element == null) element = button.gameObject.AddComponent<LayoutElement>();
                element.minWidth = 80; element.preferredWidth = 180; element.flexibleWidth = 1;
                element.minHeight = element.preferredHeight = 42; element.flexibleHeight = 0;
                foreach (var text in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
                    text.rectTransform.offsetMin = new Vector2(6, 2); text.rectTransform.offsetMax = new Vector2(-6, -2);
                    text.alignment = TextAlignmentOptions.Center; text.textWrappingMode = TextWrappingModes.NoWrap;
                    text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = text.fontSize;
                }
            }
        }
        static void Recipe(string name, UIViewBase owner, UIPreviewKind kind, UIPreviewTextBinding[] texts)
        {
            var path = Profiles + name + "Recipe.asset";
            var recipe = AssetDatabase.LoadAssetAtPath<UIPreviewRecipe>(path);
            if (recipe == null) { recipe = ScriptableObject.CreateInstance<UIPreviewRecipe>(); AssetDatabase.CreateAsset(recipe, path); }
            recipe.Configure(owner, UIPreviewBuilder.EnsureDefaultProfile(Profiles + name + ".asset", kind), texts, Array.Empty<UIPreviewListBinding>());
            EditorUtility.SetDirty(recipe);
        }
        static void PortraitSample(UIViewBase owner, Image target)
        {
            const string path = Profiles + "SoldierPortraitSample.asset";
            var sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
            if (sprite == null)
            {
                var config = AssetDatabase.LoadAssetAtPath<PortraitConfig>("Assets/Landsong/ECSContent/PortraitConfig.asset");
                using var blob = PortraitLibraryBuilder.Build(config);
                ref var library = ref blob.Value;
                var dna = PortraitOps.Generate(ref library, 123, PersonGender.Male);
                using var pixels = new NativeArray<Color32>(library.Resolution * library.Resolution, Allocator.Temp);
                PortraitPixels.Compose(ref library, dna, 24, PersonGender.Male, pixels);
                var texture = new Texture2D(library.Resolution, library.Resolution, TextureFormat.RGBA32, false);
                texture.name = "SoldierPortraitSample"; texture.filterMode = FilterMode.Point;
                texture.SetPixels32(pixels.ToArray()); texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                sprite.name = "SoldierPortraitSample";
                AssetDatabase.CreateAsset(texture, path); AssetDatabase.AddObjectToAsset(sprite, path); AssetDatabase.SaveAssets();
            }
            var sample = target.transform.Find("PreviewOnly_SoldierPortrait");
            if (sample == null) sample = target.transform.Find("EditorPortraitSample");
            if (sample == null)
            {
                var created = new GameObject("PreviewOnly_SoldierPortrait", typeof(RectTransform), typeof(Image));
                created.transform.SetParent(target.transform, false); sample = created.transform;
            }
            sample.name = "PreviewOnly_SoldierPortrait"; sample.gameObject.tag = "EditorOnly";
            var rect = (RectTransform)sample;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var image = sample.GetComponent<Image>(); image.sprite = sprite; image.preserveAspect = true; image.raycastTarget = false;
            var marker = target.GetComponent<UIPreviewOnly>();
            if (marker == null) marker = target.gameObject.AddComponent<UIPreviewOnly>();
            marker.Configure(new[] { sample.gameObject }, Array.Empty<TMP_Text>(), Array.Empty<string>());
            var markers = owner.PreviewBindings.Where(item => item != null && item != marker).ToList(); markers.Add(marker);
            owner.ConfigurePreview(markers.ToArray());
        }
    }
}
#endif
