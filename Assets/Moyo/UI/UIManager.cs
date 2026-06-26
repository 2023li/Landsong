using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Moyo.Unity
{
    /// <summary>
    /// 项目 UI 管理器。
    /// 
    /// 新架构规则：
    /// 1. PanelId 固定等于 Panel 脚本类名。
    /// 2. 外部打开 UI 使用 OpenAsync<T>()。
    /// 3. 外部关闭 UI 使用 CloseAsync<T>()。
    /// 4. 不再通过字符串常量打开 UI。
    /// 5. 场景中未通过 UIManager 打开的 Panel 会自动销毁并重新通过 UIManager 打开。
    /// </summary>
    public class UIManager : MonoSingleton<UIManager>
    {
        [Header("UI Config")]
        [SerializeField] private UIConfig uiConfig;

        [Tooltip("UIConfig 的 Addressables Key。")]
        [SerializeField] private string uiConfigAddress = "SSBX_UIConfig";


        [Header("UI Layers")]
        [SerializeField] private Transform backgroundLayer;
        [SerializeField] private Transform hudLayer;
        [SerializeField] private Transform normalLayer;
        [SerializeField] private Transform popupLayer;
        [SerializeField] private Transform toastLayer;
        [SerializeField] private Transform guideLayer;
        [SerializeField] private Transform blockerLayer;
        [SerializeField] private Transform debugerLayer;


        /// <summary>
        /// 当前已经打开的面板。
        /// Key 固定为 Panel 类名。
        /// </summary>
        private readonly Dictionary<string, UIPanelBase> openedPanels = new();

        /// <summary>
        /// 已创建但当前未打开的缓存面板。
        /// Key 固定为 Panel 类名。
        /// </summary>
        private readonly Dictionary<string, UIPanelBase> cachedPanels = new();

        /// <summary>
        /// 返回关闭栈。
        /// canCloseByBack = true 的面板打开时进入这个栈。
        /// BackAsync 会关闭最近打开的此类面板。
        /// </summary>
        private readonly Stack<string> closeStack = new();

        /// <summary>
        /// 记录某个 owner 面板隐藏过哪些面板。
        /// Key：ownerPanelId。
        /// Value：被 owner 隐藏的 panelId 列表。
        /// </summary>
        private readonly Dictionary<string, List<string>> hiddenPanelsByOwner = new();

        /// <summary>
        /// 记录某个 panel 当前被哪些 owner 隐藏。
        /// 用于处理嵌套隐藏，防止过早恢复。
        /// </summary>
        private readonly Dictionary<string, HashSet<string>> hideOwnersByPanel = new();

        private bool isLoadingUIConfig;


        #region Open / Close API

        /// <summary>
        /// 打开指定类型的 UI 面板。
        /// PanelId 使用 typeof(T).Name。
        /// </summary>
        public async Task<T> OpenAsync<T>(object args = null) where T : UIPanelBase
        {
            var panel = await OpenAsync(typeof(T), args);
            return panel as T;
        }

        /// <summary>
        /// 通过运行时类型打开 UI。
        /// 主要用于 UIPanelBase 自动修复场景中未通过 Manager 打开的 Panel。
        /// </summary>
        public async Task<UIPanelBase> OpenAsync(Type panelType, object args = null)
        {
            if (panelType == null)
            {
                Debug.LogError("打开 UI 失败：panelType 为空。");
                return null;
            }

            if (!typeof(UIPanelBase).IsAssignableFrom(panelType))
            {
                Debug.LogError($"打开 UI 失败：{panelType.Name} 不是 UIPanelBase。");
                return null;
            }

            if (!await EnsureUIConfigAsync())
            {
                return null;
            }

            var panelId = GetPanelId(panelType);
            var config = uiConfig.Get(panelId);
            if (config == null)
            {
                return null;
            }

            // 已经打开：只恢复显示、置顶、聚焦。
            if (openedPanels.TryGetValue(panelId, out var openedPanel))
            {
                ClearHideStateForPanel(panelId);

                SetPanelVisible(openedPanel, true);
                openedPanel.transform.SetAsLastSibling();

                await openedPanel.OnFocusAsync();

                return openedPanel;
            }

            // 打开前让当前返回栈顶部窗口失焦。
            if (config.blurPreviousOnOpen)
            {
                await BlurTopPanelAsync();
            }

            var panel = await GetOrCreatePanelAsync(config, panelType);
            if (panel == null)
            {
                return null;
            }

            openedPanels[panelId] = panel;

            SetPanelVisible(panel, true);

            if (config.hideSameLayerPanels)
            {
                await HideSameLayerPanelsAsync(panelId, config.layer);
            }

            panel.transform.SetAsLastSibling();

            if (config.canCloseByBack)
            {
                RemoveFromCloseStack(panelId);
                closeStack.Push(panelId);
            }

            await panel.OnOpenAsync(args);
            await panel.OnFocusAsync();

            return panel;
        }

        /// <summary>
        /// 关闭指定类型的 UI。
        /// </summary>
        public Task CloseAsync<T>() where T : UIPanelBase
        {
            return CloseAsync(typeof(T));
        }

        /// <summary>
        /// 关闭指定运行时类型的 UI。
        /// </summary>
        public Task CloseAsync(Type panelType)
        {
            if (panelType == null)
            {
                Debug.LogError("关闭 UI 失败：panelType 为空。");
                return Task.CompletedTask;
            }

            return CloseAsync(GetPanelId(panelType));
        }

        /// <summary>
        /// 关闭指定 panelId 的 UI。
        /// panelId 固定等于 Panel 类名。
        /// </summary>
        public async Task CloseAsync(string panelId)
        {
            if (string.IsNullOrEmpty(panelId))
            {
                return;
            }

            if (!await EnsureUIConfigAsync())
            {
                return;
            }

            if (!openedPanels.TryGetValue(panelId, out var panel))
            {
                return;
            }

            var config = uiConfig.Get(panelId);
            if (config == null)
            {
                return;
            }

            await panel.OnBlurAsync();
            await panel.OnCloseAsync();

            openedPanels.Remove(panelId);
            RemoveFromCloseStack(panelId);

            RemoveClosedPanelFromHideRecords(panelId);

            if (config.cachePolicy == UICachePolicy.DestroyOnClose)
            {
                cachedPanels.Remove(panelId);

                await panel.OnReleaseAsync();

                Destroy(panel.gameObject);
            }
            else
            {
                cachedPanels[panelId] = panel;
                SetPanelVisible(panel, false);
            }

            var restoredPanel = await RestorePanelsHiddenByAsync(panelId);

            if (!restoredPanel)
            {
                await FocusTopPanelAsync();
            }
        }

        /// <summary>
        /// 返回操作。
        /// 关闭最近一个 canCloseByBack = true 的面板。
        /// </summary>
        public async Task BackAsync()
        {
            while (closeStack.Count > 0)
            {
                var panelId = closeStack.Pop();

                if (openedPanels.ContainsKey(panelId))
                {
                    await CloseAsync(panelId);
                    return;
                }
            }
        }

        #endregion


        #region Query API

        /// <summary>
        /// 尝试获取当前已经打开的指定类型面板。
        /// 被隐藏但未关闭的面板也算已打开。
        /// </summary>
        public bool TryGetActivePanel<T>(out T panel) where T : UIPanelBase
        {
            var panelId = GetPanelId<T>();

            if (openedPanels.TryGetValue(panelId, out var openedPanel) && openedPanel is T typedPanel)
            {
                panel = typedPanel;
                return true;
            }

            panel = null;
            return false;
        }

        /// <summary>
        /// 指定类型面板是否已经打开。
        /// </summary>
        public bool IsOpened<T>() where T : UIPanelBase
        {
            return openedPanels.ContainsKey(GetPanelId<T>());
        }

        #endregion


        #region Preload / Cache

        /// <summary>
        /// 预加载指定类型 UI。
        /// 只创建并缓存，不打开。
        /// </summary>
        public async Task<T> PreloadAsync<T>() where T : UIPanelBase
        {
            if (!await EnsureUIConfigAsync())
            {
                return null;
            }

            var panelType = typeof(T);
            var panelId = GetPanelId(panelType);
            var config = uiConfig.Get(panelId);

            if (config == null)
            {
                return null;
            }

            if (openedPanels.TryGetValue(panelId, out var openedPanel))
            {
                return openedPanel as T;
            }

            if (cachedPanels.TryGetValue(panelId, out var cachedPanel) && cachedPanel != null)
            {
                return cachedPanel as T;
            }

            var panel = await CreatePanelAsync(config, panelType);
            if (panel == null)
            {
                return null;
            }

            cachedPanels[panelId] = panel;
            SetPanelVisible(panel, false);

            return panel as T;
        }

        /// <summary>
        /// 清理所有未打开的缓存 UI。
        /// 已打开的 UI 不会被销毁。
        /// </summary>
        public void ClearCache()
        {
            var keys = new List<string>(cachedPanels.Keys);

            foreach (var key in keys)
            {
                if (openedPanels.ContainsKey(key))
                {
                    continue;
                }

                if (cachedPanels.TryGetValue(key, out var panel) && panel != null)
                {
                    Destroy(panel.gameObject);
                }

                cachedPanels.Remove(key);
            }
        }

        /// <summary>
        /// 清理指定类型的未打开缓存 UI。
        /// </summary>
        public void ClearCache<T>() where T : UIPanelBase
        {
            ClearCache(GetPanelId<T>());
        }

        /// <summary>
        /// 清理指定 panelId 的未打开缓存 UI。
        /// </summary>
        public void ClearCache(string panelId)
        {
            if (openedPanels.ContainsKey(panelId))
            {
                return;
            }

            if (cachedPanels.TryGetValue(panelId, out var panel))
            {
                if (panel != null)
                {
                    Destroy(panel.gameObject);
                }

                cachedPanels.Remove(panelId);
            }
        }

        #endregion


        #region Unmanaged Scene Panel Repair

        /// <summary>
        /// 被 UIPanelBase 调用。
        /// 当场景中存在未通过 UIManager 打开的 Panel 时，重新通过 UIManager 打开同类型 Panel。
        /// </summary>
        public async Task<bool> ReopenUnmanagedScenePanelAsync(UIPanelBase unmanagedPanel)
        {
            if (unmanagedPanel == null)
            {
                return false;
            }

            var panelType = unmanagedPanel.GetType();
            var openedPanel = await OpenAsync(panelType);

            return openedPanel != null;
        }

        #endregion


        #region Create Panel

        private async Task<UIPanelBase> GetOrCreatePanelAsync(UIPanelConfig config, Type expectedPanelType)
        {
            if (cachedPanels.TryGetValue(config.PanelId, out var cachedPanel))
            {
                if (cachedPanel != null)
                {
                    cachedPanel.BindToManager(this);
                    SetPanelVisible(cachedPanel, true);
                    return cachedPanel;
                }

                cachedPanels.Remove(config.PanelId);
            }

            var panel = await CreatePanelAsync(config, expectedPanelType);

            if (panel == null)
            {
                return null;
            }

            cachedPanels[config.PanelId] = panel;

            return panel;
        }

        private async Task<UIPanelBase> CreatePanelAsync(UIPanelConfig config, Type expectedPanelType)
        {
            if (config == null)
            {
                return null;
            }

            var prefab = await UILoader.LoadPrefabAsync(config.addressableKey);
            if (prefab == null)
            {
                return null;
            }

            Transform parent = GetLayer(config.layer);
            if (parent == null)
            {
                Debug.LogError($"UI Layer 未配置：{config.layer}");
                return null;
            }

            var instance = Instantiate(prefab, parent);
            instance.name = config.PanelId;

            var panel = instance.GetComponent<UIPanelBase>();
            if (panel == null)
            {
                Debug.LogError($"UI Prefab 上没有 UIPanelBase 组件：{config.PanelId}");
                Destroy(instance);
                return null;
            }

            var actualPanelId = panel.PanelId;

            if (actualPanelId != config.PanelId)
            {
                Debug.LogError(
                    $"UI 配置错误：PanelId 必须等于 Panel 脚本类名。\n" +
                    $"Config PanelId = {config.PanelId}\n" +
                    $"Prefab Panel Class = {actualPanelId}",
                    instance);

                Destroy(instance);
                return null;
            }

            if (expectedPanelType != null && !expectedPanelType.IsAssignableFrom(panel.GetType()))
            {
                Debug.LogError(
                    $"UI Prefab 类型错误。\n" +
                    $"期望类型：{expectedPanelType.Name}\n" +
                    $"实际类型：{panel.GetType().Name}",
                    instance);

                Destroy(instance);
                return null;
            }

            panel.BindToManager(this);

            await panel.OnCreateAsync();

            return panel;
        }

        #endregion


        #region Same Layer Hide / Restore

        private async Task HideSameLayerPanelsAsync(string ownerPanelId, UILayer layer)
        {
            hiddenPanelsByOwner.Remove(ownerPanelId);

            var hiddenPanelIds = new List<string>();
            var snapshot = new List<KeyValuePair<string, UIPanelBase>>(openedPanels);

            foreach (var kvp in snapshot)
            {
                var otherPanelId = kvp.Key;
                var otherPanel = kvp.Value;

                if (otherPanel == null)
                {
                    continue;
                }

                if (otherPanelId == ownerPanelId)
                {
                    continue;
                }

                var otherConfig = uiConfig.Get(otherPanelId);
                if (otherConfig == null)
                {
                    continue;
                }

                if (otherConfig.layer != layer)
                {
                    continue;
                }

                var wasVisible = IsPanelVisible(otherPanel);

                if (!hideOwnersByPanel.TryGetValue(otherPanelId, out var owners))
                {
                    owners = new HashSet<string>();
                    hideOwnersByPanel[otherPanelId] = owners;
                }

                if (owners.Add(ownerPanelId))
                {
                    hiddenPanelIds.Add(otherPanelId);
                }

                if (wasVisible)
                {
                    await otherPanel.OnBlurAsync();
                }

                SetPanelVisible(otherPanel, false);
            }

            if (hiddenPanelIds.Count > 0)
            {
                hiddenPanelsByOwner[ownerPanelId] = hiddenPanelIds;
            }
        }

        private async Task<bool> RestorePanelsHiddenByAsync(string ownerPanelId)
        {
            if (!hiddenPanelsByOwner.TryGetValue(ownerPanelId, out var hiddenPanelIds))
            {
                return false;
            }

            hiddenPanelsByOwner.Remove(ownerPanelId);

            UIPanelBase topRestoredPanel = null;
            var topSiblingIndex = -1;

            foreach (var hiddenPanelId in hiddenPanelIds)
            {
                if (!hideOwnersByPanel.TryGetValue(hiddenPanelId, out var owners))
                {
                    continue;
                }

                owners.Remove(ownerPanelId);

                if (owners.Count > 0)
                {
                    continue;
                }

                hideOwnersByPanel.Remove(hiddenPanelId);

                if (!openedPanels.TryGetValue(hiddenPanelId, out var panel))
                {
                    continue;
                }

                SetPanelVisible(panel, true);

                var siblingIndex = panel.transform.GetSiblingIndex();
                if (siblingIndex > topSiblingIndex)
                {
                    topSiblingIndex = siblingIndex;
                    topRestoredPanel = panel;
                }
            }

            if (topRestoredPanel != null)
            {
                await topRestoredPanel.OnFocusAsync();
                return true;
            }

            return false;
        }

        private void RemoveClosedPanelFromHideRecords(string closedPanelId)
        {
            hideOwnersByPanel.Remove(closedPanelId);

            var emptyOwnerIds = new List<string>();

            foreach (var kvp in hiddenPanelsByOwner)
            {
                if (kvp.Key == closedPanelId)
                {
                    continue;
                }

                kvp.Value.Remove(closedPanelId);

                if (kvp.Value.Count == 0)
                {
                    emptyOwnerIds.Add(kvp.Key);
                }
            }

            foreach (var ownerId in emptyOwnerIds)
            {
                hiddenPanelsByOwner.Remove(ownerId);
            }
        }

        private void ClearHideStateForPanel(string panelId)
        {
            hideOwnersByPanel.Remove(panelId);

            var emptyOwnerIds = new List<string>();

            foreach (var kvp in hiddenPanelsByOwner)
            {
                kvp.Value.Remove(panelId);

                if (kvp.Value.Count == 0)
                {
                    emptyOwnerIds.Add(kvp.Key);
                }
            }

            foreach (var ownerId in emptyOwnerIds)
            {
                hiddenPanelsByOwner.Remove(ownerId);
            }
        }

        #endregion


        #region Focus / Blur

        private async Task BlurTopPanelAsync()
        {
            while (closeStack.Count > 0)
            {
                var topPanelId = closeStack.Peek();

                if (!openedPanels.TryGetValue(topPanelId, out var panel))
                {
                    closeStack.Pop();
                    continue;
                }

                if (IsPanelVisible(panel))
                {
                    await panel.OnBlurAsync();
                }

                return;
            }
        }

        private async Task FocusTopPanelAsync()
        {
            while (closeStack.Count > 0)
            {
                var topPanelId = closeStack.Peek();

                if (!openedPanels.TryGetValue(topPanelId, out var panel))
                {
                    closeStack.Pop();
                    continue;
                }

                if (hideOwnersByPanel.ContainsKey(topPanelId))
                {
                    return;
                }

                if (IsPanelVisible(panel))
                {
                    await panel.OnFocusAsync();
                }

                return;
            }
        }

        #endregion


        #region Visibility

        private void SetPanelVisible(UIPanelBase panel, bool visible)
        {
            if (panel == null)
            {
                return;
            }

            var canvasGroup = panel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
            }

            panel.gameObject.SetActive(true);

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
        }

        private bool IsPanelVisible(UIPanelBase panel)
        {
            if (panel == null)
            {
                return false;
            }

            if (!panel.gameObject.activeInHierarchy)
            {
                return false;
            }

            var canvasGroup = panel.GetComponent<CanvasGroup>();

            return canvasGroup == null || canvasGroup.alpha > 0.001f;
        }

        #endregion


        #region Stack

        private void RemoveFromCloseStack(string panelId)
        {
            if (closeStack.Count == 0)
            {
                return;
            }

            var tempStack = new Stack<string>();

            while (closeStack.Count > 0)
            {
                var id = closeStack.Pop();

                if (id != panelId)
                {
                    tempStack.Push(id);
                }
            }

            while (tempStack.Count > 0)
            {
                closeStack.Push(tempStack.Pop());
            }
        }

        #endregion


        #region Config / Layer

        private async Task<bool> EnsureUIConfigAsync()
        {
            if (uiConfig != null)
            {
                return true;
            }

            uiConfig = await GetUIConfigAsync();

            return uiConfig != null;
        }

        public async Task<UIConfig> GetUIConfigAsync()
        {
            if (uiConfig != null)
            {
                return uiConfig;
            }

            if (isLoadingUIConfig)
            {
                while (isLoadingUIConfig)
                {
                    await Task.Yield();
                }

                return uiConfig;
            }

            isLoadingUIConfig = true;

            var handle = Addressables.LoadAssetAsync<UIConfig>(uiConfigAddress);

            await handle.Task;

            isLoadingUIConfig = false;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"UIConfig 加载失败：{uiConfigAddress}");
                return null;
            }

            uiConfig = handle.Result;

            if (MoyoConfig.Instance.UI.LogConfigLoaded)
            {
                Debug.Log($"已加载 UIConfig：{uiConfigAddress}");
            }

            return uiConfig;
        }

        private Transform GetLayer(UILayer layer)
        {
            return layer switch
            {
                UILayer.Background => backgroundLayer,
                UILayer.HUD => hudLayer,
                UILayer.Normal => normalLayer,
                UILayer.Popup => popupLayer,
                UILayer.Toast => toastLayer,
                UILayer.Guide => guideLayer,
                UILayer.Blocker => blockerLayer,
                UILayer.Debuger => debugerLayer,
                _ => normalLayer
            };
        }

        private static string GetPanelId<T>() where T : UIPanelBase
        {
            return typeof(T).Name;
        }

        private static string GetPanelId(Type panelType)
        {
            return panelType.Name;
        }

        #endregion
    }

    /// <summary>
    /// UI Prefab 加载器。
    /// </summary>
    public static class UILoader
    {
        public static async Task<GameObject> LoadPrefabAsync(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                Debug.LogError("UI Prefab Addressables Key 为空。");
                return null;
            }

            var handle = Addressables.LoadAssetAsync<GameObject>(key);

            await handle.Task;

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"UI Prefab Addressables 加载失败：{key}");
                Addressables.Release(handle);
                return null;
            }

            return handle.Result;
        }
    }
}
