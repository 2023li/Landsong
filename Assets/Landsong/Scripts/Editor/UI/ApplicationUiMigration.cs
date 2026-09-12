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
    public static class ApplicationUiMigration
    {
        public const string UiPath = "Assets/Landsong/Objects/Prefabs/UI/";
        public const string RootPath = UiPath + "Bootstrap/UI_Root.prefab";
        public const string StartPath = UiPath + "StartPanel/UI_StartPanel.prefab";
        public const string LoadingPath = UiPath + "LoadingPanel/UI_LoadingPanel.prefab";
        public const string BootPath = UiPath + "BootPanel/UI_BootPanel.prefab";
        public const string SettingPath = UiPath + "SettingPanel/UI_SettingPanel.prefab";
        public const string SavePath = UiPath + "SavePanel/UI_SavePanel.prefab";
        public const string ConfirmPath = UiPath + "ConfirmPanel/UI_ConfirmPanel.prefab";
        public const string GamePath = UiPath + "GamePanel/UI_GamePanel.prefab";
        const string OldSettingPath = UiPath + "Common/ECS_UI_Settings.prefab";
        const string Baseline = "Library/LandsongEcs/RefactorBaseline-20260911-193405/";
        const string Marker = "Library/LandsongEcs/application-ui-migrated.txt";
        static TMP_FontAsset font;
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后迁移。");
            if (File.Exists(Marker)) throw new InvalidOperationException("应用UI资产已迁移，禁止从旧场景再次覆盖新资产。");
            foreach (var path in new[] { RootPath, StartPath, LoadingPath, BootPath, SettingPath, SavePath, ConfirmPath, GamePath }) Folder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                MigrateStart();
                MigrateSettings();
                ExtractSimple<UI_BootPanel>(EcsSceneFlow.Boot, BootPath);
                ExtractSimple<UI_LoadingPanel>(EcsSceneFlow.Loading, LoadingPath);
                CreateConfirm();
                var root = CreateRoot();
                InstallScenes(root);
                AssetDatabase.SaveAssets();
                File.WriteAllText(Marker, DateTimeOffset.Now.ToString("O") + "\n应用共享UI资产迁移完成。");
                return "应用UI根、Start、Boot、Loading、Settings、Save、Confirm及四个场景入口已迁移。";
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static void MigrateStart()
        {
            var scene = EditorSceneManager.OpenScene(EcsSceneFlow.Menu, OpenSceneMode.Single);
            var menu = All<UI_StartPanel>(scene).Single(); font = menu.Status.font;
            var source = new OldReferences(scene, Baseline + EcsSceneFlow.Menu, "EcsMainMenu");
            var popRoot = source.Get<GameObject>("NewDynastyPanel");
            var pop = popRoot.AddComponent<UI_StartPanel_GameStartPop>();
            pop.Catalog = source.Get<EcsMapMenuCatalog>("Catalog");
            pop.MapSelection = source.Get<TMP_Dropdown>("MapSelection"); pop.DifficultySelection = source.Get<TMP_Dropdown>("DifficultySelection");
            pop.DynastyName = source.Get<TMP_InputField>("DynastyName"); pop.MapInfo = source.Get<TMP_Text>("MapInfo"); pop.MapPreview = source.Get<Image>("MapPreview");
            pop.CreateDynastyButton = source.Get<Button>("CreateDynastyButton"); pop.BackButton = source.Get<Button>("NewDynastyBackButton");
            menu.NewDynasty = pop; popRoot.name = "GameStartPop";
            StripCanvas(popRoot); BindPresentation(popRoot);
            foreach (var group in popRoot.GetComponents<CanvasGroup>()) { group.alpha = 1; group.interactable = group.blocksRaycasts = true; }
            pop.ConfigureChildren(Array.Empty<UIViewBase>());
            var menuPreview = UIPreviewBuilder.EnsureDefaultProfile("Assets/Landsong/Editor/UI/Profiles/Menu.asset", UIPreviewKind.Menu);
            UIPreviewBuilder.Apply(pop, menuPreview, new[] { new UIPreviewTextBinding("mapDescription", pop.MapInfo) }, Array.Empty<UIPreviewListBinding>());
            PrefabUtility.SaveAsPrefabAssetAndConnect(popRoot, UiPath + "StartPanel/UI_StartPanel_GameStartPop.prefab", InteractionMode.AutomatedAction);
            popRoot.SetActive(false);

            var oldSave = All<UI_SavePanel>(scene).Single();
            var saveRoot = source.Get<GameObject>("ArchivePanel");
            var save = saveRoot.AddComponent<UI_SavePanel>(); EditorUtility.CopySerialized(oldSave, save); Object.DestroyImmediate(oldSave);
            save.Scroll = save.Rows.GetComponentInParent<ScrollRect>(true);
            save.BackButton = Button("Back", saveRoot.transform, "返回", new Vector2(.8f, .89f), new Vector2(.94f, .95f));
            foreach (var duplicate in save.RowTemplate.GetComponents<UI_SavePanel_ArchiveRow>()) if (duplicate != save.RowTemplate) Object.DestroyImmediate(duplicate);
            save.RowTemplate.gameObject.name = "ArchiveRowTemplate";
            save.RowTemplate.gameObject.SetActive(false);
            BindPresentation(save.RowTemplate.gameObject);
            PrefabUtility.SaveAsPrefabAssetAndConnect(save.RowTemplate.gameObject, UiPath + "SavePanel/UI_SavePanel_ArchiveRow.prefab", InteractionMode.AutomatedAction);
            saveRoot.transform.SetParent(null, false); saveRoot.name = "UI_SavePanel";
            StripCanvas(saveRoot); FullScreen(saveRoot); Configure(save); BindPresentation(saveRoot);
            // Name editing is a fixed part of the dialog, never moved during runtime refresh.
            var editor = save.RenameInput.transform;
            if (editor.IsChildOf(save.Rows))
            {
                editor.SetParent(save.Scroll.transform.parent, false);
                var rect = (RectTransform)editor; rect.anchorMin = new Vector2(.05f, .08f); rect.anchorMax = new Vector2(.95f, .15f); rect.offsetMin = rect.offsetMax = Vector2.zero;
            }
            save.PreviewRoot.SetActive(false); save.ValidateConfiguration();
            var savePreview = UIPreviewBuilder.EnsureDefaultProfile("Assets/Landsong/Editor/UI/Profiles/Save.asset", UIPreviewKind.Save);
            UIPreviewBuilder.Apply(save, savePreview, Array.Empty<UIPreviewTextBinding>(), new[] { new UIPreviewListBinding("slots", save.Rows, (RectTransform)save.RowTemplate.transform, new TMP_Text[] { save.RowTemplate.Label }) });
            SavePrefab(saveRoot, SavePath); Object.DestroyImmediate(saveRoot);
            foreach (var field in new[] { "SettingsPanelRoot", "ExitPanel" }) Object.DestroyImmediate(source.Get<GameObject>(field));
            menu.gameObject.name = "UI_StartPanel"; StripCanvas(menu.gameObject); FullScreen(menu.gameObject); Configure(menu); BindPresentation(menu.gameObject);
            menu.ConfigureChildren(new UIViewBase[] { pop });
            UIPreviewBuilder.Apply(menu, menuPreview, new[] { new UIPreviewTextBinding("continue", menu.Status) }, Array.Empty<UIPreviewListBinding>());
            SavePrefab(menu.gameObject, StartPath);
            // Scene installation below removes these old UI roots after all assets have been authored.
        }
        static void MigrateSettings()
        {
            string actual = AssetDatabase.LoadAssetAtPath<GameObject>(SettingPath) != null ? SettingPath : OldSettingPath;
            var content = PrefabUtility.LoadPrefabContents(actual);
            GameObject wrapper = null;
            try
            {
                var old = content.GetComponent<UI_SettingPanel>();
                wrapper = Rect("UI_SettingPanel", null); SceneManager.MoveGameObjectToScene(wrapper, content.scene); FullScreen(wrapper);
                var shield = wrapper.AddComponent<Image>(); shield.color = new Color(.025f, .035f, .06f, .88f);
                content.transform.SetParent(wrapper.transform, false); content.name = "SettingsContent"; content.SetActive(true);
                var panel = wrapper.AddComponent<UI_SettingPanel>(); EditorUtility.CopySerialized(old, panel); Object.DestroyImmediate(old);
                var rect = (RectTransform)content.transform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = Vector2.zero; rect.sizeDelta = new Vector2(1100, 940);
                StripCanvas(wrapper); Configure(panel); BindPresentation(wrapper); panel.ValidateConfiguration();
                var preview = UIPreviewBuilder.EnsureDefaultProfile("Assets/Landsong/Editor/UI/Profiles/Setting.asset", UIPreviewKind.Setting);
                UIPreviewBuilder.Apply(panel, preview, new[] { new UIPreviewTextBinding("status", panel.Status), new UIPreviewTextBinding("language", panel.LanguageLabel), new UIPreviewTextBinding("resolution", panel.ResolutionLabel) }, Array.Empty<UIPreviewListBinding>());
                if (actual == OldSettingPath) { var error = AssetDatabase.MoveAsset(OldSettingPath, SettingPath); if (!string.IsNullOrEmpty(error)) throw new InvalidOperationException(error); }
                SavePrefab(wrapper, SettingPath);
            }
            finally
            {
                if (content != null) content.transform.SetParent(null);
                PrefabUtility.UnloadPrefabContents(content);
                if (wrapper != null) Object.DestroyImmediate(wrapper);
            }
        }
        static void ExtractSimple<T>(string scenePath, string prefabPath) where T : UIPanelBase
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var panel = All<T>(scene).Single(); panel.gameObject.name = Path.GetFileNameWithoutExtension(prefabPath);
            StripCanvas(panel.gameObject); FullScreen(panel.gameObject); Configure(panel); BindPresentation(panel.gameObject); SavePrefab(panel.gameObject, prefabPath);
        }
        static void CreateConfirm()
        {
            var root = Rect("UI_ConfirmPanel", null); FullScreen(root);
            try
            {
                root.AddComponent<Image>().color = new Color(.02f, .025f, .04f, .87f);
                var card = Rect("Dialog", root.transform); var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(760, 410);
                card.AddComponent<Image>().color = new Color(.09f, .12f, .17f, 1);
                var panel = root.AddComponent<UI_ConfirmPanel>();
                panel.Title = Text("Title", card.transform, "确认操作", 34, new Vector2(.08f, .75f), new Vector2(.92f, .92f));
                panel.Message = Text("Message", card.transform, "请确认本次操作。取消后保留当前状态。", 25, new Vector2(.08f, .3f), new Vector2(.92f, .7f));
                panel.ConfirmButton = Button("Confirm", card.transform, "确认", new Vector2(.54f, .08f), new Vector2(.92f, .23f));
                panel.CancelButton = Button("Cancel", card.transform, "取消", new Vector2(.08f, .08f), new Vector2(.46f, .23f));
                Configure(panel); BindPresentation(root); SavePrefab(root, ConfirmPath);
            }
            finally { Object.DestroyImmediate(root); }
        }
        static ApplicationUiRoot CreateRoot()
        {
            var root = Rect("UI_Root", null);
            try
            {
                var canvas = root.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 0;
                var scaler = root.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
                var raycaster = root.AddComponent<GraphicRaycaster>();
                var eventsRoot = new GameObject("EventSystem"); eventsRoot.transform.SetParent(root.transform, false);
                var events = eventsRoot.AddComponent<EventSystem>(); var input = eventsRoot.AddComponent<InputSystemUIInputModule>(); input.AssignDefaultActions();
                root.AddComponent<AudioListener>();
                var audioRoot = new GameObject("Presentation"); audioRoot.transform.SetParent(root.transform, false);
                var runtime = audioRoot.AddComponent<PresentationRuntime>();
                AudioSource Source(string name, bool loop = false)
                {
                    var go = new GameObject(name); go.transform.SetParent(audioRoot.transform, false);
                    var source = go.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.loop = loop; return source;
                }
                var catalogPath = AssetDatabase.FindAssets("LandsongPresentation t:GamePresentationCatalog").Select(AssetDatabase.GUIDToAssetPath).Single();
                runtime.Configure(AssetDatabase.LoadAssetAtPath<GamePresentationCatalog>(catalogPath), Source("Music", true), Source("Ambient", true), Enumerable.Range(0, 16).Select(i => Source("Effect " + i)).ToArray());
                var layers = new List<UILayerBinding>();
                foreach (UILayer layer in Enum.GetValues(typeof(UILayer))) { var go = Rect(layer.ToString(), root.transform); FullScreen(go); layers.Add(new UILayerBinding(layer, (RectTransform)go.transform)); }
                var inactive = Rect("InactivePanels", root.transform); FullScreen(inactive); inactive.SetActive(false);
                var manager = root.AddComponent<UIManager>();
                var app = root.AddComponent<ApplicationUiRoot>(); var flow = root.AddComponent<GameApplicationFlow>(); app.Manager = manager; app.Flow = flow; app.Presentation = runtime;
                var configPath = "Assets/Landsong/Objects/SO/UIConfig.asset";
                var config = AssetDatabase.LoadAssetAtPath<UIConfig>(configPath);
                if (config == null) { config = ScriptableObject.CreateInstance<UIConfig>(); AssetDatabase.CreateAsset(config, configPath); }
                var configs = new List<UIPanelConfig>();
                foreach (var path in new[] { BootPath, StartPath, LoadingPath, SettingPath, SavePath, ConfirmPath, GamePath })
                {
                    var assetRoot = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? throw new InvalidOperationException("缺少已迁移面板：" + path);
                    var panel = assetRoot.GetComponent<UIPanelBase>() ?? throw new InvalidOperationException(path + "没有根面板组件。");
                    var descriptorPath = Path.ChangeExtension(path, ".asset").Replace('\\', '/');
                    var descriptor = AssetDatabase.LoadAssetAtPath<UIPanelAsset>(descriptorPath);
                    if (descriptor == null) { descriptor = ScriptableObject.CreateInstance<UIPanelAsset>(); AssetDatabase.CreateAsset(descriptor, descriptorPath); }
                    descriptor.Configure(panel); EditorUtility.SetDirty(descriptor);
                    string address = "UI/" + Path.GetFileNameWithoutExtension(path);
                    var settings = AddressableAssetSettingsDefaultObject.Settings;
                    if (settings == null) throw new InvalidOperationException("Addressables没有配置。");
                    settings.BuildAddressablesWithPlayerBuild = AddressableAssetSettings.PlayerBuildOption.BuildWithPlayer;
                    var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(descriptorPath), settings.DefaultGroup); entry.address = address; EditorUtility.SetDirty(settings);
                    bool game = path == GamePath; bool shared = path == SettingPath || path == SavePath || path == ConfirmPath;
                    var layer = path == LoadingPath ? UILayer.Blocker : shared ? UILayer.Popup : game ? UILayer.HUD : UILayer.Normal;
                    configs.Add(new UIPanelConfig(panel.GetType().Name, address, layer, game ? UICachePolicy.DestroyOnClose : UICachePolicy.HideOnClose, shared, game ? UIScopePolicy.Session : UIScopePolicy.Application));
                }
                config.SetPanels(configs.ToArray()); EditorUtility.SetDirty(config);
                manager.Configure(config, canvas, scaler, raycaster, events, input, (RectTransform)inactive.transform, layers.ToArray()); manager.ValidateConfiguration();
                var saved = PrefabUtility.SaveAsPrefabAsset(root, RootPath);
                // Replace the unused old multi-canvas example with the project's configured root variant.
                var variant = (GameObject)PrefabUtility.InstantiatePrefab(saved);
                try { PrefabUtility.SaveAsPrefabAsset(variant, "Assets/Moyo/Prefabs/UIManager.prefab"); }
                finally { Object.DestroyImmediate(variant); }
                return saved.GetComponent<ApplicationUiRoot>();
            }
            finally { Object.DestroyImmediate(root); }
        }
        static void InstallScenes(ApplicationUiRoot root)
        {
            var paths = EcsSceneFlow.BuildScenes;
            foreach (string path in paths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var host = All<EcsGameHost>(scene).SingleOrDefault();
                foreach (var gamePanel in All<UI_GamePanel>(scene).ToArray()) Object.DestroyImmediate(gamePanel.gameObject);
                foreach (var canvas in All<Canvas>(scene).Where(c => c.isRootCanvas).ToArray()) if (canvas != null) Object.DestroyImmediate(canvas.gameObject);
                foreach (var events in All<EventSystem>(scene).ToArray()) if (events != null) Object.DestroyImmediate(events.gameObject);
                foreach (var oldEntry in All<ApplicationSceneEntry>(scene).ToArray()) Object.DestroyImmediate(oldEntry.gameObject);
                foreach (var listener in All<AudioListener>(scene).ToArray()) Object.DestroyImmediate(listener);
                // Boot and menu are entirely rendered by the persistent overlay; obsolete presentation cameras are unnecessary.
                if (path != EcsSceneFlow.Game) foreach (var camera in All<Camera>(scene).ToArray()) Object.DestroyImmediate(camera.gameObject);
                var entryGo = new GameObject("ApplicationSceneEntry"); SceneManager.MoveGameObjectToScene(entryGo, scene);
                var entry = entryGo.AddComponent<ApplicationSceneEntry>(); entry.RootTemplate = root; entry.GameHost = host;
                entry.Role = path == EcsSceneFlow.Boot ? ApplicationSceneRole.Boot : path == EcsSceneFlow.Menu ? ApplicationSceneRole.Menu : path == EcsSceneFlow.Loading ? ApplicationSceneRole.Loading : ApplicationSceneRole.Game;
                if (path == EcsSceneFlow.Game && host == null) throw new InvalidOperationException("Game宿主缺失。");
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
        }
        public static string FinishSceneInstallation()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("请退出运行模式后迁移。");
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(RootPath);
            if (root == null) throw new InvalidOperationException("应用根预制体尚未迁移。");
            var setup = EditorSceneManager.GetSceneManagerSetup();
            try
            {
                InstallScenes(root.GetComponent<ApplicationUiRoot>());
                AssetDatabase.SaveAssets();
                File.WriteAllText(Marker, DateTimeOffset.Now.ToString("O") + "\n应用共享UI资产迁移完成。");
                return "应用共享UI已配置到四个正式场景。";
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        }
        static T[] All<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
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
        static void SavePrefab(GameObject root, string path) { if (PrefabUtility.SaveAsPrefabAsset(root, path) == null) throw new InvalidOperationException("无法保存：" + path); }
        static GameObject Rect(string name, Transform parent) { var go = new GameObject(name, typeof(RectTransform)); if (parent != null) go.transform.SetParent(parent, false); return go; }
        static void FullScreen(GameObject go) { var r = (RectTransform)go.transform; r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero; r.localScale = Vector3.one; }
        static TMP_Text Text(string name, Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            var go = Rect(name, parent); var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>(); t.font = font; t.text = value; t.fontSize = size; t.color = new Color(.89f, .85f, .74f); t.raycastTarget = false; return t;
        }
        static Button Button(string name, Transform parent, string label, Vector2 min, Vector2 max)
        {
            var go = Rect(name, parent); var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.offsetMin = r.offsetMax = Vector2.zero;
            var image = go.AddComponent<Image>(); image.color = new Color(.19f, .25f, .31f); var button = go.AddComponent<Button>(); button.targetGraphic = image;
            Text("Label", go.transform, label, 26, Vector2.zero, Vector2.one).alignment = TextAlignmentOptions.Center; return button;
        }
        static void Folder(string path) { Directory.CreateDirectory(path); AssetDatabase.Refresh(); }
        sealed class OldReferences
        {
            readonly Dictionary<long, Object> objects = new Dictionary<long, Object>(); readonly string source;
            public OldReferences(Scene scene, string path, string type)
            {
                source = Regex.Split(File.ReadAllText(path), @"(?m)^--- !u!").Single(b => b.Contains("::Landsong.ECS.Presentation." + type + "\n") || b.Contains("::Landsong.ECS.Presentation." + type + "\r\n"));
                foreach (var go in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject))
                {
                    Add(go); foreach (var component in go.GetComponents<Component>()) if (component != null) Add(component);
                }
            }
            void Add(Object obj) { var id = GlobalObjectId.GetGlobalObjectIdSlow(obj).targetObjectId; if (id != 0) objects[(long)id] = obj; }
            public T Get<T>(string field) where T : Object
            {
                var match = Regex.Match(source, @"(?m)^  " + Regex.Escape(field) + @": \{fileID: (-?\d+)(?:, guid: ([a-f0-9]+), type: \d+)?\}");
                if (!match.Success) throw new InvalidOperationException("基准中缺少字段：" + field);
                long id = long.Parse(match.Groups[1].Value); Object value;
                if (match.Groups[2].Success)
                {
                    var path = AssetDatabase.GUIDToAssetPath(match.Groups[2].Value);
                    value = AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(v => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(v, out string _, out long local) && local == id);
                }
                else objects.TryGetValue(id, out value);
                return value as T ?? throw new InvalidOperationException("无法解析旧引用：" + field + " / " + id);
            }
        }
    }
}
#endif
