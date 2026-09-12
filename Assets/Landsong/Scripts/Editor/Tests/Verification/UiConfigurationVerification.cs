#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Landsong.ECS.Presentation;
using Landsong.Editor.UI;
using Moyo.Unity;
using Sirenix.OdinInspector;
using TMPro;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Landsong.ECS.Editor
{
    /// <summary>验证最终资产与配置契约；Editor 查询只用于检查，不构成运行时引用修补。</summary>
    public static class UiConfigurationVerification
    {
        static readonly string[] PanelPaths = {
            ApplicationUiMigration.BootPath, ApplicationUiMigration.StartPath, ApplicationUiMigration.LoadingPath,
            ApplicationUiMigration.SettingPath, ApplicationUiMigration.SavePath, ApplicationUiMigration.ConfirmPath,
            ApplicationUiMigration.GamePath
        };
        // Unity's generic AddComponent has no arguments. ECS AddComponent<T>(Entity) creates
        // presentation work data and is not a lookup or repair of a fixed GameObject reference.
        static readonly Regex ForbiddenLookup = new Regex(
            @"\b(?:GetComponent|GetComponents|GetComponentInParent|GetComponentsInParent|GetComponentInChildren|GetComponentsInChildren|TryGetComponent|FindObjectOfType|FindObjectsOfType|FindFirstObjectByType|FindAnyObjectByType|FindObjectsByType|FindGameObjectWithTag|FindGameObjectsWithTag)\s*(?:<[^;{}()]+>)?\s*\(|\bAddComponent\s*(?:<[^;{}()]+>\s*\(\s*\)|\()|\bnew\s+GameObject\s*\(|\b(?:GameObject|transform)\s*\.\s*Find\s*\(",
            RegexOptions.Compiled);

        [MenuItem("Landsong/ECS/Verification/UI inspector configuration")]
        public static string Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("UI 配置验证应在退出 Play 后执行，不干扰活动会话。");
            var report = new StringBuilder();
            var failures = new List<string>();
            var actualPanelPaths = AssetDatabase.FindAssets("t:Prefab", new[] { ApplicationUiMigration.UiPath.TrimEnd('/') })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => AssetDatabase.LoadAssetAtPath<GameObject>(path)?.GetComponent<UIPanelBase>() != null)
                .Concat(PanelPaths).Distinct().ToArray();
            var assertions = 0;
            void Check(bool ok, string label)
            {
                assertions++;
                report.AppendLine((ok ? "PASS " : "FAIL ") + label);
                if (!ok) failures.Add(label);
            }
            void Attempt(string label, Action action)
            {
                try { action(); Check(true, label); }
                catch (Exception error)
                {
                    if (error is TargetInvocationException target && target.InnerException != null) error = target.InnerException;
                    Check(false, label + ": " + error.Message);
                }
            }

            var root = AssetDatabase.LoadAssetAtPath<GameObject>(ApplicationUiMigration.RootPath);
            Check(root != null, "唯一 UI 根预制体存在");
            UIConfig registry = null;
            ApplicationUiRoot applicationRoot = null;
            if (root != null)
            {
                var managers = root.GetComponentsInChildren<UIManager>(true);
                var canvases = root.GetComponentsInChildren<Canvas>(true);
                var events = root.GetComponentsInChildren<EventSystem>(true);
                Check(managers.Length == 1 && managers[0].gameObject == root, "UIManager 仅挂在唯一根对象上");
                Check(canvases.Length == 1 && canvases[0].gameObject == root, "UI 根使用同对象唯一 Canvas");
                Check(root.GetComponentsInChildren<CanvasScaler>(true).Length == 1
                    && root.GetComponentsInChildren<GraphicRaycaster>(true).Length == 1, "缩放器和图形输入只有唯一根配置");
                Check(events.Length == 1, "应用 UI 根拥有唯一 EventSystem");
                applicationRoot = root.GetComponent<ApplicationUiRoot>();
                Check(applicationRoot != null, "UI 根绑定应用组合入口");
                if (managers.Length == 1)
                {
                    Attempt("UIManager 显式根引用及注册表校验", managers[0].ValidateConfiguration);
                    VerifyOdinEditor(managers[0], Check);
                    var data = new SerializedObject(managers[0]);
                    registry = data.FindProperty("uiConfig")?.objectReferenceValue as UIConfig;
                    Check(registry != null, "UIManager 直接绑定唯一面板注册表");
                    Check(applicationRoot != null && applicationRoot.Manager == managers[0]
                        && applicationRoot.Flow != null && applicationRoot.Flow.gameObject == root,
                        "应用入口显式绑定同根管理器与流程");
                }
                VerifyComponents(root, Check, Attempt, false);
            }

            var registeredIds = new HashSet<string>(StringComparer.Ordinal);
            if (registry != null)
            {
                Attempt("注册表不包含空项、重复类型或失效资源来源", () => registry.CreateValidatedRegistry());
                foreach (var config in registry.Panels ?? Array.Empty<UIPanelConfig>())
                {
                    if (config == null) continue;
                    registeredIds.Add(config.PanelId);
                    var descriptor = ResolveDescriptor(config, Check);
                    if (descriptor == null) continue;
                    Attempt("资源描述类型与根引用: " + config.PanelId, () => descriptor.Validate(config.PanelId));
                    var prefabPath = AssetDatabase.GetAssetPath(descriptor.PrefabRoot);
                    Check(actualPanelPaths.Contains(prefabPath), "注册项对应实际根面板: " + config.PanelId);
                    Check(!string.IsNullOrWhiteSpace(config.assetAddress) && config.asset == null,
                        "根面板通过已登记资源描述地址按需加载: " + config.PanelId);
                    if (prefabPath == ApplicationUiMigration.GamePath)
                        Check(config.scopePolicy == UIScopePolicy.Session, "Game 根必须属于明确游戏会话");
                    else
                        Check(config.scopePolicy == UIScopePolicy.Application, "跨场景根使用应用作用域: " + config.PanelId);
                    if (prefabPath == ApplicationUiMigration.SettingPath || prefabPath == ApplicationUiMigration.SavePath)
                        Check(config.cachePolicy != UICachePolicy.DestroyOnClose && config.canCloseByBack,
                            "共享设置/存档复用缓存实例并支持返回: " + config.PanelId);
                }
                Check(registry.Panels != null && registry.Panels.Count == actualPanelPaths.Length, "注册表完整覆盖实际根面板，不遗留重复或过期项");
                VerifyOdinEditor(registry, Check);
            }

            foreach (var path in actualPanelPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab != null, "面板预制体存在: " + path);
                if (prefab == null) continue;
                Check(UiPanelLayoutAuthoring.HasStretchRoot(prefab), "根布局按实际 Canvas 拉伸，不保留旧根 Canvas 像素尺寸: " + path);
                var panels = prefab.GetComponentsInChildren<UIPanelBase>(true).Where(p => !IsEditorOnly(p.transform)).ToArray();
                var expectedName = Path.GetFileNameWithoutExtension(path);
                Check(panels.Length == 1 && panels[0].gameObject == prefab,
                    "只有独立根继承 UIPanelBase，嵌套子视图不注册为根: " + expectedName);
                if (panels.Length == 1)
                {
                    var panel = panels[0];
                    if (panel is UI_GamePanel game)
                        Check(game.FeatureRoot != null && game.FeatureRoot.anchorMin == Vector2.zero
                            && game.FeatureRoot.anchorMax == Vector2.one
                            && game.FeatureRoot.offsetMin.y >= FeaturePanelLayoutAuthoring.HudFooterHeight
                            && game.FeatureRoot.offsetMax.y == 0,
                            "普通功能窗为固定 HUD 底栏保留空间");
                    Check(panel.PanelId == expectedName && prefab.name == expectedName, "类型、根对象、Prefab 命名一致: " + expectedName);
                    Check(registeredIds.Contains(panel.PanelId), "面板拥有唯一有效注册: " + expectedName);
                    Attempt("根面板引用和 childViews 归属: " + expectedName, panel.ValidateConfiguration);
                    VerifyOdinEditor(panel, Check);
                    var declaredViews = new HashSet<UIViewBase>();
                    CollectOwnedViews(panel, declaredViews);
                    foreach (var child in prefab.GetComponentsInChildren<UIViewBase>(true).Where(x => !IsEditorOnly(x.transform)))
                        Check(declaredViews.Contains(child), "UIView 子视图有明确所有者: " + expectedName + "/" + child.name);
                }
                Check(prefab.GetComponentsInChildren<Canvas>(true).Length == 0
                    && prefab.GetComponentsInChildren<EventSystem>(true).Length == 0,
                    "业务面板不再另建画布或输入系统: " + expectedName);
                VerifyPresentationBindings(prefab, Check);
                VerifyControlWidths(prefab, Check);
                VerifyComponents(prefab, Check, Attempt, true);
                VerifyPreview(prefab, path, Check, Attempt);
            }

            foreach (var path in EcsSceneFlow.BuildScenes)
            {
                Scene scene = default;
                try
                {
                    scene = EditorSceneManager.OpenPreviewScene(path);
                    var roots = scene.GetRootGameObjects();
                    var entries = roots.SelectMany(x => x.GetComponentsInChildren<ApplicationSceneEntry>(true)).ToArray();
                    Check(entries.Length == 1, path + " 只有一个显式应用场景入口");
                    Check(!roots.SelectMany(x => x.GetComponentsInChildren<Canvas>(true)).Any(), path + " 不再持有场景 UI 画布");
                    Check(!roots.SelectMany(x => x.GetComponentsInChildren<EventSystem>(true)).Any(), path + " 不再持有场景 EventSystem");
                    Check(!roots.SelectMany(x => x.GetComponentsInChildren<UIPanelBase>(true)).Any(), path + " 不再放置未受管理根面板");
                    Check(!roots.SelectMany(x => x.GetComponentsInChildren<UIManager>(true)).Any(), path + " 通过同一预制体安装管理器");
                    if (entries.Length == 1)
                    {
                        var entry = entries[0];
                        Check(entry.RootTemplate != null && entry.RootTemplate == applicationRoot, path + " 引用相同的应用 UI 根");
                        var expectedRole = path == EcsSceneFlow.Game ? ApplicationSceneRole.Game
                            : path == EcsSceneFlow.Menu ? ApplicationSceneRole.Menu
                            : path == EcsSceneFlow.Loading ? ApplicationSceneRole.Loading : ApplicationSceneRole.Boot;
                        Check(entry.Role == expectedRole, path + " 入口职责与场景一致");
                        var hosts = roots.SelectMany(x => x.GetComponentsInChildren<EcsGameHost>(true)).ToArray();
                        if (expectedRole == ApplicationSceneRole.Game)
                            Check(hosts.Length == 1 && entry.GameHost == hosts[0] && hosts[0].Camera != null
                                && hosts[0].Sun != null && hosts[0].Catalog != null,
                                "Game 入口显式绑定世界宿主、相机与地图配置");
                        else Check(entry.GameHost == null, path + " 不携带失效游戏宿主");
                    }
                    foreach (var sceneRoot in roots) VerifyMissingScripts(sceneRoot, Check);
                }
                catch (Exception error) { Check(false, path + " 场景静态验证: " + error.Message); }
                finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            }

            VerifySourceRules(Check);
            UiRuntimeCallVerification.Verify(Check);
            VerifyInspectorLabels(Check);
            Attempt("预览制作与运行清理隔离回归", () => report.AppendLine(UIPreviewVerification.Run()));
            report.AppendLine("Assertions: " + assertions);
            Directory.CreateDirectory("Library/LandsongEcs");
            File.WriteAllText("Library/LandsongEcs/ui-configuration-verification.txt", report.ToString(), new UTF8Encoding(false));
            if (failures.Count > 0)
                throw new InvalidOperationException("UI 配置验证失败 " + failures.Count + " 项。\n"
                    + string.Join("\n", failures.Take(40)) + "\n完整报告：Library/LandsongEcs/ui-configuration-verification.txt");
            return report.ToString();
        }

        static UIPanelAsset ResolveDescriptor(UIPanelConfig config, Action<bool, string> check)
        {
            if (config.asset != null) return config.asset;
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) { check(false, "Addressables 配置缺失"); return null; }
            var entries = settings.groups.Where(g => g != null).SelectMany(g => g.entries)
                .Where(e => e.address == config.assetAddress).ToArray();
            check(entries.Length == 1, "资源描述地址唯一有效: " + config.assetAddress);
            if (entries.Length != 1) return null;
            var path = AssetDatabase.GUIDToAssetPath(entries[0].guid);
            var descriptor = AssetDatabase.LoadAssetAtPath<UIPanelAsset>(path);
            check(descriptor != null, "地址指向 UIPanelAsset 而不是未绑定 GameObject: " + config.assetAddress);
            return descriptor;
        }

        static void VerifyComponents(GameObject prefab, Action<bool, string> check, Action<string, Action> attempt, bool verifyUiNaming)
        {
            VerifyMissingScripts(prefab, check);
            foreach (var transform in prefab.GetComponentsInChildren<Transform>(true))
            {
                if (IsEditorOnly(transform)) continue;
                check(!string.IsNullOrWhiteSpace(transform.name), prefab.name + " 不存在空对象名称");
                var projectComponents = transform.GetComponents<MonoBehaviour>().Where(x => x != null && IsProjectType(x.GetType())).ToArray();
                foreach (var duplicate in projectComponents.GroupBy(x => x.GetType()).Where(g => g.Count() > 1))
                    check(false, prefab.name + "/" + transform.name + " 重复挂载 " + duplicate.Key.Name);
                foreach (var component in projectComponents)
                {
                    var type = component.GetType();
                    var validation = type.GetMethod("ValidateConfiguration", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    if (validation != null)
                        attempt(prefab.name + "/" + transform.name + " " + type.Name + " 配置校验", () => validation.Invoke(component, null));
                    foreach (var field in SerializedFields(type))
                    {
                        if (!typeof(Object).IsAssignableFrom(field.FieldType)
                            || !field.IsDefined(typeof(RequiredAttribute), true)) continue;
                        check((Object)field.GetValue(component) != null,
                            prefab.name + "/" + transform.name + " 必需引用: " + type.Name + "." + field.Name);
                    }
                    if (verifyUiNaming && IsUiController(type)) VerifyUiScriptPath(component, check);
                }
            }
        }

        static void VerifyControlWidths(GameObject prefab, Action<bool, string> check)
        {
            foreach (var group in prefab.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true))
            {
                if (!group.enabled || group.childControlWidth) continue;
                foreach (Transform child in group.transform)
                {
                    if (IsEditorOnly(child)) continue;
                    var element = child.GetComponent<LayoutElement>();
                    if (element != null && element.ignoreLayout) continue;
                    if (child.GetComponent<Button>() == null && child.GetComponent<TMP_InputField>() == null
                        && child.GetComponent<TMP_Dropdown>() == null && child.GetComponent<InputField>() == null
                        && child.GetComponent<Dropdown>() == null) continue;
                    var rect = child as RectTransform;
                    var fitter = child.GetComponent<ContentSizeFitter>(); var aspect = child.GetComponent<AspectRatioFitter>();
                    bool selfWidth = fitter != null && fitter.horizontalFit != ContentSizeFitter.FitMode.Unconstrained
                        || aspect != null && (aspect.aspectMode == AspectRatioFitter.AspectMode.HeightControlsWidth
                            || aspect.aspectMode == AspectRatioFitter.AspectMode.FitInParent || aspect.aspectMode == AspectRatioFitter.AspectMode.EnvelopeParent);
                    // LayoutGroup collapses both anchors even when it only drives position.
                    // Authored stretch anchors therefore do not supply a runtime width here.
                    check(rect != null && (rect.sizeDelta.x > 0 || selfWidth),
                        prefab.name + "/" + child.name + " 布局组不管理宽度时，控件具备非零固定宽度或自身宽度驱动");
                }
            }
        }

        static void VerifyUiScriptPath(MonoBehaviour component, Action<bool, string> check)
        {
            if (!component.GetType().Namespace.StartsWith("Landsong", StringComparison.Ordinal)) return;
            var script = MonoScript.FromMonoBehaviour(component);
            var path = AssetDatabase.GetAssetPath(script).Replace('\\', '/');
            var typeName = component.GetType().Name;
            check(typeName.StartsWith("UI_", StringComparison.Ordinal), "界面控制器采用层级类名: " + typeName);
            var parts = typeName.Split('_');
            var folder = parts.Length > 1 ? parts[1] : "未命名";
            check(path.StartsWith("Assets/Landsong/Scripts/UI/" + folder + "/", StringComparison.Ordinal),
                "界面脚本目录对应根 Panel: " + path);
            check(Path.GetFileNameWithoutExtension(path) == typeName, "可挂载脚本文件名与类型一致: " + path);
        }

        static bool IsUiController(Type type)
        {
            if (typeof(UIViewBase).IsAssignableFrom(type) || type.Name.StartsWith("UI_", StringComparison.Ordinal)) return true;
            return SerializedFields(type).Any(field =>
                typeof(Graphic).IsAssignableFrom(field.FieldType) || typeof(Selectable).IsAssignableFrom(field.FieldType)
                || typeof(ScrollRect).IsAssignableFrom(field.FieldType) || field.FieldType == typeof(CanvasGroup));
        }

        static void VerifyPreview(GameObject prefab, string path, Action<bool, string> check, Action<string, Action> attempt)
        {
            var configured = new HashSet<UIPreviewOnly>(prefab.GetComponentsInChildren<UIViewBase>(true)
                .SelectMany(x => x.PreviewBindings ?? Array.Empty<UIPreviewOnly>()).Where(x => x != null));
            foreach (var marker in prefab.GetComponentsInChildren<UIPreviewOnly>(true))
            {
                check(configured.Contains(marker), prefab.name + " 的预览标记由所属视图显式绑定");
                attempt(prefab.name + " 预览标记引用有效", marker.ValidateConfiguration);
                foreach (var sample in marker.SampleObjects)
                    check(sample != null && sample.CompareTag("EditorOnly") && sample.name.StartsWith("PreviewOnly_", StringComparison.Ordinal),
                        prefab.name + " 的样例对象明确标记且不会进入 Player");
            }
            if (path == ApplicationUiMigration.StartPath || path == ApplicationUiMigration.SettingPath
                || path == ApplicationUiMigration.SavePath || path == ApplicationUiMigration.GamePath)
                check(configured.Any(marker => marker.SampleObjects.Length > 0 || marker.SampleTextTargets.Length > 0),
                    prefab.name + " 提供可直接看到且可运行清理的代表性样例");
        }

        static void CollectOwnedViews(UIViewBase view, HashSet<UIViewBase> result)
        {
            if (view == null || !result.Add(view)) return;
            foreach (var child in view.ChildViews ?? Array.Empty<UIViewBase>()) CollectOwnedViews(child, result);
        }

        static void VerifyPresentationBindings(GameObject root, Action<bool, string> check)
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (IsEditorOnly(text.transform)) continue;
                var input = text.GetComponentInParent<TMP_InputField>(true);
                var bindings = text.GetComponents<UI_Common_TextBinding>();
                if (input != null && input.textComponent == text)
                    check(bindings.Length == 0, root.name + " 输入正文保持玩家原文: " + text.name);
                else
                    check(bindings.Length == 1 && bindings[0].Target == text,
                        root.name + " TMP 文字显式绑定本地化: " + text.name);
            }
            foreach (var button in root.GetComponentsInChildren<Button>(true))
            {
                if (IsEditorOnly(button.transform)) continue;
                var bindings = button.GetComponents<UI_Common_Click>();
                check(bindings.Length == 1 && bindings[0].Target == button, root.name + " 按钮显式绑定点击音效: " + button.name);
            }
            foreach (var input in root.GetComponentsInChildren<TMP_InputField>(true))
            {
                if (IsEditorOnly(input.transform)) continue;
                var bindings = input.GetComponents<UI_Common_InputFocusBinding>();
                check(bindings.Length == 1 && bindings[0].Target == input, root.name + " 输入焦点有明确登记: " + input.name);
            }
        }

        static void VerifyMissingScripts(GameObject root, Action<bool, string> check)
        {
            check(root.GetComponentsInChildren<MonoBehaviour>(true).All(x => x != null), root.name + " 没有 Missing Script");
        }
        static bool IsEditorOnly(Transform transform)
        {
            for (var current = transform; current != null; current = current.parent)
                if (current.CompareTag("EditorOnly")) return true;
            return false;
        }

        static void VerifySourceRules(Action<bool, string> check)
        {
            var projectUi = "Assets/Landsong/Scripts/UI";
            check(Directory.Exists(projectUi), "项目界面脚本独立归入 Scripts/UI");
            var paths = (Directory.Exists(projectUi) ? Directory.GetFiles(projectUi, "*.cs", SearchOption.AllDirectories) : Array.Empty<string>())
                .Concat(Directory.GetFiles("Assets/Moyo/UI", "*.cs", SearchOption.AllDirectories))
                .Where(path => !path.Replace('\\', '/').Contains("/Tests/") && !path.Replace('\\', '/').Contains("/Editor/"));
            foreach (var path in paths)
            {
                var code = StripCommentsAndStrings(File.ReadAllText(path));
                check(!ForbiddenLookup.IsMatch(code), "运行时 UI 禁止查找/补建固定组件: " + path);
            }
        }

        // 屏蔽注释和非插值文字。插值保留，避免把其中真正执行的组件查询藏在文字里。
        static string StripCommentsAndStrings(string source)
        {
            var chars = source.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] == '/' && i + 1 < chars.Length && chars[i + 1] == '/')
                { while (i < chars.Length && chars[i] != '\n') chars[i++] = ' '; i--; }
                else if (chars[i] == '/' && i + 1 < chars.Length && chars[i + 1] == '*')
                {
                    chars[i++] = ' '; chars[i] = ' ';
                    while (++i < chars.Length)
                    {
                        if (chars[i] == '*' && i + 1 < chars.Length && chars[i + 1] == '/')
                        { chars[i++] = ' '; chars[i] = ' '; break; }
                        if (chars[i] != '\n') chars[i] = ' ';
                    }
                }
                else if (chars[i] == '"' || chars[i] == '\'')
                {
                    var delimiter = chars[i];
                    var verbatim = delimiter == '"' && i > 0 && source[i - 1] == '@';
                    var interpolated = delimiter == '"' && (i > 0 && source[i - 1] == '$'
                        || i > 1 && (source[i - 1] == '@' && source[i - 2] == '$' || source[i - 1] == '$' && source[i - 2] == '@'));
                    if (!interpolated) chars[i] = ' ';
                    while (++i < chars.Length)
                    {
                        var value = chars[i]; if (!interpolated) chars[i] = value == '\n' ? '\n' : ' ';
                        if (!verbatim && value == '\\' && i + 1 < chars.Length) { i++; if (!interpolated) chars[i] = ' '; continue; }
                        if (value != delimiter) continue;
                        if (verbatim && i + 1 < chars.Length && chars[i + 1] == '"') { i++; if (!interpolated) chars[i] = ' '; continue; }
                        break;
                    }
                }
            }
            return new string(chars);
        }

        static void VerifyInspectorLabels(Action<bool, string> check)
        {
            var paths = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets/Landsong/Scripts", "Assets/Moyo" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !path.Contains("/Editor/") && !path.Contains("/Tests/")).ToArray();
            var inspected = new HashSet<Type>();
            foreach (var path in paths)
            {
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                var type = script != null ? script.GetClass() : null;
                if (type == null || !IsProjectType(type)
                    || (!typeof(MonoBehaviour).IsAssignableFrom(type) && !typeof(ScriptableObject).IsAssignableFrom(type))) continue;
                VerifyLabelsInType(type, inspected, check);
            }
        }

        static void VerifyLabelsInType(Type type, HashSet<Type> inspected, Action<bool, string> check)
        {
            if (!IsProjectType(type) || !inspected.Add(type)) return;
            if (type.IsEnum)
            {
                foreach (var value in type.GetFields(BindingFlags.Public | BindingFlags.Static))
                    check(HasChineseLabel(value), "可编辑枚举使用 Odin 中文标签: " + type.Name + "." + value.Name);
                return;
            }
            foreach (var field in SerializedFields(type))
            {
                check(HasChineseLabel(field), "检查器字段使用 Odin 中文标签: " + field.DeclaringType.Name + "." + field.Name);
                var nested = field.FieldType;
                if (nested.IsArray) nested = nested.GetElementType();
                else if (nested.IsGenericType && nested.GetGenericTypeDefinition() == typeof(List<>)) nested = nested.GetGenericArguments()[0];
                if (nested != null && !typeof(Object).IsAssignableFrom(nested)) VerifyLabelsInType(nested, inspected, check);
            }
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Where(p => p.IsDefined(typeof(ShowInInspectorAttribute), true)))
                check(HasChineseLabel(property), "显式显示的属性使用 Odin 中文标签: " + type.Name + "." + property.Name);
        }

        static IEnumerable<FieldInfo> SerializedFields(Type type)
        {
            for (var current = type; current != null && IsProjectType(current); current = current.BaseType)
                foreach (var field in current.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (field.IsStatic || field.IsLiteral || field.IsInitOnly || field.IsNotSerialized
                        || field.IsDefined(typeof(HideInInspector), true)) continue;
                    if (!(field.IsPublic || field.IsDefined(typeof(SerializeField), true)
                        || field.IsDefined(typeof(SerializeReference), true) || field.IsDefined(typeof(ShowInInspectorAttribute), true))) continue;
                    if (field.IsDefined(typeof(SerializeReference), true) || field.IsDefined(typeof(ShowInInspectorAttribute), true)
                        || IsSerializedType(field.FieldType)) yield return field;
                }
        }

        static bool IsSerializedType(Type type)
        {
            if (type.IsPointer || typeof(Delegate).IsAssignableFrom(type) || type.IsInterface) return false;
            if (type.IsPrimitive || type == typeof(string) || type.IsEnum || typeof(Object).IsAssignableFrom(type)) return true;
            if (type.IsArray) return type.GetArrayRank() == 1 && IsSerializedType(type.GetElementType());
            if (type.IsGenericType)
                return type.GetGenericTypeDefinition() == typeof(List<>) && IsSerializedType(type.GetGenericArguments()[0]);
            return type.IsSerializable;
        }
        static bool HasChineseLabel(MemberInfo member)
        {
            var label = member.GetCustomAttributesData().FirstOrDefault(a => a.AttributeType == typeof(LabelTextAttribute));
            return label != null && label.ConstructorArguments.Count > 0 && label.ConstructorArguments[0].Value is string text
                && Regex.IsMatch(text, @"[\u3400-\u9fff]");
        }
        static void VerifyOdinEditor(Object target, Action<bool, string> check)
        {
            UnityEditor.Editor editor = null;
            try
            {
                editor = UnityEditor.Editor.CreateEditor(target);
                check(editor is Sirenix.OdinInspector.Editor.OdinEditor, "实际 Inspector 使用 Odin 属性树: " + target.name);
            }
            finally { if (editor != null) Object.DestroyImmediate(editor); }
        }
        static bool IsProjectType(Type type) => type != null && type.Namespace != null
            && (type.Namespace.StartsWith("Landsong", StringComparison.Ordinal) || type.Namespace.StartsWith("Moyo", StringComparison.Ordinal));
    }
}
#endif
