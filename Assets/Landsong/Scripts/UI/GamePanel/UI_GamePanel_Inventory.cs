using System.Collections.Generic;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Inventory : UI_GamePanel_List
    {
        protected override bool UsesConfiguredPresenter => false;

        [Sirenix.OdinInspector.LabelText("网格模板")]
        public UI_GamePanel_InventoryGridRow GridTemplate;
        [Sirenix.OdinInspector.LabelText("数量模板")]
        public UI_GamePanel_QuantityRow QuantityTemplate;
        internal IGameBuildingUi Buildings;
        internal IGameUiFeedback Feedback;
        ulong inventoryProvider;
        int inventoryIndex = -1, inventoryPending = -1;
        InputField inventoryAmountInput;
        string inventoryAmount = "1";
        public bool IsDragging => inventoryDragging;
        public bool IsEditing => inventoryAmountInput != null && inventoryAmountInput.isFocused;

        bool inventoryDragging;
        bool inventoryChoosingTarget;
        InventorySelection inventoryDrag;
        [Sirenix.OdinInspector.LabelText("拖拽根对象")]
        public RectTransform DragRoot;
        [Sirenix.OdinInspector.LabelText("拖拽文字")]
        public Text DragLabel;
        [Sirenix.OdinInspector.LabelText("拖拽坐标空间")]
        public RectTransform DragSpace;
        GameObject inventoryDragLabel;
        string inventoryDisplayedFingerprint;
        readonly Dictionary<UI_GamePanel_Row, Dictionary<string, UI_GamePanel_InventorySlot>> gridSlots = new Dictionary<UI_GamePanel_Row, Dictionary<string, UI_GamePanel_InventorySlot>>();
        internal void ResetSession()
        {
            EndInventoryDrag();
            gridSlots.Clear();
            inventoryProvider = 0;
            inventoryIndex = inventoryPending = -1;
            inventoryAmountInput = null;
            inventoryAmount = "1";
            inventoryChoosingTarget = false;
            inventoryDisplayedFingerprint = null;
        }

        public override void Render()
        {
            var day = em.GetComponentData<Session>(root).Phase == Phase.Day;
            var currentFingerprint = InventoryOps.Fingerprint(em, root);
            if (inventoryDisplayedFingerprint != null && currentFingerprint != inventoryDisplayedFingerprint)
            {
                inventoryIndex = -1;
                inventoryPending = -1;
                inventoryChoosingTarget = false;
            }

            inventoryDisplayedFingerprint = currentFingerprint;
            Row("经济总览 · 最近账本 / 白天预测", () => Navigation.OpenEconomy());
            Row("库存 · 点击查看，拖动整堆可移动、合并或交换。拆分请先选择物品和数量，再启用选择目标格。夜晚只读。");
            if (inventoryChoosingTarget)
                Row("正在选择目标格 · 点击空格或同类物品格，按填写数量转移。");
            var stamp = inventoryDisplayedFingerprint;
            Row("整理库存（合并同类，优先低损耗槽）", day ? () => QueueInventory(CommandRequests.SortInventory(stamp)) : null);
            var slots = new List<InventorySlot>();
            foreach (var slot in em.GetBuffer<InventorySlot>(root))
                slots.Add(slot);
            slots.Sort((a, b) =>
            {
                var c = a.Provider.CompareTo(b.Provider);
                return c != 0 ? c : a.Index.CompareTo(b.Index);
            });
            var storageKeys = new List<string>();
            foreach (var slot in slots) storageKeys.Add("slot:" + slot.Provider + ":" + slot.Index);
            var views = Grid("storage", storageKeys);
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                var v = views[i];
                if (v == null || !v.CanRebind) continue;
                v.Provider = s.Provider;
                v.Index = s.Index;
                v.Item = s.Item;
                v.Count = s.Count;
                v.Pending = false;
                v.Locked = !day || s.Unavailable != 0;
                v.Fingerprint = stamp;
                BindSlot(v, $"{Session.EntityName(s.Provider)} / {s.Index + 1}\n{(s.Count > 0 ? Session.Name(s.Item) + " × " + s.Count : "空格")}\n{Session.Name(s.SlotType)}" + (s.Unavailable != 0 ? " · 已损失" : ""), s.Provider == inventoryProvider && s.Index == inventoryIndex && inventoryPending < 0, s.Provider == Session.Selected);
            }

            Row("待存放池 · 本回合进入夜晚前清空；不用于普通生产、食谱或任务提交。");
            var pending = em.GetBuffer<PendingItem>(root);
            var pendingKeys = new List<string>();
            foreach (var pendingItem in pending) pendingKeys.Add("pending:" + pendingItem.Item);
            var pendingViews = Grid("pending", pendingKeys);
            for (var i = 0; i < pending.Length; i++)
            {
                var p = pending[i];
                var v = pendingViews[i];
                if (v == null || !v.CanRebind) continue;
                v.Provider = 0;
                v.Index = -1;
                v.Item = p.Item;
                v.Count = p.Amount;
                v.Pending = true;
                v.Locked = !day;
                v.Fingerprint = stamp;
                BindSlot(v, $"{Session.Name(p.Item)} × {p.Amount}\n待存放 · 余量 {p.LossRemainder:0.###}", inventoryPending == p.Item, false);
            }

            Row("尝试存入全部", day ? () => QueueInventory(CommandRequests.StorePending(stamp)) : null);
            Row("数量：用于选中物品的移动、拆分、存入或丢弃；拖动默认整堆。");
            QuantityRow();
            var index = InventoryOps.SlotIndex(em, root, inventoryProvider, inventoryIndex);
            var item = -1;
            var count = 0;
            var locked = false;
            if (inventoryPending >= 0)
            {
                item = inventoryPending;
                count = InventoryOps.PendingCount(em, root, item);
                Row("已选待存放：" + Session.Name(item) + " × " + count);
            }
            else if (index >= 0)
            {
                var s = em.GetBuffer<InventorySlot>(root)[index];
                item = s.Item;
                count = s.Count;
                locked = s.Unavailable != 0;
                Row($"已选 {Session.EntityName(s.Provider)} / 格 {s.Index + 1} · {Session.Name(s.SlotType)}", () => Buildings.FocusBuilding(s.Provider));
                Row("接纳规则：" + AcceptText(s.SlotType));
                if (count > 0)
                    Row($"本格损耗率 {InventoryOps.LossRate(em, root, s, item):P2} / 回合；累计余量 {s.LossRemainder:0.###}");
            }

            if (item >= 0 && count > 0)
            {
                var d = Sim.Definition(em, root, item);
                Row($"{Session.Name(item)} · 数量 {count} · 单格上限 {d.Capacity}\n基础价值 {d.Value} · 基础损耗 {d.Loss:P2} / 回合");
                var source = Buildings.BuildingSource(item);
                if (!string.IsNullOrEmpty(source?.Description))
                    Row(source.Description);
                var room = 0L;
                foreach (var at in InventoryOps.StorageOrder(em, root, item))
                {
                    var s = em.GetBuffer<InventorySlot>(root)[at];
                    room += math.max(0, d.Capacity - s.Count);
                }

                Row("当前可接纳该物品的剩余空间：" + room);
                Row("移动/存入所选数量：选择目标格", day && !locked ? () =>
                {
                    if (SelectedAmount(count) <= 0)
                    {
                        Feedback.ShowMessage("请输入大于 0 的数量。");
                        return;
                    }

                    inventoryChoosingTarget = true;
                    Session.NextRefresh = 0;
                } : null);
                var discardSource = new InventorySelection(inventoryPending >= 0, inventoryProvider, inventoryIndex, item, count, stamp);
                Row("丢弃所选数量…", day && !locked ? () =>
                {
                    var discard = CommandRequests.DiscardInventory(discardSource, SelectedAmount(count));
                    if (discard.Amount <= 0)
                    {
                        Feedback.ShowMessage("请输入大于 0 的数量。");
                        return;
                    }

                    Buildings.ShowBuildingConfirmation("确认丢弃", new List<string> { $"{Session.Name(discard.Definition)} × {discard.Amount} 将永久损失。", "库存变化后本次确认将失效，不会丢弃新出现的物资。" }, () => QueueInventory(discard));
                } : null);
            }

            Row("取消物品选择", () =>
            {
                inventoryIndex = -1;
                inventoryPending = -1;
                inventoryChoosingTarget = false;
                Session.NextRefresh = 0;
            });
        }

        string AcceptText(int type)
        {
            if (type < 0)
                return "所有物品";
            var names = new List<string>();
            var d = Sim.Definition(em, root, type);
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i);
                if (r.Kind == RuleKind.SlotAccept)
                    names.Add(Session.Name(r.Target));
            }

            return names.Count == 0 ? "所有物品（槽型只影响损耗）" : string.Join(" / ", names);
        }

        int SelectedAmount(int maximum) => int.TryParse(inventoryAmount, out var value) && value > 0 ? math.min(maximum, value) : 0;
        void QuantityRow()
        {
            var row = Rows.Item(QuantityTemplate, "", parent: PrimaryRows, key: "inventory:quantity");
            if (row == null || !row.CanRebind) return;
            inventoryAmountInput = row.Quantity;
            if (inventoryAmountInput == null)
                throw new System.InvalidOperationException("通用行模板缺少 InventoryQuantity 引用。");
            inventoryAmountInput.gameObject.SetActive(true);
            inventoryAmountInput.onValueChanged.RemoveAllListeners();
            inventoryAmountInput.onValueChanged.AddListener(value => inventoryAmount = value);
            inventoryAmountInput.SetTextWithoutNotify(inventoryAmount);
        }

        List<UI_GamePanel_InventorySlot> Grid(string group, IReadOnlyList<string> keys)
        {
            var count = keys.Count;
            var row = Rows.Item(GridTemplate, "", parent: PrimaryRows, key: "inventory-grid:" + group);
            var result = new List<UI_GamePanel_InventorySlot>(count);
            if (row == null) { for (var i = 0; i < count; i++) result.Add(null); return result; }
            if (!gridSlots.TryGetValue(row, out var slots))
                gridSlots.Add(row, slots = new Dictionary<string, UI_GamePanel_InventorySlot>());
            if (!row.CanRebind)
            {
                foreach (var key in keys) { slots.TryGetValue(key, out var existing); result.Add(existing); }
                return result;
            }
            var child = row.Grid;
            child.gameObject.SetActive(true);
            var grid = row.GridLayout;
            var width = math.max(140, PrimaryRows.rect.width - 8);
            var columns = math.max(1, (int)(width / 150));
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2((width - 6 * (columns - 1)) / columns, 104);
            grid.spacing = new Vector2(6, 6);
            var layout = row.Layout;
            layout.preferredHeight = layout.minHeight = math.max(1, (int)math.ceil((float)count / columns)) * 110;
            var seen = new HashSet<string>();
            for (var i = 0; i < count; i++)
            {
                var key = keys[i];
                if (!seen.Add(key)) throw new System.InvalidOperationException("库存显示出现重复槽位身份：" + key);
                if (!slots.TryGetValue(key, out var view))
                {
                    view = Instantiate(row.SlotTemplate, child);
                    view.name = "库存格 " + key;
                    var captured = view;
                    view.Select.onClick.AddListener(() => SelectInventory(captured));
                    slots.Add(key, view);
                }
                view.Owner = this;
                view.gameObject.SetActive(true); view.transform.SetSiblingIndex(i);
                result.Add(view);
            }
            foreach (var key in new List<string>(slots.Keys))
                if (!seen.Contains(key)) { if (slots[key] != null) Destroy(slots[key].gameObject); slots.Remove(key); }
            return result;
        }

        void BindSlot(UI_GamePanel_InventorySlot view, string label, bool active, bool building)
        {
            view.Label.text = label;
            view.Background.color = view.Locked ? new Color(.25f, .2f, .2f) : active ? new Color(.2f, .45f, .65f) : building ? new Color(.22f, .37f, .32f) : new Color(.22f, .24f, .28f);
            var source = view.Count > 0 ? Buildings.BuildingSource(view.Item) : null;
            view.Icon.sprite = source?.Icon;
            view.Icon.gameObject.SetActive(view.Icon.sprite != null);
        }

        void QueueInventory(Command command)
        {
            if (!Commands.TryQueue(command))
                return;
            inventoryIndex = -1;
            inventoryPending = -1;
            inventoryChoosingTarget = false;
            Session.NextRefresh = 0;
        }

        void SelectInventory(UI_GamePanel_InventorySlot slot)
        {
            if (inventoryChoosingTarget && !slot.Pending && !slot.Locked && (inventoryPending >= 0 || inventoryIndex >= 0 && (slot.Provider != inventoryProvider || slot.Index != inventoryIndex)))
            {
                var from = InventoryOps.SlotIndex(em, root, inventoryProvider, inventoryIndex);
                var item = inventoryPending >= 0 ? inventoryPending : from >= 0 ? em.GetBuffer<InventorySlot>(root)[from].Item : -1;
                var count = inventoryPending >= 0 ? InventoryOps.PendingCount(em, root, item) : from >= 0 ? em.GetBuffer<InventorySlot>(root)[from].Count : 0;
                if (count > 0)
                {
                    var selected = new InventorySelection(inventoryPending >= 0, inventoryProvider, inventoryIndex, item, SelectedAmount(count), slot.Fingerprint);
                    QueueInventory(CommandRequests.TransferInventory(selected, slot.Provider, slot.Index));
                    return;
                }
            }

            inventoryPending = slot.Pending ? slot.Item : -1;
            inventoryProvider = slot.Provider;
            inventoryIndex = slot.Pending ? -1 : slot.Index;
            inventoryChoosingTarget = false;
            Session.NextRefresh = 0;
        }

        public void BeginInventoryDrag(UI_GamePanel_InventorySlot slot)
        {
            inventoryDrag = new InventorySelection(slot.Pending, slot.Provider, slot.Index, slot.Item, slot.Count, slot.Fingerprint);
            inventoryDragging = true;
            if (DragRoot == null || DragLabel == null || DragSpace == null)
                throw new System.InvalidOperationException("库存面板拖动物检查器引用不完整。");
            inventoryDragLabel = DragRoot.gameObject;
            inventoryDragLabel.SetActive(true);
            DragLabel.text = Session.Name(slot.Item) + " × " + slot.Count;
        }

        public void UpdateInventoryDrag(Vector2 position)
        {
            if (!inventoryDragging || inventoryDragLabel == null)
                return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragSpace, position, null, out var point))
                DragRoot.anchoredPosition = point + new Vector2(80, 30);
        }

        public void DropInventory(UI_GamePanel_InventorySlot target)
        {
            if (!inventoryDragging)
                return;
            QueueInventory(CommandRequests.TransferInventory(inventoryDrag, target.Provider, target.Index));
        }

        void OnDisable()
        {
            if (Session != null)
                EndInventoryDrag();
        }

        public void EndInventoryDrag()
        {
            inventoryDragging = false;
            if (inventoryDragLabel != null)
                inventoryDragLabel.SetActive(false);
            if (Session != null)
                Session.NextRefresh = 0;
        }
    }
}
