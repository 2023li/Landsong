using Landsong.ECS.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.Content;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Stable controls preserve input focus while the simulation refreshes their read models.
    public sealed class UI_GamePanel_BuildingDetails : MonoBehaviour
    {
        [Sirenix.OdinInspector.LabelText("物品显示目录"), Sirenix.OdinInspector.Required]
        public ItemDisplayCatalog Items;
        [Sirenix.OdinInspector.LabelText("作物显示目录"), Sirenix.OdinInspector.Required]
        public CropDisplayCatalog Crops;
        internal UI_GamePanel_Economy economy;
        internal WorldSelectionState worldSelection;
        internal IntelligenceViewState intelligence;
        internal GameUiRefreshScheduler refresh;
        [LabelText("名称"), Required]
        public TMP_InputField Name;
        [LabelText("图标"), Required]
        public Image Icon;
        [LabelText("经验填充"), Required]
        public Image ExperienceFill;
        [LabelText("等级"), Required]
        public TMP_Text Level;
        [LabelText("经验"), Required]
        public TMP_Text Experience;
        [LabelText("耐久条"), Required]
        public Slider silder_HP条;
        [LabelText("耐久文本"), Required]
        public TMP_Text TMP_Text_HP_文本;
        [LabelText("页脚"), Required]
        public TMP_Text Footer;
        [LabelText("关闭"), Required]
        public Button Close;
        [LabelText("样式"), Required]
        public Button Style;
        [LabelText("升级"), Required]
        public Button Upgrade;
        [LabelText("警告"), Required]
        public Button Warning;
        [LabelText("经验跟踪"), Required]
        public GameObject ExperienceTrack;
        [LabelText("经验悬浮信息"), Required]
        public UI_GamePanel_BuildingDetails_SidebarTrigger ExperienceHover;
        [LabelText("模块滚动视图"), Required]
        public ScrollRect ModulesScroll;
        [LabelText("建筑模块"), Required]
        public List<UI_GamePanel_BuildingDetails_Block> Blocks = new List<UI_GamePanel_BuildingDetails_Block>();
        [FormerlySerializedAs("WorkerSidebarScroll")]
        [LabelText("侧栏滚动视图"), Required]
        public ScrollRect SidebarScroll;
        [FormerlySerializedAs("WorkerSidebarLayout")]
        [LabelText("侧栏布局"), Required]
        public LayoutElement SidebarLayout;
        [LabelText("提示框"), Required]
        public GameObject Tooltip;
        [LabelText("提示框文字"), Required]
        public TMP_Text TooltipText;
        [FormerlySerializedAs("WorkerSidebar")]
        [LabelText("侧栏"), Required]
        public GameObject Sidebar;
        [FormerlySerializedAs("WorkerSidebarText")]
        [LabelText("侧栏文字"), Required]
        public TMP_Text SidebarText;
        Component sidebarOwner;
        Func<string> sidebarContent;
        float sidebarHideAt, sidebarRefreshAt;
        bool sidebarHovered;
        public ulong BuildingId { get; private set; }

        string warningText;
        public T Block<T>()
            where T : UI_GamePanel_BuildingDetails_Block
        {
            T result = null;
            foreach (var block in Blocks)
                if (block is T typed)
                {
                    if (result != null)
                        throw new InvalidOperationException("建筑详情面板重复配置模块：" + typeof(T).Name);
                    result = typed;
                }

            if (result == null)
                throw new InvalidOperationException("建筑详情面板缺少模块：" + typeof(T).Name);
            return result;
        }

        public static void Span(Image image, float start, float end)
        {
            image.rectTransform.anchorMin = new Vector2(Mathf.Clamp01(start), 0);
            image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(end), 1);
            image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        }

        public void Select(ulong id, string name)
        {
            if (BuildingId != id)
            {
                BuildingId = id;
                ShowWarnings(false);
                HideSidebar();
                ModulesScroll.verticalNormalizedPosition = 1;
            }

            if (!Name.isFocused)
                Name.SetTextWithoutNotify(name);
        }

        public void SetWarnings(string value)
        {
            warningText = value;
            Warning.gameObject.SetActive(value.Length > 0);
            TooltipText.text = value;
            if (value.Length == 0)
                ShowWarnings(false);
        }

        public void ShowWarnings(bool show)
        {
            if (show)
                HideSidebar();
            Tooltip.SetActive(show && warningText.Length > 0);
            if (Tooltip.activeSelf)
                Tooltip.transform.SetAsLastSibling();
        }

        void OnDisable()
        {
            if (Tooltip != null)
                Tooltip.SetActive(false);
            HideSidebar();
        }

        public void ConfigureSidebar(UI_GamePanel_BuildingDetails_SidebarTrigger trigger, Func<string> content)
        {
            trigger.Content = content;
            if (sidebarOwner == trigger && content == null)
                HideSidebar();
        }

        public void ShowSidebar(Component owner, Func<string> content)
        {
            if (owner == null)
                throw new ArgumentNullException(nameof(owner));
            if (content == null)
                return;
            ShowWarnings(false);
            sidebarOwner = owner;
            sidebarContent = content;
            sidebarHideAt = 0;
            sidebarRefreshAt = 0;
            Sidebar.SetActive(true);
            Sidebar.transform.SetAsLastSibling();
            SidebarScroll.verticalNormalizedPosition = 1;
            RefreshSidebar();
        }

        public void LeaveSidebar(Component owner)
        {
            if (sidebarOwner == owner)
                sidebarHideAt = Time.unscaledTime + .18f;
        }

        public void SetSidebarHovered(bool value)
        {
            sidebarHovered = value;
            if (!value)
                sidebarHideAt = Time.unscaledTime + .18f;
        }

        public void HideSidebar()
        {
            sidebarOwner = null;
            sidebarContent = null;
            sidebarHovered = false;
            sidebarHideAt = 0;
            if (Sidebar != null)
                Sidebar.SetActive(false);
        }

        void LateUpdate()
        {
            if (sidebarOwner == null)
            {
                if (Sidebar.activeSelf)
                    HideSidebar();
                return;
            }

            var content = sidebarOwner is UI_GamePanel_BuildingDetails_SidebarTrigger trigger ? trigger.Content : sidebarContent;
            if (!sidebarOwner.gameObject.activeInHierarchy || content == null || (!sidebarHovered && sidebarHideAt > 0 && Time.unscaledTime >= sidebarHideAt))
            {
                HideSidebar();
                return;
            }

            sidebarContent = content;
            if (Time.unscaledTime >= sidebarRefreshAt)
                RefreshSidebar();
        }

        void RefreshSidebar()
        {
            sidebarRefreshAt = Time.unscaledTime + .3f;
            SidebarText.text = sidebarContent();
            SidebarLayout.preferredHeight = SidebarText.GetPreferredValues(SidebarText.text, Mathf.Max(180, ((RectTransform)Sidebar.transform).rect.width - 44), float.PositiveInfinity).y + 16;
        }

        public static void Bind(Button b, Action action)
        {
            b.onClick.RemoveAllListeners();
            b.interactable = action != null;
            if (action != null)
                b.onClick.AddListener(() => action());
        }

        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_Hud hudController;
        internal IGameUiNavigation navigation;
        UI_GamePanel_RowCollection rowsController;
        internal GameUiSessionHandle sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal IGameBuildingUi buildingUi;
        internal bool showSupplySources;
        internal bool showSpatialSources;
        bool isOpen;
        [LabelText("详情条目模板"), Required]
        public UI_GamePanel_Row RowTemplate;
        public RectTransform DetailsRows => Block<UI_GamePanel_BuildingDetails_Block_其他>().Rows;
        public bool IsOpen => isOpen;

        internal void BindPresenter(GameUiSessionHandle session, GameUiCommandWriter commands, IGameUiNavigation navigation, IGameBuildingUi buildingUi, UI_GamePanel_Court court, UI_GamePanel_Hud hud, UI_GamePanel_Soldier soldier)
        {
            sessionController = session ?? throw new ArgumentNullException(nameof(session));
            commandsController = commands ?? throw new ArgumentNullException(nameof(commands));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            if (RowTemplate == null)
                throw new InvalidOperationException("建筑详情缺少条目模板。");
            RowTemplate.ValidateConfiguration();
            rowsController = new UI_GamePanel_RowCollection(RowTemplate);
            this.buildingUi = buildingUi ?? throw new ArgumentNullException(nameof(buildingUi));
            courtController = court ?? throw new ArgumentNullException(nameof(court));
            hudController = hud ?? throw new ArgumentNullException(nameof(hud));
            soldierController = soldier ?? throw new ArgumentNullException(nameof(soldier));
        }

        internal void Initialize()
        {
            Close.onClick.AddListener(() => Hide());
            Name.onEndEdit.AddListener(value =>
            {
                if (BuildingId != 0)
                    commandsController.TryQueue(new RenameBuildingRequest { Building = BuildingId, Name = new Unity.Collections.FixedString128Bytes(BuildingNaming.SanitizeName(value)) });
            });
            gameObject.SetActive(false);
        }

        internal void Open()
        {
            var selected = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            if (selected == Entity.Null || !sessionController.em.HasComponent<Building>(selected))
                return;
            isOpen = true;
            gameObject.SetActive(true);
            refresh.NextPanel = 0;
        }

        internal bool Hide()
        {
            var wasOpen = isOpen || gameObject.activeSelf;
            isOpen = false;
            rowsController.Clear(DetailsRows);
            gameObject.SetActive(false);
            refresh.NextPanel = 0;
            return wasOpen;
        }

        internal void RefreshBuildingCard(Entity entity)
        {
            var card = this;
            var baseOutputBlock = card.Block<UI_GamePanel_BuildingDetails_Block_基础产出>();
            var workforceBlock = card.Block<UI_GamePanel_BuildingDetails_Block_岗位>();
            var plantingBlock = card.Block<UI_GamePanel_BuildingDetails_Block_种植>();
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var b = sessionController.em.GetComponentData<Building>(entity);
            BuildingFarmingState bFarming = sessionController.em.GetComponentData<BuildingFarmingState>(entity);
            BuildingExperienceState bExperience = sessionController.em.GetComponentData<BuildingExperienceState>(entity);
            var stats = sessionController.em.GetComponentData<BuildingHousingStats>(entity);
            BuildingWorkforceStats statsWorkforce = sessionController.em.GetComponentData<BuildingWorkforceStats>(entity);
            BuildingGarrisonStats statsGarrison = sessionController.em.GetComponentData<BuildingGarrisonStats>(entity);
            BuildingQuestStats statsQuests = sessionController.em.GetComponentData<BuildingQuestStats>(entity);
            ref var d = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, DefinitionOf(entity));
            var session = sessionController.em.GetComponentData<Session>(sessionController.root);
            SimulationControl sessionControl = sessionController.em.GetComponentData<SimulationControl>(sessionController.root);
            PersistenceGate sessionPersistence = sessionController.em.GetComponentData<PersistenceGate>(sessionController.root);
            bool can = session.Phase == Phase.Day && sessionControl.Paused == 0 && sessionPersistence.CheckpointPending == 0;
            bool normal = BuildingStatus.Operational(sessionController.em, entity);
            card.Select(id.Id, id.Name.ToString());
            card.Name.interactable = can;
            card.Icon.sprite = buildingUi.BuildingSource(DefinitionOf(entity))?.Icon;
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

            int required = 0;
            for (int i = 0; i < d.Capabilities.Upgrade.Experience.Length; i++)
            {
                var experience = d.Capabilities.Upgrade.Experience[i];
                if (experience.Level == 0 || experience.Level == b.Level)
                {
                    required = Mathf.Max(0, experience.UpgradeExperience);
                    break;
                }
            }

            bool full = required == 0 || bExperience.Experience >= required;
            bool maximum = b.Level >= d.MaximumLevel;
            UI_GamePanel_BuildingDetails.Span(card.ExperienceFill, 0, required > 0 ? (float)bExperience.Experience / required : maximum ? 1 : 0);
            card.Experience.text = maximum && full ? "MAX" : bExperience.Experience + " / " + required;
            card.Upgrade.gameObject.SetActive(!(maximum && full));
            var upgrade = BuildingUpgradeCommands.Check(sessionController.em, sessionController.root, entity);
            UI_GamePanel_BuildingDetails.Bind(card.Upgrade, () =>
            {
                if (!can)
                {
                    hudController.Message.text = "请在未暂停的白天升级";
                    return;
                }

                buildingUi.ConfirmBuildingCommand(CommandKind.Upgrade);
            });
            card.Upgrade.image.color = can && upgrade.Allowed ? new Color(.2f, .4f, .66f) : new Color(.34f, .36f, .36f);
            var outputs = new List<string>();
            if (stats.MaxPopulation + stats.BasePopulation > 0)
                outputs.Add("人口 +" + (stats.MaxPopulation + stats.BasePopulation));
            int research = 0;
            for (int i = 0; i < d.Capabilities.Research.Levels.Length; i++)
            {
                var level = d.Capabilities.Research.Levels[i];
                if (level.Level == 0 || level.Level == b.Level)
                    research += level.PointsPerTurn;
            }

            if (research > 0)
                outputs.Add("科研值 +" + research);
            if (statsGarrison.Capacity > 0)
                outputs.Add("士兵槽 " + statsGarrison.Capacity);
            for (int i = 0; i < d.Capabilities.Storage.Warehouses.Length; i++)
            {
                var warehouse = d.Capabilities.Storage.Warehouses[i];
                if ((warehouse.Level == 0 || warehouse.Level == b.Level) && warehouse.Slots > 0)
                    outputs.Add("库存 " + StorageSlotDefinitions.Get(sessionController.em, sessionController.root, warehouse.SlotType).Metadata.Name + " ×" + warehouse.Slots + "（工人≥" + warehouse.RequiredWorkers + "）");
            }

            int invitations = sessionController.em.HasBuffer<QuestOfferSlot>(entity) ? sessionController.em.GetBuffer<QuestOfferSlot>(entity).Length : 0;
            if (invitations > 0)
                outputs.Add("邀约槽 " + invitations);
            if (statsQuests.Capacity > 0)
                outputs.Add("任务槽位 " + statsQuests.Capacity);
            baseOutputBlock.Refresh(outputs, statsWorkforce.Capacity > 0 ? () => WorkerEfficiencyOps.Describe(sessionController.em, sessionController.root, entity, "全部") : null);
            card.ConfigureSidebar(card.ExperienceHover, statsWorkforce.Capacity > 0 ? () => WorkerEfficiencyOps.Describe(sessionController.em, sessionController.root, entity, "经验") : null);
            soldierController.RefreshBuildingGarrison(entity);
            var pos = EntityState.Position(sessionController.em, entity);
            card.Footer.text = $"(x: {pos.x:0.#}, y: {pos.y:0.#}, z: {pos.z:0.#})  移动力: {BuildingRangeOps.ActionPower(sessionController.em, sessionController.root, entity)}";
            card.SetWarnings(BuildingWarnings(entity));
            bool skins = sessionController.em.HasBuffer<BuildingVisualSlot>(entity) && sessionController.em.GetBuffer<BuildingVisualSlot>(entity).Length > 0;
            UI_GamePanel_BuildingDetails.Bind(card.Style, skins ? () => BuildingSkins(id.Id) : null);
            var workforce = WorkforceOps.Quote(sessionController.em, sessionController.root, entity);
            workforceBlock.Refresh(workforce, ItemName(workforce.Gold), can && normal, value => commandsController.TryQueue(new SetWorkforceBudgetRequest { Building = id.Id, Budget = value - workforce.SubsidyCost, Relative = 1 }), can && normal && WorkforceOps.CanChange(workforce, 1) == ResultCode.Success ? () => commandsController.TryQueue(new RecruitWorkersRequest { Building = id.Id, Count = 1, ExpectedGoldCostPerWorker = workforce.RecruitCost }) : null, can && normal && WorkforceOps.CanChange(workforce, -1) == ResultCode.Success ? () => commandsController.TryQueue(new ChangeWorkersRequest { Building = id.Id, Delta = -1 }) : null, () => WorkforceOps.Quote(sessionController.em, sessionController.root, entity));
            bool crops = d.Capabilities.Farming.Enabled && d.Capabilities.Farming.Crops.Length > 0;
            if (crops)
            {
                bool planted = bFarming.Crop.IsValid;
                int duration = planted ? Mathf.Max(1, CropDefinitions.Get(sessionController.em, sessionController.root, bFarming.Crop).GrowthTurns) : 1;
                plantingBlock.Refresh(planted ? "种植 · " + CropName(bFarming.Crop) + " " + bFarming.Progress + "/" + duration : "种植 · 点击圆钮选择作物", planted ? (float)bFarming.Progress / duration : 0, planted ? CropPortrait(bFarming.Crop) : null, () => BuildingCrops(id.Id), can && normal && planted ? () => buildingUi.ShowBuildingConfirmation("铲除 " + CropName(bFarming.Crop), new[] { "失去当前作物与进度，不返种植费用。" }, () => commandsController.TryQueue(new ClearCropRequest { Building = id.Id })) : null, () => WorkerEfficiencyOps.Describe(sessionController.em, sessionController.root, entity, "种植"));
            }
            else
                plantingBlock.Hide();
        }

        internal string BuildingWarnings(Entity e)
        {
            var b = sessionController.em.GetComponentData<Building>(e);
            BuildingConstructionState bConstruction = sessionController.em.GetComponentData<BuildingConstructionState>(e);
            BuildingWorkforceState bWorkforce = sessionController.em.GetComponentData<BuildingWorkforceState>(e);
            BuildingHousingState bHousing = sessionController.em.GetComponentData<BuildingHousingState>(e);
            BuildingFarmingState bFarming = sessionController.em.GetComponentData<BuildingFarmingState>(e);
            BuildingMaintenanceState bMaintenance = sessionController.em.GetComponentData<BuildingMaintenanceState>(e);
            var id = sessionController.em.GetComponentData<Identity>(e);
            var stats = sessionController.em.GetComponentData<BuildingHousingStats>(e);
            var warnings = new List<string>();
            if (b.Stage != LifeStage.Operational)
                warnings.Add(BuildingStageName(b.Stage));
            if (stats.IsCore == 0)
            {
                var h = sessionController.em.GetComponentData<Health>(e);
                if (h.Current < h.Maximum)
                    warnings.Add($"耐久受损：{h.Current:0}/{h.Maximum:0}");
            }

            if (bHousing.FoodFailures > 0)
                warnings.Add("居民连续缺粮 " + bHousing.FoodFailures + " 回合");
            if (WorkforceSettlement.Locked(sessionController.em, id.Id))
                warnings.Add("远征在途，岗位、移动与升级锁定");
            var maintenance = BuildingCostOps.Maintenance(sessionController.em, sessionController.root, DefinitionOf(e), b.Level);
            if (maintenance.Count > 0 && bMaintenance.Maintained == 0)
                warnings.Add("维护未满足：" + buildingUi.CostText(maintenance));
            void Shortage(string label, IEnumerable<BuildingCost> costs)
            {
                foreach (var cost in costs)
                {
                    int missing = cost.Amount - InventoryOps.Count(sessionController.em, sessionController.root, cost.Item);
                    if (missing > 0)
                        warnings.Add(label + "：" + ItemName(cost.Item) + " 缺 " + missing);
                }
            }

            if (b.Stage == LifeStage.Operational)
            {
                Shortage("下次维护材料不足", maintenance);
                Shortage("生产原料不足", BuildingCostOps.ProductionInputs(sessionController.em, sessionController.root, DefinitionOf(e), b.Level));
            }

            if (b.Stage == LifeStage.Construction)
                Shortage("下期施工材料不足", BuildingCostOps.ConstructionStage(sessionController.em, sessionController.root, DefinitionOf(e), bConstruction.Progress + 1));
            if (bFarming.Crop.IsValid && bWorkforce.Workers < CropDefinitions.Get(sessionController.em, sessionController.root, bFarming.Crop).RequiredWorkers)
                warnings.Add("作物停止生长：工人不足");
            var q = WorkforceOps.Quote(sessionController.em, sessionController.root, e);
            if (q.Capacity > 0)
            {
                if (bWorkforce.Workers == 0)
                    warnings.Add("没有工人入驻");
                if (q.SubsidyCost > q.Stock)
                    warnings.Add("下次补贴资金不足");
                if (q.Workers > q.CurrentStable)
                    warnings.Add("工人数超过当前可稳定人数，可能离职");
            }

            ref var definition = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, DefinitionOf(e));
            bool needsNetwork = maintenance.Count > 0 || BuildingCostOps.ProductionInputs(sessionController.em, sessionController.root, DefinitionOf(e), b.Level).Count > 0;
            if (b.Stage == LifeStage.Construction)
                needsNetwork |= BuildingCostOps.ConstructionStage(sessionController.em, sessionController.root, DefinitionOf(e), bConstruction.Progress + 1).Count > 0;
            if (b.Stage == LifeStage.Repairing)
            {
                var repair = BuildingCostOps.QuoteRepair(sessionController.em, sessionController.root, e);
                if (repair.Payments.Any(p => p.Missing > 0))
                    warnings.Add("修复材料不足");
                needsNetwork |= repair.NeedsNetwork;
            }

            if (needsNetwork && ResourceNetworkOps.Provider(sessionController.em, sessionController.root, e) == Entity.Null)
                warnings.Add("无法连接资源提供点，需要普通库存的生产/维护/施工会暂停");
            for (int i = 0; i < definition.Capabilities.Production.Cycles.Length; i++)
            {
                var cycle = definition.Capabilities.Production.Cycles[i];
                if ((cycle.Level == 0 || cycle.Level == b.Level) && bWorkforce.Workers < cycle.RequiredWorkers)
                    warnings.Add("生产缺少工人：需要 " + cycle.RequiredWorkers);
            }

            for (int i = 0; i < definition.Capabilities.Housing.Environment.Length; i++)
            {
                var requirement = definition.Capabilities.Housing.Environment[i];
                if ((requirement.Level == 0 || requirement.Level == b.Level) && BuildingEnvironment.Value(sessionController.em, sessionController.root, e, requirement.Type) < requirement.RequiredValue)
                    warnings.Add(EnvironmentName((int)requirement.Type) + "不足：需要 " + requirement.RequiredValue);
            }

            return string.Join("\n", warnings.Distinct().Select(w => "• " + w));
        }

        internal Sprite CropPortrait(CropId definition)
        {
            var icon = Crops.Get(definition)?.Icon;
            if (icon != null)
                return icon;
            ref var crop = ref CropDefinitions.Get(sessionController.em, sessionController.root, definition);
            return crop.HarvestOutputs.Length > 0 ? Items.Get(crop.HarvestOutputs[0].Item)?.Icon : null;
        }

        internal void BuildingSkins(ulong key)
        {
            var e = WorldQueries.Find(sessionController.em, key);
            if (e == Entity.Null || !sessionController.em.HasBuffer<BuildingVisualSlot>(e))
                return;
            var skins = new HashSet<string>();
            BuildingAppearanceState bAppearance = sessionController.em.GetComponentData<BuildingAppearanceState>(e);
            bool can = courtController.CourtDay && sessionController.em.GetComponentData<SimulationControl>(sessionController.root).Paused == 0 && BuildingStatus.Operational(sessionController.em, e);
            buildingUi.ShowBuildingChoices("选择建筑皮肤", (row, close) =>
            {
                foreach (var slot in sessionController.em.GetBuffer<BuildingVisualSlot>(e))
                    if (slot.Purpose == BuildingVisualPurpose.Operational && skins.Add(slot.Skin.ToString()))
                    {
                        var skin = slot.Skin.ToString();
                        row((skin == bAppearance.Skin.ToString() ? "✓ " : "") + (skin.Length == 0 ? "默认" : skin), can ? () =>
                        {
                            close();
                            commandsController.TryQueue(new ChangeBuildingSkinRequest { Building = key, Skin = new Unity.Collections.FixedString64Bytes(skin) });
                        } : null, key: "building-skin:" + key + ":" + skin);
                    }

                row("关闭", close);
            });
        }

        internal void BuildingCrops(ulong key)
        {
            var e = WorldQueries.Find(sessionController.em, key);
            if (e == Entity.Null)
                return;
            var b = sessionController.em.GetComponentData<Building>(e);
            BuildingFarmingState bFarming = sessionController.em.GetComponentData<BuildingFarmingState>(e);
            var definition = DefinitionOf(e);
            buildingUi.ShowBuildingChoices("选择作物", (row, close) =>
            {
                ref var d = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, definition);
                if (bFarming.Crop.IsValid)
                    row("已种植 " + CropName(bFarming.Crop) + "，更换前请先使用 X 铲除。");
                var seen = new HashSet<CropId>();
                for (int i = 0; i < d.Capabilities.Farming.Crops.Length; i++)
                {
                    var allowed = d.Capabilities.Farming.Crops[i];
                    if ((allowed.Level != 0 && allowed.Level != b.Level) || !seen.Add(allowed.Crop))
                        continue;
                    var cropId = allowed.Crop;
                    ref var crop = ref CropDefinitions.Get(sessionController.em, sessionController.root, cropId);
                    var costs = new List<BuildingCost>();
                    for (int cost = 0; cost < crop.PlantingCosts.Length; cost++)
                        costs.Add(new BuildingCost(crop.PlantingCosts[cost].Item, crop.PlantingCosts[cost].Quantity));
                    bool can = courtController.CourtDay && sessionController.em.GetComponentData<SimulationControl>(sessionController.root).Paused == 0 && BuildingStatus.Operational(sessionController.em, e) && !bFarming.Crop.IsValid && BuildingCostOps.CanPay(sessionController.em, sessionController.root, costs);
                    row(CropName(cropId) + " · " + buildingUi.CostText(costs) + " · " + crop.GrowthTurns + " 回合", can ? () =>
                    {
                        close();
                        commandsController.TryQueue(new PlantCropRequest { Building = key, Crop = cropId });
                    } : null, key: "building-crop:" + key + ":" + cropId.Index);
                }

                row("关闭", close);
            });
        }

        internal void Refresh(Entity entity)
        {
            gameObject.SetActive(isOpen && !intelligence.IsOpen);
            if (!isOpen || intelligence.IsOpen)
                return;
            var b = sessionController.em.GetComponentData<Building>(entity);
            BuildingConstructionState bConstruction = sessionController.em.GetComponentData<BuildingConstructionState>(entity);
            BuildingWorkforceState bWorkforce = sessionController.em.GetComponentData<BuildingWorkforceState>(entity);
            BuildingHousingState bHousing = sessionController.em.GetComponentData<BuildingHousingState>(entity);
            BuildingProductionState bProduction = sessionController.em.GetComponentData<BuildingProductionState>(entity);
            BuildingFarmingState bFarming = sessionController.em.GetComponentData<BuildingFarmingState>(entity);
            BuildingSanctumState bSanctum = sessionController.em.GetComponentData<BuildingSanctumState>(entity);
            BuildingGatheringState bGathering = sessionController.em.GetComponentData<BuildingGatheringState>(entity);
            BuildingMarketState bMarket = sessionController.em.GetComponentData<BuildingMarketState>(entity);
            BuildingMaintenanceState bMaintenance = sessionController.em.GetComponentData<BuildingMaintenanceState>(entity);
            var stats = sessionController.em.GetComponentData<BuildingHousingStats>(entity);
            BuildingGarrisonStats statsGarrison = sessionController.em.GetComponentData<BuildingGarrisonStats>(entity);
            BuildingIntelligenceStats statsIntelligence = sessionController.em.GetComponentData<BuildingIntelligenceStats>(entity);
            BuildingSanctumStats statsSanctum = sessionController.em.GetComponentData<BuildingSanctumStats>(entity);
            BuildingBellStats statsBell = sessionController.em.GetComponentData<BuildingBellStats>(entity);
            var id = sessionController.em.GetComponentData<Identity>(entity);
            ref var d = ref BuildingDefinitions.Get(sessionController.em, sessionController.root, DefinitionOf(entity));
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;
            var normal = b.Stage == LifeStage.Operational;
            rowsController.Clear(DetailsRows);
            void Detail(string text, Action action = null, string key = null)
            {
                rowsController.Row(text, action, parent: DetailsRows, key: key);
            }

            void ProductionDetail(string text, string key = null, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0) => Detail(text, key: "building:" + id.Id + ":production:" + (key ?? sourceLine.ToString()));
            void PlantingDetail(string text, [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0) => Detail(text, key: "building:" + id.Id + ":planting:" + sourceLine);
            RefreshBuildingCard(entity);
            Detail("查看本建筑账单", () => economy.OpenEconomy(id.Id));
            var source = buildingUi.BuildingSource(DefinitionOf(entity));
            if (!string.IsNullOrEmpty(source?.Description))
                Detail(source.Description);
            var provider = ResourceNetworkOps.Provider(sessionController.em, sessionController.root, entity);
            if (b.Stage == LifeStage.Construction)
            {
                Detail($"施工 {bConstruction.Progress}/{d.ConstructionTurns} 回合");
                Detail("下期材料：" + buildingUi.CostText(BuildingCostOps.ConstructionStage(sessionController.em, sessionController.root, DefinitionOf(entity), bConstruction.Progress + 1)));
                if (provider == Entity.Null)
                    Detail("需要从正常库存支付的施工：断连时暂停。");
            }

            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing)
            {
                BuildingCostOps.RepairTotal(sessionController.em, sessionController.root, entity, out var duration);
                Detail(b.Stage == LifeStage.Ruined ? "荒废：无建筑功能；修复无需工人。" : $"修复 {bConstruction.Progress}/{bConstruction.RepairDuration} 回合，材料不足或断连暂停。");
                if (b.RuinPending != 0)
                    Detail($"本夜已失效，黎明提交居民损失 {bHousing.Population} / 工人失业 {bWorkforce.Workers}。库存已标记损失。");
                RepairDetails(entity, (text, action) => Detail(text, action));
                if (day && b.Stage == LifeStage.Ruined)
                    Detail("开始修复（" + duration + " 回合）", () => buildingUi.ConfirmBuildingCommand(CommandKind.Repair));
                return;
            }

            var maintenance = BuildingCostOps.Maintenance(sessionController.em, sessionController.root, DefinitionOf(entity), b.Level);
            if (maintenance.Count > 0)
                Detail("每回合维护：" + buildingUi.CostText(maintenance) + (bMaintenance.Maintained != 0 ? " · 已满足" : " · 未满足"));
            if (stats.MaxPopulation > 0)
            {
                Detail($"居住 {bHousing.Population}/{stats.MaxPopulation} · 增长进度 {bHousing.Growth} · 税收进度 {bHousing.TaxProgress} · 连续缺粮 {bHousing.FoodFailures}");
                if (bHousing.DeferredResidents > 0)
                    Detail("修复迁入人口将在黎明加入：" + bHousing.DeferredResidents);
                foreach (var food in sessionController.em.GetBuffer<FoodSelection>(entity))
                    Detail("上次实际食物：" + ItemName(food.Item) + " × " + food.Amount);
            }

            for (int i = 0; i < d.Capabilities.Production.Cycles.Length; i++)
            {
                var cycle = d.Capabilities.Production.Cycles[i];
                if (cycle.Level != 0 && cycle.Level != b.Level)
                    continue;
                ProductionDetail($"生产周期进度 {bProduction.Progress}/{cycle.Interval} · 所需工人 {cycle.RequiredWorkers}");
                ProductionDetail("原料：" + buildingUi.CostText(BuildingCostOps.ProductionInputs(sessionController.em, sessionController.root, DefinitionOf(entity), b.Level)));
                break;
            }

            for (int i = 0; i < d.Capabilities.Production.Outputs.Length; i++)
            {
                var output = d.Capabilities.Production.Outputs[i];
                if (output.Level != 0 && output.Level != b.Level)
                    continue;
                ProductionDetail("产品：" + ItemName(output.Item) + " × " + output.Quantity + " · 工人 " + output.MinimumWorkers + (output.MaximumWorkers > 0 ? "～" + output.MaximumWorkers : " 以上") + (bWorkforce.Workers >= output.MinimumWorkers && (output.MaximumWorkers <= 0 || bWorkforce.Workers <= output.MaximumWorkers) ? "（当前档）" : ""), "output:" + i);
            }

            for (int i = 0; i < d.Capabilities.Production.RareOutputs.Length; i++)
            {
                var output = d.Capabilities.Production.RareOutputs[i];
                if (output.Level != 0 && output.Level != b.Level)
                    continue;
                ProductionDetail($"随机产出：{ItemName(output.Item)} ×{output.Quantity} · 概率 {output.Probability:P0} · 工人 ≥{output.RequiredWorkers}", "random:" + i);
            }

            for (int i = 0; i < d.Capabilities.Housing.Food.Length; i++)
            {
                var food = d.Capabilities.Housing.Food[i];
                if (food.Level == 0 || food.Level == b.Level)
                    Detail($"食谱：{ItemGroupDefinitions.Get(sessionController.em, sessionController.root, food.FoodGroup).Metadata.Name} · {food.Varieties} 种 · 每居民每种 {math.max(1, food.AmountPerResident)}");
            }

            for (int i = 0; i < d.Capabilities.Housing.Environment.Length; i++)
            {
                var requirement = d.Capabilities.Housing.Environment[i];
                if (requirement.Level == 0 || requirement.Level == b.Level)
                    Detail($"环境条件 {EnvironmentName((int)requirement.Type)} ≥ {requirement.RequiredValue} · 当前 {BuildingEnvironment.Value(sessionController.em, sessionController.root, entity, requirement.Type):0.#}");
            }

            for (int i = 0; i < d.Capabilities.Effects.Spatial.Length; i++)
            {
                var effect = d.Capabilities.Effects.Spatial[i];
                if (effect.Level == 0 || effect.Level == b.Level)
                    Detail($"作用 {EnvironmentName((int)effect.Type)} +{effect.Magnitude} · 范围 {effect.Radius} 格 · 工人 ≥{effect.RequiredWorkers}", key: "spatial:" + i);
            }

            for (int i = 0; i < d.Capabilities.Market.Levels.Length; i++)
            {
                var market = d.Capabilities.Market.Levels[i];
                if (market.Level == 0 || market.Level == b.Level)
                    Detail("市场结算比例：" + market.IncomeRatio + " · 本回合归因价值 " + bMarket.TurnValue, key: "market:" + i);
            }

            for (int i = 0; i < d.Capabilities.Gathering.Levels.Length; i++)
            {
                var gathering = d.Capabilities.Gathering.Levels[i];
                if (gathering.Level == 0 || gathering.Level == b.Level)
                    Detail("剩余采集次数：" + bGathering.RemainingUses, day && normal ? () => commandsController.TryQueue(new HarvestBuildingRequest { Building = id.Id }) : null);
            }

            if (bFarming.Crop.IsValid)
            {
                PlantingDetail($"作物 {CropName(bFarming.Crop)} · 生长 {bFarming.Progress}/{CropDefinitions.Get(sessionController.em, sessionController.root, bFarming.Crop).GrowthTurns}");
                if (day && normal)
                {
                    Detail("收获", () => commandsController.TryQueue(new HarvestBuildingRequest { Building = id.Id }));
                    Detail(bFarming.AutoHarvest != 0 ? "关闭自动收获" : "开启自动收获", () => commandsController.TryQueue(new SetAutoHarvestRequest { Building = id.Id, Enabled = (byte)((bFarming.AutoHarvest == 0 ? 1 : 0) != 0 ? 1 : 0) }));
                }
            }

            if (statsGarrison.Capacity > 0)
            {
                if (normal && (sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Night || sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Retreat))
                {
                    Detail("召回所属士兵", () => commandsController.TryQueue(new RecallGarrisonRequest { Garrison = id.Id, Cancel = false }));
                    Detail("取消途中召回", () => commandsController.TryQueue(new RecallGarrisonRequest { Garrison = id.Id, Cancel = true }));
                }
            }

            if (statsBell.Radius > 0)
                Detail("警铃集结范围 " + statsBell.Radius, !day && normal ? () => commandsController.TryQueue(new RingBellRequest { Building = id.Id }) : null);
            if (statsIntelligence.Points > 0)
                Detail("情报贡献 " + statsIntelligence.Points, () => navigation.OpenPanel(GamePanelId.Intelligence));
            if (HasExpeditionSite(ref d.Capabilities.Expeditions, b.Level))
                Detail(WorkforceSettlement.Locked(sessionController.em, id.Id) ? "远征在途，岗位/移动/升级锁定" : "打开远征", () => navigation.OpenPanel(GamePanelId.Expedition));
            if (statsSanctum.Hero.IsValid)
            {
                ref var hero = ref HeroDefinitions.Get(sessionController.em, sessionController.root, statsSanctum.Hero);
                Detail($"{hero.Metadata.Name} · 招募 {hero.FallbackWakeGold} 金币 · 人口 {hero.PopulationCost} · 神殿所需工人 {statsSanctum.RequiredWorkers}");
                Detail("供奉：" + buildingUi.CostText(HeroCosts(ref hero.OfferingCosts)) + " · 唤醒：" + buildingUi.CostText(HeroCosts(ref hero.AwakeningCosts)));
                Detail($"当前工人 {bWorkforce.Workers}/{statsSanctum.RequiredWorkers} · 持续供奉{(bSanctum.Offering == 0 ? "关闭" : "开启")} · 本回合供奉{(bSanctum.PaidOfferingTurn == sessionController.em.GetComponentData<GameClock>(sessionController.root).Turn ? "已支付" : "未支付")}");
                Detail("供奉在平安夜也消耗资源；唤醒另外付费，未实际参战不获得战斗经验。英雄阵亡后经验清零，冷却结束重招支付完整费用。缺工不会杀死英雄，神殿荒废/拆除会。");
                bool wake = !day;
                string reason = HeroOps.HeroAvailability(sessionController.em, sessionController.root, entity, wake);
                Detail((wake ? "唤醒英雄" : "招募英雄") + (reason.Length > 0 ? " · " + reason : ""), reason.Length == 0 ? () =>
                {
                    if (wake)
                        commandsController.TryQueue(new WakeHeroRequest { Sanctum = id.Id });
                    else
                        commandsController.TryQueue(new RecruitHeroRequest { Sanctum = id.Id });
                } : null);
                if (normal && day)
                    Detail(bSanctum.Offering == 0 ? "开启持续供奉" : "关闭持续供奉", () => commandsController.TryQueue(new SetOfferingRequest { Building = id.Id, Enabled = (byte)((bSanctum.Offering == 0 ? 1 : 0) != 0 ? 1 : 0) }));
            }
        }

        Landsong.ECS.Definitions.BuildingId DefinitionOf(Entity entity) => sessionController.em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
        string ItemName(ItemId item) => item.IsValid ? ItemDefinitions.Get(sessionController.em, sessionController.root, item).Metadata.Name.ToString() : "—";
        string CropName(CropId crop) => CropDefinitions.Get(sessionController.em, sessionController.root, crop).Metadata.Name.ToString();
        static List<BuildingCost> HeroCosts(ref BlobArray<LeveledItemAmount> costs)
        {
            var result = new List<BuildingCost>();
            for (int i = 0; i < costs.Length; i++)
                if (costs[i].Level == 0 || costs[i].Level == 1)
                    result.Add(new BuildingCost(costs[i].Item, costs[i].Quantity));
            return result;
        }

        static bool HasExpeditionSite(ref BuildingExpeditions expeditions, int level)
        {
            if (!expeditions.Enabled)
                return false;
            for (int i = 0; i < expeditions.Levels.Length; i++)
                if (expeditions.Levels[i].Level == 0 || expeditions.Levels[i].Level == level)
                    return true;
            return false;
        }

        internal static string EnvironmentName(int kind) => kind == 20 ? "美观" : kind == 30 ? "医疗" : kind == 40 ? "治安" : kind == 10 ? "生产/作物收益百分比" : "效果 " + kind;
        internal static string BuildingStageName(LifeStage stage) => stage == LifeStage.Construction ? "施工中" : stage == LifeStage.Ruined ? "建筑荒废" : stage == LifeStage.Repairing ? "修复中" : "正常运营";
        internal void RepairDetails(Entity entity, Action<string, Action> detail)
        {
            var q = BuildingCostOps.QuoteRepair(sessionController.em, sessionController.root, entity);
            detail((sessionController.em.GetComponentData<Building>(entity).Stage == LifeStage.Repairing ? "已冻结修复总额：" : "拟定修复总额：") + buildingUi.CostText(q.Total) + " · 尚需 " + buildingUi.CostText(q.Remaining), null);
            detail($"下一期 {q.Step + 1}/{q.Duration}：{q.Reason}", null);
            foreach (var p in q.Payments)
                detail($"{ItemName(p.Item)}：本期需 {p.Required} = 待存放 {p.Pending} + 正常库存 {p.Normal}；正常可用 {p.Available}；缺口 {p.Missing}", null);
            detail("以上为当前资源预览，不预留材料；同回合较早结算的建筑仍可能先用这些物资。修复开始只冻结计划，不立即付款。", null);
        }

        internal void SupplyDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSupplySources ? "收起供给来源" : "展开供给来源", () =>
            {
                showSupplySources = !showSupplySources;
                refresh.NextPanel = 0;
            });
            if (!showSupplySources)
                return;
            var q = ResourceNetworkOps.Quote(sessionController.em, sessionController.root, entity);
            detail("先比较提供点优先级，再比较道路加权距离，同值按稳定建筑 ID。提供点用于连接/市场归因，材料仍从全城正常库存扣除。", null);
            foreach (var c in q.Candidates)
            {
                var candidate = c;
                detail($"{sessionController.EntityName(c.Id)} · 优先级 {c.Priority} · 路径成本 {(float.IsInfinity(c.Cost) ? "不可达" : c.Cost.ToString("0.##"))} · {(q.Selected == c.Entity ? "已选中" : c.Reason)}", () => buildingUi.FocusBuilding(candidate.Id));
            }

            if (q.Candidates.Count == 0)
                detail("没有其他资源提供点。", null);
        }

        internal void SpatialDetails(Entity entity, Action<string, Action> detail)
        {
            detail(showSpatialSources ? "收起空间效果来源" : "展开空间效果来源", () =>
            {
                showSpatialSources = !showSpatialSources;
                refresh.NextPanel = 0;
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
                var q = SpatialOps.Quote(sessionController.em, sessionController.root, entity, (BuildingEnvironmentKind)kind);
                detail(EnvironmentName(kind) + " · 实际合计 " + q.Value, null);
                foreach (var source in q.Sources)
                {
                    var s = source;
                    detail($"{sessionController.EntityName(s.Source)} · {s.Group} · 配置 {s.Amount} / 计入 {s.Applied} · {s.Reason}", () => buildingUi.FocusBuilding(s.Source));
                }
            }
        }

        internal void ResetSession()
        {
            isOpen = false;
            showSupplySources = showSpatialSources = false;
            rowsController?.ClearAll();
            gameObject.SetActive(false);
        }
    }
}
