using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        ulong inventoryProvider;
        int inventoryIndex = -1, inventoryPending = -1;
        InputField inventoryAmountInput;
        string inventoryAmount = "1";
        bool inventoryDragging;
        bool inventoryChoosingTarget;
        Command inventoryDrag;
        GameObject inventoryDragLabel;
        string inventoryDisplayedFingerprint;
        void InventoryRows()
        {
            var day = em.GetComponentData<Session>(root).Phase == Phase.Day;
            var currentFingerprint = InventoryOps.Fingerprint(em, root);
            if (inventoryDisplayedFingerprint != null && currentFingerprint != inventoryDisplayedFingerprint) { inventoryIndex = -1; inventoryPending = -1; inventoryChoosingTarget = false; }
            inventoryDisplayedFingerprint = currentFingerprint;
            Row("经济总览 · 最近账本 / 白天预测", () => OpenEconomy());
            Row("库存 · 点击查看，拖动整堆可移动、合并或交换。拆分请先选择物品和数量，再启用选择目标格。夜晚只读。");
            if (inventoryChoosingTarget) Row("正在选择目标格 · 点击空格或同类物品格，按填写数量转移。");
            var stamp = inventoryDisplayedFingerprint;
            Row("整理库存（合并同类，优先低损耗槽）", day ? () => QueueInventory(new Command { Kind = CommandKind.SortInventory, Text = stamp }) : null);
            var slots = new List<InventorySlot>(); foreach (var slot in em.GetBuffer<InventorySlot>(root)) slots.Add(slot);
            slots.Sort((a, b) => { var c = a.Provider.CompareTo(b.Provider); return c != 0 ? c : a.Index.CompareTo(b.Index); });
            var views = Grid(slots.Count);
            for (var i = 0; i < slots.Count; i++)
            {
                var s = slots[i]; var v = views[i];
                v.Provider = s.Provider; v.Index = s.Index; v.Item = s.Item; v.Count = s.Count; v.Pending = false; v.Locked = !day || s.Unavailable != 0; v.Fingerprint = stamp;
                BindSlot(v, $"{EntityName(s.Provider)} / {s.Index + 1}\n{(s.Count > 0 ? Name(s.Item) + " × " + s.Count : "空格")}\n{Name(s.SlotType)}" + (s.Unavailable != 0 ? " · 已损失" : ""), s.Provider == inventoryProvider && s.Index == inventoryIndex && inventoryPending < 0, s.Provider == selected);
            }
            Row("待存放池 · 本回合进入夜晚前清空；不用于普通生产、食谱或任务提交。");
            var pending = em.GetBuffer<PendingItem>(root); var pendingViews = Grid(pending.Length);
            for (var i = 0; i < pending.Length; i++)
            {
                var p = pending[i]; var v = pendingViews[i]; v.Provider = 0; v.Index = -1; v.Item = p.Item; v.Count = p.Amount; v.Pending = true; v.Locked = !day; v.Fingerprint = stamp;
                BindSlot(v, $"{Name(p.Item)} × {p.Amount}\n待存放 · 余量 {p.LossRemainder:0.###}", inventoryPending == p.Item, false);
            }
            Row("尝试存入全部", day ? () => QueueInventory(new Command { Kind = CommandKind.StorePending, Text = stamp }) : null);
            Row("数量：用于选中物品的移动、拆分、存入或丢弃；拖动默认整堆。"); QuantityRow();
            var index = InventoryOps.SlotIndex(em, root, inventoryProvider, inventoryIndex);
            var item = -1; var count = 0; var locked = false;
            if (inventoryPending >= 0) { item = inventoryPending; count = InventoryOps.PendingCount(em, root, item); Row("已选待存放：" + Name(item) + " × " + count); }
            else if (index >= 0)
            {
                var s = em.GetBuffer<InventorySlot>(root)[index]; item = s.Item; count = s.Count; locked = s.Unavailable != 0;
                Row($"已选 {EntityName(s.Provider)} / 格 {s.Index + 1} · {Name(s.SlotType)}", () => FocusBuilding(s.Provider));
                Row("接纳规则：" + AcceptText(s.SlotType));
                if (count > 0) Row($"本格损耗率 {InventoryOps.LossRate(em, root, s, item):P2} / 回合；累计余量 {s.LossRemainder:0.###}");
            }
            if (item >= 0 && count > 0)
            {
                var d = Sim.Definition(em, root, item); Row($"{Name(item)} · 数量 {count} · 单格上限 {d.Capacity}\n基础价值 {d.Value} · 基础损耗 {d.Loss:P2} / 回合");
                var source = BuildingSource(item); if (!string.IsNullOrEmpty(source?.Description)) Row(source.Description);
                var room = 0L; foreach (var at in InventoryOps.StorageOrder(em, root, item)) { var s = em.GetBuffer<InventorySlot>(root)[at]; room += math.max(0, d.Capacity - s.Count); }
                Row("当前可接纳该物品的剩余空间：" + room);
                Row("移动/存入所选数量：选择目标格", day && !locked ? () => { if (SelectedAmount(count) <= 0) { Message.text = "请输入大于 0 的数量。"; return; } inventoryChoosingTarget = true; nextRefresh = 0; } : null);
                var discardSource = new Command { Kind = inventoryPending >= 0 ? CommandKind.DiscardPending : CommandKind.DiscardSlot, Target = inventoryProvider, SourceSlot = inventoryIndex, Definition = item, Text = stamp };
                Row("丢弃所选数量…", day && !locked ? () =>
                {
                    var discard = discardSource; discard.Amount = SelectedAmount(count);
                    if (discard.Amount <= 0) { Message.text = "请输入大于 0 的数量。"; return; }
                    ShowBuildingConfirmation("确认丢弃", new List<string> { $"{Name(discard.Definition)} × {discard.Amount} 将永久损失。", "库存变化后本次确认将失效，不会丢弃新出现的物资。" }, () => QueueInventory(discard));
                } : null);
            }
            Row("取消物品选择", () => { inventoryIndex = -1; inventoryPending = -1; inventoryChoosingTarget = false; nextRefresh = 0; });
        }
        string AcceptText(int type)
        {
            if (type < 0) return "所有物品";
            var names = new List<string>(); var d = Sim.Definition(em, root, type);
            for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Kind == RuleKind.SlotAccept) names.Add(Name(r.Target)); }
            return names.Count == 0 ? "所有物品（槽型只影响损耗）" : string.Join(" / ", names);
        }
        int SelectedAmount(int maximum) => int.TryParse(inventoryAmount, out var value) && value > 0 ? math.min(maximum, value) : 0;
        void QuantityRow()
        {
            var row = Row(""); var child = row.transform.Find("InventoryQuantity");
            if (child == null)
            {
                var go = new GameObject("InventoryQuantity", typeof(RectTransform), typeof(Image), typeof(InputField)); go.transform.SetParent(row.transform, false);
                var rect = (RectTransform)go.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(8, 2); rect.offsetMax = new Vector2(-8, -2);
                var label = new GameObject("Text", typeof(RectTransform), typeof(Text)); label.transform.SetParent(go.transform, false); var text = label.GetComponent<Text>(); text.font = RowTemplate.GetComponentInChildren<Text>().font; text.fontSize = 18; text.color = Color.black; text.richText = false;
                var textRect = (RectTransform)label.transform; textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = new Vector2(8, 0); textRect.offsetMax = new Vector2(-8, 0);
                var input = go.GetComponent<InputField>(); input.textViewport = input.GetComponent<RectTransform>(); input.textComponent = text; input.contentType = InputField.ContentType.IntegerNumber; input.characterLimit = 9; input.onValueChanged.AddListener(value => inventoryAmount = value); child = go.transform;
            }
            child.gameObject.SetActive(true); inventoryAmountInput = child.GetComponent<InputField>(); inventoryAmountInput.SetTextWithoutNotify(inventoryAmount);
        }
        List<InventorySlotView> Grid(int count)
        {
            var row = Row(""); var child = row.transform.Find("InventoryGrid");
            if (child == null) { var go = new GameObject("InventoryGrid", typeof(RectTransform), typeof(GridLayoutGroup)); go.transform.SetParent(row.transform, false); child = go.transform; var rect = (RectTransform)child; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }
            child.gameObject.SetActive(true); var grid = child.GetComponent<GridLayoutGroup>(); var width = math.max(140, PrimaryRows.rect.width - 8); var columns = math.max(1, (int)(width / 150));
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = columns; grid.cellSize = new Vector2((width - 6 * (columns - 1)) / columns, 104); grid.spacing = new Vector2(6, 6);
            var layout = row.GetComponent<LayoutElement>(); layout.preferredHeight = layout.minHeight = math.max(1, (int)math.ceil((float)count / columns)) * 110;
            var result = new List<InventorySlotView>();
            for (var i = 0; i < math.max(count, child.childCount); i++)
            {
                InventorySlotView view;
                if (i < child.childCount) view = child.GetChild(i).GetComponent<InventorySlotView>();
                else
                {
                    var go = new GameObject("Slot", typeof(RectTransform), typeof(Image), typeof(Button), typeof(InventorySlotView)); go.transform.SetParent(child, false); view = go.GetComponent<InventorySlotView>();
                    var label = new GameObject("Label", typeof(RectTransform), typeof(Text)); label.transform.SetParent(go.transform, false); view.Label = label.GetComponent<Text>(); view.Label.font = RowTemplate.GetComponentInChildren<Text>().font; view.Label.fontSize = 13; view.Label.alignment = TMPro.TextAlignmentOptions.Center; view.Label.color = Color.white; view.Label.richText = false; view.Label.raycastTarget = false;
                    var rect = (RectTransform)label.transform; rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(4, 3); rect.offsetMax = new Vector2(-4, -3);
                    var image = new GameObject("Icon", typeof(RectTransform), typeof(Image)); image.transform.SetParent(go.transform, false); view.Icon = image.GetComponent<Image>(); view.Icon.raycastTarget = false; view.Icon.preserveAspect = true; var ir = (RectTransform)image.transform; ir.anchorMin = ir.anchorMax = new Vector2(1, 1); ir.pivot = Vector2.one; ir.anchoredPosition = new Vector2(-2, -2); ir.sizeDelta = new Vector2(20, 20);
                    var captured = view; go.GetComponent<Button>().onClick.AddListener(() => SelectInventory(captured));
                }
                view.gameObject.SetActive(i < count); if (i < count) { view.Owner = this; result.Add(view); }
            }
            return result;
        }
        void BindSlot(InventorySlotView view, string label, bool active, bool building)
        {
            view.Label.text = label; view.GetComponent<Image>().color = view.Locked ? new Color(.25f, .2f, .2f) : active ? new Color(.2f, .45f, .65f) : building ? new Color(.22f, .37f, .32f) : new Color(.22f, .24f, .28f);
            var source = view.Count > 0 ? BuildingSource(view.Item) : null; view.Icon.sprite = source?.Icon; view.Icon.gameObject.SetActive(view.Icon.sprite != null);
        }
        void QueueInventory(Command command)
        {
            if (!EcsSceneFlow.GameReady || root == Entity.Null || !em.Exists(root)) return;
            if (PauseMenu != null && PauseMenu.IsOpen || intel || BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) return;
            command.RequestId = ++request; em.GetBuffer<Command>(root).Add(command); inventoryIndex = -1; inventoryPending = -1; inventoryChoosingTarget = false; nextRefresh = 0;
        }
        void SelectInventory(InventorySlotView slot)
        {
            if (inventoryChoosingTarget && !slot.Pending && !slot.Locked && (inventoryPending >= 0 || inventoryIndex >= 0 && (slot.Provider != inventoryProvider || slot.Index != inventoryIndex)))
            {
                var from = InventoryOps.SlotIndex(em, root, inventoryProvider, inventoryIndex);
                var item = inventoryPending >= 0 ? inventoryPending : from >= 0 ? em.GetBuffer<InventorySlot>(root)[from].Item : -1;
                var count = inventoryPending >= 0 ? InventoryOps.PendingCount(em, root, item) : from >= 0 ? em.GetBuffer<InventorySlot>(root)[from].Count : 0;
                if (count > 0) { QueueInventory(new Command { Kind = inventoryPending >= 0 ? CommandKind.StorePendingSlot : CommandKind.MoveInventory, Target = inventoryProvider, SourceSlot = inventoryIndex, Other = slot.Provider, DestinationSlot = slot.Index, Definition = item, Amount = SelectedAmount(count), Text = slot.Fingerprint }); return; }
            }
            inventoryPending = slot.Pending ? slot.Item : -1; inventoryProvider = slot.Provider; inventoryIndex = slot.Pending ? -1 : slot.Index; inventoryChoosingTarget = false; nextRefresh = 0;
        }
        public void BeginInventoryDrag(InventorySlotView slot)
        {
            inventoryDrag = new Command { Kind = slot.Pending ? CommandKind.StorePendingSlot : CommandKind.MoveInventory, Target = slot.Provider, SourceSlot = slot.Index, Definition = slot.Item, Amount = slot.Count, Text = slot.Fingerprint };
            inventoryDragging = true;
            if (inventoryDragLabel == null) { inventoryDragLabel = new GameObject("Inventory drag", typeof(RectTransform), typeof(Text)); inventoryDragLabel.transform.SetParent(GetComponentInParent<Canvas>().transform, false); var t = inventoryDragLabel.GetComponent<Text>(); t.font = RowTemplate.GetComponentInChildren<Text>().font; t.fontSize = 18; t.color = Color.yellow; t.raycastTarget = false; ((RectTransform)t.transform).sizeDelta = new Vector2(220, 44); }
            inventoryDragLabel.SetActive(true); inventoryDragLabel.GetComponent<Text>().text = Name(slot.Item) + " × " + slot.Count;
        }
        public void UpdateInventoryDrag(Vector2 position)
        {
            if (!inventoryDragging || inventoryDragLabel == null) return; var canvas = inventoryDragLabel.GetComponentInParent<Canvas>();
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)canvas.transform, position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out var point)) ((RectTransform)inventoryDragLabel.transform).anchoredPosition = point + new Vector2(80, 30);
        }
        public void DropInventory(InventorySlotView target)
        { if (!inventoryDragging) return; var c = inventoryDrag; c.Other = target.Provider; c.DestinationSlot = target.Index; QueueInventory(c); }
        public void EndInventoryDrag()
        { inventoryDragging = false; if (inventoryDragLabel != null) inventoryDragLabel.SetActive(false); nextRefresh = 0; }
    }
}
