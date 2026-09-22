using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>独立根面板。没有 Start 自动修复；应用仅通过 UIManager 创建并绑定。</summary>
    public class UIPanelBase : UIViewBase
    {
        [SerializeField, LabelText("面板交互组"), Required] private CanvasGroup panelCanvasGroup;
        public string PanelId => GetType().Name;
        public CanvasGroup PanelCanvasGroup => panelCanvasGroup;
        public UIScope Scope { get; private set; }
        public UIPanelState State { get; internal set; } = UIPanelState.Creating;
        public bool IsManagedByUIManager => Manager != null;
        public virtual bool CanCloseByBack => true;

        public void ConfigurePanel(CanvasGroup interactionGroup, params UIViewBase[] children)
        {
            if (Manager != null) throw new InvalidOperationException("已绑定的根面板不能重新配置。");
            panelCanvasGroup = interactionGroup;
            ConfigureChildren(children);
        }

        protected override void ValidateLocalConfiguration()
        {
            base.ValidateLocalConfiguration();
            if (panelCanvasGroup == null || panelCanvasGroup.gameObject != gameObject)
                throw new InvalidOperationException($"面板 {name} 必须显式绑定自身的 CanvasGroup。");
        }

        internal void BindToManager(UIManager manager, UIScope scope)
        {
            ValidateConfiguration();
            Scope = scope;
            BindOwner(this, manager);
        }

        internal void SetVisibility(bool visible)
        {
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
            gameObject.SetActive(visible);
        }

        internal void ClearScope() { Scope = null; }
    }
}