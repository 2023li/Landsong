using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Moyo.Unity
{
    [Serializable]
    public sealed class UIViewPreviewContent
    {
        [SerializeField, LabelText("预览内容键"), Required] private string key;
        [SerializeField, LabelText("示例对象")] private GameObject[] sampleObjects = Array.Empty<GameObject>();
        [SerializeField, LabelText("示例文字")] private TMP_Text[] sampleTextTargets = Array.Empty<TMP_Text>();
        [SerializeField, LabelText("运行时初始文字")] private string[] runtimeTexts = Array.Empty<string>();
        public string Key => key;
        public GameObject[] SampleObjects => sampleObjects;
        public TMP_Text[] SampleTextTargets => sampleTextTargets;
        public string[] RuntimeTexts => runtimeTexts;

        public UIViewPreviewContent() { }

        public UIViewPreviewContent(string key, GameObject[] objects, TMP_Text[] texts, string[] initialTexts)
        {
            this.key = key;
            Configure(objects, texts, initialTexts);
        }

        public void Configure(GameObject[] objects, TMP_Text[] texts, string[] initialTexts)
        {
            sampleObjects = objects == null ? Array.Empty<GameObject>() : (GameObject[])objects.Clone();
            sampleTextTargets = texts == null ? Array.Empty<TMP_Text>() : (TMP_Text[])texts.Clone();
            runtimeTexts = initialTexts == null ? Array.Empty<string>() : (string[])initialTexts.Clone();
        }

        public void ValidateConfiguration(Transform owner)
        {
            if (string.IsNullOrWhiteSpace(key) || sampleObjects == null || sampleTextTargets == null
                || runtimeTexts == null || sampleTextTargets.Length != runtimeTexts.Length)
                throw new InvalidOperationException($"{owner.name} 的预览内容配置不完整。");
            foreach (var sample in sampleObjects)
                if (sample != null && (sample.transform == owner || !sample.transform.IsChildOf(owner)))
                    throw new InvalidOperationException($"{owner.name} 的预览对象必须是所属视图的子对象。");
            foreach (var target in sampleTextTargets)
                if (target == null || (target.transform != owner && !target.transform.IsChildOf(owner)))
                    throw new InvalidOperationException($"{owner.name} 的预览文字未绑定或不属于所属视图。");
        }

        public void PrepareRuntime()
        {
            foreach (var sample in sampleObjects) if (sample != null) sample.SetActive(false);
            for (var i = 0; i < sampleTextTargets.Length; i++) sampleTextTargets[i].text = runtimeTexts[i] ?? string.Empty;
        }
    }

    /// <summary>
    /// 根面板明确拥有的子视图。只通过 childViews 建立归属，不搜索层级，不登记成全局面板。
    /// 子类在生命周期中只处理自身职责；不能在根生命周期中等待 Manager 的另一个导航操作。
    /// </summary>
    public class UIViewBase : MonoBehaviour
    {
        [SerializeField, LabelText("直属子视图")] private UIViewBase[] childViews = Array.Empty<UIViewBase>();
        [SerializeField, LabelText("随所属视图打开")] private bool openWithOwner;
        [SerializeField, LabelText("界面预览内容")] private UIViewPreviewContent[] previewContent = Array.Empty<UIViewPreviewContent>();
        private readonly SemaphoreSlim viewGate = new SemaphoreSlim(1, 1);
        private bool created;
        private bool opening;
        private bool closeNeeded;
        private bool released;
        public UIPanelBase OwnerPanel { get; private set; }
        public UIManager Manager { get; private set; }
        public IReadOnlyList<UIViewBase> ChildViews => childViews;
        public IReadOnlyList<UIViewPreviewContent> PreviewContent => previewContent;
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

        public UIViewPreviewContent ConfigurePreview(string key, GameObject[] objects, TMP_Text[] texts, string[] initialTexts)
        {
            if (Manager != null) throw new InvalidOperationException("已绑定的 UI 不能更改预览内容。");
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("预览内容必须有稳定键。", nameof(key));
            var entries = new List<UIViewPreviewContent>(previewContent ?? Array.Empty<UIViewPreviewContent>());
            var content = entries.Find(item => item != null && item.Key == key);
            if (content == null)
            {
                content = new UIViewPreviewContent(key, objects, texts, initialTexts);
                entries.Add(content);
                previewContent = entries.ToArray();
            }
            else content.Configure(objects, texts, initialTexts);
            content.ValidateConfiguration(transform);
            return content;
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
            if (view.previewContent == null) throw new InvalidOperationException($"{view.name} 的预览内容列表未配置。");
            var keys = new HashSet<string>();
            foreach (var preview in view.previewContent)
            {
                if (preview == null || !keys.Add(preview.Key))
                    throw new InvalidOperationException($"{view.name} 的预览内容为空或键重复。");
                preview.ValidateConfiguration(view.transform);
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
            foreach (var preview in previewContent) preview.PrepareRuntime();
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
