using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sirenix.OdinInspector;
using TMPro;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Inventory : UI_GamePanel_View
    {
        [LabelText("按建筑显示按钮")] public Button BuildingsButton;
        [LabelText("按资源显示按钮")] public Button ResourcesButton;
        [LabelText("建筑滚动视图")] public ScrollRect BuildingsScroll;
        [LabelText("资源滚动视图")] public ScrollRect ResourcesScroll;
        [LabelText("建筑条目模板")] public UI_GamePanel_InventoryBuilding BuildingTemplate;
        [LabelText("资源条目模板")] public UI_GamePanel_InventoryResource ResourceTemplate;
        [LabelText("待存区")] public RectTransform PendingRoot;
        [LabelText("待存条目容器")] public RectTransform PendingContent;
        [LabelText("待存条目模板")] public UI_GamePanel_InventorySlot PendingTemplate;
        [LabelText("待存区提示")] public TMP_Text PendingSummary;
        [LabelText("一键入库按钮")] public Button StoreAllButton;
        [LabelText("面板交互状态")] public UI_GamePanel_InteractionLock Interaction;
        [LabelText("选中物品详情")] public TMP_Text SelectionDetails;
        [LabelText("数量输入")] public TMP_InputField Quantity;
        [LabelText("选择目标格按钮")] public Button MoveButton;
        [LabelText("移到待存区按钮")] public Button PendingButton;
        [LabelText("丢弃按钮")] public Button DiscardButton;
        [LabelText("取消选择按钮")] public Button CancelButton;
        [LabelText("整理库存按钮")] public Button SortButton;
        [LabelText("账单按钮")] public Button EconomyButton;
        [LabelText("资源详情子面板")] [UnityEngine.Serialization.FormerlySerializedAs("LedgerRoot")] public GameObject ResourceDetailsRoot;
        [LabelText("资源详情标题")] [UnityEngine.Serialization.FormerlySerializedAs("LedgerTitle")] public TMP_Text ResourceDetailsTitle;
        [LabelText("预计产出正文")] [UnityEngine.Serialization.FormerlySerializedAs("LedgerBody")] public TMP_Text IncomeBody;
        [LabelText("预计产出滚动视图")] [UnityEngine.Serialization.FormerlySerializedAs("LedgerScroll")] public ScrollRect IncomeScroll;
        [LabelText("资源详情关闭按钮")] [UnityEngine.Serialization.FormerlySerializedAs("LedgerClose")] public Button ResourceDetailsClose;
        [LabelText("预计消耗正文")] public TMP_Text ExpenseBody;
        [LabelText("预计消耗滚动视图")] public ScrollRect ExpenseScroll;
        [LabelText("预测状态")] public TMP_Text ForecastStatus;
        [LabelText("拖拽根对象")] public RectTransform DragRoot;
        [LabelText("拖拽文字")] public TMP_Text DragLabel;
        [LabelText("拖拽坐标空间")] public RectTransform DragSpace;

        internal IGameBuildingUi Buildings;
        internal IGameUiFeedback Feedback;
        readonly Dictionary<ulong, UI_GamePanel_InventoryBuilding> buildingViews = new Dictionary<ulong, UI_GamePanel_InventoryBuilding>();
        readonly Dictionary<(ulong provider, int index), UI_GamePanel_InventorySlot> slotViews = new Dictionary<(ulong, int), UI_GamePanel_InventorySlot>();
        readonly Dictionary<int, UI_GamePanel_InventorySlot> pendingViews = new Dictionary<int, UI_GamePanel_InventorySlot>();
        readonly Dictionary<int, UI_GamePanel_InventoryResource> resourceViews = new Dictionary<int, UI_GamePanel_InventoryResource>();
        readonly HashSet<ulong> pinned = new HashSet<ulong>();
        InventorySelection? selection;
        InventorySelection drag;
        string displayedFingerprint;
        bool showingBuildings, choosingTarget, dragging, restoreScrollPosition, buildingsRendered, resourcesRendered;
        Vector2 buildingScrollPosition = new Vector2(0, 1), resourceScrollPosition = new Vector2(0, 1);
        int detailsItem = -1;
        float forecastCheckAt;
        string forecastFingerprint, requestedForecast;
        bool forecastReady;
        readonly SortedDictionary<int, ResourceForecastReadModel.Resource> forecast = new SortedDictionary<int, ResourceForecastReadModel.Resource>();
        public bool IsDragging => dragging;
        public bool IsEditing => Quantity != null && Quantity.isFocused || Interaction != null && Interaction.IsPinned;
        public bool ResourceDetailsOpen => ResourceDetailsRoot != null && ResourceDetailsRoot.activeInHierarchy;
        public bool ShowingBuildings => showingBuildings;
        public IEnumerable<UI_GamePanel_InventorySlot> StorageSlots => slotViews.Values;
        public IEnumerable<UI_GamePanel_InventorySlot> PendingSlots => pendingViews.Values;
        public IEnumerable<UI_GamePanel_InventoryBuilding> BuildingViews => buildingViews.Values;
        public IEnumerable<UI_GamePanel_InventoryResource> ResourceViews => resourceViews.Values;
        bool Day => Session != null && Session.IsBound && em.GetComponentData<Session>(root).Phase == Phase.Day;
        bool CanOperate => Day && Navigation.InputPolicy.Capture().CanQueue(CommandKind.MoveInventory);

        public override void ValidateConfiguration()
        {
            base.ValidateConfiguration();
            if (BuildingsButton == null || ResourcesButton == null || BuildingsScroll == null || ResourcesScroll == null
                || BuildingsScroll == ResourcesScroll || BuildingsScroll.content == null || ResourcesScroll.content == null
                || BuildingTemplate == null || ResourceTemplate == null || PendingRoot == null || PendingContent == null
                || PendingTemplate == null || PendingSummary == null || StoreAllButton == null || Interaction == null
                || SelectionDetails == null || Quantity == null || MoveButton == null || PendingButton == null || DiscardButton == null
                || CancelButton == null || SortButton == null || EconomyButton == null || ResourceDetailsRoot == null || ResourceDetailsTitle == null
                || IncomeBody == null || IncomeScroll == null || ExpenseBody == null || ExpenseScroll == null || ForecastStatus == null || ResourceDetailsClose == null || DragRoot == null || DragLabel == null || DragSpace == null)
                throw new InvalidOperationException("库存面板的模式、模板、操作区或资源详情引用不完整。");
            if (BuildingTemplate.transform.parent != BuildingsScroll.content || ResourceTemplate.transform.parent != ResourcesScroll.content
                || PendingTemplate.transform.parent != PendingContent)
                throw new InvalidOperationException("库存模板必须配置在各自的内容容器中。");
            BuildingTemplate.ValidateConfiguration(); ResourceTemplate.ValidateConfiguration(); PendingTemplate.ValidateConfiguration();
            Interaction.ValidateConfiguration();
        }

        public override void Bind(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation)
        {
            base.Bind(session, commands, navigation);
            ResetSession();
            Click(BuildingsButton, () => SetMode(true)); Click(ResourcesButton, () => SetMode(false));
            Click(StoreAllButton, () => { if (CanOperate) QueueInventory(CommandRequests.StorePending(displayedFingerprint)); });
            Click(SortButton, () => { if (CanOperate) QueueInventory(CommandRequests.SortInventory(displayedFingerprint)); });
            Click(MoveButton, ChooseTarget); Click(PendingButton, MoveSelectedToPending); Click(DiscardButton, DiscardSelected);
            Click(CancelButton, ClearSelection); Click(EconomyButton, () => Navigation.OpenEconomy()); Click(ResourceDetailsClose, CloseResourceDetails);
            Quantity.onValueChanged.RemoveAllListeners(); Quantity.onValueChanged.AddListener(_ => RefreshSelection());
            BuildingTemplate.gameObject.SetActive(false); ResourceTemplate.gameObject.SetActive(false); PendingTemplate.gameObject.SetActive(false);
            SetMode(false);
        }

        static void Click(Button button, UnityEngine.Events.UnityAction action)
        { button.onClick.RemoveAllListeners(); button.onClick.AddListener(action); }

        public void SetMode(bool buildings)
        {
            if (ResourceDetailsOpen || Session != null && !Navigation.InputPolicy.Capture().CanNavigate) return;
            if (showingBuildings && buildingsRendered) buildingScrollPosition = BuildingsScroll.normalizedPosition;
            if (!showingBuildings && resourcesRendered) resourceScrollPosition = ResourcesScroll.normalizedPosition;
            EndInventoryDrag(); choosingTarget = false; showingBuildings = buildings; restoreScrollPosition = true;
            BuildingsScroll.gameObject.SetActive(buildings); ResourcesScroll.gameObject.SetActive(!buildings);
            BuildingsButton.interactable = !buildings; ResourcesButton.interactable = buildings;
            if (Session != null) Session.NextRefresh = 0;
        }

        public override void Render()
        {
            if (dragging || IsEditing) return;
            var fingerprint = InventoryOps.Fingerprint(em, root);
            if (displayedFingerprint != fingerprint) ClearSelection();
            displayedFingerprint = fingerprint;
            if (!showingBuildings || ResourceDetailsOpen) RefreshForecast();
            if (showingBuildings) RenderBuildings(); else RenderResources();
            RenderPending(); RefreshSelection();
            if (restoreScrollPosition)
            {
                Canvas.ForceUpdateCanvases();
                var scroll = showingBuildings ? BuildingsScroll : ResourcesScroll;
                scroll.normalizedPosition = showingBuildings ? buildingScrollPosition : resourceScrollPosition;
                restoreScrollPosition = false;
            }
            if (showingBuildings) buildingsRendered = true; else resourcesRendered = true;
            SortButton.interactable = CanOperate;
            if (ResourceDetailsOpen) RenderResourceDetails();
        }

        void RenderBuildings()
        {
            var groups = new SortedDictionary<ulong, List<InventorySlot>>();
            foreach (var slot in em.GetBuffer<InventorySlot>(root))
            {
                if (!groups.TryGetValue(slot.Provider, out var list)) groups.Add(slot.Provider, list = new List<InventorySlot>());
                list.Add(slot);
            }
            pinned.RemoveWhere(id => !groups.ContainsKey(id));
            var providers = groups.Keys.OrderByDescending(id => pinned.Contains(id)).ThenBy(id => id).ToArray();
            var seen = new HashSet<(ulong, int)>();
            for (int n = 0; n < providers.Length; n++)
            {
                var id = providers[n];
                if (!buildingViews.TryGetValue(id, out var view))
                {
                    view = Instantiate(BuildingTemplate, BuildingsScroll.content); view.Provider = id;
                    view.name = "库存建筑 " + id; view.Interaction.Parent = Interaction; view.SlotTemplate.gameObject.SetActive(false);
                    Click(view.Locate, () => { if (Navigation.InputPolicy.Capture().CanNavigate) Buildings.FocusBuilding(id); });
                    view.Pin.onValueChanged.RemoveAllListeners();
                    view.Pin.onValueChanged.AddListener(value => { if (value) pinned.Add(id); else pinned.Remove(id); Session.NextRefresh = 0; });
                    buildingViews.Add(id, view);
                }
                view.gameObject.SetActive(true); view.transform.SetSiblingIndex(n + 1);
                var slots = groups[id]; slots.Sort((a, b) => a.Index.CompareTo(b.Index));
                int columns = math.max(1, (int)((BuildingsScroll.viewport.rect.width - 36 + 6) / 62));
                view.GridLayout.constraintCount = columns;
                view.Layout.preferredHeight = view.Layout.minHeight = 56 + math.ceil((float)slots.Count / columns) * 62;
                int occupied = slots.Count(s => s.Count > 0 && s.Unavailable == 0);
                view.Information.text = $"{Session.EntityName(id)}\n已用 {occupied} / {slots.Count} 格";
                view.Pin.SetIsOnWithoutNotify(pinned.Contains(id));
                view.Locate.interactable = Sim.Find(em, id) != Entity.Null;
                for (int i = 0; i < slots.Count; i++)
                {
                    var slot = slots[i]; var key = (id, slot.Index); seen.Add(key);
                    if (!slotViews.TryGetValue(key, out var child))
                    {
                        child = Instantiate(view.SlotTemplate, view.Slots); child.name = "库存格 " + id + ":" + slot.Index;
                        child.InteractionOwner.Parent = view.Interaction; BindClick(child); slotViews.Add(key, child);
                    }
                    child.Provider = id; child.Index = slot.Index; child.Item = slot.Item; child.Count = slot.Count;
                    child.Pending = false; child.Locked = !Day || slot.Unavailable != 0; child.Fingerprint = displayedFingerprint;
                    child.gameObject.SetActive(true); child.transform.SetSiblingIndex(i + 1); BindSlot(child);
                }
            }
            RemoveMissing(slotViews, seen);
            RemoveMissing(buildingViews, new HashSet<ulong>(groups.Keys));
        }

        void RenderResources()
        {
            var resources = InventoryReadModel.Resources(em, root); int index = 1;
            if (forecastReady) foreach (var item in forecast.Keys) if (!resources.ContainsKey(item)) resources.Add(item, default);
            foreach (var pair in resources)
            {
                int item = pair.Key;
                if (!resourceViews.TryGetValue(item, out var view))
                {
                    view = Instantiate(ResourceTemplate, ResourcesScroll.content); view.Item = item;
                    view.name = "库存资源 " + item; view.Interaction.Parent = Interaction;
                    Click(view.Details, () => OpenResourceDetails(item)); resourceViews.Add(item, view);
                }
                view.gameObject.SetActive(true); view.transform.SetSiblingIndex(index++);
                forecast.TryGetValue(item, out var predicted);
                var delta = forecastReady ? UI_GamePanel_Economy.Signed((predicted?.Income ?? 0) - (predicted?.Expense ?? 0)) : ForecastUnavailable;
                view.Information.text = $"{Session.Name(item)}  {pair.Value.Stored}（{delta}）";
                SetIcon(view.Icon, item);
            }
            RemoveMissing(resourceViews, new HashSet<int>(resources.Keys));
        }

        void RenderPending()
        {
            var pending = em.GetBuffer<PendingItem>(root); var seen = new HashSet<int>(); long total = 0; int index = 1;
            foreach (var item in pending)
            {
                if (item.Amount <= 0) continue;
                seen.Add(item.Item); total += item.Amount;
                if (!pendingViews.TryGetValue(item.Item, out var view))
                {
                    view = Instantiate(PendingTemplate, PendingContent); view.name = "待存物资 " + item.Item;
                    view.InteractionOwner.Parent = Interaction; BindClick(view); pendingViews.Add(item.Item, view);
                }
                view.Provider = 0; view.Index = -1; view.Item = item.Item; view.Count = item.Amount;
                view.Pending = true; view.Locked = !Day; view.Fingerprint = displayedFingerprint;
                view.gameObject.SetActive(true); view.transform.SetSiblingIndex(index++); BindSlot(view);
            }
            RemoveMissing(pendingViews, seen);
            PendingSummary.text = $"待存区 · {seen.Count} 种 / {total}\n可拖入物资；入夜前清空。";
            StoreAllButton.interactable = CanOperate && total > 0;
        }

        void BindClick(UI_GamePanel_InventorySlot slot)
        { slot.Owner = this; Click(slot.Select, () => SelectInventory(slot)); }

        void BindSlot(UI_GamePanel_InventorySlot slot)
        {
            bool selected = selection.HasValue && (slot.Pending ? selection.Value.Pending && selection.Value.Item == slot.Item
                : !selection.Value.Pending && selection.Value.Provider == slot.Provider && selection.Value.Slot == slot.Index);
            slot.Label.text = slot.Pending ? $"{Session.Name(slot.Item)} × {slot.Count}" : slot.Locked && Day ? "不可用" : slot.Count > 0 ? slot.Count.ToString() : "空";
            slot.Background.color = selected ? new Color(.25f, .5f, .65f) : slot.Locked ? new Color(.35f, .3f, .3f) : Color.white;
            SetIcon(slot.Icon, slot.Count > 0 ? slot.Item : -1);
        }

        void SetIcon(Image image, int item)
        { image.sprite = item >= 0 ? Buildings.BuildingSource(item)?.Icon : null; image.gameObject.SetActive(image.sprite != null); }

        static void RemoveMissing<TKey, TView>(Dictionary<TKey, TView> views, HashSet<TKey> seen) where TView : Component
        {
            foreach (var key in views.Keys.Where(key => !seen.Contains(key)).ToArray())
            { var view = views[key]; if (view != null) { view.gameObject.SetActive(false); Destroy(view.gameObject); } views.Remove(key); }
        }

        void SelectInventory(UI_GamePanel_InventorySlot slot)
        {
            if (ResourceDetailsOpen || !Navigation.InputPolicy.Capture().CanNavigate) return;
            if (choosingTarget && selection.HasValue && !slot.Pending && !slot.Locked)
            {
                if (!CurrentSelection(out var source)) return;
                QueueInventory(CommandRequests.TransferInventory(WithAmount(source, Amount(source.Quantity)), slot.Provider, slot.Index)); return;
            }
            selection = new InventorySelection(slot.Pending, slot.Provider, slot.Index, slot.Item, slot.Count, slot.Fingerprint);
            choosingTarget = false; RefreshSelection(); Session.NextRefresh = 0;
        }

        int Amount(int maximum) => int.TryParse(Quantity.text, out var value) && value > 0 ? math.min(maximum, value) : 0;
        static InventorySelection WithAmount(InventorySelection source, int amount) => new InventorySelection(source.Pending, source.Provider, source.Slot, source.Item, amount, source.ExpectedInventory);

        bool CurrentSelection(out InventorySelection source)
        {
            source = selection.GetValueOrDefault();
            if (!selection.HasValue || source.Quantity <= 0 || source.Item < 0) return false;
            if (source.ExpectedInventory != InventoryOps.Fingerprint(em, root))
            { ClearSelection(); Feedback.ShowMessage("库存已变化，请重新选择物资。"); return false; }
            if (!source.Pending)
            { int at = InventoryOps.SlotIndex(em, root, source.Provider, source.Slot); if (at < 0 || em.GetBuffer<InventorySlot>(root)[at].Unavailable != 0) return false; }
            return true;
        }

        void RefreshSelection()
        {
            bool valid = selection.HasValue && selection.Value.Item >= 0 && selection.Value.Quantity > 0;
            bool available = valid;
            if (valid && !selection.Value.Pending)
            {
                int index = InventoryOps.SlotIndex(em, root, selection.Value.Provider, selection.Value.Slot);
                available = index >= 0 && em.GetBuffer<InventorySlot>(root)[index].Unavailable == 0;
            }
            bool editable = available && CanOperate && Amount(selection.Value.Quantity) > 0;
            MoveButton.interactable = editable; PendingButton.interactable = editable && !selection.Value.Pending;
            DiscardButton.interactable = editable; CancelButton.interactable = selection.HasValue;
            Quantity.interactable = valid && CanOperate;
            if (!selection.HasValue) { SelectionDetails.text = "点击物资查看详情。建筑模式可跨建筑拖拽，或与待存区互相转移；夜晚只读。"; return; }
            var source = selection.Value;
            string location = source.Pending ? "待存区" : Session.EntityName(source.Provider) + " / 格 " + (source.Slot + 1);
            string details = location;
            if (!source.Pending)
            {
                int at = InventoryOps.SlotIndex(em, root, source.Provider, source.Slot);
                if (at >= 0)
                {
                    var slot = em.GetBuffer<InventorySlot>(root)[at];
                    details += " · " + Session.Name(slot.SlotType) + "\n接纳：" + AcceptText(slot.SlotType);
                    if (slot.Unavailable != 0) details += " · 不可用";
                    if (valid) details += $" · 损耗 {InventoryOps.LossRate(em, root, slot, source.Item):P2}/回合 · 累计余量 {slot.LossRemainder:0.###}";
                }
            }
            if (valid)
            {
                var definition = Sim.Definition(em, root, source.Item);
                details += $"\n{Session.Name(source.Item)} × {source.Quantity} · 单格上限 {definition.Capacity} · 基础价值 {definition.Value}";
            }
            SelectionDetails.text = (choosingTarget ? "请选择目标建筑的空格或同类物品格。\n" : "") + details;
        }

        string AcceptText(int type)
        {
            if (type < 0) return "所有物品";
            var names = new List<string>(); var definition = Sim.Definition(em, root, type);
            for (int i = 0; i < definition.RuleCount; i++)
            { var rule = Sim.GetRule(em, root, definition.RuleStart + i); if (rule.Kind == RuleKind.SlotAccept) names.Add(Session.Name(rule.Target)); }
            return names.Count == 0 ? "所有物品" : string.Join(" / ", names);
        }

        void ChooseTarget()
        {
            if (!CanOperate || !CurrentSelection(out var source) || Amount(source.Quantity) <= 0) return;
            SetMode(true); choosingTarget = true; RefreshSelection(); Session.NextRefresh = 0;
        }

        void MoveSelectedToPending()
        {
            if (!CanOperate || !CurrentSelection(out var source) || source.Pending) return;
            QueueInventory(CommandRequests.MoveInventoryToPending(WithAmount(source, Amount(source.Quantity))));
        }

        void DiscardSelected()
        {
            if (!CanOperate || !CurrentSelection(out var source)) return;
            var command = CommandRequests.DiscardInventory(source, Amount(source.Quantity));
            if (command.Amount <= 0) return;
            Buildings.ShowBuildingConfirmation("确认丢弃", new List<string> { $"{Session.Name(source.Item)} × {command.Amount} 将永久损失。", "库存变化后本次确认将失效。" }, () => QueueInventory(command));
        }

        void ClearSelection()
        { selection = null; choosingTarget = false; if (Session != null) Session.NextRefresh = 0; }

        void QueueInventory(Command command)
        { if (Commands.TryQueue(command)) { ClearSelection(); EndInventoryDrag(); } }

        public void BeginInventoryDrag(UI_GamePanel_InventorySlot slot)
        {
            if (!CanOperate || slot.Owner != this || slot.Locked || slot.Count <= 0 || !showingBuildings) return;
            drag = new InventorySelection(slot.Pending, slot.Provider, slot.Index, slot.Item, slot.Count, slot.Fingerprint);
            dragging = true; choosingTarget = false; DragRoot.gameObject.SetActive(true); DragRoot.SetAsLastSibling();
            DragLabel.text = Session.Name(slot.Item) + " × " + slot.Count;
        }

        public void UpdateInventoryDrag(Vector2 position, Camera camera = null)
        {
            if (!dragging) return;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(DragSpace, position, camera, out var point)) DragRoot.anchoredPosition = point + new Vector2(70, 25);
        }

        public void DropInventory(UI_GamePanel_InventorySlot target)
        {
            if (!dragging || !CanOperate || target.Owner != this || target.Locked) return;
            if (target.Pending) { DropInventoryToPending(); return; }
            var source = drag; dragging = false;
            QueueInventory(CommandRequests.TransferInventory(source, target.Provider, target.Index)); EndInventoryDrag();
        }

        public void DropInventoryToPending()
        {
            if (!dragging || !CanOperate || drag.Pending) return;
            var source = drag; dragging = false;
            QueueInventory(CommandRequests.MoveInventoryToPending(source)); EndInventoryDrag();
        }

        public void EndInventoryDrag()
        { dragging = false; if (DragRoot != null) DragRoot.gameObject.SetActive(false); if (Session != null) Session.NextRefresh = 0; }

        public void OpenResourceDetails(int item)
        {
            if (!Navigation.InputPolicy.Capture().CanOpenModal(GameUiInputOwner.InventoryDetails)) return;
            EndInventoryDrag(); detailsItem = item; ResourceDetailsRoot.SetActive(true); ResourceDetailsRoot.transform.SetAsLastSibling();
            forecastCheckAt = 0; RefreshForecast(); RenderResourceDetails();
            Canvas.ForceUpdateCanvases(); IncomeScroll.verticalNormalizedPosition = ExpenseScroll.verticalNormalizedPosition = 1;
        }

        public void CloseResourceDetails()
        { if (ResourceDetailsRoot != null) ResourceDetailsRoot.SetActive(false); detailsItem = -1; if (Session != null) Session.NextRefresh = 0; }

        string ForecastUnavailable => !Day || em.GetComponentData<Session>(root).LastSettledTurn == em.GetComponentData<Session>(root).Turn ? "已结算" : "计算中…";

        void RefreshForecast()
        {
            var state = em.GetComponentData<Session>(root);
            if (state.Phase != Phase.Day || state.LastSettledTurn == state.Turn)
            { forecastReady = false; forecast.Clear(); return; }
            if (Time.unscaledTime >= forecastCheckAt || forecastFingerprint == null)
            {
                forecastCheckAt = Time.unscaledTime + 1;
                forecastFingerprint = EconomyForecastOps.Fingerprint(em, root);
            }
            forecastReady = em.HasComponent<EconomyForecastState>(root)
                && em.GetComponentData<EconomyForecastState>(root).Turn == state.Turn
                && em.GetComponentData<EconomyForecastState>(root).Fingerprint.ToString() == forecastFingerprint;
            forecast.Clear();
            if (forecastReady)
            {
                foreach (var pair in ResourceForecastReadModel.Read(em, root, state.Turn)) forecast.Add(pair.Key, pair.Value);
                requestedForecast = null;
            }
            else if (state.CheckpointPending != 0 || state.Paused != 0 || state.IntelligenceMode != 0) requestedForecast = null;
            else if (requestedForecast != forecastFingerprint && Commands.TryQueue(new Command { Kind = CommandKind.ForecastEconomy }))
                requestedForecast = forecastFingerprint;
        }

        void Update()
        {
            if (Session == null || !Session.IsBound || !ContentRoot.activeInHierarchy || showingBuildings && !ResourceDetailsOpen || Time.unscaledTime < forecastCheckAt) return;
            var previous = forecastFingerprint; bool wasReady = forecastReady;
            RefreshForecast();
            if (previous != forecastFingerprint || wasReady != forecastReady) Session.NextRefresh = 0;
        }

        void RenderResourceDetails()
        {
            var state = em.GetComponentData<Session>(root);
            ResourceDetailsTitle.text = Session.Name(detailsItem) + $" · 第 {state.Turn} 回合资源详情";
            ForecastStatus.text = forecastReady
                ? "本回合预计收支 · 随当前条件更新；不含随机收益、手动操作与入夜待存区清空。"
                : ForecastUnavailable == "已结算" ? "本回合已结算；进入下一白天后显示新的预计收支。" : "正在按当前库存、建筑和岗位计算…";
            forecast.TryGetValue(detailsItem, out var item);
            IncomeBody.text = ForecastText(item?.Incomes, item?.Income ?? 0, true);
            ExpenseBody.text = ForecastText(item?.Expenses, item?.Expense ?? 0, false);
        }

        string ForecastText(List<EconomyEntry> entries, long total, bool income)
        {
            if (!forecastReady) return ForecastUnavailable == "已结算" ? "本回合已结算" : "正在计算…";
            var text = new StringBuilder("合计 " + total + "\n\n");
            if (entries == null || entries.Count == 0) return text.Append(income ? "本回合无预计产出。" : "本回合无预计消耗。").ToString();
            foreach (var entry in entries)
            {
                string source = entry.Source == 0 || Sim.Find(em, entry.Source) == Entity.Null ? entry.SourceName.ToString() : Session.EntityName(entry.Source);
                text.AppendLine($"{source} · {UI_GamePanel_Economy.EconomyReasonName(entry.Reason)}");
                text.AppendLine($"{Session.Name(detailsItem)}  {UI_GamePanel_Economy.Signed(entry.Delta)}" + (entry.Pending != 0 ? " · 待存区" : ""));
                if (!entry.Note.IsEmpty) text.AppendLine(entry.Note.ToString());
                text.AppendLine();
            }
            return text.ToString();
        }

        void OnDisable() { EndInventoryDrag(); CloseResourceDetails(); ClearSelection(); }

        internal void ResetSession()
        {
            EndInventoryDrag(); CloseResourceDetails(); ClearSelection();
            RemoveMissing(slotViews, new HashSet<(ulong, int)>()); RemoveMissing(buildingViews, new HashSet<ulong>());
            RemoveMissing(resourceViews, new HashSet<int>()); RemoveMissing(pendingViews, new HashSet<int>());
            forecast.Clear(); forecastReady = false; forecastCheckAt = 0; forecastFingerprint = requestedForecast = null;
            pinned.Clear(); displayedFingerprint = null; showingBuildings = false;
            buildingsRendered = resourcesRendered = false; restoreScrollPosition = true;
            buildingScrollPosition = resourceScrollPosition = new Vector2(0, 1);
            if (Quantity != null) Quantity.SetTextWithoutNotify("1");
        }

        internal override void ClearAllRows() => ResetSession();
    }
}
