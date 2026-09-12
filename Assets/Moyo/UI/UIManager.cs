using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Moyo.Unity
{
    /// <summary>
    /// 唯一应用 UI 根。由组合入口显式 Initialize；不扫描场景、不补建组件、不监听场景名。
    /// 所有导航经同一队列串行化，因此 Open/Preload 同类型并发只有一个创建者。
    /// 生命周期内不要 await 另一个 Manager 导航操作；子视图可使用自己的 OpenViewAsync。
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [SerializeField, LabelText("面板注册表"), Required] private UIConfig uiConfig;
        [SerializeField, LabelText("根画布"), Required] private Canvas rootCanvas;
        [SerializeField, LabelText("画布缩放器"), Required] private CanvasScaler canvasScaler;
        [SerializeField, LabelText("图形射线检测"), Required] private GraphicRaycaster graphicRaycaster;
        [SerializeField, LabelText("输入事件系统"), Required] private EventSystem eventSystem;
        [SerializeField, LabelText("输入模块"), Required] private BaseInputModule inputModule;
        [SerializeField, LabelText("隐藏创建容器"), Required] private RectTransform inactiveRoot;
        [SerializeField, LabelText("显示层级")] private UILayerBinding[] layers = Array.Empty<UILayerBinding>();

        private static UIManager instance;
        private readonly SemaphoreSlim operationGate = new SemaphoreSlim(1, 1);
        private readonly AsyncLocal<int> lifecycleDepth = new AsyncLocal<int>();
        private readonly CancellationTokenSource lifetime = new CancellationTokenSource();
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly Dictionary<UILayer, RectTransform> layerRoots = new Dictionary<UILayer, RectTransform>();
        private readonly List<string> openOrder = new List<string>();
        private readonly HashSet<UIScope> scopes = new HashSet<UIScope>();
        private Dictionary<string, UIPanelConfig> registry;
        private UIPanelBase focused;
        private long nextGeneration;
        private bool shuttingDown;
        private Task shutdownTask;

        private sealed class Entry
        {
            internal UIPanelConfig Config;
            internal UIPanelBase Panel;
            internal UIPanelLease Lease;
            internal UIScope Scope;
            internal bool Visible;
            internal UIPanelState State;
        }

        public static UIManager Instance => instance != null && instance.IsInitialized ? instance
            : throw new InvalidOperationException("UIManager 尚未由应用启动入口初始化。");
        public static bool TryGetInstance(out UIManager manager)
        { manager = instance; return manager != null && manager.IsInitialized; }
        public bool IsInitialized { get; private set; }
        public bool IsShuttingDown => shuttingDown;
        public bool IsBusy => operationGate.CurrentCount == 0;
        public Canvas RootCanvas => rootCanvas;
        public CanvasScaler Scaler => canvasScaler;
        public EventSystem EventSystem => eventSystem;
        public BaseInputModule InputModule => inputModule;
        public UIPanelBase TopFocusedPanel => focused;
        public UIScope ApplicationScope { get; private set; }
        public int LoadedPanelCount => entries.Count;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistration() { instance = null; }

        /// <summary>用于编辑器制作和显式组合，不用于缺失配置时的运行时修补。</summary>
        public void Configure(UIConfig config, Canvas canvas, CanvasScaler scaler, GraphicRaycaster raycaster,
            EventSystem events, BaseInputModule input, RectTransform inactiveContainer, params UILayerBinding[] layerBindings)
        {
            if (IsInitialized || shuttingDown) throw new InvalidOperationException("已初始化的 UI 根不可重新配置。");
            uiConfig = config; rootCanvas = canvas; canvasScaler = scaler; graphicRaycaster = raycaster;
            eventSystem = events; inputModule = input; inactiveRoot = inactiveContainer;
            layers = layerBindings == null ? Array.Empty<UILayerBinding>() : (UILayerBinding[])layerBindings.Clone();
        }

        public void ValidateConfiguration()
        {
            if (rootCanvas == null || rootCanvas.gameObject != gameObject || transform.parent != null)
                throw new InvalidOperationException("UIManager 必须显式绑定同对象的根 Canvas，且该对象不能有父级。");
            if (canvasScaler == null || canvasScaler.gameObject != gameObject
                || graphicRaycaster == null || graphicRaycaster.gameObject != gameObject)
                throw new InvalidOperationException("UI 根必须绑定自身的 CanvasScaler 和 GraphicRaycaster。");
            if (eventSystem == null || eventSystem.transform == transform || !eventSystem.transform.IsChildOf(transform)
                || inputModule == null || inputModule.gameObject != eventSystem.gameObject)
                throw new InvalidOperationException("UI 根必须绑定所属子对象上的 EventSystem 及同对象输入模块。");
            if (inactiveRoot == null || inactiveRoot.parent != transform || inactiveRoot.gameObject.activeSelf)
                throw new InvalidOperationException("隐藏创建容器必须是 UI 根的直属子对象，且保持未激活。");
            if (uiConfig == null) throw new InvalidOperationException("UI 根未绑定面板注册表。");
            var configuredLayers = new Dictionary<UILayer, RectTransform>();
            var roots = new HashSet<RectTransform>();
            if (layers == null) throw new InvalidOperationException("UI 根未配置显示层列表。");
            foreach (var binding in layers)
            {
                if (binding == null || binding.root == null || binding.root.parent != transform
                    || binding.root == inactiveRoot || !binding.root.gameObject.activeSelf)
                    throw new InvalidOperationException("每个 UI 显示层必须显式绑定根下已激活的独立容器。");
                if (!Enum.IsDefined(typeof(UILayer), binding.layer)
                    || configuredLayers.ContainsKey(binding.layer) || !roots.Add(binding.root))
                    throw new InvalidOperationException("UI 显示层包含无效枚举、重复层级或重复容器。");
                configuredLayers.Add(binding.layer, binding.root);
            }
            foreach (var pair in uiConfig.CreateValidatedRegistry())
                if (!configuredLayers.ContainsKey(pair.Value.layer))
                    throw new InvalidOperationException($"面板 {pair.Key} 使用了未配置的层 {pair.Value.layer}。");
        }

        public void Initialize()
        {
            if (IsInitialized) return;
            if (shuttingDown) throw new InvalidOperationException("已结束的 UIManager 不能重新初始化。");
            if (instance != null && instance != this)
                throw new InvalidOperationException("应用已经注册了另一份 UIManager；请修正重复启动配置。");
            ValidateConfiguration();
            registry = uiConfig.CreateValidatedRegistry();
            layerRoots.Clear();
            foreach (var binding in layers) layerRoots.Add(binding.layer, binding.root);
            ApplicationScope = new UIScope(this, "应用", ++nextGeneration);
            scopes.Add(ApplicationScope);
            instance = this;
            IsInitialized = true;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);
        }

        public UIScope CreateScope(string name)
        {
            EnsureReady();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("UI 会话名称不能为空。", nameof(name));
            var scope = new UIScope(this, name, ++nextGeneration);
            scopes.Add(scope);
            return scope;
        }

        public Task<T> OpenAsync<T>(object args = null, UIScope scope = null, CancellationToken cancellationToken = default)
            where T : UIPanelBase => OpenTypedAsync<T>(args, scope, cancellationToken);

        private async Task<T> OpenTypedAsync<T>(object args, UIScope scope, CancellationToken token) where T : UIPanelBase
        { return (T)await OpenAsync(typeof(T), args, scope, token); }

        public Task<UIPanelBase> OpenAsync(Type panelType, object args = null, UIScope scope = null,
            CancellationToken cancellationToken = default)
        {
            EnsurePanelType(panelType);
            var config = GetConfig(panelType.Name);
            var actualScope = ResolveScope(config, scope);
            return RunQueuedAsync(token => OpenCoreAsync(config, panelType, args, actualScope, token), actualScope, cancellationToken);
        }

        public Task<T> PreloadAsync<T>(UIScope scope = null, CancellationToken cancellationToken = default)
            where T : UIPanelBase => PreloadTypedAsync<T>(scope, cancellationToken);

        private async Task<T> PreloadTypedAsync<T>(UIScope scope, CancellationToken cancellationToken) where T : UIPanelBase
        {
            var config = GetConfig(typeof(T).Name);
            var actualScope = ResolveScope(config, scope);
            return (T)await RunQueuedAsync(async token =>
                (await GetOrCreateAsync(config, typeof(T), actualScope, token)).Panel, actualScope, cancellationToken);
        }

        public Task CloseAsync<T>() where T : UIPanelBase => CloseAsync(typeof(T));
        public Task CloseAsync(Type panelType)
        { EnsurePanelType(panelType); return CloseAsync(panelType.Name); }
        public Task CloseAsync(string panelId)
        {
            EnsureReady();
            return RunQueuedAsync(async token =>
            {
                if (entries.TryGetValue(panelId, out var entry) && entry.State == UIPanelState.Open)
                    await CloseCoreAsync(entry, false);
                return true;
            }, ApplicationScope, CancellationToken.None);
        }

        /// <summary>先处理最上层根的局部返回；不可关闭弹窗消费返回，不能穿透关闭下面的窗口。</summary>
        public Task<bool> BackAsync()
        {
            EnsureReady();
            return RunQueuedAsync(async token =>
            {
                var top = FindTopFocusable();
                if (top == null) return false;
                if (await InvokeLifecycleAsync(() => top.Panel.TryHandleBackAsync())) return true;
                if (!top.Config.canCloseByBack) return false;
                if (!top.Panel.CanCloseByBack) return true;
                await CloseCoreAsync(top, false);
                return true;
            }, ApplicationScope, CancellationToken.None);
        }

        public bool TryGetActivePanel<T>(out T panel) where T : UIPanelBase
        {
            if (entries.TryGetValue(typeof(T).Name, out var entry) && entry.State == UIPanelState.Open && entry.Panel is T value)
            { panel = value; return true; }
            panel = null; return false;
        }
        public bool IsOpened<T>() where T : UIPanelBase
            => entries.TryGetValue(typeof(T).Name, out var entry) && entry.State == UIPanelState.Open;
        public bool TryGetState<T>(out UIPanelState state) where T : UIPanelBase
        {
            if (entries.TryGetValue(typeof(T).Name, out var entry)) { state = entry.State; return true; }
            state = UIPanelState.Released; return false;
        }

        public Task ClearCacheAsync(bool includePermanent = false)
        {
            EnsureReady();
            return RunQueuedAsync(async token =>
            {
                var errors = new List<Exception>();
                foreach (var entry in new List<Entry>(entries.Values))
                {
                    if (entry.State != UIPanelState.Cached || (!includePermanent && entry.Config.cachePolicy == UICachePolicy.Permanent)) continue;
                    try { await ReleaseEntryAsync(entry); } catch (Exception error) { errors.Add(error); }
                }
                ThrowCollected(errors, "UI 缓存清理失败。");
                return true;
            }, ApplicationScope, CancellationToken.None);
        }
        public Task ClearCacheAsync<T>() where T : UIPanelBase
        {
            EnsureReady();
            return RunQueuedAsync(async token =>
            {
                if (entries.TryGetValue(typeof(T).Name, out var entry) && entry.State == UIPanelState.Cached)
                    await ReleaseEntryAsync(entry);
                return true;
            }, ApplicationScope, CancellationToken.None);
        }

        public Task EndScopeAsync(UIScope scope)
        {
            EnsureReady();
            ValidateScope(scope);
            if (scope == ApplicationScope) throw new InvalidOperationException("应用作用域只能通过 ShutdownAsync 结束。");
            scope.Cancel(); // 在等待导航队列之前失效；迟到加载永远无法发布。
            return RunQueuedAsync(async token =>
            {
                var errors = new List<Exception>();
                foreach (var entry in new List<Entry>(entries.Values))
                {
                    if (entry.Scope != scope) continue;
                    try { await CloseCoreAsync(entry, true, false); } catch (Exception error) { errors.Add(error); }
                }
                scopes.Remove(scope);
                try { await SynchronizePresentationAsync(); } catch (Exception error) { errors.Add(error); }
                ThrowCollected(errors, "UI 会话结束失败。");
                return true;
            }, ApplicationScope, CancellationToken.None);
        }

        public Task ShutdownAsync()
        {
            if (shutdownTask != null) return shutdownTask;
            RejectLifecycleNavigation();
            shuttingDown = true;
            foreach (var scope in scopes) scope.Cancel();
            lifetime.Cancel();
            shutdownTask = ShutdownCoreAsync();
            return shutdownTask;
        }

        private async Task ShutdownCoreAsync()
        {
            await operationGate.WaitAsync();
            var errors = new List<Exception>();
            try
            {
                foreach (var entry in new List<Entry>(entries.Values))
                { try { await CloseCoreAsync(entry, true, false); } catch (Exception error) { errors.Add(error); } }
                entries.Clear(); openOrder.Clear(); scopes.Clear(); focused = null;
                if (eventSystem != null) eventSystem.enabled = false;
            }
            finally
            {
                IsInitialized = false;
                if (instance == this) instance = null;
                operationGate.Release();
            }
            ThrowCollected(errors, "UI 根释放失败。");
        }

        private async Task<UIPanelBase> OpenCoreAsync(UIPanelConfig config, Type type, object args,
            UIScope scope, CancellationToken token)
        {
            Entry entry = null;
            try
            {
                entry = await GetOrCreateAsync(config, type, scope, token);
                if (entry.State == UIPanelState.Open)
                {
                    await BlurIfFocusedAsync(entry);
                    await InvokeLifecycleAsync(entry.Panel.CloseTreeAsync);
                    openOrder.Remove(config.PanelId);
                    entry.Panel.SetVisibility(false);
                }
                SetState(entry, UIPanelState.Opening);
                await InvokeLifecycleAsync(() => entry.Panel.OpenTreeAsync(args, token));
                token.ThrowIfCancellationRequested();
                SetState(entry, UIPanelState.Open);
                openOrder.Remove(config.PanelId);
                openOrder.Add(config.PanelId);
                entry.Panel.transform.SetAsLastSibling();
                await SynchronizePresentationAsync();
                token.ThrowIfCancellationRequested();
                return entry.Panel;
            }
            catch
            {
                if (entry != null)
                {
                    try { await ReleaseEntryAsync(entry); } catch (Exception cleanup) { Debug.LogException(cleanup, this); }
                    try { await SynchronizePresentationAsync(); } catch (Exception cleanup) { Debug.LogException(cleanup, this); }
                }
                throw;
            }
        }

        private async Task<Entry> GetOrCreateAsync(UIPanelConfig config, Type expectedType, UIScope scope, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (entries.TryGetValue(config.PanelId, out var existing))
            {
                if (existing.Scope != scope) throw new InvalidOperationException($"面板 {config.PanelId} 仍属于 {existing.Scope}，请先结束旧作用域。");
                if (existing.Panel == null || !expectedType.IsInstanceOfType(existing.Panel))
                    throw new InvalidOperationException($"面板 {config.PanelId} 的实例被外部销毁或类型与注册不符。");
                return existing;
            }
            var entry = new Entry { Config = config, Scope = scope, State = UIPanelState.Creating };
            // 先登记唯一创建者；所有调用仍由队列串行化，预加载与打开共享此条目。
            entries.Add(config.PanelId, entry);
            try
            {
                entry.Lease = await UILoader.LoadAsync(config, token);
                token.ThrowIfCancellationRequested();
                entry.Lease.Asset.Validate(config.PanelId);
                if (!expectedType.IsInstanceOfType(entry.Lease.Asset.PrefabRoot))
                    throw new InvalidOperationException($"面板 {config.PanelId} 的预制体类型与请求不匹配。");
                entry.Panel = Instantiate(entry.Lease.Asset.PrefabRoot, inactiveRoot, false);
                entry.Panel.name = config.PanelId;
                entry.Panel.gameObject.SetActive(false);
                entry.Panel.BindToManager(this, scope);
                await InvokeLifecycleAsync(() => entry.Panel.CreateTreeAsync(token));
                token.ThrowIfCancellationRequested();
                entry.Panel.transform.SetParent(layerRoots[config.layer], false);
                entry.Panel.SetVisibility(false);
                SetState(entry, UIPanelState.Cached);
                return entry;
            }
            catch
            {
                try { await ReleaseEntryAsync(entry); } catch (Exception cleanup) { Debug.LogException(cleanup, this); }
                throw;
            }
        }

        private async Task CloseCoreAsync(Entry entry, bool forceRelease, bool synchronize = true)
        {
            var errors = new List<Exception>();
            if (entry.State == UIPanelState.Open || entry.State == UIPanelState.Opening)
            {
                SetState(entry, UIPanelState.Closing);
                try { await BlurIfFocusedAsync(entry); } catch (Exception error) { errors.Add(error); }
                try { if (entry.Panel != null) await InvokeLifecycleAsync(entry.Panel.CloseTreeAsync); }
                catch (Exception error) { errors.Add(error); }
                finally
                {
                    openOrder.Remove(entry.Config.PanelId);
                    entry.Visible = false;
                    if (entry.Panel != null) entry.Panel.SetVisibility(false);
                    SetState(entry, UIPanelState.Cached);
                }
            }
            if (forceRelease || entry.Config.cachePolicy == UICachePolicy.DestroyOnClose || errors.Count > 0)
            {
                try { await ReleaseEntryAsync(entry); } catch (Exception error) { errors.Add(error); }
            }
            if (synchronize)
            { try { await SynchronizePresentationAsync(); } catch (Exception error) { errors.Add(error); } }
            ThrowCollected(errors, $"面板 {entry.Config.PanelId} 关闭失败。");
        }

        private async Task ReleaseEntryAsync(Entry entry)
        {
            var errors = new List<Exception>();
            try { await BlurIfFocusedAsync(entry); } catch (Exception error) { errors.Add(error); }
            entries.Remove(entry.Config.PanelId);
            openOrder.Remove(entry.Config.PanelId);
            SetState(entry, UIPanelState.Released);
            try
            {
                if (entry.Panel != null) await InvokeLifecycleAsync(entry.Panel.ReleaseTreeAsync);
            }
            catch (Exception error) { errors.Add(error); }
            finally
            {
                if (entry.Panel != null)
                {
                    entry.Panel.ClearScope();
                    entry.Panel.gameObject.SetActive(false);
                    if (Application.isPlaying) Destroy(entry.Panel.gameObject); else DestroyImmediate(entry.Panel.gameObject);
                }
                entry.Panel = null;
                entry.Lease?.Dispose();
                entry.Lease = null;
                entry.Visible = false;
            }
            ThrowCollected(errors, $"面板 {entry.Config.PanelId} 释放失败。");
        }

        private async Task SynchronizePresentationAsync()
        {
            if (shuttingDown) return;
            var hiddenLayers = new HashSet<UILayer>();
            for (var i = openOrder.Count - 1; i >= 0; i--)
            {
                var entry = entries[openOrder[i]];
                entry.Visible = entry.State == UIPanelState.Open && !entry.Scope.IsEnded && !hiddenLayers.Contains(entry.Config.layer);
                if (entry.Panel != null) entry.Panel.SetVisibility(entry.Visible);
                if (entry.Visible && entry.Config.hideSameLayerPanels) hiddenLayers.Add(entry.Config.layer);
            }
            var top = FindTopFocusable();
            var next = top?.Panel;
            if (focused == next) return;
            var previous = focused;
            focused = null;
            if (previous != null) await InvokeLifecycleAsync(previous.OnBlurAsync);
            if (next != null)
            {
                await InvokeLifecycleAsync(next.OnFocusAsync);
                focused = next;
            }
        }

        private Entry FindTopFocusable()
        {
            Entry result = null;
            var bestLayer = int.MinValue;
            for (var i = openOrder.Count - 1; i >= 0; i--)
            {
                if (!entries.TryGetValue(openOrder[i], out var entry) || !entry.Visible || !entry.Config.takesFocus
                    || entry.State != UIPanelState.Open || entry.Scope.IsEnded || entry.Panel == null) continue;
                var rank = layerRoots[entry.Config.layer].GetSiblingIndex();
                if (rank <= bestLayer) continue;
                bestLayer = rank;
                result = entry;
            }
            return result;
        }

        private async Task BlurIfFocusedAsync(Entry entry)
        {
            if (focused == null || focused != entry.Panel) return;
            var previous = focused;
            focused = null;
            await InvokeLifecycleAsync(previous.OnBlurAsync);
        }

        private static void SetState(Entry entry, UIPanelState state)
        { entry.State = state; if (entry.Panel != null) entry.Panel.State = state; }

        private async Task<T> RunQueuedAsync<T>(Func<CancellationToken, Task<T>> action, UIScope scope, CancellationToken token)
        {
            RejectLifecycleNavigation();
            EnsureReady();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, scope.Token, token);
            await operationGate.WaitAsync(linked.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                EnsureReady();
                return await action(linked.Token);
            }
            finally { operationGate.Release(); }
        }

        private async Task InvokeLifecycleAsync(Func<Task> callback)
        {
            lifecycleDepth.Value++;
            try { await callback(); }
            finally { lifecycleDepth.Value--; }
        }
        private async Task<T> InvokeLifecycleAsync<T>(Func<Task<T>> callback)
        {
            lifecycleDepth.Value++;
            try { return await callback(); }
            finally { lifecycleDepth.Value--; }
        }
        private void RejectLifecycleNavigation()
        {
            if (lifecycleDepth.Value != 0)
                throw new InvalidOperationException("面板生命周期内不能等待另一个 UIManager 导航操作。请在应用流程中依次导航，或使用子视图 API。");
        }
        private void EnsureReady()
        {
            if (!IsInitialized || shuttingDown) throw new InvalidOperationException("UIManager 尚未初始化或已经开始释放。");
        }
        private static void EnsurePanelType(Type type)
        {
            if (type == null || type.IsAbstract || !typeof(UIPanelBase).IsAssignableFrom(type))
                throw new ArgumentException("必须提供具体的 UIPanelBase 类型。", nameof(type));
        }
        private UIPanelConfig GetConfig(string panelId)
        {
            EnsureReady();
            if (!registry.TryGetValue(panelId, out var config)) throw new InvalidOperationException($"UI 注册表中不存在 {panelId}。");
            return config;
        }
        private UIScope ResolveScope(UIPanelConfig config, UIScope requested)
        {
            var scope = requested ?? ApplicationScope;
            ValidateScope(scope);
            if (scope.IsEnded) throw new OperationCanceledException($"UI 作用域 {scope} 已结束。", scope.Token);
            if (config.scopePolicy == UIScopePolicy.Application && scope != ApplicationScope)
                throw new InvalidOperationException($"共享面板 {config.PanelId} 必须使用应用作用域。");
            if (config.scopePolicy == UIScopePolicy.Session && scope == ApplicationScope)
                throw new InvalidOperationException($"面板 {config.PanelId} 必须提供明确的游戏会话作用域。");
            return scope;
        }
        private void ValidateScope(UIScope scope)
        {
            if (scope == null || scope.Owner != this) throw new InvalidOperationException("UI 作用域不属于当前管理器。");
        }
        private static void ThrowCollected(List<Exception> errors, string message)
        { if (errors.Count > 0) throw new AggregateException(message, errors); }

        private async void OnDestroy()
        {
            if (!IsInitialized && shutdownTask == null) return;
            try { await ShutdownAsync(); }
            catch (Exception error) { Debug.LogException(error); }
        }
    }
}
