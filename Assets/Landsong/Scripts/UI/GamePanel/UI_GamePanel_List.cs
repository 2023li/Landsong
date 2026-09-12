using System;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Sirenix.OdinInspector;
using Unity.Collections;

namespace Landsong.ECS.Presentation
{
    // Each feature owns its content and references an explicit presenter.
    public class UI_GamePanel_List : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("面板标识")]
        public GamePanelId PanelId;
        [LabelText("所需功能稳定标识")]
        public string RequiredFeatureId;
        [LabelText("目录未定义功能时允许访问")]
        public bool AllowMissingFeature;
        [LabelText("功能未解锁时允许打开说明")]
        public bool AllowLockedOpen;
        [Sirenix.OdinInspector.LabelText("内容根对象")]
        public GameObject ContentRoot;
        [Sirenix.OdinInspector.LabelText("主条目容器")]
        public RectTransform PrimaryRows;
        [Sirenix.OdinInspector.LabelText("次条目容器")]
        public RectTransform SecondaryRows;
        [Sirenix.OdinInspector.LabelText("主滚动视图")]
        public ScrollRect PrimaryScroll;
        [Sirenix.OdinInspector.LabelText("标题")]
        public TMP_Text Title;
        [Sirenix.OdinInspector.LabelText("关闭按钮")]
        public Button CloseButton;
        [LabelText("内容展示器")]
        public MonoBehaviour Presenter;
        [LabelText("允许关闭")]
        public bool CloseAllowed = true;
        [LabelText("由根控制主内容显隐")]
        public bool ManageContentVisibility = true;
        public void SetContentVisible(bool visible)
        {
            if (ManageContentVisibility)
                ContentRoot.SetActive(visible);
        }

        protected virtual bool UsesConfiguredPresenter => true;

        public bool IsFeatureUnlocked(EntityManager manager, Entity owner)
        {
            if (owner == Entity.Null || !manager.Exists(owner)) return false;
            if (string.IsNullOrEmpty(RequiredFeatureId)) return true;
            int definition = Sim.FindDefinition(manager, owner, new FixedString128Bytes(RequiredFeatureId));
            return definition < 0 ? AllowMissingFeature : FeatureOps.IsUnlocked(manager, owner, definition);
        }

        public bool CanOpen(EntityManager manager, Entity owner) => owner != Entity.Null && manager.Exists(owner)
            && (AllowLockedOpen || IsFeatureUnlocked(manager, owner));

        public void ValidateConfiguration()
        {
            if (!string.IsNullOrEmpty(RequiredFeatureId) && (!RequiredFeatureId.StartsWith("feature.", StringComparison.Ordinal) || System.Text.Encoding.UTF8.GetByteCount(RequiredFeatureId) > 125))
                throw new InvalidOperationException(name + " 的功能稳定标识必须为有效的 feature.* 内容 ID。");
            if ((PanelId == GamePanelId.None || !Enum.IsDefined(typeof(GamePanelId), PanelId)) || ContentRoot == null || PrimaryRows == null || PrimaryScroll == null || Title == null || CloseButton == null)
                throw new InvalidOperationException(name + " 的列表面板引用不完整。");
            if (UsesConfiguredPresenter && !(Presenter is IGameFeatureRenderer))
                throw new InvalidOperationException(name + " 必须配置实现 IGameFeatureRenderer 的展示器。");
        }

        protected GameUiSession Session { get; private set; }
        protected GameUiCommandWriter Commands { get; private set; }
        protected IGameUiNavigation Navigation { get; private set; }
        protected UI_GamePanel_RowRenderer Rows { get; private set; }
        protected EntityManager em => Session.Manager;
        protected Entity root => Session.SessionRoot;

        public void Bind(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null || (PanelId == GamePanelId.None || !Enum.IsDefined(typeof(GamePanelId), PanelId)) || ContentRoot == null || PrimaryRows == null || PrimaryScroll == null || Title == null || CloseButton == null)
                throw new InvalidOperationException(name + " 的列表面板检查器引用不完整。");
            ValidateConfiguration();
            Session = session;
            Commands = commands;
            Navigation = navigation;
            Rows = rows;
            if (UsesConfiguredPresenter)
                ((IGameFeatureRenderer)Presenter).BindFeature(session, commands, navigation, rows);
            CloseButton.onClick.RemoveAllListeners();
            CloseButton.onClick.AddListener(navigation.ClosePanel);
            CloseButton.interactable = CloseAllowed;
        }

        public virtual void Render()
        {
            if (!(Presenter is IGameFeatureRenderer renderer))
                throw new InvalidOperationException(name + " 的展示器配置无效。");
            renderer.Render();
        }

        protected UI_GamePanel_Row Row(string label, Action action = null, bool right = false, RectTransform parent = null, string key = null,
            [System.Runtime.CompilerServices.CallerFilePath] string caller = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
            => Rows.CreatePanelItem(label, action, parent ?? (right ? SecondaryRows : PrimaryRows), key, caller, line);
        protected void Clear(RectTransform rows) => Rows.ClearPanelItems(rows);
    }
}
