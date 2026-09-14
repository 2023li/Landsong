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
    public class UI_GamePanel_List : UI_GamePanel_View
    {
        [Sirenix.OdinInspector.LabelText("主条目容器")]
        public RectTransform PrimaryRows;
        [Sirenix.OdinInspector.LabelText("次条目容器")]
        public RectTransform SecondaryRows;
        [Sirenix.OdinInspector.LabelText("主滚动视图")]
        public ScrollRect PrimaryScroll;
        [LabelText("内容展示器")]
        public MonoBehaviour Presenter;
        [LabelText("本面板条目模板"), Required]
        public UI_GamePanel_Row RowTemplate;
        protected virtual bool UsesConfiguredPresenter => true;

        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (PrimaryRows == null || PrimaryScroll == null || RowTemplate == null)
                throw new InvalidOperationException(name + " 的列表面板引用不完整。");
            RowTemplate.ValidateConfiguration();
            if (UsesConfiguredPresenter && !(Presenter is IGameFeatureRenderer))
                throw new InvalidOperationException(name + " 必须配置实现 IGameFeatureRenderer 的展示器。");
        }

        UI_GamePanel_RowCollection rows;
        private protected UI_GamePanel_RowCollection Rows => rows;
        public override void Bind(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation)
        {
            base.Bind(session, commands, navigation);
            rows = new UI_GamePanel_RowCollection(RowTemplate);
            if (UsesConfiguredPresenter)
                ((IGameFeatureRenderer)Presenter).BindFeature(session, commands, navigation, this);

        }

        public override void Render()
        {
            if (!(Presenter is IGameFeatureRenderer renderer))
                throw new InvalidOperationException(name + " 的展示器配置无效。");
            renderer.Render();
        }

        internal UI_GamePanel_Row Row(string label, Action action = null, bool right = false, RectTransform parent = null, string key = null,
            [System.Runtime.CompilerServices.CallerFilePath] string caller = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
            => Rows.Row(label, action, parent ?? (right ? SecondaryRows : PrimaryRows), key, caller, line);
        internal T Item<T>(T template, string label, Action action = null, RectTransform parent = null, string key = null,
            [System.Runtime.CompilerServices.CallerFilePath] string caller = "", [System.Runtime.CompilerServices.CallerLineNumber] int line = 0) where T : UI_GamePanel_Row
            => Rows.Item(template, label, action, parent, key, null, caller, line);
        internal void Clear(RectTransform target) => Rows.Clear(target);
        internal void OwnContainer(RectTransform target, UI_GamePanel_InteractionLock owner) => Rows.Own(target, owner);
        internal void Place(RectTransform view, RectTransform parent) => Rows.Place(view, parent);
        internal float PanelItemsHeight(RectTransform target) => Rows.Height(target);
        internal void ReleaseContainer(RectTransform target) => Rows.Release(target);
        internal void BeginRender() => Rows.Begin(PrimaryRows, SecondaryRows);
        internal void EndRender() => Rows.End();
        internal override void ClearAllRows() => Rows?.ClearAll();
    }
}
