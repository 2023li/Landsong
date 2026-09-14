using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Authoring;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_BuildingActionBar : Moyo.Unity.UIViewBase, IGameBuildingUi
    {
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Hud hudController;
        internal IGameUiNavigation navigation;
        UI_GamePanel_RowCollection rowsController;
        internal GameUiSession sessionController;
        internal UI_GamePanel_WorldInteraction worldController;
        [Sirenix.OdinInspector.LabelText("确认条目模板"), Sirenix.OdinInspector.Required]
        public UI_GamePanel_Row ConfirmRowTemplate;
        [Sirenix.OdinInspector.LabelText("建筑栏")]
        public UI_GamePanel_BuildingCatalogBar BuildingBar;
        internal bool buildingBarOpen;
        public void ToggleBuildingCatalog()
        {
            if (!navigation.InputPolicy.Capture().CanNavigate)
                return;
            if (navigation.Panel == GamePanelId.Building && buildingBarOpen)
            {
                navigation.ClosePanel();
                return;
            }

            showBuildingActionBar = false;
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
            BuildingBar.EconomyButton.onClick.AddListener(() => navigation.OpenEconomy());
        }

        internal void RefreshBuildingCatalog()
        {
            if (BuildingBar == null)
                return;
            BuildingBar.gameObject.SetActive(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Building && buildingBarOpen && sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day);
            if (!BuildingBar.gameObject.activeSelf)
                return;
            var choices = new List<int>();
            sessionController.ForDefinitions(ContentKind.Building, (i, d) =>
            {
                if (BlueprintOps.Has(sessionController.em, sessionController.root, i))
                    choices.Add(i);
            });
            choices.Sort((a, b) =>
            {
                var order = Sim.Definition(sessionController.em, sessionController.root, a).BuildingPolicy.MenuOrder.CompareTo(Sim.Definition(sessionController.em, sessionController.root, b).BuildingPolicy.MenuOrder);
                return order != 0 ? order : string.CompareOrdinal(sessionController.Name(a), sessionController.Name(b));
            });
            var cards = new List<BuildingCatalogCard>();
            foreach (var definition in choices)
            {
                var d = Sim.Definition(sessionController.em, sessionController.root, definition);
                var quote = BuildingOps.CheckBuild(sessionController.em, sessionController.root, definition);
                var source = BuildingSource(definition);
                var tooltip = sessionController.Name(definition) + " · LV1 · " + d.Size.x + "×" + d.Size.y + "\n放置成本：" + CostText(quote.Costs) + "\n每回合消耗（固定维护）：" + CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, definition, RuleKind.Maintenance, 1));
                if (d.Duration > 0)
                    tooltip += "\n施工 " + d.Duration + " 回合；首期材料：" + CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, definition, RuleKind.ConstructionCost, 1)) + "（以后按施工阶段配置）";
                var inputs = BuildingCostOps.Rules(sessionController.em, sessionController.root, definition, RuleKind.Input, 1);
                if (inputs.Count > 0)
                    tooltip += "\n每次生产投入：" + CostText(inputs);
                tooltip += "\n岗位食物、补贴、配方及供奉等按实际配置另计。";
                if (!string.IsNullOrEmpty(source?.Description))
                    tooltip += "\n" + source.Description;
                if (!quote.Allowed)
                    tooltip += "\n不可建造：" + quote.Reason;
                cards.Add(new BuildingCatalogCard { Definition = definition, Name = sessionController.Name(definition), Category = d.BuildingPolicy.Category, Icon = source?.Icon, Tooltip = tooltip, Allowed = quote.Allowed });
            }

            BuildingBar.Bind(cards, worldController.BeginBuildingPlacement, cards.Count == 0 ? "尚未拥有建筑蓝图" : "选择建筑后点击地图放置 · R 旋转 · 右键取消");
        }

        public bool BuildingRangesVisible => showBuildingRange;
        public int ResourcePathCellCount { get; internal set; }

        public void SelectBuilding(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanNavigate)
                return;
            var entity = Sim.Find(sessionController.em, id);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity))
                return;
            navigation.ClosePanel();
            sessionController.selected = id;
            showBuildingActionBar = true;
            DetailsPanel.Hide();
            showBuildingRange = true;
            worldController.rangeRevision = -1;
            sessionController.nextRefresh = 0;
        }

        [Header("Building workflow (presentation only)")]
        [Sirenix.OdinInspector.LabelText("建筑目录")]
        public GameCatalogAsset BuildingCatalog;
        [FormerlySerializedAs("BuildingToolbar")]
        [Sirenix.OdinInspector.LabelText("建筑操作条")]
        public RectTransform BuildingActionBar;
        [Sirenix.OdinInspector.LabelText("建筑确认条目容器")]
        public RectTransform BuildingConfirmRows;
        [Sirenix.OdinInspector.LabelText("建筑详情面板")]
        public UI_GamePanel_BuildingDetails DetailsPanel;
        [Sirenix.OdinInspector.LabelText("建筑确认面板")]
        public GameObject BuildingConfirmPanel;
        [Sirenix.OdinInspector.LabelText("建筑移动按钮")]
        public Button BuildingMoveButton;
        [Sirenix.OdinInspector.LabelText("建筑范围按钮")]
        public Button BuildingRangeButton;
        [Sirenix.OdinInspector.LabelText("建筑升级按钮")]
        public Button BuildingUpgradeButton;
        [Sirenix.OdinInspector.LabelText("建筑修理按钮")]
        public Button BuildingRepairButton;
        [Sirenix.OdinInspector.LabelText("建筑拆除按钮")]
        public Button BuildingDemolishButton;
        [Sirenix.OdinInspector.LabelText("建筑详情按钮")]
        public Button BuildingDetailsButton;
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
        public ulong SelectedBuildingId => sessionController.selected;

        internal void InitializeBuildings()
        {
            if (BuildingHint != null)
            {
                BuildingHint.text = "";
                BuildingPlacementPanel.SetActive(false);
            }

            if (DetailsPanel == null)
                throw new InvalidOperationException("建筑操作条未绑定独立的建筑详情面板。");
            DetailsPanel.Initialize();
            BuildingDetailsButton.onClick.AddListener(() =>
            {
                var selected = Sim.Find(sessionController.em, sessionController.selected);
                if (selected == Entity.Null || !sessionController.em.HasComponent<Building>(selected))
                    return;
                DetailsPanel.Open();
            });
            BuildingMoveButton.onClick.AddListener(worldController.BeginMoveBuilding);
            BuildingRangeButton.onClick.AddListener(() =>
            {
                showBuildingRange = !showBuildingRange;
                worldController.rangeRevision = -1;
                sessionController.nextRefresh = 0;
            });
            BuildingUpgradeButton.onClick.AddListener(() => ConfirmBuildingCommand(CommandKind.Upgrade));
            BuildingRepairButton.onClick.AddListener(() => ConfirmBuildingCommand(CommandKind.Repair));
            BuildingDemolishButton.onClick.AddListener(() => ConfirmBuildingCommand(CommandKind.Demolish));
            InitializeBuildingCatalog();
            BuildingActionBar.gameObject.SetActive(false);
            DetailsPanel.gameObject.SetActive(false);
            BuildingConfirmPanel.SetActive(false);
        }

        public bool CancelBuildingInteraction()
        {
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
                worldController.buildingOverlays.Clear();
                return true;
            }

            if (DetailsPanel.Hide())
                return true;

            if (showBuildingActionBar)
            {
                showBuildingActionBar = false;
                showBuildingRange = false;
                BuildingActionBar.gameObject.SetActive(false);
                worldController.buildingOverlays.Clear();
                return true;
            }

            return false;
        }

        internal void LifecycleOnDisable()
        {
            worldController.EndBuildingPlacement();
            navigation.EndInventoryDrag();
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
            if (ready && worldController.HasBuildingPlacement && (sessionController.intel || sessionController.em.GetComponentData<Session>(sessionController.root).Phase != Phase.Day))
                worldController.EndBuildingPlacement();
            BuildingPlacementPanel.SetActive(ready && worldController.HasBuildingPlacement && (navigation.PauseMenu == null || !navigation.PauseMenu.IsOpen) && (BuildingConfirmPanel == null || !BuildingConfirmPanel.activeSelf));
        }

        public ContentSource BuildingSource(int definition)
        {
            if (BuildingCatalog == null || !Sim.ValidDefinition(sessionController.em, sessionController.root, definition))
                return null;
            var id = Sim.Definition(sessionController.em, sessionController.root, definition).Id.ToString();
            var index = BuildingCatalog.Find(id);
            return index < 0 ? null : BuildingCatalog.Definitions[index].Data;
        }

        public string CostText(IEnumerable<BuildingCost> costs)
        {
            var lines = costs.Select(c => sessionController.Name(c.Item) + " × " + c.Amount).ToArray();
            return lines.Length == 0 ? "无资源费用" : string.Join("、", lines);
        }

        public void ShowBuildingConfirmation(string title, IEnumerable<string> lines, Action confirm)
        {
            if (BuildingConfirmPanel == null || BuildingConfirmTitle == null || BuildingConfirmGroup == null)
                throw new InvalidOperationException("建筑确认面板检查器引用不完整。");
            BuildingConfirmTitle.text = "确认操作";
            rowsController.Clear(BuildingConfirmRows);
            rowsController.Row(title, parent: BuildingConfirmRows);
            foreach (var line in lines)
                rowsController.Row(line, parent: BuildingConfirmRows);
            rowsController.Row("确认", () =>
            {
                BuildingConfirmPanel.SetActive(false);
                confirm();
            }, parent: BuildingConfirmRows);
            rowsController.Row("取消", () => BuildingConfirmPanel.SetActive(false), parent: BuildingConfirmRows);
            BuildingConfirmPanel.SetActive(true);
            BuildingConfirmPanel.transform.SetAsLastSibling();
        }

        public void ShowBuildingChoices(string title, Action<RectTransform, Action> populate)
        {
            if (populate == null)
                throw new ArgumentNullException(nameof(populate));
            ShowBuildingConfirmation(title, Array.Empty<string>(), () => { });
            rowsController.Clear(BuildingConfirmRows);
            rowsController.Row(title, parent: BuildingConfirmRows);
            populate(BuildingConfirmRows, () => BuildingConfirmPanel.SetActive(false));
        }

        internal T ConfirmationItem<T>(T template, string label, RectTransform parent, string key) where T : UI_GamePanel_Row
        {
            if (rowsController == null)
                throw new InvalidOperationException("建筑确认条目尚未初始化。");
            return rowsController.Item(template, label, parent: parent, key: key);
        }

        internal void ClearRows() => rowsController?.ClearAll();

        public void ConfirmBuildingCommand(CommandKind kind)
        {
            var entity = Sim.Find(sessionController.em, sessionController.selected);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity))
                return;
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var b = sessionController.em.GetComponentData<Building>(entity);
            var lines = new List<string>();
            var title = "";
            if (kind == CommandKind.Upgrade)
            {
                var quote = BuildingOps.CheckUpgrade(sessionController.em, sessionController.root, entity);
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
                    lines.Add($"首期 {sessionController.Name(c.Item)}：待存放 {c.Pending} + 普通库存 {c.Normal}，缺口 {c.Missing}");
                lines.Add(payment.Reason + "；开始修复不会立即扣除材料。");
                lines.Add("完工保留等级/名称/皮肤，不自动招工或召回驻军；库存为空，住房黎明迁入 2 人（不超过容量）。");
            }
            else
            {
                if (sessionController.em.GetComponentData<BuildingStats>(entity).IsCore != 0)
                    return;
                title = "拆除 " + id.Name;
                lines.Add("此操作删除建筑。修复投入不返还，按已支付建造/升级成本计算返还。");
                foreach (var slot in sessionController.em.GetBuffer<InventorySlot>(sessionController.root))
                    if (slot.Provider == id.Id && slot.Count > 0)
                        lines.Add("原库存损失：" + sessionController.Name(slot.Item) + " × " + slot.Count);
                foreach (var refund in BuildingCostOps.DemolitionRefund(sessionController.em, sessionController.root, entity))
                    lines.Add($"返还 {sessionController.Name(refund.Item)} × {refund.Amount}：存入 {refund.Stored}，空间不足损失 {refund.Lost}");
                var soldiers = MilitaryOps.GarrisonCount(sessionController.em, id.Id);
                var empty = 0;
                using (var buildings = Sim.Entities<Building>(sessionController.em))
                    foreach (var other in buildings)
                        if (other != entity && Sim.Operational(sessionController.em, other))
                            empty += math.max(0, sessionController.em.GetComponentData<BuildingStats>(other).Garrison - MilitaryOps.GarrisonCount(sessionController.em, sessionController.em.GetComponentData<Identity>(other).Id));
                if (soldiers > 0)
                    lines.Add($"{soldiers} 名士兵转待分配池；其他驻地空槽 {empty}，下一次入夜前未安排将解散。");
                if (b.Workers > 0)
                    lines.Add(b.Workers + " 名工人失业。");
                if (b.Population > 0)
                    lines.Add("住宅中的 " + b.Population + " 人将损失，请确认人口后果。");
                if (sessionController.em.GetComponentData<BuildingStats>(entity).HeroDefinition >= 0)
                    lines.Add("已招募的关联英雄死亡、经验清零并进入重招冷却。");
                if (EconomyOps.WorkforceLocked(sessionController.em, id.Id))
                    lines.Add("在途远征将终止，进度、携带物资和奖励不保留。");
            }

            ShowBuildingConfirmation(title, lines, () => worldController.SubmitBuilding(CommandRequests.ConfirmBuildingChange(kind, id.Id)));
        }

        internal void RefreshBuildingSelection()
        {
            var entity = Sim.Find(sessionController.em, sessionController.selected);
            var has = entity != Entity.Null && sessionController.em.HasComponent<Building>(entity);
            var actionBarVisible = has && showBuildingActionBar && !worldController.HasBuildingPlacement && !sessionController.intel &&
                                   !(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Garrison);
            if (BuildingActionBar != null)
                BuildingActionBar.gameObject.SetActive(actionBarVisible && PositionActionBar(entity));
            if (!has)
            {
                showBuildingActionBar = false;
                DetailsPanel.Hide();
                hudController.Selection.text = "点击建筑显示操作条；WASD 镜头，滚轮缩放。";
                return;
            }

            var b = sessionController.em.GetComponentData<Building>(entity);
            var stats = sessionController.em.GetComponentData<BuildingStats>(entity);
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var d = Sim.Definition(sessionController.em, sessionController.root, id.Definition);
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;
            var normal = b.Stage == LifeStage.Operational;
            hudController.Selection.text = $"{id.Name} · LV{b.Level} · {UI_GamePanel_BuildingDetails.BuildingStageName(b.Stage)}";
            BuildingMoveButton.gameObject.SetActive(day && normal);
            BuildingMoveButton.interactable = BuildingOps.CheckMove(sessionController.em, sessionController.root, entity).Allowed;
            BuildingUpgradeButton.gameObject.SetActive(day && normal && b.Level < d.Level);
            BuildingUpgradeButton.interactable = BuildingOps.CheckUpgrade(sessionController.em, sessionController.root, entity).Allowed;
            BuildingRepairButton.gameObject.SetActive(day && b.Stage == LifeStage.Ruined);
            BuildingRepairButton.interactable = b.RuinPending == 0;
            BuildingDemolishButton.gameObject.SetActive(day && stats.IsCore == 0);
            BuildingDetailsButton.gameObject.SetActive(true);
            BuildingDetailsButton.interactable = true;
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
            if (!showBuildingActionBar || worldController.HasBuildingPlacement || sessionController.intel || navigation.IsPanelOpen && navigation.Panel == GamePanelId.Garrison)
            {
                BuildingActionBar.gameObject.SetActive(false);
                return;
            }
            var entity = Sim.Find(sessionController.em, sessionController.selected);
            BuildingActionBar.gameObject.SetActive(PositionActionBar(entity));
        }

        public void FocusBuilding(ulong id)
        {
            var entity = Sim.Find(sessionController.em, id);
            if (entity == Entity.Null)
                return;
            worldController.LocateHistory(id, Sim.Position(sessionController.em, entity));
            sessionController.selected = id;
            sessionController.nextRefresh = 0;
        }

        internal void ResetSession()
        {
            buildingBarOpen = showBuildingActionBar = showBuildingRange = false;
            ResourcePathCellCount = 0;
            BuildingConfirmPanel.SetActive(false);
            DetailsPanel.ResetSession();
            BuildingActionBar.gameObject.SetActive(false);
            BuildingBar.gameObject.SetActive(false);
        }
    }
}
