using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Moyo.Unity
{
    /// <summary>
    /// 根面板明确拥有的子视图。只通过 childViews 建立归属，不搜索层级，不登记成全局面板。
    /// 子类在生命周期中只处理自身职责；不能在根生命周期中等待 Manager 的另一个导航操作。
    /// </summary>
    public class UIViewBase : MonoBehaviour
    {
        [SerializeField, LabelText("直属子视图")] private UIViewBase[] childViews = Array.Empty<UIViewBase>();
        [SerializeField, LabelText("随所属视图打开")] private bool openWithOwner;
        [SerializeField, LabelText("编辑示例清理配置")] private UIPreviewOnly[] previewBindings = Array.Empty<UIPreviewOnly>();
        private readonly SemaphoreSlim viewGate = new SemaphoreSlim(1, 1);
        private bool created;
        private bool opening;
        private bool closeNeeded;
        private bool released;
        public UIPanelBase OwnerPanel { get; private set; }
        public UIManager Manager { get; private set; }
        public IReadOnlyList<UIViewBase> ChildViews => childViews;
        public IReadOnlyList<UIPreviewOnly> PreviewBindings => previewBindings;
        public bool IsViewOpen { get; private set; }
        public object Context { get; private set; }
        public CancellationToken OperationToken { get; private set; }
        public CancellationToken ScopeToken => OwnerPanel != null && OwnerPanel.Scope != null
            ? OwnerPanel.Scope.Token : CancellationToken.None;

        public void ConfigureChildren(UIViewBase[] children, bool opensWithOwner = false)
        {
            if (Manager != null) throw new InvalidOperationException("已绑定的 UI 不能更改子视图归属。");
            childViews = children == null ? Array.Empty<UIViewBase>() : (UIViewBase[])children.Clone();
            openWithOwner = opensWithOwner;
        }

        public void ConfigurePreview(params UIPreviewOnly[] bindings)
        {
            if (Manager != null) throw new InvalidOperationException("已绑定的 UI 不能更改预览清理引用。");
            previewBindings = bindings == null ? Array.Empty<UIPreviewOnly>() : (UIPreviewOnly[])bindings.Clone();
        }

        public virtual void ValidateConfiguration()
        { ValidateTree(this, null, new HashSet<UIViewBase>()); }

        protected virtual void ValidateLocalConfiguration() { }

        private static void ValidateTree(UIViewBase view, UIViewBase parent, HashSet<UIViewBase> visited)
        {
            if (view == null) throw new InvalidOperationException("UI 子视图列表包含空引用。");
            if (!visited.Add(view)) throw new InvalidOperationException($"UI 子视图 {view.name} 重复登记或存在循环归属。");
            if (parent != null)
            {
                if (view is UIPanelBase) throw new InvalidOperationException($"{view.name} 是独立根面板，不能登记为子视图。");
                if (view.transform == parent.transform || !view.transform.IsChildOf(parent.transform))
                    throw new InvalidOperationException($"{view.name} 不属于配置父视图 {parent.name} 的子对象。");
            }
            view.ValidateLocalConfiguration();
            if (view.previewBindings == null) throw new InvalidOperationException($"{view.name} 的预览清理列表未配置。");
            foreach (var preview in view.previewBindings)
            {
                if (preview == null) continue; // 构建可能移除显式 EditorOnly 预览节点。
                if (preview.transform != view.transform && !preview.transform.IsChildOf(view.transform))
                    throw new InvalidOperationException($"{view.name} 的预览清理引用不属于该视图。");
                preview.ValidateConfiguration();
            }
            if (view.childViews == null) throw new InvalidOperationException($"{view.name} 的子视图列表未配置。");
            foreach (var child in view.childViews) ValidateTree(child, view, visited);
        }

        internal void BindOwner(UIPanelBase owner, UIManager manager)
        {
            if (Manager != null && (Manager != manager || OwnerPanel != owner))
                throw new InvalidOperationException($"{name} 已有其他 UI 所有者。");
            OwnerPanel = owner;
            Manager = manager;
            foreach (var child in childViews) child.BindOwner(owner, manager);
        }

        internal async Task CreateTreeAsync(CancellationToken token)
        {
            if (created) return;
            created = true; // 即便本次创建失败，已经注册的资源也会进入 Release。
            OperationToken = token;
            foreach (var preview in previewBindings) if (preview != null) preview.PrepareRuntime();
            if (!(this is UIPanelBase)) gameObject.SetActive(false);
            token.ThrowIfCancellationRequested();
            await OnCreateAsync();
            token.ThrowIfCancellationRequested();
            foreach (var child in childViews) await child.CreateTreeAsync(token);
        }

        internal async Task OpenTreeAsync(object context, CancellationToken token)
        {
            await viewGate.WaitAsync(token);
            try { await OpenTreeCoreAsync(context, token); }
            finally { viewGate.Release(); }
        }

        private async Task OpenTreeCoreAsync(object context, CancellationToken token)
        {
            if (released || !created) throw new InvalidOperationException($"{name} 尚未创建或已经释放。");
            if (opening) throw new InvalidOperationException($"{name} 的打开生命周期发生递归调用。");
            if (IsViewOpen) await CloseTreeCoreAsync();
            opening = true;
            closeNeeded = true;
            Context = context;
            OperationToken = token;
            try
            {
                token.ThrowIfCancellationRequested();
                await OnOpenAsync(context);
                token.ThrowIfCancellationRequested();
                foreach (var child in childViews)
                    if (child.openWithOwner) await child.OpenTreeAsync(context, token);
                token.ThrowIfCancellationRequested();
                if (!(this is UIPanelBase) && (OwnerPanel == null
                    || (OwnerPanel.State != UIPanelState.Open && OwnerPanel.State != UIPanelState.Opening)))
                    throw new OperationCanceledException("所属根面板已经关闭，子视图不能重新发布。", token);
                IsViewOpen = true;
                if (!(this is UIPanelBase)) gameObject.SetActive(true);
            }
            finally { opening = false; }
        }

        internal async Task CloseTreeAsync()
        {
            await viewGate.WaitAsync();
            try { await CloseTreeCoreAsync(); }
            finally { viewGate.Release(); }
        }

        private async Task CloseTreeCoreAsync()
        {
            var errors = new List<Exception>();
            for (var i = childViews.Length - 1; i >= 0; i--)
            {
                try { if (childViews[i] != null) await childViews[i].CloseTreeAsync(); }
                catch (Exception error) { errors.Add(error); }
            }
            try
            {
                if (closeNeeded) await OnCloseAsync();
            }
            catch (Exception error) { errors.Add(error); }
            finally
            {
                IsViewOpen = false;
                closeNeeded = false;
                Context = null;
                OperationToken = CancellationToken.None;
                if (this != null && !(this is UIPanelBase)) gameObject.SetActive(false);
            }
            if (errors.Count > 0) throw new AggregateException($"{name} 关闭失败。", errors);
        }

        internal async Task ReleaseTreeAsync()
        {
            if (released) return;
            released = true;
            var errors = new List<Exception>();
            try { await CloseTreeAsync(); }
            catch (Exception error) { errors.Add(error); }
            for (var i = childViews.Length - 1; i >= 0; i--)
            {
                try { if (childViews[i] != null) await childViews[i].ReleaseTreeAsync(); }
                catch (Exception error) { errors.Add(error); }
            }
            try { if (created) await OnReleaseAsync(); }
            catch (Exception error) { errors.Add(error); }
            finally { Context = null; OwnerPanel = null; Manager = null; OperationToken = CancellationToken.None; }
            if (errors.Count > 0) throw new AggregateException("UI 释放生命周期失败。", errors);
        }

        public async Task OpenViewAsync(object context = null, CancellationToken cancellationToken = default)
        {
            if (this is UIPanelBase) throw new InvalidOperationException("独立根面板必须通过 UIManager 打开。");
            if (OwnerPanel == null || Manager == null || OwnerPanel.Scope == null)
                throw new InvalidOperationException($"{name} 未配置所属根面板。");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ScopeToken, cancellationToken);
            if (OwnerPanel.State != UIPanelState.Open && OwnerPanel.State != UIPanelState.Opening)
                throw new InvalidOperationException($"{name} 的所属根面板未打开。");
            try { await OpenTreeAsync(context, linked.Token); }
            catch { await CloseTreeAsync(); throw; }
        }

        public async Task CloseViewAsync()
        {
            if (this is UIPanelBase) throw new InvalidOperationException("独立根面板必须通过 UIManager 关闭。");
            await CloseTreeAsync();
        }

        public virtual Task OnCreateAsync() => Task.CompletedTask;
        public virtual Task OnOpenAsync(object args) => Task.CompletedTask;
        public virtual Task OnFocusAsync() => Task.CompletedTask;
        public virtual Task OnBlurAsync() => Task.CompletedTask;
        public virtual Task OnCloseAsync() => Task.CompletedTask;
        public virtual Task OnReleaseAsync() => Task.CompletedTask;
        /// <summary>根可将返回事件交给自己的最上层子弹窗；返回 true 表示已经消费。</summary>
        public virtual Task<bool> TryHandleBackAsync() => Task.FromResult(false);
    }
}
