using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Landsong.Content;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using TMPro;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public readonly struct BuildingActionContext
    {
        public readonly EntityManager Manager;
        public readonly Entity Root;
        public readonly Entity Entity;
        public readonly ulong BuildingId;
        public readonly Building Building;

        public BuildingActionContext(EntityManager manager, Entity root, Entity entity)
        {
            Manager = manager;
            Root = root;
            Entity = entity;
            BuildingId = manager.GetComponentData<Identity>(entity).Id;
            Building = manager.GetComponentData<Building>(entity);
        }
    }

    public sealed class BuildingActionDefinition
    {
        public readonly string Key;
        public readonly int Order;
        public readonly Func<BuildingActionContext, string> Label;
        public readonly Func<BuildingActionContext, bool> Visible;
        public readonly Func<BuildingActionContext, bool> Enabled;
        public readonly Action<BuildingActionContext> Execute;

        public BuildingActionDefinition(string key, int order, Func<BuildingActionContext, string> label,
            Func<BuildingActionContext, bool> visible, Func<BuildingActionContext, bool> enabled,
            Action<BuildingActionContext> execute)
        {
            if (string.IsNullOrWhiteSpace(key) || label == null || visible == null || enabled == null || execute == null)
                throw new ArgumentException("建筑操作定义缺少标识或处理函数。", nameof(key));
            Key = key;
            Order = order;
            Label = label;
            Visible = visible;
            Enabled = enabled;
            Execute = execute;
        }
    }

    public sealed class UI_GamePanel_BuildingActionBar : Moyo.Unity.UIViewBase, IGameBuildingUi
    {
        internal GameUiInputContext inputContext;
        internal UI_GamePanel_Inventory inventory;
        internal UI_GamePanel_Economy economy;
        internal WorldSelectionState worldSelection;
        internal IntelligenceViewState intelligence;
        internal GameUiRefreshScheduler refresh;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Hud hudController;
        internal IGameUiNavigation navigation;
        UI_GamePanel_RowCollection rowsController;
        internal GameUiSessionHandle sessionController;
        internal UI_GamePanel_WorldInteraction worldController;
        readonly Dictionary<string, BuildingActionDefinition> actions = new Dictionary<string, BuildingActionDefinition>();
        readonly List<BuildingActionDefinition> orderedActions = new List<BuildingActionDefinition>();
        readonly Dictionary<string, Button> actionButtons = new Dictionary<string, Button>();
        readonly Dictionary<string, ulong> actionButtonTargets = new Dictionary<string, ulong>();
        readonly HashSet<string> visibleActions = new HashSet<string>();
        Button actionButtonTemplate;
        internal CropDisplayCatalog cropCatalog;
        internal ItemDisplayCatalog itemCatalog;
        [Sirenix.OdinInspector.LabelText("确认条目模板"), Sirenix.OdinInspector.Required]
        public UI_GamePanel_Row ConfirmRowTemplate;
        [Sirenix.OdinInspector.LabelText("建筑栏")]
        public UI_GamePanel_BuildingCatalogBar BuildingBar;
        internal bool buildingBarOpen;
        public void ToggleBuildingCatalog()
        {
            if (!inputContext.Policy.Capture().CanNavigate)
                return;
            if (navigation.Panel == GamePanelId.Building && buildingBarOpen)
            {
                navigation.ClosePanel();
                return;
            }

            showBuildingActionBar = false;
            CropSelectionPanel?.Hide();
            if (BuildingActionBar != null)
                BuildingActionBar.gameObject.SetActive(false);
            DetailsPanel.Hide();
            navigation.OpenPanel(GamePanelId.Building);
        }

        internal void InitializeBuildingCatalog()
        {
            if (ConfirmRowTemplate == null)
                throw new InvalidOperationException("建筑操作条缺少确认条目模板。");
            ConfirmRowTemplate.ValidateConfiguration();
            rowsController = new UI_GamePanel_RowCollection(ConfirmRowTemplate);
            if (BuildingBar == null)
                return;
            BuildingBar.CloseButton.onClick.AddListener(navigation.ClosePanel);
            BuildingBar.EconomyButton.onClick.AddListener(() => economy.OpenEconomy());
        }

        internal void RefreshBuildingCatalog()
        {
            if (BuildingBar == null)
                return;
            BuildingBar.gameObject.SetActive(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Building && buildingBarOpen && sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day);
            if (!BuildingBar.gameObject.activeSelf)
                return;
            var choices = new List<BuildingId>();
            for (int i = 0; i < BuildingDefinitions.Count(sessionController.em, sessionController.root); i++)
            {
                var building = BuildingId.FromIndex(i);
                if (BuildingBlueprints.Has(sessionController.em, sessionController.root, building))
                    choices.Add(building);
            }

            choices.Sort((a, b) =>
            {
                var order = BuildingDefinitions.Get(sessionController.em, sessionController.root, a).PlacementAndVisuals.MenuOrder.CompareTo(BuildingDefinitions.Get(sessionController.em, sessionController.root, b).PlacementAndVisuals.MenuOrder);
                return order != 0 ? order : string.CompareOrdinal(BuildingName(a), BuildingName(b));
            });
            var cards = new List<BuildingCatalogCard>();
            foreach (var definition in choices)
            {
                ref var d = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, definition);
                var quote = BuildingPlacementCommands.CheckBuild(sessionController.em, sessionController.root, definition);
                var source = BuildingSource(definition);
                var name = BuildingName(definition);
                var tooltip = new StringBuilder(name);
                if (!string.IsNullOrWhiteSpace(source?.Description))
                    tooltip.Append('\n').Append(source.Description.Trim());
                tooltip.Append("\n\n放置消耗：").Append(CostText(quote.Costs));
                for (var turn = 1; turn <= d.ConstructionTurns; turn++)
                    tooltip.Append("\n\n第").Append(turn).Append("回合消耗：")
                        .Append(CostText(BuildingCostOps.ConstructionStage(sessionController.em, sessionController.root, definition, turn)));
                if (!quote.Allowed)
                    tooltip.Append("\n\n不可建造：").Append(quote.Reason);
                cards.Add(new BuildingCatalogCard { Definition = definition, Name = name, Category = d.PlacementAndVisuals.Category, Icon = source?.Icon, Tooltip = tooltip.ToString(), Allowed = quote.Allowed });
            }

            BuildingBar.Bind(cards, worldController.BeginBuildingPlacement, cards.Count == 0 ? "尚未拥有建筑蓝图" : "选择建筑后点击地图放置 · R 旋转 · 右键取消");
        }

        public bool BuildingRangesVisible => showBuildingRange;
        public int ResourcePathCellCount { get; internal set; }

        public void SelectBuilding(ulong id)
        {
            if (!inputContext.Policy.Capture().CanNavigate)
                return;
            var entity = WorldQueries.Find(sessionController.em, id);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity))
                return;
            navigation.ClosePanel();
            CropSelectionPanel?.Hide();
            worldSelection.SelectedEntityId = id;
            showBuildingActionBar = true;
            DetailsPanel.Hide();
            showBuildingRange = true;
            RefreshActionButtons(entity);
            worldController.RebuildBuildingRange();
            refresh.NextPanel = 0;
        }

        [Header("Building workflow (presentation only)")]
        [Sirenix.OdinInspector.LabelText("建筑目录")]
        public BuildingDisplayCatalog BuildingCatalog;
        [FormerlySerializedAs("BuildingToolbar")]
        [Sirenix.OdinInspector.LabelText("建筑操作条")]
        public RectTransform BuildingActionBar;
        [Sirenix.OdinInspector.LabelText("建筑操作按钮容器"), Sirenix.OdinInspector.Required]
        public RectTransform BuildingActionButtons;
        [Sirenix.OdinInspector.LabelText("建筑确认条目容器")]
        public RectTransform BuildingConfirmRows;
        [Sirenix.OdinInspector.LabelText("建筑详情面板")]
        public UI_GamePanel_BuildingDetails DetailsPanel;
        [Sirenix.OdinInspector.LabelText("建筑确认面板")]
        public GameObject BuildingConfirmPanel;
        [Sirenix.OdinInspector.LabelText("作物选择面板")]
        public UI_GamePanel_CropSelection CropSelectionPanel;
        [Sirenix.OdinInspector.LabelText("建筑提示")]
        public Text BuildingHint;
        [Sirenix.OdinInspector.LabelText("建筑确认标题")]
        public Text BuildingConfirmTitle;
        [Sirenix.OdinInspector.LabelText("建筑确认组")]
        public CanvasGroup BuildingConfirmGroup;
        [Sirenix.OdinInspector.LabelText("建筑放置面板")]
        public GameObject BuildingPlacementPanel;
        internal bool showBuildingActionBar;
        internal bool showBuildingRange;
        public ulong SelectedBuildingId => worldSelection.SelectedEntityId;

        internal void InitializeBuildings()
        {
            if (BuildingHint != null)
            {
                BuildingHint.text = "";
                BuildingPlacementPanel.SetActive(false);
            }

            if (DetailsPanel == null)
                throw new InvalidOperationException("建筑操作条未绑定独立的建筑详情面板。");
            if (BuildingActionButtons == null || BuildingActionButtons.childCount == 0)
                throw new InvalidOperationException("建筑操作条缺少按钮容器或第一个按钮子对象。");
            actionButtonTemplate = BuildingActionButtons.GetChild(0).GetComponent<Button>();
            if (actionButtonTemplate == null || actionButtonTemplate.GetComponentInChildren<TMP_Text>(true) == null)
                throw new InvalidOperationException("建筑操作按钮容器的第一个子对象必须是带文字的按钮。");
            foreach (var button in BuildingActionButtons.GetComponentsInChildren<Button>(true))
                if (button.transform != BuildingActionButtons)
                    button.gameObject.SetActive(false);
            DetailsPanel.Initialize();
            CropSelectionPanel.ValidateConfiguration();
            cropCatalog = DetailsPanel.Crops;
            itemCatalog = DetailsPanel.Items;
            RegisterDefaultActions();
            InitializeBuildingCatalog();
            BuildingActionBar.gameObject.SetActive(false);
            DetailsPanel.gameObject.SetActive(false);
            BuildingConfirmPanel.SetActive(false);
            CropSelectionPanel.Hide();
        }

        public void RegisterAction(BuildingActionDefinition action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            if (actions.ContainsKey(action.Key))
                throw new InvalidOperationException("建筑操作重复注册：" + action.Key);
            actions.Add(action.Key, action);
            orderedActions.Add(action);
            orderedActions.Sort((left, right) =>
            {
                var order = left.Order.CompareTo(right.Order);
                return order != 0 ? order : string.CompareOrdinal(left.Key, right.Key);
            });
            if (refresh != null)
                refresh.NextPanel = 0;
        }

        public Button ActionButton(string key) => actionButtons.TryGetValue(key, out var button) ? button : null;

        void RegisterDefaultActions()
        {
            RegisterAction(new BuildingActionDefinition("details", 10, _ => "详情", _ => true, _ => true,
                _ => DetailsPanel.Open()));
            RegisterAction(new BuildingActionDefinition("range", 20,
                _ => showBuildingRange ? "隐藏连接范围" : "连接范围", _ => true, _ => true, context =>
                {
                    showBuildingRange = !showBuildingRange;
                    if (showBuildingRange)
                        worldController.RebuildBuildingRange();
                    else
                        worldController.ClearBuildingRange();
                    RefreshActionButtons(context.Entity);
                    refresh.NextPanel = 0;
                }));
            RegisterAction(new BuildingActionDefinition("upgrade", 30, _ => "升级",
                context => IsDay(context) && context.Building.Stage == LifeStage.Operational &&
                    context.Building.Level < BuildingDefinitions.Get(context.Manager, context.Root,
                        context.Manager.GetComponentData<BuildingDefinitionRef>(context.Entity).Definition).MaximumLevel,
                context => BuildingUpgradeCommands.Check(context.Manager, context.Root, context.Entity).Allowed,
                _ => ConfirmBuildingCommand(CommandKind.Upgrade)));
            RegisterAction(new BuildingActionDefinition("repair", 40, _ => "修复",
                context => IsDay(context) && context.Building.Stage == LifeStage.Ruined,
                context => context.Building.RuinPending == 0,
                _ => ConfirmBuildingCommand(CommandKind.Repair)));
            RegisterAction(new BuildingActionDefinition("plant", 50, _ => "种植", CanPlantHere,
                context => CanEditBuilding(context), context => OpenCropSelection(context.BuildingId)));
            RegisterAction(new BuildingActionDefinition("inventory", 60, _ => "库存",
                context => context.Manager.GetComponentData<BuildingStorageStats>(context.Entity).Capacity > 0,
                context => inventory.CanOpen(context.Manager, context.Root),
                context =>
                {
                    navigation.OpenPanel(GamePanelId.Inventory);
                    if (navigation.IsPanelOpen && navigation.Panel == GamePanelId.Inventory)
                    {
                        showBuildingRange = false;
                        worldController.ClearBuildingRange();
                        inventory.FocusBuilding(context.BuildingId);
                    }
                }));
        }

        static bool IsDay(BuildingActionContext context) =>
            context.Manager.GetComponentData<Session>(context.Root).Phase == Phase.Day;

        static bool CanEditBuilding(BuildingActionContext context) =>
            IsDay(context) && BuildingStatus.Operational(context.Manager, context.Entity) &&
            context.Manager.GetComponentData<SimulationControl>(context.Root).Paused == 0 &&
            context.Manager.GetComponentData<PersistenceGate>(context.Root).CheckpointPending == 0;

        static bool CanPlantHere(BuildingActionContext context)
        {
            if (context.Manager.GetComponentData<BuildingFarmingState>(context.Entity).Crop.IsValid)
                return false;
            ref var farming = ref BuildingDefinitions.Get(context.Manager, context.Root,
                context.Manager.GetComponentData<BuildingDefinitionRef>(context.Entity).Definition).Capabilities.Farming;
            if (!farming.Enabled)
                return false;
            for (int i = 0; i < farming.Crops.Length; i++)
                if (farming.Crops[i].Level == 0 || farming.Crops[i].Level == context.Building.Level)
                    return true;
            return false;
        }

        void RefreshActionButtons(Entity entity)
        {
            var context = new BuildingActionContext(sessionController.em, sessionController.root, entity);
            visibleActions.Clear();
            foreach (var action in orderedActions)
            {
                if (!action.Visible(context))
                    continue;
                visibleActions.Add(action.Key);
                if (!actionButtons.TryGetValue(action.Key, out var button))
                {
                    button = Instantiate(actionButtonTemplate, BuildingActionButtons);
                    button.name = "建筑操作 " + action.Key;
                    actionButtons.Add(action.Key, button);
                }
                if (!actionButtonTargets.TryGetValue(action.Key, out var target) || target != context.BuildingId)
                {
                    button.onClick.RemoveAllListeners();
                    var key = action.Key;
                    var buildingId = context.BuildingId;
                    button.onClick.AddListener(() => ExecuteAction(key, buildingId));
                    actionButtonTargets[action.Key] = buildingId;
                }
                button.GetComponentInChildren<TMP_Text>(true).text = action.Label(context);
                button.interactable = action.Enabled(context);
                button.gameObject.SetActive(true);
                button.transform.SetAsLastSibling();
            }
            foreach (var pair in actionButtons)
                if (!visibleActions.Contains(pair.Key))
                    pair.Value.gameObject.SetActive(false);
        }

        void ExecuteAction(string key, ulong buildingId)
        {
            if (worldSelection.SelectedEntityId != buildingId ||
                !inputContext.Policy.Capture().CanNavigate || !actions.TryGetValue(key, out var action))
                return;
            var entity = WorldQueries.Find(sessionController.em, buildingId);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity))
                return;
            var context = new BuildingActionContext(sessionController.em, sessionController.root, entity);
            if (action.Visible(context) && action.Enabled(context))
                action.Execute(context);
        }

        public bool CancelBuildingInteraction()
        {
            if (CropSelectionPanel != null && CropSelectionPanel.IsOpen)
            {
                CropSelectionPanel.Hide();
                return true;
            }
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf)
            {
                BuildingConfirmPanel.SetActive(false);
                return true;
            }

            if (worldController.HasBuildingPlacement)
            {
                worldController.EndBuildingPlacement();
                return true;
            }

            if (showBuildingRange)
            {
                showBuildingRange = false;
                worldController.ClearBuildingRange();
                refresh.NextPanel = 0;
                return true;
            }

            if (DetailsPanel.Hide())
                return true;
            if (showBuildingActionBar)
            {
                showBuildingActionBar = false;
                showBuildingRange = false;
                BuildingActionBar.gameObject.SetActive(false);
                worldController.ClearBuildingRange();
                return true;
            }

            return false;
        }

        internal void LifecycleOnDisable()
        {
            CropSelectionPanel?.Hide();
            worldController.EndBuildingPlacement();
            inventory.EndInventoryDrag();
        }

        internal void BeginPlacementHint(string action)
        {
            if (BuildingHint == null)
                return;
            BuildingHint.text = action + " · 点击地图选择位置；R 旋转；右键/Esc 取消";
            RefreshPlacementHint();
        }

        internal void RefreshPlacementHint()
        {
            if (BuildingHint == null)
                return;
            bool ready = EcsSceneFlow.GameReady && sessionController.root != Entity.Null && sessionController.em.World != null && sessionController.em.World.IsCreated && sessionController.em.Exists(sessionController.root);
            if (ready && worldController.HasBuildingPlacement && (intelligence.IsOpen || sessionController.em.GetComponentData<Session>(sessionController.root).Phase != Phase.Day))
                worldController.EndBuildingPlacement();
            BuildingPlacementPanel.SetActive(ready && worldController.HasBuildingPlacement && (inputContext.PauseMenu == null || !inputContext.PauseMenu.IsOpen) && (BuildingConfirmPanel == null || !BuildingConfirmPanel.activeSelf));
        }

        public ContentDisplay BuildingSource(BuildingId definition) => BuildingCatalog.Get(definition);
        string BuildingName(BuildingId definition) => BuildingDefinitions.Get(sessionController.em, sessionController.root, definition).Metadata.Name.ToString();
        string ItemName(ItemId item) => ItemDefinitions.Get(sessionController.em, sessionController.root, item).Metadata.Name.ToString();
        public string CostText(IEnumerable<BuildingCost> costs)
        {
            var lines = costs.Select(c => ItemName(c.Item) + " × " + c.Amount).ToArray();
            return lines.Length == 0 ? "无资源费用" : string.Join("、", lines);
        }

        public void ShowBuildingConfirmation(string title, IEnumerable<string> lines, Action confirm)
        {
            ShowBuildingChoices(title, (row, close) =>
            {
                foreach (var line in lines)
                    row(line);
                row("确认", () =>
                {
                    close();
                    confirm();
                });
                row("取消", close);
            });
        }

        public void ShowBuildingChoices(string title, Action<BuildingChoiceRow, Action> populate)
        {
            if (populate == null)
                throw new ArgumentNullException(nameof(populate));
            if (BuildingConfirmPanel == null || BuildingConfirmTitle == null || BuildingConfirmGroup == null || BuildingConfirmRows == null)
                throw new InvalidOperationException("建筑确认面板检查器引用不完整。");
            CropSelectionPanel?.Hide();
            BuildingConfirmTitle.text = "确认操作";
            BuildingConfirmPanel.SetActive(true);
            BuildingConfirmPanel.transform.SetAsLastSibling();
            // The confirmation container has one owner, including rows supplied by other panels.
            rowsController.Begin(BuildingConfirmRows);
            try
            {
                rowsController.Row(title, parent: BuildingConfirmRows, key: "confirmation-title");
                populate((label, action, key) => rowsController.Row(label, action, parent: BuildingConfirmRows, key: key), () => BuildingConfirmPanel.SetActive(false));
            }
            finally
            {
                rowsController.End();
            }
        }

        void ShowCropSelection(IReadOnlyList<CropSelectionEntry> entries, Action<CropId> choose, string hint = null)
        {
            BuildingConfirmPanel.SetActive(false);
            CropSelectionPanel.Show(entries, choose, hint);
        }

        public void OpenCropSelection(ulong buildingId)
        {
            if (!inputContext.Policy.Capture().CanNavigate)
                return;
            var entity = WorldQueries.Find(sessionController.em, buildingId);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity) ||
                worldSelection.SelectedEntityId != buildingId)
                return;
            var context = new BuildingActionContext(sessionController.em, sessionController.root, entity);
            var farming = sessionController.em.GetComponentData<BuildingFarmingState>(entity);
            var definition = sessionController.em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
            ref var buildingDefinition = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, definition);
            if (!buildingDefinition.Capabilities.Farming.Enabled)
                return;
            var entries = new List<CropSelectionEntry>();
            var seen = new HashSet<CropId>();
            for (int i = 0; i < buildingDefinition.Capabilities.Farming.Crops.Length; i++)
            {
                var allowed = buildingDefinition.Capabilities.Farming.Crops[i];
                if ((allowed.Level != 0 && allowed.Level != context.Building.Level) || !seen.Add(allowed.Crop))
                    continue;
                var cropId = allowed.Crop;
                ref var crop = ref CropDefinitions.Get(sessionController.em, sessionController.root, cropId);
                var costs = new List<BuildingCost>();
                for (int cost = 0; cost < crop.PlantingCosts.Length; cost++)
                    costs.Add(new BuildingCost(crop.PlantingCosts[cost].Item, crop.PlantingCosts[cost].Quantity));
                var yields = new List<string>();
                for (int output = 0; output < crop.HarvestOutputs.Length; output++)
                {
                    var harvest = crop.HarvestOutputs[output];
                    var item = ItemDefinitions.Get(sessionController.em, sessionController.root, harvest.Item).Metadata.Name;
                    yields.Add(item + " " + harvest.MinimumQuantity + "～" + harvest.MaximumQuantity);
                }
                var icon = cropCatalog.Get(cropId)?.Icon;
                if (icon == null && crop.HarvestOutputs.Length > 0)
                    icon = itemCatalog.Get(crop.HarvestOutputs[0].Item)?.Icon;
                entries.Add(new CropSelectionEntry
                {
                    Crop = cropId,
                    Icon = icon,
                    Name = crop.Metadata.Name.ToString(),
                    BaseYield = yields.Count == 0 ? "无" : string.Join("、", yields),
                    GrowthTurns = crop.GrowthTurns,
                    PlantingCost = CostText(costs),
                    Available = CanEditBuilding(context) && !farming.Crop.IsValid &&
                        BuildingCostOps.CanPay(sessionController.em, sessionController.root, costs)
                });
            }
            ShowCropSelection(entries, crop =>
            {
                var current = WorldQueries.Find(sessionController.em, buildingId);
                if (current != Entity.Null && sessionController.em.HasComponent<Building>(current) &&
                    !sessionController.em.GetComponentData<BuildingFarmingState>(current).Crop.IsValid)
                    commandsController.TryQueue(new PlantCropRequest { Building = buildingId, Crop = crop });
            }, farming.Crop.IsValid ? "已种植 " + CropDefinitions.Get(sessionController.em, sessionController.root, farming.Crop).Metadata.Name + "，更换前请先使用 X 铲除。" : null);
        }

        internal T ConfirmationItem<T>(T template, string label, RectTransform parent, string key)
            where T : UI_GamePanel_Row
        {
            if (rowsController == null)
                throw new InvalidOperationException("建筑确认条目尚未初始化。");
            return rowsController.Item(template, label, parent: parent, key: key);
        }

        internal void ClearRows() => rowsController?.ClearAll();
        public void ConfirmBuildingCommand(CommandKind kind)
        {
            var entity = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity))
                return;
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var b = sessionController.em.GetComponentData<Building>(entity);
            var lines = new List<string>();
            var title = "";
            if (kind == CommandKind.Upgrade)
            {
                var quote = BuildingUpgradeCommands.Check(sessionController.em, sessionController.root, entity);
                if (!quote.Allowed)
                {
                    hudController.Message.text = quote.Reason;
                    return;
                }

                title = "升级 " + id.Name + " → LV" + (b.Level + 1);
                lines.Add("费用：" + CostText(quote.Costs));
                lines.Add("保留建筑身份与现有状态，按新等级更新能力和模型。");
            }
            else if (kind == CommandKind.Repair)
            {
                if (b.Stage != LifeStage.Ruined || b.RuinPending != 0)
                    return;
                var costs = BuildingCostOps.RepairTotal(sessionController.em, sessionController.root, entity, out var turns);
                title = "修复 " + id.Name;
                lines.Add("共 " + turns + " 回合，逐回合扣费，无需工人。");
                lines.Add("总费用：" + CostText(costs));
                lines.Add("先用待存放材料，再用正常库存；普通库存断连或当期材料不足时暂停，不吞进度。");
                var payment = BuildingCostOps.QuoteRepair(sessionController.em, sessionController.root, entity);
                foreach (var c in payment.Payments)
                    lines.Add($"首期 {ItemName(c.Item)}：待存放 {c.Pending} + 普通库存 {c.Normal}，缺口 {c.Missing}");
                lines.Add(payment.Reason + "；开始修复不会立即扣除材料。");
                lines.Add("完工保留等级/名称/皮肤，不自动招工或召回驻军；库存为空，住房黎明迁入 2 人（不超过容量）。");
            }
            else
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "建筑操作条只处理升级和修复。");

            ShowBuildingConfirmation(title, lines, () =>
            {
                if (kind == CommandKind.Upgrade)
                    worldController.SubmitBuilding(new UpgradeBuildingRequest { Building = id.Id });
                else if (kind == CommandKind.Repair)
                    worldController.SubmitBuilding(new RepairBuildingRequest { Building = id.Id });
            });
        }

        internal void RefreshBuildingSelection()
        {
            var entity = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            var has = entity != Entity.Null && sessionController.em.HasComponent<Building>(entity);
            var actionBarVisible = has && showBuildingActionBar && !worldController.HasBuildingPlacement && !intelligence.IsOpen && !(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Garrison);
            if (BuildingActionBar != null)
                BuildingActionBar.gameObject.SetActive(actionBarVisible && PositionActionBar(entity));
            if (!has)
            {
                showBuildingActionBar = false;
                foreach (var button in actionButtons.Values)
                    button.gameObject.SetActive(false);
                DetailsPanel.Hide();
                hudController.Selection.text = "点击建筑显示操作条；WASD 镜头，滚轮缩放。";
                return;
            }

            var b = sessionController.em.GetComponentData<Building>(entity);
            var id = sessionController.em.GetComponentData<Identity>(entity);
            hudController.Selection.text = $"{id.Name} · LV{b.Level} · {UI_GamePanel_BuildingDetails.BuildingStageName(b.Stage)}";
            RefreshActionButtons(entity);
            DetailsPanel.Refresh(entity);
        }

        bool PositionActionBar(Entity building)
        {
            if (building == Entity.Null || !sessionController.em.Exists(building) || !sessionController.em.HasComponent<BuildingSelectionAnchor>(building))
                return false;
            var anchor = sessionController.em.GetComponentData<BuildingSelectionAnchor>(building).Value;
            if (anchor == Entity.Null || !sessionController.em.Exists(anchor) || !sessionController.em.HasComponent<LocalToWorld>(anchor))
                return false;
            var camera = worldController.Camera;
            var screen = camera.WorldToScreenPoint(sessionController.em.GetComponentData<LocalToWorld>(anchor).Position);
            if (screen.z <= 0 || screen.x < 0 || screen.x > Screen.width || screen.y < 0 || screen.y > Screen.height)
                return false;
            var space = BuildingActionBar.parent as RectTransform;
            if (space == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(space, screen, null, out var local))
                return false;
            BuildingActionBar.anchoredPosition = local;
            return true;
        }

        void LateUpdate()
        {
            if (BuildingActionBar == null || sessionController == null)
                return;
            if (!showBuildingActionBar || worldController.HasBuildingPlacement || intelligence.IsOpen || navigation.IsPanelOpen && navigation.Panel == GamePanelId.Garrison)
            {
                BuildingActionBar.gameObject.SetActive(false);
                return;
            }

            var entity = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            BuildingActionBar.gameObject.SetActive(PositionActionBar(entity));
        }

        public void FocusBuilding(ulong id)
        {
            var entity = WorldQueries.Find(sessionController.em, id);
            if (entity == Entity.Null)
                return;
            worldController.LocateHistory(id, EntityState.Position(sessionController.em, entity));
            worldController.ClearBuildingRange();
            worldSelection.SelectedEntityId = id;
            if (sessionController.em.HasComponent<Building>(entity))
                RefreshActionButtons(entity);
            refresh.NextPanel = 0;
        }

        internal void ResetSession()
        {
            buildingBarOpen = showBuildingActionBar = showBuildingRange = false;
            ResourcePathCellCount = 0;
            worldController.ClearBuildingRange();
            BuildingConfirmPanel.SetActive(false);
            CropSelectionPanel.Hide();
            foreach (var button in actionButtons.Values)
                button.gameObject.SetActive(false);
            DetailsPanel.ResetSession();
            BuildingActionBar.gameObject.SetActive(false);
            BuildingBar.gameObject.SetActive(false);
        }
    }
}
