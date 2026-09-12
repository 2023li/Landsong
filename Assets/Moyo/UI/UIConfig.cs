using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Moyo.Unity
{
    public enum UILayer
    {
        [LabelText("背景")] Background,
        [LabelText("游戏信息")] HUD,
        [LabelText("普通页面")] Normal,
        [LabelText("弹窗")] Popup,
        [LabelText("消息")] Toast,
        [LabelText("引导")] Guide,
        [LabelText("阻断遮罩")] Blocker,
        [LabelText("调试")] Debuger
    }

    public enum UICachePolicy
    {
        [LabelText("关闭后释放")] DestroyOnClose,
        [LabelText("关闭后缓存")] HideOnClose,
        [LabelText("保留至所属作用域结束")] Permanent
    }

    public enum UIScopePolicy
    {
        [LabelText("应用共享")] Application,
        [LabelText("必须提供会话")] Session
    }

    public enum UIPanelState
    {
        [LabelText("创建中")] Creating,
        [LabelText("打开中")] Opening,
        [LabelText("已打开")] Open,
        [LabelText("关闭中")] Closing,
        [LabelText("已缓存")] Cached,
        [LabelText("已释放")] Released
    }

    [Serializable]
    public sealed class UILayerBinding
    {
        [LabelText("层级")] public UILayer layer;
        [LabelText("层级容器"), Required] public RectTransform root;
        public UILayerBinding() { }
        public UILayerBinding(UILayer layer, RectTransform root) { this.layer = layer; this.root = root; }
    }

    [Serializable]
    public sealed class UIPanelConfig
    {
        [SerializeField, LabelText("面板类型名"), Tooltip("必须等于面板脚本类名；错误值不会自动替换。")]
        private string panelId;
        [LabelText("面板资源描述")] public UIPanelAsset asset;
        [LabelText("资源描述地址"), Tooltip("与直接资源引用二选一；该地址加载 UIPanelAsset，不加载未绑定的 GameObject。")]
        public string assetAddress;
        [LabelText("显示层级")] public UILayer layer = UILayer.Normal;
        [LabelText("缓存策略")] public UICachePolicy cachePolicy = UICachePolicy.HideOnClose;
        [LabelText("所属作用域")] public UIScopePolicy scopePolicy;
        [LabelText("允许返回关闭")] public bool canCloseByBack = true;
        [LabelText("接收输入焦点")] public bool takesFocus = true;
        [LabelText("隐藏同层其他面板")] public bool hideSameLayerPanels;
        public string PanelId => panelId;

        public UIPanelConfig() { }
        public UIPanelConfig(UIPanelAsset asset, UILayer layer = UILayer.Normal,
            UICachePolicy cachePolicy = UICachePolicy.HideOnClose, bool canCloseByBack = true,
            UIScopePolicy scopePolicy = UIScopePolicy.Application)
        {
            this.asset = asset;
            panelId = asset != null ? asset.PanelId : null;
            this.layer = layer;
            this.cachePolicy = cachePolicy;
            this.canCloseByBack = canCloseByBack;
            this.scopePolicy = scopePolicy;
        }

        public UIPanelConfig(string panelId, string assetAddress, UILayer layer = UILayer.Normal,
            UICachePolicy cachePolicy = UICachePolicy.HideOnClose, bool canCloseByBack = true,
            UIScopePolicy scopePolicy = UIScopePolicy.Application)
        {
            this.panelId = panelId;
            this.assetAddress = assetAddress;
            this.layer = layer;
            this.cachePolicy = cachePolicy;
            this.canCloseByBack = canCloseByBack;
            this.scopePolicy = scopePolicy;
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(panelId)) throw new InvalidOperationException("UI 配置的面板类型名为空。");
            if ((asset != null) == !string.IsNullOrWhiteSpace(assetAddress))
                throw new InvalidOperationException($"面板 {panelId} 必须配置且只能配置一种资源来源。");
            if (!Enum.IsDefined(typeof(UILayer), layer) || !Enum.IsDefined(typeof(UICachePolicy), cachePolicy)
                || !Enum.IsDefined(typeof(UIScopePolicy), scopePolicy))
                throw new InvalidOperationException($"面板 {panelId} 的层级、缓存或作用域配置无效。");
            if (asset != null) asset.Validate(panelId);
        }
    }

    [CreateAssetMenu(fileName = "UIConfig", menuName = "Moyo/UI/面板注册表")]
    public sealed class UIConfig : ScriptableObject
    {
        [SerializeField, LabelText("面板注册列表")] private UIPanelConfig[] panels = Array.Empty<UIPanelConfig>();
        public IReadOnlyList<UIPanelConfig> Panels => panels;
        public void SetPanels(params UIPanelConfig[] configurations)
        { panels = configurations == null ? Array.Empty<UIPanelConfig>() : (UIPanelConfig[])configurations.Clone(); }

        public Dictionary<string, UIPanelConfig> CreateValidatedRegistry()
        {
            var result = new Dictionary<string, UIPanelConfig>(StringComparer.Ordinal);
            if (panels == null) throw new InvalidOperationException($"{name} 的面板列表未配置。");
            foreach (var panel in panels)
            {
                if (panel == null) throw new InvalidOperationException($"{name} 的面板列表包含空项。");
                panel.Validate();
                if (result.ContainsKey(panel.PanelId)) throw new InvalidOperationException($"{name} 重复注册面板 {panel.PanelId}。");
                result.Add(panel.PanelId, panel);
            }
            return result;
        }

        public UIPanelConfig Get<T>() where T : UIPanelBase => Get(typeof(T).Name);
        public UIPanelConfig Get(string panelId)
        {
            if (TryGet(panelId, out var config)) return config;
            throw new InvalidOperationException($"{name} 未注册面板 {panelId}。");
        }
        public bool TryGet(string panelId, out UIPanelConfig config)
        {
            config = null;
            if (panels == null || string.IsNullOrWhiteSpace(panelId)) return false;
            foreach (var candidate in panels)
                if (candidate != null && candidate.PanelId == panelId) { config = candidate; return true; }
            return false;
        }
    }
}
