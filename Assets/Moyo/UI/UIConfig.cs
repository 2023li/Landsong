using System;
using System.Collections.Generic;
using UnityEngine;

namespace Moyo.Unity
{
    public enum UILayer
    {
        Background,
        HUD,
        Normal,
        Popup,
        Toast,
        Guide,
        Blocker,
        Debuger,

    }

    public enum UICachePolicy
    {
        DestroyOnClose,
        HideOnClose,
        Permanent
    }

    [Serializable]
    public class UIPanelConfig
    {
        [SerializeField, Tooltip("面板 ID。该值必须等于 Panel 脚本类名，由 Editor 下拉框自动选择，不要手动填写。")]
        private string panelId;

        [Tooltip("Addressables 中配置的 Prefab Key。可以和 PanelId 不同。")]
        public string addressableKey;

        [Tooltip("面板所属 UI 层级。")]
        public UILayer layer = UILayer.Normal;

        [Tooltip("关闭面板时的缓存策略。")]
        public UICachePolicy cachePolicy = UICachePolicy.HideOnClose;

        [Tooltip("是否允许返回操作关闭该面板。勾选后，BackAsync 会关闭最近打开的此类面板。")]
        public bool canCloseByBack = true;

        [Tooltip("打开该面板时，是否让当前返回栈顶部面板失焦。这里只调用 OnBlurAsync，不会隐藏旧面板。")]
        public bool blurPreviousOnOpen;

        [Tooltip("打开该面板时，是否隐藏同一 UILayer 下其它已打开的面板。关闭该面板时会自动恢复。")]
        public bool hideSameLayerPanels;

        /// <summary>
        /// 面板 ID。
        /// 固定等于 UIPanelBase 子类的类名。
        /// </summary>
        public string PanelId => panelId;
    }

    [CreateAssetMenu(fileName = "UIConfig", menuName = "MoyoUnity/UISystem/UIConfig")]
    public class UIConfig : ScriptableObject
    {
        [SerializeField]
        private UIPanelConfig[] panels;

        private Dictionary<string, UIPanelConfig> panelMap;

        public IReadOnlyList<UIPanelConfig> Panels => panels;

        public UIPanelConfig Get<T>() where T : UIPanelBase
        {
            return Get(typeof(T).Name);
        }

        public UIPanelConfig Get(string panelId)
        {
            if (string.IsNullOrEmpty(panelId))
            {
                Debug.LogError("获取 UI 配置失败：panelId 为空。");
                return null;
            }

            BuildPanelMapIfNeeded();

            if (panelMap.TryGetValue(panelId, out var config))
            {
                return config;
            }

            Debug.LogError($"找不到 UI 配置：{panelId}。请确认 UIConfig 中是否选择了对应的 UIPanelBase 子类。");
            return null;
        }

        public bool TryGet(string panelId, out UIPanelConfig config)
        {
            config = null;

            if (string.IsNullOrEmpty(panelId))
            {
                return false;
            }

            BuildPanelMapIfNeeded();

            return panelMap.TryGetValue(panelId, out config);
        }

        private void BuildPanelMapIfNeeded()
        {
            if (panelMap != null)
            {
                return;
            }

            panelMap = new Dictionary<string, UIPanelConfig>();

            if (panels == null)
            {
                return;
            }

            foreach (var panel in panels)
            {
                if (panel == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(panel.PanelId))
                {
                    Debug.LogError($"{name} 中存在 PanelId 为空的配置。", this);
                    continue;
                }

                if (panelMap.ContainsKey(panel.PanelId))
                {
                    Debug.LogError($"{name} 中存在重复 PanelId：{panel.PanelId}", this);
                    continue;
                }

                panelMap.Add(panel.PanelId, panel);
            }
        }

        private void OnValidate()
        {
            panelMap = null;

            if (panels == null)
            {
                return;
            }

            var ids = new HashSet<string>();

            foreach (var panel in panels)
            {
                if (panel == null)
                {
                    continue;
                }

                if (string.IsNullOrEmpty(panel.PanelId))
                {
                    continue;
                }

                if (!ids.Add(panel.PanelId))
                {
                    Debug.LogError($"UIConfig 存在重复 PanelId：{panel.PanelId}", this);
                }

                if (string.IsNullOrEmpty(panel.addressableKey))
                {
                    Debug.LogWarning($"Panel {panel.PanelId} 的 Addressable Key 为空。", this);
                }
            }
        }
    }
}
