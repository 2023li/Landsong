using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    /// <summary>Reconciles visible containers by semantic identity and retains pressed targets until their click completes.</summary>
    public sealed class UI_GamePanel_RowRenderer : MonoBehaviour
    {
        [LabelText("条目模板")] public UI_GamePanel_Row RowTemplate;
        internal IGameUiNavigation navigation;
        internal bool reconcilingRows;
        sealed class Container
        {
            public readonly Dictionary<string, UI_GamePanel_Row> Active = new Dictionary<string, UI_GamePanel_Row>();
            public readonly List<UI_GamePanel_Row> Pool = new List<UI_GamePanel_Row>();
            public readonly HashSet<string> Seen = new HashSet<string>();
            public readonly List<UI_GamePanel_Row> Order = new List<UI_GamePanel_Row>();
            public readonly Dictionary<string, int> AnonymousCounts = new Dictionary<string, int>();
            public UI_GamePanel_InteractionLock Owner;
            public bool Suppressed;
        }
        readonly Dictionary<RectTransform, Container> containers = new Dictionary<RectTransform, Container>();
        readonly HashSet<RectTransform> pending = new HashSet<RectTransform>();
        public int ContainerCount => containers.Count;
        public void BeginReconcile(params RectTransform[] parents)
        {
            reconcilingRows = true;
            foreach (var parent in parents) Clear(parent);
        }
        public void EndReconcile() => FinishRows();
        internal UI_GamePanel_Row CreatePanelItem(string label, Action action, RectTransform parent, string key = null,
            [CallerFilePath] string caller = "", [CallerLineNumber] int line = 0) => Row(label, action, parent: parent, key: key, caller: caller, line: line);
        internal void ClearPanelItems(RectTransform rows) => Clear(rows);
        internal float PanelItemsHeight(RectTransform rows)
        {
            if (!containers.TryGetValue(rows, out var container)) return 0;
            float height = 0;
            foreach (var row in container.Order)
                if (row != null && row.gameObject.activeSelf) height += row.Layout.preferredHeight + 6;
            return height;
        }
        Container Get(RectTransform parent)
        {
            if (parent == null) throw new InvalidOperationException("列表未配置内容容器。");
            if (!containers.TryGetValue(parent, out var container)) containers.Add(parent, container = new Container());
            return container;
        }
        public void OwnContainer(RectTransform parent, UI_GamePanel_InteractionLock owner) { Get(parent).Owner = owner; }
        internal void Clear(RectTransform rows)
        {
            var container = Get(rows);
            container.Suppressed = reconcilingRows && !rows.gameObject.activeInHierarchy;
            if (container.Suppressed) return;
            if (!reconcilingRows) RetireUnused(container);
            container.Seen.Clear(); container.Order.Clear(); container.AnonymousCounts.Clear();
            if (reconcilingRows) pending.Add(rows);
            else foreach (var row in container.Active.Values)
                if (row != null && !row.IsInteractionPinned) row.gameObject.SetActive(false);
        }
        internal UI_GamePanel_Row Row(string label, Action action = null, bool right = false, RectTransform parent = null, string key = null,
            [CallerFilePath] string caller = "", [CallerLineNumber] int line = 0)
            => Item(RowTemplate, label, action, parent ?? (right ? navigation.SecondaryRows : navigation.PrimaryRows), key, caller, line);
        public T Item<T>(T template, string label, Action action = null, RectTransform parent = null, string key = null,
            [CallerFilePath] string caller = "", [CallerLineNumber] int line = 0) where T : UI_GamePanel_Row
        {
            if (template == null || parent == null) throw new InvalidOperationException("列表条目模板或目标容器未配置。");
            var container = Get(parent);
            if (reconcilingRows && (!parent.gameObject.activeInHierarchy || container.Suppressed)) return null;
            if (key == null)
            {
                // Explanatory text has no domain identity. A changed message uses a different pooled row.
                var basis = caller + ":" + line + ":" + label;
                container.AnonymousCounts.TryGetValue(basis, out var occurrence);
                container.AnonymousCounts[basis] = occurrence + 1;
                key = "text:" + basis + ":" + occurrence;
            }
            else key = "domain:" + key;
            if (!container.Seen.Add(key)) throw new InvalidOperationException("同一列表重复提交条目身份：" + key);
            T row;
            if (container.Active.TryGetValue(key, out var existing))
            {
                if (!(existing is T typed) || existing.Template != template)
                    throw new InvalidOperationException("同一条目身份使用了不同的类型模板：" + key);
                row = typed;
            }
            else
            {
                row = null;
                for (int index = container.Pool.Count - 1; index >= 0; index--)
                    if (container.Pool[index] is T reusable && reusable.Template == template)
                    { row = reusable; container.Pool.RemoveAt(index); break; }
                if (row == null) { row = Instantiate(template, parent); row.Template = template; row.InitializeAction(); }
                row.Identity = key; row.Interaction.Parent = container.Owner;
                row.ResetPresentation();
                container.Active.Add(key, row);
            }
            container.Order.Add(row);
            if (!row.CanRebind) return row;
            if (!reconcilingRows && !IsPinned(container)) row.transform.SetSiblingIndex(container.Order.Count - 1);
            row.gameObject.SetActive(true);
            bool changed = row.Label.text != label;
            if (changed) row.Label.text = label;
            row.Label.enableAutoSizing = false; row.Label.textWrappingMode = TextWrappingModes.Normal;
            if (changed || row.Layout.preferredHeight < 0)
            {
                var preferred = row.Label.GetPreferredValues(label, Mathf.Max(200, parent.rect.width - 32), float.PositiveInfinity);
                row.Layout.preferredHeight = row.Layout.minHeight = Mathf.Max(38, preferred.y + 18);
            }
            row.BindAction(action);
            return row;
        }
        internal void FinishRows()
        {
            foreach (var parent in pending)
            {
                if (parent == null || !parent.gameObject.activeInHierarchy) continue;
                var container = containers[parent];
                if (container.Suppressed) continue;
                RetireUnused(container);
                if (IsPinned(container)) continue;
                for (int index = 0; index < container.Order.Count; index++)
                    if (container.Order[index] != null) container.Order[index].transform.SetSiblingIndex(index);
            }
            pending.Clear(); reconcilingRows = false;
        }
        static bool IsPinned(Container container)
        {
            foreach (var row in container.Active.Values) if (row != null && row.IsInteractionPinned) return true;
            return false;
        }
        static void RetireUnused(Container container)
        {
            var removed = new List<string>();
            foreach (var pair in container.Active)
            {
                if (container.Seen.Contains(pair.Key) || pair.Value != null && pair.Value.IsInteractionPinned) continue;
                if (pair.Value != null)
                { pair.Value.gameObject.SetActive(false); pair.Value.BindAction(null); container.Pool.Add(pair.Value); }
                removed.Add(pair.Key);
            }
            foreach (var key in removed) container.Active.Remove(key);
        }
        public void ReleaseContainer(RectTransform parent)
        {
            if (!containers.TryGetValue(parent, out var container)) return;
            foreach (var row in container.Active.Values) if (row != null) Destroy(row.gameObject);
            foreach (var row in container.Pool) if (row != null) Destroy(row.gameObject);
            containers.Remove(parent); pending.Remove(parent);
        }
        public void ClearAll()
        {
            foreach (var parent in new List<RectTransform>(containers.Keys)) ReleaseContainer(parent);
            containers.Clear(); pending.Clear(); reconcilingRows = false;
        }
    }
}
