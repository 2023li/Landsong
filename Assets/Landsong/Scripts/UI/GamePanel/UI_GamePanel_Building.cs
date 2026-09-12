using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.ECS.Authoring;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using Text = TMPro.TextMeshProUGUI;
using System.Text;
using Unity.Transforms;
using UnityEngine.EventSystems;
using InputField = TMPro.TMP_InputField;
using Landsong.ECS.Persistence;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public sealed class UI_GamePanel_Building : Moyo.Unity.UIViewBase, IGameFeatureRenderer, IGameBuildingUi
    {
        public void BindFeature(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation, UI_GamePanel_RowRenderer rows)
        {
            if (session == null || commands == null || navigation == null || rows == null)
                throw new System.ArgumentException("功能展示器缺少会话服务。");
            sessionController = session;
            rowsController = rows;
            commandsController = commands;
            this.navigation = navigation;
        }

        public void Render() => BuildMenu();
        [Sirenix.OdinInspector.LabelText("工人信息模板")]
        public UI_GamePanel_WorkerInfoRow WorkerInfoTemplate;
        [Sirenix.OdinInspector.LabelText("劳动力模板")]
        public UI_GamePanel_WorkforceRow WorkforceTemplate;
        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_Hud hudController;
        internal UI_GamePanel_Marriage marriageController;
        internal UI_GamePanel_PersonRequests requestsController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal UI_GamePanel_WorldInteraction worldController;
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

            showBuildingDetails = false;
            if (BuildingDetailsPanel != null)
                BuildingDetailsPanel.SetActive(false);
            navigation.OpenPanel(GamePanelId.Building);
        }

        internal void InitializeBuildingCatalog()
        {
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

            BuildingBar.Bind(cards, worldController.BeginBuildingPlacement, !FeatureOps.Unlocked(sessionController.em, sessionController.root, "Building") ? "建造许可尚未解锁：先完成主线任务" : cards.Count == 0 ? "尚未拥有建筑蓝图" : "选择建筑后点击地图放置 · R 旋转 · 右键取消");
        }

        [Sirenix.OdinInspector.LabelText("建筑卡片")]
        public UI_GamePanel_BuildingDetails BuildingCard;
        public bool BuildingRangesVisible => showBuildingRange;
        public int ResourcePathCellCount { get; internal set; }

        internal void InitializeBuildingCard()
        {
            if (BuildingCard == null || BuildingCard.gameObject != BuildingDetailsPanel)
                throw new InvalidOperationException("建筑详情面板检查器引用错误：BuildingCard 必须绑定 BuildingDetailsPanel 上的 BuildingDetailsView。");
            NameInput.onEndEdit.AddListener(value =>
            {
                if (BuildingCard.BuildingId != 0)
                    commandsController.TryQueue(CommandRequests.RenameBuilding(BuildingCard.BuildingId, value));
            });
        }

        public void SelectBuildingDetails(ulong id)
        {
            if (!navigation.InputPolicy.Capture().CanNavigate)
                return;
            var entity = Sim.Find(sessionController.em, id);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity))
                return;
            navigation.ClosePanel();
            sessionController.selected = id;
            showBuildingDetails = true;
            showBuildingRange = true;
            worldController.rangeRevision = -1;
            sessionController.nextRefresh = 0;
        }

        internal void RefreshBuildingCard(Entity entity)
        {
            var card = BuildingCard;
            var baseOutputBlock = card.Block<UI_GamePanel_BuildingDetails_Block_基础产出>();
            var workforceBlock = card.Block<UI_GamePanel_BuildingDetails_Block_岗位>();
            var plantingBlock = card.Block<UI_GamePanel_BuildingDetails_Block_种植>();
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var b = sessionController.em.GetComponentData<Building>(entity);
            var stats = sessionController.em.GetComponentData<BuildingStats>(entity);
            var d = Sim.Definition(sessionController.em, sessionController.root, id.Definition);
            var session = sessionController.em.GetComponentData<Session>(sessionController.root);
            bool can = session.Phase == Phase.Day && session.Paused == 0 && session.CheckpointPending == 0;
            bool normal = Sim.Operational(sessionController.em, entity);
            card.Select(id.Id, id.Name.ToString());
            card.Name.interactable = can;
            card.Icon.sprite = BuildingSource(id.Definition)?.Icon;
            card.Icon.color = Color.white;
            card.Level.text = "LV" + b.Level;
            bool showHealth = stats.IsCore == 0;
            card.silder_HP条.gameObject.SetActive(showHealth);
            card.TMP_Text_HP_文本.gameObject.SetActive(showHealth);
            if (showHealth)
            {
                var health = sessionController.em.GetComponentData<Health>(entity);
                card.silder_HP条.minValue = 0;
                card.silder_HP条.maxValue = Mathf.Max(1, health.Maximum);
                card.silder_HP条.value = Mathf.Clamp(health.Current, 0, card.silder_HP条.maxValue);
                card.TMP_Text_HP_文本.text = $"耐久 {health.Current:0}/{health.Maximum:0}";
            }
            var xp = Sim.Rule(sessionController.em, sessionController.root, id.Definition, RuleKind.Experience, b.Level);
            int required = Mathf.Max(0, xp.B);
            bool full = required == 0 || b.Experience >= required;
            bool maximum = b.Level >= d.Level;
            UI_GamePanel_BuildingDetails.Span(card.ExperienceFill, 0, required > 0 ? (float)b.Experience / required : maximum ? 1 : 0);
            card.Experience.text = maximum && full ? "MAX" : b.Experience + " / " + required;
            card.Upgrade.gameObject.SetActive(!(maximum && full));
            var upgrade = BuildingOps.CheckUpgrade(sessionController.em, sessionController.root, entity);
            UI_GamePanel_BuildingDetails.Bind(card.Upgrade, () =>
            {
                if (!can)
                {
                    hudController.Message.text = "请在未暂停的白天升级";
                    return;
                }

                ConfirmBuildingCommand(CommandKind.Upgrade);
            });
            card.Upgrade.image.color = can && upgrade.Allowed ? new Color(.2f, .4f, .66f) : new Color(.34f, .36f, .36f);
            var outputs = new List<string>();
            if (stats.MaxPopulation + stats.BasePopulation > 0)
                outputs.Add("人口 +" + (stats.MaxPopulation + stats.BasePopulation));
            int research = 0;
            for (int i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                if (EconomyOps.Matches(r, RuleKind.ResearchOutput, b.Level))
                    research += r.Amount;
            }

            if (research > 0)
                outputs.Add("科研值 +" + research);
            if (stats.Garrison > 0)
                outputs.Add("士兵槽 " + stats.Garrison);
            for (int i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                if (EconomyOps.Matches(r, RuleKind.Warehouse, b.Level) && r.Amount > 0)
                    outputs.Add("库存 " + sessionController.Name(r.Target) + " ×" + r.Amount + "（工人≥" + r.B + "）");
            }

            int invitations = sessionController.em.HasBuffer<QuestOfferSlot>(entity) ? sessionController.em.GetBuffer<QuestOfferSlot>(entity).Length : 0;
            if (invitations > 0)
                outputs.Add("邀约槽 " + invitations);
            if (stats.QuestCapacity > 0)
                outputs.Add("任务槽位 " + stats.QuestCapacity);
            baseOutputBlock.Refresh(outputs, stats.JobCapacity > 0 ? () => WorkerDetails(entity) : null);
            card.ConfigureSidebar(card.ExperienceHover, stats.JobCapacity > 0 ? () => WorkerDetails(entity, "经验") : null);
            soldierController.RefreshBuildingGarrison(entity);
            var pos = Sim.Position(sessionController.em, entity);
            card.Footer.text = $"(x: {pos.x:0.#}, y: {pos.y:0.#}, z: {pos.z:0.#})  移动力: {BuildingRangeOps.ActionPower(sessionController.em, sessionController.root, entity)}";
            card.SetWarnings(BuildingWarnings(entity));
            bool skins = sessionController.em.HasBuffer<BuildingVisualSlot>(entity) && sessionController.em.GetBuffer<BuildingVisualSlot>(entity).Length > 0;
            UI_GamePanel_BuildingDetails.Bind(card.Style, skins ? () => BuildingSkins(id.Id) : null);
            var workforce = WorkforceOps.Quote(sessionController.em, sessionController.root, entity);
            workforceBlock.Refresh(workforce, sessionController.Name(workforce.Gold), can && normal,
                value => commandsController.TryQueue(CommandRequests.AdjustWorkforceBudget(id.Id, value - workforce.SubsidyCost)),
                can && normal && WorkforceOps.CanChange(workforce, 1) == ResultCode.Success
                    ? () => commandsController.TryQueue(CommandRequests.RecruitWorkers(id.Id, 1, workforce.RecruitCost)) : null,
                can && normal && WorkforceOps.CanChange(workforce, -1) == ResultCode.Success
                    ? () => commandsController.TryQueue(CommandRequests.ChangeWorkers(id.Id, -1)) : null,
                () => WorkerDetails(entity));
            bool crops = false;
            for (int i = 0; i < d.RuleCount; i++)
                if (Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i).Kind == RuleKind.Crop)
                    crops = true;
            if (crops)
            {
                bool planted = b.Crop >= 0;
                int duration = planted ? Mathf.Max(1, Sim.Definition(sessionController.em, sessionController.root, b.Crop).Duration) : 1;
                plantingBlock.Refresh(
                    planted ? "种植 · " + sessionController.Name(b.Crop) + " " + b.CropProgress + "/" + duration : "种植 · 点击圆钮选择作物",
                    planted ? (float)b.CropProgress / duration : 0,
                    planted ? CropPortrait(b.Crop) : null,
                    () => BuildingCrops(id.Id),
                    can && normal && planted ? () => ShowBuildingConfirmation("铲除 " + sessionController.Name(b.Crop), new[] { "失去当前作物与进度，不返种植费用。" }, () => commandsController.Send(CommandKind.ClearCrop, id.Id)) : null,
                    () => WorkerDetails(entity, "种植"));
            }
            else plantingBlock.Hide();
        }

        internal string BuildingWarnings(Entity e)
        {
            var b = sessionController.em.GetComponentData<Building>(e);
            var id = sessionController.em.GetComponentData<Identity>(e);
            var stats = sessionController.em.GetComponentData<BuildingStats>(e);
            var warnings = new List<string>();
            if (b.Stage != LifeStage.Operational)
                warnings.Add(BuildingStageName(b.Stage));
            if (stats.IsCore == 0)
            {
                var h = sessionController.em.GetComponentData<Health>(e);
                if (h.Current < h.Maximum)
                    warnings.Add($"耐久受损：{h.Current:0}/{h.Maximum:0}");
            }

            if (b.FoodFailures > 0)
                warnings.Add("居民连续缺粮 " + b.FoodFailures + " 回合");
            if (EconomyOps.WorkforceLocked(sessionController.em, id.Id))
                warnings.Add("远征在途，岗位、移动与升级锁定");
            var maintenance = BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Maintenance, b.Level);
            if (maintenance.Count > 0 && b.Maintained == 0)
                warnings.Add("维护未满足：" + CostText(maintenance));
            void Shortage(string label, IEnumerable<BuildingCost> costs)
            {
                foreach (var cost in costs)
                {
                    int missing = cost.Amount - InventoryOps.Count(sessionController.em, sessionController.root, cost.Item);
                    if (missing > 0)
                        warnings.Add(label + "：" + sessionController.Name(cost.Item) + " 缺 " + missing);
                }
            }

            if (b.Stage == LifeStage.Operational)
            {
                Shortage("下次维护材料不足", maintenance);
                Shortage("生产原料不足", BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Input, b.Level));
            }

            if (b.Stage == LifeStage.Construction)
                Shortage("下期施工材料不足", BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.ConstructionCost, b.Progress + 1));
            if (b.Crop >= 0 && b.Workers < Sim.Definition(sessionController.em, sessionController.root, b.Crop).Population)
                warnings.Add("作物停止生长：工人不足");
            var q = WorkforceOps.Quote(sessionController.em, sessionController.root, e);
            if (q.Capacity > 0)
            {
                if (b.Workers == 0)
                    warnings.Add("没有工人入驻");
                if (q.SubsidyCost > q.Stock)
                    warnings.Add("下次补贴资金不足");
                if (q.Workers > q.CurrentStable)
                    warnings.Add("工人数超过当前可稳定人数，可能离职");
            }

            var definition = Sim.Definition(sessionController.em, sessionController.root, id.Definition);
            bool needsNetwork = maintenance.Count > 0 || BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Input, b.Level).Count > 0;
            if (b.Stage == LifeStage.Construction)
                needsNetwork |= BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.ConstructionCost, b.Progress + 1).Count > 0;
            if (b.Stage == LifeStage.Repairing)
            {
                var repair = BuildingCostOps.QuoteRepair(sessionController.em, sessionController.root, e);
                if (repair.Payments.Any(p => p.Missing > 0))
                    warnings.Add("修复材料不足");
                needsNetwork |= repair.NeedsNetwork;
            }

            if (needsNetwork && ResourceNetworkOps.Provider(sessionController.em, sessionController.root, e) == Entity.Null)
                warnings.Add("无法连接资源提供点，需要普通库存的生产/维护/施工会暂停");
            for (int i = 0; i < definition.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, definition.RuleStart + i);
                if (r.Level != 0 && r.Level != b.Level)
                    continue;
                if (r.Kind == RuleKind.Production && b.Workers < r.B)
                    warnings.Add("生产缺少工人：需要 " + r.B);
                if (r.Kind == RuleKind.Environment && EconomyOps.SpatialValue(sessionController.em, sessionController.root, e, r.B) < r.Amount)
                    warnings.Add(EnvironmentName(r.B) + "不足：需要 " + r.Amount);
            }

            return string.Join("\n", warnings.Distinct().Select(w => "• " + w));
        }

        internal Sprite CropPortrait(int definition)
        {
            var icon = BuildingSource(definition)?.Icon;
            if (icon != null)
                return icon;
            var harvest = Sim.Rule(sessionController.em, sessionController.root, definition, RuleKind.RewardItem);
            return harvest.Target >= 0 ? BuildingSource(harvest.Target)?.Icon : null;
        }

        internal void BuildingChoice(string title)
        {
            ShowBuildingConfirmation(title, Array.Empty<string>(), () =>
            {
            });
            rowsController.Clear(BuildingConfirmRows);
            rowsController.Row(title, parent: BuildingConfirmRows);
        }

        internal void BuildingSkins(ulong key)
        {
            var e = Sim.Find(sessionController.em, key);
            if (e == Entity.Null || !sessionController.em.HasBuffer<BuildingVisualSlot>(e))
                return;
            BuildingChoice("选择建筑皮肤");
            var skins = new HashSet<string>();
            var b = sessionController.em.GetComponentData<Building>(e);
            bool can = courtController.CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0 && Sim.Operational(sessionController.em, e);
            foreach (var slot in sessionController.em.GetBuffer<BuildingVisualSlot>(e))
                if (slot.Purpose == BuildingVisualPurpose.Operational && skins.Add(slot.Skin.ToString()))
                {
                    var skin = slot.Skin.ToString();
                    rowsController.Row((skin == b.Skin.ToString() ? "✓ " : "") + (skin.Length == 0 ? "默认" : skin), can ? () =>
                    {
                        BuildingConfirmPanel.SetActive(false);
                        commandsController.TryQueue(CommandRequests.ChangeBuildingSkin(key, skin));
                    } : null, parent: BuildingConfirmRows, key: "building-skin:" + key + ":" + skin);
                }

            rowsController.Row("关闭", () => BuildingConfirmPanel.SetActive(false), parent: BuildingConfirmRows);
        }

        internal void BuildingCrops(ulong key)
        {
            var e = Sim.Find(sessionController.em, key);
            if (e == Entity.Null)
                return;
            var b = sessionController.em.GetComponentData<Building>(e);
            var d = Sim.Definition(sessionController.em, sessionController.root, sessionController.em.GetComponentData<Identity>(e).Definition);
            BuildingChoice("选择作物");
            if (b.Crop >= 0)
                rowsController.Row("已种植 " + sessionController.Name(b.Crop) + "，更换前请先使用 X 铲除。", parent: BuildingConfirmRows);
            var seen = new HashSet<int>();
            for (int i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                if (!EconomyOps.Matches(r, RuleKind.Crop, b.Level) || !seen.Add(r.Target))
                    continue;
                int crop = r.Target;
                var costs = BuildingCostOps.Rules(sessionController.em, sessionController.root, crop, RuleKind.PlacementCost, 1);
                bool can = courtController.CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0 && Sim.Operational(sessionController.em, e) && b.Crop < 0 && BuildingCostOps.CanPay(sessionController.em, sessionController.root, costs);
                rowsController.Row(sessionController.Name(crop) + " · " + CostText(costs) + " · " + Sim.Definition(sessionController.em, sessionController.root, crop).Duration + " 回合", can ? () =>
                {
                    BuildingConfirmPanel.SetActive(false);
                    commandsController.TryQueue(CommandRequests.PlantCrop(key, crop));
                } : null, parent: BuildingConfirmRows, key: "building-crop:" + key + ":" + crop);
            }

            rowsController.Row("关闭", () => BuildingConfirmPanel.SetActive(false), parent: BuildingConfirmRows);
        }

        [Header("Building workflow (presentation only)")]
        [Sirenix.OdinInspector.LabelText("建筑目录")]
        public GameCatalogAsset BuildingCatalog;
        [Sirenix.OdinInspector.LabelText("建筑工具栏")]
        public RectTransform BuildingToolbar;
        [Sirenix.OdinInspector.LabelText("建筑确认条目容器")]
        public RectTransform BuildingConfirmRows;
        public RectTransform BuildingDetailsRows => BuildingCard.Block<UI_GamePanel_BuildingDetails_Block_其他>().Rows;

        [Sirenix.OdinInspector.LabelText("建筑详情面板")]
        public GameObject BuildingDetailsPanel;
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
        [Sirenix.OdinInspector.LabelText("建筑详情关闭")]
        public Button BuildingDetailsClose;
        [Sirenix.OdinInspector.LabelText("建筑提示")]
        public Text BuildingHint;
        [Sirenix.OdinInspector.LabelText("建筑确认标题")]
        public Text BuildingConfirmTitle;
        [Sirenix.OdinInspector.LabelText("建筑确认组")]
        public CanvasGroup BuildingConfirmGroup;
        [Sirenix.OdinInspector.LabelText("建筑放置面板")]
        public GameObject BuildingPlacementPanel;
        internal bool showBuildingDetails;
        internal bool showBuildingRange;
        public ulong SelectedBuildingId => sessionController.selected;

        internal void InitializeBuildings()
        {
            if (BuildingHint != null)
            {
                BuildingHint.text = "";
                BuildingPlacementPanel.SetActive(false);
            }

            if (BuildingDetailsPanel == null)
                return;
            BuildingDetailsClose.onClick.AddListener(() =>
            {
                showBuildingDetails = false;
                rowsController.Clear(BuildingDetailsRows);
                BuildingDetailsPanel.SetActive(false);
                sessionController.nextRefresh = 0;
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
            InitializeBuildingCard();
            BuildingToolbar.gameObject.SetActive(false);
            BuildingDetailsPanel.SetActive(false);
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

            if (showBuildingDetails)
            {
                showBuildingDetails = false;
                BuildingDetailsPanel.SetActive(false);
                sessionController.nextRefresh = 0;
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

        internal void ConfirmBuildingCommand(CommandKind kind)
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

        internal void BuildCatalogRows()
        {
            if (!FeatureOps.Unlocked(sessionController.em, sessionController.root, "Building"))
                rowsController.Row("建造许可尚未解锁。先完成镜头与收集材料的主线任务；地图上的资源堆仍可采集。", () => navigation.OpenPanel(GamePanelId.Quest));
        }

        internal void RefreshBuildingDetails()
        {
            var entity = Sim.Find(sessionController.em, sessionController.selected);
            var has = entity != Entity.Null && sessionController.em.HasComponent<Building>(entity);
            if (BuildingToolbar != null)
                BuildingToolbar.gameObject.SetActive(has && showBuildingDetails && !worldController.HasBuildingPlacement && !sessionController.intel && !(navigation.IsPanelOpen && navigation.Panel == GamePanelId.Garrison));
            if (!has)
            {
                hudController.Selection.text = "点击建筑打开详情；WASD 镜头，滚轮缩放。";
                if (BuildingDetailsPanel != null)
                    BuildingDetailsPanel.SetActive(false);
                return;
            }

            var b = sessionController.em.GetComponentData<Building>(entity);
            var stats = sessionController.em.GetComponentData<BuildingStats>(entity);
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var d = Sim.Definition(sessionController.em, sessionController.root, id.Definition);
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;
            var normal = b.Stage == LifeStage.Operational;
            hudController.Selection.text = $"{id.Name} · LV{b.Level} · {BuildingStageName(b.Stage)}";
            if (BuildingToolbar == null)
                return;
            BuildingMoveButton.gameObject.SetActive(day && normal);
            BuildingMoveButton.interactable = BuildingOps.CheckMove(sessionController.em, sessionController.root, entity).Allowed;
            BuildingUpgradeButton.gameObject.SetActive(day && normal && b.Level < d.Level);
            BuildingUpgradeButton.interactable = BuildingOps.CheckUpgrade(sessionController.em, sessionController.root, entity).Allowed;
            BuildingRepairButton.gameObject.SetActive(day && b.Stage == LifeStage.Ruined);
            BuildingRepairButton.interactable = b.RuinPending == 0;
            BuildingDemolishButton.gameObject.SetActive(day && stats.IsCore == 0);
            BuildingDetailsPanel.SetActive(showBuildingDetails && !sessionController.intel);
            NameInput.interactable = day;
            if (!showBuildingDetails || sessionController.intel)
                return;
            rowsController.Clear(BuildingDetailsRows);
            void Detail(string text, Action action = null, string workers = null, string key = null,
                [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
            {
                if (workers == null)
                    rowsController.Row(text, action, parent: BuildingDetailsRows);
                else
                {
                    var row = rowsController.Item(WorkerInfoTemplate, text, action, BuildingDetailsRows,
                        key: "building:" + id.Id + ":worker:" + workers + ":" + (key ?? sourceLine.ToString()));
                    if (row == null || !row.CanRebind) return;
                    BuildingCard.ConfigureSidebar(row.WorkerHover, () => WorkerDetails(entity, workers));
                }
            }

            RefreshBuildingCard(entity);
            Detail("查看本建筑账本 / 参考预测", () => navigation.OpenEconomy(id.Id));
            var source = BuildingSource(id.Definition);
            if (!string.IsNullOrEmpty(source?.Description))
                Detail(source.Description);
            var provider = ResourceNetworkOps.Provider(sessionController.em, sessionController.root, entity);
            if (b.Stage == LifeStage.Construction)
            {
                Detail($"施工 {b.Progress}/{d.Duration} 回合");
                Detail("下期材料：" + CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.ConstructionCost, b.Progress + 1)));
                if (provider == Entity.Null)
                    Detail("需要从正常库存支付的施工：断连时暂停。");
            }

            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing)
            {
                BuildingCostOps.RepairTotal(sessionController.em, sessionController.root, entity, out var duration);
                Detail(b.Stage == LifeStage.Ruined ? "荒废：无建筑功能；修复无需工人。" : $"修复 {b.Progress}/{b.RepairDuration} 回合，材料不足或断连暂停。");
                if (b.RuinPending != 0)
                    Detail($"本夜已失效，黎明提交居民损失 {b.Population} / 工人失业 {b.Workers}。库存已标记损失。");
                RepairDetails(entity, (text, action) => Detail(text, action));
                if (day && b.Stage == LifeStage.Ruined)
                    Detail("开始修复（" + duration + " 回合）", () => ConfirmBuildingCommand(CommandKind.Repair));
                return;
            }

            var maintenance = BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Maintenance, b.Level);
            if (maintenance.Count > 0)
                Detail("每回合维护：" + CostText(maintenance) + (b.Maintained != 0 ? " · 已满足" : " · 未满足"));
            if (stats.MaxPopulation > 0)
            {
                Detail($"居住 {b.Population}/{stats.MaxPopulation} · 增长进度 {b.Growth} · 税收进度 {b.TaxProgress} · 连续缺粮 {b.FoodFailures}");
                if (b.DeferredResidents > 0)
                    Detail("修复迁入人口将在黎明加入：" + b.DeferredResidents);
                foreach (var food in sessionController.em.GetBuffer<FoodSelection>(entity))
                    Detail("上次实际食物：" + sessionController.Name(food.Item) + " × " + food.Amount);
            }

            var production = Sim.Rule(sessionController.em, sessionController.root, id.Definition, RuleKind.Production, b.Level);
            if (production.Level >= 0)
            {
                Detail($"生产周期进度 {b.ProductionProgress}/{production.Amount} · 所需工人 {production.B}", workers: "生产");
                Detail("原料：" + CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Input, b.Level)), workers: "生产");
            }

            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                if (r.Level != 0 && r.Level != b.Level)
                    continue;
                if (r.Kind == RuleKind.ProductionTier)
                    Detail("产品：" + sessionController.Name(r.Target) + " × " + r.Amount + " · 工人 " + r.B + (r.C > 0 ? "～" + r.C : " 以上") + (b.Workers >= r.B && (r.C <= 0 || b.Workers <= r.C) ? "（当前档）" : ""), workers: "生产", key: "output:" + i);
                if (r.Kind == RuleKind.RareOutput)
                    Detail($"随机产出：{sessionController.Name(r.Target)} ×{r.Amount} · 概率 {r.Value:P0} · 工人 ≥{r.B}", workers: "生产", key: "random:" + i);
                if (r.Kind == RuleKind.Food)
                    Detail($"食谱：{sessionController.Name(r.Target)} · {r.Amount} 种 · 每居民每种 {math.max(1, r.B)}");
                if (r.Kind == RuleKind.Environment)
                    Detail($"环境条件 {EnvironmentName(r.B)} ≥ {r.Amount} · 当前 {EconomyOps.SpatialValue(sessionController.em, sessionController.root, entity, r.B):0.#}");
                if (r.Kind == RuleKind.SpatialEffect)
                    Detail($"作用 {EnvironmentName(r.B)} +{r.Amount} · 范围 {r.Value} 格 · 工人 ≥{r.C}", workers: "空间效果", key: "spatial:" + i);
                if (r.Kind == RuleKind.Market)
                    Detail("市场结算比例：" + r.Value + " · 本回合归因价值 " + b.MarketValue, workers: "邀约", key: "market:" + i);
                if (r.Kind == RuleKind.Harvest)
                    Detail("剩余采集次数：" + b.HarvestRemaining, day && normal ? () => commandsController.TryQueue(CommandRequests.Harvest(id.Id)) : null);
            }

            if (b.Crop >= 0)
            {
                Detail($"作物 {sessionController.Name(b.Crop)} · 生长 {b.CropProgress}/{Sim.Definition(sessionController.em, sessionController.root, b.Crop).Duration}", workers: "种植");
                if (day && normal)
                {
                    Detail("收获", () => commandsController.TryQueue(CommandRequests.Harvest(id.Id)));
                    Detail(b.AutoHarvest != 0 ? "关闭自动收获" : "开启自动收获", () => commandsController.TryQueue(CommandRequests.SetAutoHarvest(id.Id, (b.AutoHarvest == 0 ? 1 : 0) != 0)));
                }
            }

            if (stats.Garrison > 0)
            {
                if (normal && (sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Night || sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Retreat))
                {
                    Detail("召回所属士兵", () => commandsController.TryQueue(CommandRequests.RecallGarrison(id.Id)));
                    Detail("取消途中召回", () => commandsController.TryQueue(CommandRequests.RecallGarrison(id.Id, true)));
                }
            }

            if (stats.BellRadius > 0)
                Detail("警铃集结范围 " + stats.BellRadius, !day && normal ? () => commandsController.Send(CommandKind.Bell, id.Id) : null);
            if (stats.Intelligence > 0)
                Detail("情报贡献 " + stats.Intelligence, () => navigation.OpenPanel(GamePanelId.Intelligence));
            if (Sim.Rule(sessionController.em, sessionController.root, id.Definition, RuleKind.ExpeditionSite, b.Level).Level >= 0)
                Detail(EconomyOps.WorkforceLocked(sessionController.em, id.Id) ? "远征在途，岗位/移动/升级锁定" : "打开远征", () => navigation.OpenPanel(GamePanelId.Expedition), workers: "远征");
            if (stats.HeroDefinition >= 0)
            {
                var hero = Sim.Definition(sessionController.em, sessionController.root, stats.HeroDefinition);
                Detail($"{hero.Name} · 招募 {hero.Cost} 金币 · 人口 {hero.Population} · 神殿所需工人 {stats.RequiredWorkers}", workers: "神殿");
                Detail("供奉：" + CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, stats.HeroDefinition, RuleKind.Supply, 1)) + " · 唤醒：" + CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, stats.HeroDefinition, RuleKind.WakeCost, 1)));
                Detail($"当前工人 {b.Workers}/{stats.RequiredWorkers} · 持续供奉{(b.Offering == 0 ? "关闭" : "开启")} · 本回合供奉{(b.PaidOfferingTurn == sessionController.em.GetComponentData<Session>(sessionController.root).Turn ? "已支付" : "未支付")}", workers: "神殿");
                Detail("供奉在平安夜也消耗资源；唤醒另外付费，未实际参战不获得战斗经验。英雄阵亡后经验清零，冷却结束重招支付完整费用。缺工不会杀死英雄，神殿荒废/拆除会。");
                bool wake = !day;
                string reason = MilitaryOps.HeroAvailability(sessionController.em, sessionController.root, entity, wake);
                Detail((wake ? "唤醒英雄" : "招募英雄") + (reason.Length > 0 ? " · " + reason : ""), reason.Length == 0 ? () => commandsController.Send(wake ? CommandKind.WakeHero : CommandKind.RecruitHero, id.Id) : null);
                if (normal && day)
                    Detail(b.Offering == 0 ? "开启持续供奉" : "关闭持续供奉", () => commandsController.TryQueue(CommandRequests.SetOffering(id.Id, (b.Offering == 0 ? 1 : 0) != 0)));
            }
        }

        internal static string EnvironmentName(int kind) => kind == 20 ? "美观" : kind == 30 ? "医疗" : kind == 40 ? "治安" : kind == 10 ? "生产/作物收益百分比" : "效果 " + kind;
        internal static string BuildingStageName(LifeStage stage) => stage == LifeStage.Construction ? "施工中" : stage == LifeStage.Ruined ? "建筑荒废" : stage == LifeStage.Repairing ? "修复中" : "正常运营";
        public void FocusBuilding(ulong id)
        {
            var entity = Sim.Find(sessionController.em, id);
            if (entity == Entity.Null)
                return;
            worldController.LocateHistory(id, Sim.Position(sessionController.em, entity));
            sessionController.selected = id;
            sessionController.nextRefresh = 0;
        }

        [Sirenix.OdinInspector.LabelText("名称输入框")]
        public InputField NameInput;
        internal void Details() => RefreshBuildingDetails();
        internal void BuildMenu() => BuildCatalogRows();
        internal string WorkerDetails(Entity entity, string module = "全部") => WorkerEfficiencyOps.Describe(sessionController.em, sessionController.root, entity, module);
        internal UI_GamePanel_WorkforceScale workforceScale;
        internal bool showSupplySources;
        internal bool showAttractionSources;
        internal bool showSpatialSources;
        internal void WorkforceDetails(Entity entity, bool editable)
        {
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var q = WorkforceOps.Quote(sessionController.em, sessionController.root, entity);
            void Detail(string label, Action action = null) => rowsController.Row(label, action, parent: BuildingDetailsRows);
            Detail($"岗位 · 实际 {q.Workers}/{q.Capacity} · 当前可稳定 {q.CurrentStable} · 目标 {q.Target} · 付款后预计稳定 {q.PlannedStable}");
            Detail($"每回合预计补贴：-{q.SubsidyCost} {sessionController.Name(q.Gold)} · 当前正常库存 {q.Stock}" + (q.Stock < q.SubsidyCost ? "（不足，付款失败不会获得加成）" : ""));
            Detail(q.PaidTurn > 0 ? $"最近白天 {q.PaidTurn} 已付补贴：{q.Paid} {sessionController.Name(q.Gold)}" : "尚未支付过岗位补贴");
            Detail("目标只设置未来补贴，不立即扣钱或招人。低于环境稳定人数时不补贴，也不会强制裁员。结算先维护再算补贴，实际费用可能变化。");
            var row = rowsController.Item(WorkforceTemplate, "", parent: BuildingDetailsRows, key: "workforce:" + id.Id);
            if (row == null) return;
            workforceScale = row.Workforce;
            if (workforceScale == null)
                throw new InvalidOperationException("通用行模板缺少 WorkforceScale 引用。");
            workforceScale.gameObject.SetActive(true);
            row.Layout.preferredHeight = row.Layout.minHeight = 100;
            if (row.CanRebind) workforceScale.Bind(q, editable && !q.Locked, value => commandsController.TryQueue(CommandRequests.SetWorkforceTarget(id.Id, value)));
            Detail("绿：环境稳定范围；黄：目标补贴；橙：整数金币产生的溢出；青线：实际工人；白柄/数字：目标人数。刻度按真实人数定位。");
            Detail($"当前招募 1 人：{q.RecruitCost} {sessionController.Name(q.Gold)} · 空闲人口 {q.FreePopulation}");
            if (WorkforceOps.CanChange(q, 1) != ResultCode.Success)
                Detail("招工限制：" + WorkforceOps.Reason(q, 1));
            Detail(q.Locked ? "远征在途：人数与补贴目标锁定。" : $"自然招入/离职最多每次结算 1 人；当前离职保护剩余 {q.ProtectionTurns} 次结算。普通岗位和军事单位均占人口，不能挪用军事人口。");
            Detail(showAttractionSources ? "收起吸引力来源" : "展开吸引力来源", () =>
            {
                showAttractionSources = !showAttractionSources;
                sessionController.nextRefresh = 0;
            });
            if (showAttractionSources)
            {
                foreach (var source in q.Sources)
                {
                    var s = source;
                    var label = s.Building != 0 ? sessionController.EntityName(s.Building) : s.Definition >= 0 ? sessionController.Name(s.Definition) : "";
                    rowsController.Row($"{s.Label} · {label}：{s.Value:+0.##;-0.##;0}", s.Building != 0 ? () => FocusBuilding(s.Building) : null, parent: BuildingDetailsRows, key: "attraction:" + id.Id + ":" + s.Building + ":" + s.Definition + ":" + s.Label);
                }

                Detail($"未截断合计 {q.Raw:0.##} → 环境吸引力 {q.Natural:0.##}（0～100）\n已付补贴加成 {q.Paid * q.PerGold:0.##} → 当前 {q.Current:0.##}；计划付款后 {q.Planned:0.##}");
            }
        }

        internal void RepairDetails(Entity entity, Action<string, Action> detail)
        {
            var q = BuildingCostOps.QuoteRepair(sessionController.em, sessionController.root, entity);
            detail((sessionController.em.GetComponentData<Building>(entity).Stage == LifeStage.Repairing ? "已冻结修复总额：" : "拟定修复总额：") + CostText(q.Total) + " · 尚需 " + CostText(q.Remaining), null);
            detail($"下一期 {q.Step + 1}/{q.Duration}：{q.Reason}", null);
            foreach (var p in q.Payments)
                detail($"{sessionController.Name(p.Item)}：本期需 {p.Required} = 待存放 {p.Pending} + 正常库存 {p.Normal}；正常可用 {p.Available}；缺口 {p.Missing}", null);
            detail("以上为当前资源预览，不预留材料；同回合较早结算的建筑仍可能先用这些物资。修复开始只冻结计划，不立即付款。", null);
        }

        internal void SupplyDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSupplySources ? "收起供给来源" : "展开供给来源", () =>
            {
                showSupplySources = !showSupplySources;
                sessionController.nextRefresh = 0;
            });
            if (!showSupplySources)
                return;
            var q = ResourceNetworkOps.Quote(sessionController.em, sessionController.root, entity);
            detail("先比较提供点优先级，再比较道路加权距离，同值按稳定建筑 ID。提供点用于连接/市场归因，材料仍从全城正常库存扣除。", null);
            foreach (var c in q.Candidates)
            {
                var candidate = c;
                detail($"{sessionController.EntityName(c.Id)} · 优先级 {c.Priority} · 路径成本 {(float.IsInfinity(c.Cost) ? "不可达" : c.Cost.ToString("0.##"))} · {(q.Selected == c.Entity ? "已选中" : c.Reason)}", () => FocusBuilding(candidate.Id));
            }

            if (q.Candidates.Count == 0)
                detail("没有其他资源提供点。", null);
        }

        internal void SpatialDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSpatialSources ? "收起空间效果来源" : "展开空间效果来源", () =>
            {
                showSpatialSources = !showSpatialSources;
                sessionController.nextRefresh = 0;
            });
            if (!showSpatialSources)
                return;
            foreach (var kind in new[]
            {
                10,
                20,
                30,
                40
            }

            )
            {
                var q = SpatialOps.Quote(sessionController.em, sessionController.root, entity, kind);
                detail(EnvironmentName(kind) + " · 实际合计 " + q.Value, null);
                foreach (var source in q.Sources)
                {
                    var s = source;
                    detail($"{sessionController.EntityName(s.Source)} · {s.Group} · 配置 {s.Amount} / 计入 {s.Applied} · {s.Reason}", () => FocusBuilding(s.Source));
                }
            }
        }

        internal void ResetSession()
        {
            buildingBarOpen = showBuildingDetails = showBuildingRange = false;
            showSupplySources = showAttractionSources = showSpatialSources = false;
            workforceScale = null;
            ResourcePathCellCount = 0;
            BuildingConfirmPanel.SetActive(false);
            BuildingDetailsPanel.SetActive(false);
            BuildingBar.gameObject.SetActive(false);
        }
    }
}
