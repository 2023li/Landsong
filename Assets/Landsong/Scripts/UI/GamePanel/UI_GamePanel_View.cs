using System;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    /// <summary>Navigation and session lifetime shared by lists and authored feature views.</summary>
    public abstract class UI_GamePanel_View : MonoBehaviour
    {
        [LabelText("面板标识")] public GamePanelId PanelId;
        [LabelText("所需功能稳定标识")] public string RequiredFeatureId;
        [LabelText("目录未定义功能时允许访问")] public bool AllowMissingFeature;
        [LabelText("功能未解锁时允许打开说明")] public bool AllowLockedOpen;
        [LabelText("内容根对象")] public GameObject ContentRoot;
        [LabelText("标题")] public TMP_Text Title;
        [LabelText("关闭按钮")] public Button CloseButton;
        [LabelText("允许关闭")] public bool CloseAllowed = true;
        [LabelText("由根控制主内容显隐")] public bool ManageContentVisibility = true;

        protected GameUiSession Session { get; private set; }
        protected GameUiCommandWriter Commands { get; private set; }
        protected IGameUiNavigation Navigation { get; private set; }
        protected EntityManager em => Session.Manager;
        protected Entity root => Session.SessionRoot;

        public void SetContentVisible(bool visible)
        {
            if (ManageContentVisibility) ContentRoot.SetActive(visible);
        }

        public bool IsFeatureUnlocked(EntityManager manager, Entity owner)
        {
            if (owner == Entity.Null || !manager.Exists(owner)) return false;
            if (string.IsNullOrEmpty(RequiredFeatureId)) return true;
            int definition = Sim.FindDefinition(manager, owner, new FixedString128Bytes(RequiredFeatureId));
            return definition < 0 ? AllowMissingFeature : FeatureOps.IsUnlocked(manager, owner, definition);
        }

        public bool CanOpen(EntityManager manager, Entity owner) => owner != Entity.Null && manager.Exists(owner)
            && (AllowLockedOpen || IsFeatureUnlocked(manager, owner));

        public virtual void ValidateConfiguration()
        {
            if (!string.IsNullOrEmpty(RequiredFeatureId) && (!RequiredFeatureId.StartsWith("feature.", StringComparison.Ordinal)
                || System.Text.Encoding.UTF8.GetByteCount(RequiredFeatureId) > 125))
                throw new InvalidOperationException(name + " 的功能稳定标识必须为有效的 feature.* 内容 ID。");
            if (PanelId == GamePanelId.None || !Enum.IsDefined(typeof(GamePanelId), PanelId)
                || ContentRoot == null || Title == null || CloseButton == null)
                throw new InvalidOperationException(name + " 的功能面板引用不完整。");
        }

        public virtual void Bind(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation)
        {
            if (session == null || commands == null || navigation == null) throw new ArgumentNullException("面板会话服务");
            ValidateConfiguration();
            Session = session; Commands = commands; Navigation = navigation;
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(navigation.ClosePanel);
            CloseButton.interactable = CloseAllowed;
        }

        public abstract void Render();
        internal virtual void ClearAllRows() { }
    }
}
