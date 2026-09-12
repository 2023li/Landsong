using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moyo.Unity.Tests;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Moyo.Unity.Editor
{
    /// <summary>
    /// 自包含框架行为验证。只使用 PreviewScene 和内存配置，不覆盖用户场景、项目地址目录或玩家文件。
    /// 由主项目验证入口 await RunAsync()；不可在主线程使用 Wait/Result 阻塞编辑器任务队列。
    /// </summary>
    public static class UIFrameworkVerification
    {
        [MenuItem("Moyo/UI/验证框架生命周期")]
        private static async void RunMenu()
        {
            try { Debug.Log(await RunAsync()); }
            catch (Exception error) { Debug.LogException(error); }
        }

        public static async Task<string> RunAsync()
        {
            if (Application.isPlaying) throw new InvalidOperationException("框架隔离验证应在非 Play 状态执行。");
            if (UIManager.TryGetInstance(out _)) throw new InvalidOperationException("存在活动 UI 根，请先由应用正常结束该根再运行隔离验证。");
            var scene = EditorSceneManager.NewPreviewScene();
            var owned = new List<Object>();
            UIManager manager = null;
            var oldOpening = UIFrameworkProbePanel.Opening;
            var oldReleasing = UIFrameworkProbePanel.Releasing;
            var checks = 0;
            try
            {
                var assetA = CreateTemplate<UIFrameworkTestPanelA>(scene, owned, true);
                var assetB = CreateTemplate<UIFrameworkTestPanelB>(scene, owned);
                var assetSession = CreateTemplate<UIFrameworkTestSessionPanel>(scene, owned);
                var registry = ScriptableObject.CreateInstance<UIConfig>();
                owned.Add(registry);
                registry.SetPanels(
                    new UIPanelConfig(assetA, UILayer.Normal, UICachePolicy.HideOnClose),
                    new UIPanelConfig(assetB, UILayer.Normal, UICachePolicy.Permanent, false),
                    new UIPanelConfig(assetSession, UILayer.HUD, UICachePolicy.HideOnClose, false, UIScopePolicy.Session));
                manager = CreateManager(scene, registry, owned);
                manager.Initialize();
                Require(UIManager.Instance == manager && manager.RootCanvas.gameObject == manager.gameObject, "根显式注册"); checks++;

                var entered = NewSignal();
                var resume = NewSignal();
                UIFrameworkProbePanel.Opening = async (panel, args) =>
                { if (panel is UIFrameworkTestPanelA) { entered.TrySetResult(true); await resume.Task; } };
                var first = manager.OpenAsync<UIFrameworkTestPanelA>("第一次");
                await entered.Task;
                var second = manager.OpenAsync<UIFrameworkTestPanelA>("第二次");
                var preload = manager.PreloadAsync<UIFrameworkTestPanelA>();
                UIFrameworkProbePanel.Opening = null;
                resume.TrySetResult(true);
                var a = await first;
                Require(a == await second && a == await preload && a.Creates == 1 && a.Opens == 2
                    && (string)a.Context == "第二次", "并发打开/预加载去重与新上下文"); checks++;
                var child = (UIFrameworkTestChild)a.ChildViews[0];
                Require(child.OwnerPanel == a && child.Manager == manager && child.Creates == 1 && child.IsViewOpen,
                    "子视图配置归属和一次创建"); checks++;
                Require(a.PreviewBindings.Count == 1 && !a.PreviewBindings[0].SampleObjects[0].activeSelf,
                    "框架创建阶段通过显式引用禁用编辑样例"); checks++;

                var b = await manager.OpenAsync<UIFrameworkTestPanelB>();
                await manager.OpenAsync<UIFrameworkTestPanelA>("置顶");
                Require(manager.TopFocusedPanel == a && await manager.BackAsync() && !manager.IsOpened<UIFrameworkTestPanelA>()
                    && manager.TopFocusedPanel == b, "A→B→A→Back 按显示顺序关闭"); checks++;
                Require(a.Releases == 0 && !a.gameObject.activeSelf && a.Context == null && !child.IsViewOpen,
                    "关闭缓存不释放，停止显示并清理上下文/子视图"); checks++;
                b.ConsumeLocalBack = true;
                Require(await manager.BackAsync() && manager.IsOpened<UIFrameworkTestPanelB>(),
                    "不可关闭根仍能处理局部返回"); checks++;
                b.ConsumeLocalBack = false;
                Require(!await manager.BackAsync(), "不可关闭根不被 Back 强制销毁"); checks++;

                await manager.ClearCacheAsync();
                Require(a.Releases == 1 && child.Releases == 1 && manager.LoadedPanelCount == 1,
                    "缓存释放覆盖根与子视图且不影响已打开面板"); checks++;
                await manager.CloseAsync<UIFrameworkTestPanelB>();
                await manager.ClearCacheAsync();
                Require(b.Releases == 0 && manager.LoadedPanelCount == 1, "普通缓存清理保留 Permanent"); checks++;
                await manager.ClearCacheAsync<UIFrameworkTestPanelB>();
                Require(b.Releases == 1 && manager.LoadedPanelCount == 0, "明确指定清理可以释放 Permanent"); checks++;

                UIFrameworkProbePanel failed = null;
                UIFrameworkProbePanel.Opening = (panel, args) =>
                { failed = panel; throw new InvalidOperationException("验证注入：打开失败"); };
                await ExpectFailure<InvalidOperationException>(() => manager.OpenAsync<UIFrameworkTestPanelA>());
                Require(!ReferenceEquals(failed, null) && failed.Closes == 1 && failed.Releases == 1,
                    "空上下文的 OnOpen 失败也经过 Close 与 Release");
                Require(manager.LoadedPanelCount == 0, "打开失败不保留半创建实例"); checks++;
                UIFrameworkProbePanel.Opening = null;

                entered = NewSignal();
                using (var cancellation = new CancellationTokenSource())
                {
                    UIFrameworkProbePanel.Opening = async (panel, args) =>
                    { entered.TrySetResult(true); await Task.Delay(Timeout.Infinite, panel.OperationToken); };
                    var canceledOpen = manager.OpenAsync<UIFrameworkTestPanelA>(cancellationToken: cancellation.Token);
                    await entered.Task;
                    cancellation.Cancel();
                    await ExpectFailure<OperationCanceledException>(() => canceledOpen);
                    Require(manager.LoadedPanelCount == 0, "调用者取消撤销未完成打开"); checks++;
                }

                entered = NewSignal();
                var session = manager.CreateScope("验证会话");
                var sessionOpen = manager.OpenAsync<UIFrameworkTestSessionPanel>(scope: session);
                await entered.Task;
                var endScope = manager.EndScopeAsync(session);
                await ExpectFailure<OperationCanceledException>(() => sessionOpen);
                await endScope;
                Require(session.IsEnded && manager.LoadedPanelCount == 0, "会话结束先取消，迟到结果不能发布"); checks++;
                await ExpectFailure<OperationCanceledException>(() => manager.OpenAsync<UIFrameworkTestSessionPanel>(scope: session));
                UIFrameworkProbePanel.Opening = null;

                a = await manager.OpenAsync<UIFrameworkTestPanelA>();
                a.AllowBack = false;
                Require(await manager.BackAsync() && manager.IsOpened<UIFrameworkTestPanelA>(),
                    "动态不可关闭状态消费返回且不穿透"); checks++;
                await manager.CloseAsync<UIFrameworkTestPanelA>();
                UIFrameworkProbePanel.Releasing = panel => { throw new InvalidOperationException("验证注入：释放钩子失败"); };
                await ExpectFailure<AggregateException>(() => manager.ClearCacheAsync());
                Require(manager.LoadedPanelCount == 0 && a.Releases == 1, "释放钩子异常仍清理实例和注册"); checks++;
                UIFrameworkProbePanel.Releasing = null;

                // 验证真实 ResourceManager handle 的租约转移及取消释放，不改 Addressables 配置或加载玩家目录。
                var baseline = UILoader.LiveAddressableLeaseCount;
                var operation = new ManualAssetOperation(assetA);
                var handle = Addressables.ResourceManager.StartOperation(operation, default);
                operation.Succeed();
                var lease = await UILoader.AdoptOperationAsync(handle, CancellationToken.None);
                Require(UILoader.LiveAddressableLeaseCount == baseline + 1, "资源句柄由租约持有");
                lease.Dispose(); lease.Dispose();
                Require(operation.Destroys == 1 && UILoader.LiveAddressableLeaseCount == baseline,
                    "资源成功路径恰好释放一次"); checks++;

                var canceledOperation = new ManualAssetOperation(assetA);
                var canceledHandle = Addressables.ResourceManager.StartOperation(canceledOperation, default);
                var providerOwnership = Addressables.ResourceManager.Acquire(canceledHandle);
                using (var cancellation = new CancellationTokenSource())
                {
                    var pendingLoad = UILoader.AdoptOperationAsync(canceledHandle, cancellation.Token);
                    cancellation.Cancel();
                    await ExpectFailure<OperationCanceledException>(() => pendingLoad);
                    canceledOperation.Succeed();
                    Addressables.Release(providerOwnership);
                    Require(canceledOperation.Destroys == 1 && UILoader.LiveAddressableLeaseCount == baseline,
                        "待完成加载取消释放调用者句柄，迟到完成不泄漏"); checks++;
                }

                registry.SetPanels(new UIPanelConfig(assetA), new UIPanelConfig(assetA));
                ExpectSyncFailure<InvalidOperationException>(() => registry.CreateValidatedRegistry());
                registry.SetPanels(new UIPanelConfig("不存在的面板", string.Empty));
                ExpectSyncFailure<InvalidOperationException>(() => registry.CreateValidatedRegistry());
                checks += 2;

                await manager.ShutdownAsync();
                await manager.ShutdownAsync();
                Require(!UIManager.TryGetInstance(out _) && !manager.IsInitialized && manager.LoadedPanelCount == 0,
                    "根结束注销且可幂等重入"); checks++;
                return $"Moyo UI 框架验证通过：{checks} 项；未改场景、地址目录或玩家文件。";
            }
            finally
            {
                UIFrameworkProbePanel.Opening = null;
                UIFrameworkProbePanel.Releasing = null;
                try { if (manager != null) await manager.ShutdownAsync(); }
                finally
                {
                    foreach (var value in owned) if (value != null) Object.DestroyImmediate(value);
                    EditorSceneManager.ClosePreviewScene(scene);
                    UIFrameworkProbePanel.Opening = oldOpening;
                    UIFrameworkProbePanel.Releasing = oldReleasing;
                }
            }
        }

        private static UIPanelAsset CreateTemplate<T>(Scene scene, List<Object> owned, bool hasChild = false) where T : UIPanelBase
        {
            var root = NewObject(typeof(T).Name, scene, null);
            root.SetActive(false);
            var group = root.AddComponent<CanvasGroup>();
            var panel = root.AddComponent<T>();
            if (hasChild)
            {
                var child = NewObject("显式子视图", scene, root.transform).AddComponent<UIFrameworkTestChild>();
                child.ConfigureChildren(Array.Empty<UIViewBase>(), true);
                panel.ConfigurePanel(group, child);
                var previewObject = NewObject("PreviewOnly_验证条目", scene, root.transform);
                previewObject.tag = "EditorOnly";
                var preview = root.AddComponent<UIPreviewOnly>();
                preview.Configure(new[] { previewObject }, Array.Empty<TMPro.TMP_Text>(), Array.Empty<string>());
                panel.ConfigurePreview(preview);
            }
            else panel.ConfigurePanel(group);
            var asset = ScriptableObject.CreateInstance<UIPanelAsset>();
            asset.Configure(panel);
            owned.Add(asset);
            owned.Add(root);
            return asset;
        }

        private static UIManager CreateManager(Scene scene, UIConfig config, List<Object> owned)
        {
            var root = NewObject("验证专用 UI 根", scene, null);
            owned.Add(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.AddComponent<CanvasScaler>();
            var raycaster = root.AddComponent<GraphicRaycaster>();
            var inputRoot = NewObject("输入", scene, root.transform);
            var events = inputRoot.AddComponent<EventSystem>();
            var input = inputRoot.AddComponent<UIFrameworkTestInputModule>();
            var inactive = (RectTransform)NewObject("隐藏创建", scene, root.transform).transform;
            inactive.gameObject.SetActive(false);
            var bindings = new List<UILayerBinding>();
            foreach (UILayer layer in Enum.GetValues(typeof(UILayer)))
                bindings.Add(new UILayerBinding(layer, (RectTransform)NewObject(layer.ToString(), scene, root.transform).transform));
            var manager = root.AddComponent<UIManager>();
            manager.Configure(config, canvas, scaler, raycaster, events, input, inactive, bindings.ToArray());
            return manager;
        }

        private static GameObject NewObject(string name, Scene scene, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform)) { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(value, scene);
            if (parent != null) value.transform.SetParent(parent, false);
            return value;
        }
        private static TaskCompletionSource<bool> NewSignal() => new TaskCompletionSource<bool>();
        private static void Require(bool condition, string invariant)
        { if (!condition) throw new InvalidOperationException("框架验证失败：" + invariant); }
        private static async Task ExpectFailure<T>(Func<Task> action) where T : Exception
        {
            try { await action(); }
            catch (T) { return; }
            throw new InvalidOperationException($"框架验证应抛出 {typeof(T).Name}。");
        }
        private static void ExpectSyncFailure<T>(Action action) where T : Exception
        {
            try { action(); }
            catch (T) { return; }
            throw new InvalidOperationException($"配置验证应抛出 {typeof(T).Name}。");
        }
        private sealed class ManualAssetOperation : AsyncOperationBase<UIPanelAsset>
        {
            private readonly UIPanelAsset asset;
            internal int Destroys;
            internal ManualAssetOperation(UIPanelAsset asset) { this.asset = asset; }
            protected override void Execute() { }
            internal void Succeed() { Complete(asset, true, (Exception)null); }
            protected override void Destroy() { Destroys++; }
        }
    }
}
