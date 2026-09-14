using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    /// <summary>Panel-owned dynamic row state. Each panel creates and clears its own collection.</summary>
    internal sealed class UI_GamePanel_RowCollection
    {
        readonly UI_GamePanel_Row rowTemplate;
        bool reconciling;

        sealed class Container
        {
            public readonly Dictionary<string, UI_GamePanel_Row> Active = new Dictionary<string, UI_GamePanel_Row>();
            public readonly List<UI_GamePanel_Row> Pool = new List<UI_GamePanel_Row>();
            public readonly HashSet<string> Seen = new HashSet<string>();
            public readonly List<RectTransform> Order = new List<RectTransform>();
            public readonly Dictionary<string, int> AnonymousCounts = new Dictionary<string, int>();
            public UI_GamePanel_InteractionLock Owner;
            public bool Suppressed;
        }

        readonly Dictionary<RectTransform, Container> containers = new Dictionary<RectTransform, Container>();
        readonly HashSet<RectTransform> pending = new HashSet<RectTransform>();

        public UI_GamePanel_RowCollection(UI_GamePanel_Row rowTemplate)
        {
            this.rowTemplate = rowTemplate != null ? rowTemplate : throw new ArgumentNullException(nameof(rowTemplate));
        }

        public void Begin(params RectTransform[] parents)
        {
            reconciling = true;
            foreach (var parent in parents)
                if (parent != null)
                    Clear(parent);
        }

        public void End() => Finish();

        Container Get(RectTransform parent)
        {
            if (parent == null)
                throw new InvalidOperationException("列表未配置内容容器。");
            if (!containers.TryGetValue(parent, out var container))
                containers.Add(parent, container = new Container());
            return container;
        }

        public void Own(RectTransform parent, UI_GamePanel_InteractionLock owner) => Get(parent).Owner = owner;

        public void Clear(RectTransform rows)
        {
            var container = Get(rows);
            container.Suppressed = reconciling && !rows.gameObject.activeInHierarchy;
            if (container.Suppressed)
                return;
            if (!reconciling)
                RetireUnused(container);
            container.Seen.Clear();
            container.Order.Clear();
            container.AnonymousCounts.Clear();
            if (reconciling)
                pending.Add(rows);
            else
                foreach (var row in container.Active.Values)
                    if (row != null && !row.IsInteractionPinned)
                        row.gameObject.SetActive(false);
        }

        public UI_GamePanel_Row Row(string label, Action action = null, RectTransform parent = null, string key = null,
            [CallerFilePath] string caller = "", [CallerLineNumber] int line = 0) =>
            Item(rowTemplate, label, action, parent, key, null, caller, line);

        public T Item<T>(T template, string label, Action action = null, RectTransform parent = null, string key = null,
            Action<T> prepare = null,
            [CallerFilePath] string caller = "", [CallerLineNumber] int line = 0) where T : UI_GamePanel_Row
        {
            if (template == null || parent == null)
                throw new InvalidOperationException("列表条目模板或目标容器未配置。");
            var container = Get(parent);
            if (reconciling && (!parent.gameObject.activeInHierarchy || container.Suppressed))
                return null;
            if (key == null)
            {
                var basis = caller + ":" + line + ":" + label;
                container.AnonymousCounts.TryGetValue(basis, out var occurrence);
                container.AnonymousCounts[basis] = occurrence + 1;
                key = "text:" + basis + ":" + occurrence;
            }
            else
                key = "domain:" + key;
            if (!container.Seen.Add(key))
                throw new InvalidOperationException("同一列表重复提交条目身份：" + key);

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
                for (var index = container.Pool.Count - 1; index >= 0; index--)
                    if (container.Pool[index] is T reusable && reusable.Template == template)
                    {
                        row = reusable;
                        container.Pool.RemoveAt(index);
                        break;
                    }
                if (row == null)
                {
                    row = UnityEngine.Object.Instantiate(template, parent);
                    row.Template = template;
                    row.InitializeAction();
                }
                row.Identity = key;
                row.Interaction.Parent = container.Owner;
                prepare?.Invoke(row);
                row.ResetPresentation();
                container.Active.Add(key, row);
            }

            container.Order.Add(row.transform as RectTransform);
            if (!row.CanRebind)
                return row;
            if (!reconciling && !IsPinned(container))
                row.transform.SetSiblingIndex(container.Order.Count - 1);
            row.gameObject.SetActive(true);
            var changed = row.Label.text != label;
            if (changed)
                row.Label.text = label;
            row.Label.enableAutoSizing = false;
            row.Label.textWrappingMode = TextWrappingModes.Normal;
            if (changed || row.Layout.preferredHeight < 0)
            {
                var preferred = row.Label.GetPreferredValues(label, Mathf.Max(200, parent.rect.width - 32), float.PositiveInfinity);
                row.Layout.preferredHeight = row.Layout.minHeight = Mathf.Max(38, preferred.y + 18);
            }
            row.BindAction(action);
            return row;
        }

        public void Place(RectTransform view, RectTransform parent)
        {
            if (view == null || parent == null)
                throw new InvalidOperationException("列表固定内容或目标容器未配置。");
            var container = Get(parent);
            if (reconciling && (!parent.gameObject.activeInHierarchy || container.Suppressed))
                return;
            if (view.parent != parent)
                throw new InvalidOperationException("列表固定内容必须属于目标容器。");
            view.gameObject.SetActive(true);
            container.Order.Add(view);
            if (!reconciling && !IsPinned(container))
                view.SetSiblingIndex(container.Order.Count - 1);
        }

        public float Height(RectTransform rows)
        {
            if (!containers.TryGetValue(rows, out var container))
                return 0;
            var height = 0f;
            foreach (var item in container.Order)
            {
                if (item == null || !item.gameObject.activeSelf)
                    continue;
                var layout = item.GetComponent<LayoutElement>();
                if (layout != null)
                    height += layout.preferredHeight + 6;
            }
            return height;
        }

        void Finish()
        {
            foreach (var parent in pending)
            {
                if (parent == null || !parent.gameObject.activeInHierarchy)
                    continue;
                var container = containers[parent];
                if (container.Suppressed)
                    continue;
                RetireUnused(container);
                if (IsPinned(container))
                    continue;
                for (var index = 0; index < container.Order.Count; index++)
                    if (container.Order[index] != null)
                        container.Order[index].SetSiblingIndex(index);
            }
            pending.Clear();
            reconciling = false;
        }

        static bool IsPinned(Container container)
        {
            foreach (var row in container.Active.Values)
                if (row != null && row.IsInteractionPinned)
                    return true;
            foreach (var item in container.Order)
            {
                var row = item != null ? item.GetComponent<UI_GamePanel_Row>() : null;
                if (row != null && row.IsInteractionPinned)
                    return true;
                var interaction = item != null ? item.GetComponent<UI_GamePanel_InteractionLock>() : null;
                if (interaction != null && interaction.IsPinned)
                    return true;
            }
            return false;
        }

        static void RetireUnused(Container container)
        {
            var removed = new List<string>();
            foreach (var pair in container.Active)
            {
                if (container.Seen.Contains(pair.Key) || pair.Value != null && pair.Value.IsInteractionPinned)
                    continue;
                if (pair.Value != null)
                {
                    pair.Value.gameObject.SetActive(false);
                    pair.Value.BindAction(null);
                    container.Pool.Add(pair.Value);
                }
                removed.Add(pair.Key);
            }
            foreach (var key in removed)
                container.Active.Remove(key);
        }

        public void Release(RectTransform parent)
        {
            if (!containers.TryGetValue(parent, out var container))
                return;
            foreach (var row in container.Active.Values)
                if (row != null)
                    UnityEngine.Object.Destroy(row.gameObject);
            foreach (var row in container.Pool)
                if (row != null)
                    UnityEngine.Object.Destroy(row.gameObject);
            containers.Remove(parent);
            pending.Remove(parent);
        }

        public void ClearAll()
        {
            foreach (var parent in new List<RectTransform>(containers.Keys))
                Release(parent);
            containers.Clear();
            pending.Clear();
            reconciling = false;
        }
    }
}
