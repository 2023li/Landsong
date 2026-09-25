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
using Moyo.Unity;

namespace Landsong.ECS.Presentation
{
    // Stable controls preserve input focus while the simulation refreshes their read models.
    public sealed class UI_GamePanel_BuildingDetails : MonoBehaviour
    {
        [SerializeField, LabelText("界面预览内容"), Required]
        private UIViewPreviewContent previewContent;
        public UIViewPreviewContent PreviewContent => previewContent;
        internal void RefreshInitialPreview()
        {
            if (previewContent == null)
                throw new InvalidOperationException("建筑详情预览内容未配置。");
            previewContent.ValidateConfiguration(transform);
            previewContent.PrepareRuntime();
        }
        [Sirenix.OdinInspector.LabelText("物品显示目录"), Sirenix.OdinInspector.Required]
        public ItemDisplayCatalog Items;
        [Sirenix.OdinInspector.LabelText("作物显示目录"), Sirenix.OdinInspector.Required]
        public CropDisplayCatalog Crops;
        internal UI_GamePanel_History history;
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
        [LabelText("移动建筑"), Required]
        public Button Move;
        [LabelText("拆除建筑"), Required]
        public Button Demolish;
        [LabelText("警告"), Required]
        public Button Warning;
        [LabelText("经验悬浮信息"), Required]
        public UI_GamePanel_BuildingDetails_ExperienceHover ExperienceHover;
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

        public void SetSidebarContent(Component owner, Func<string> content)
        {
            if (sidebarOwner != owner)
                return;
            if (content == null)
                HideSidebar();
            else
                sidebarContent = content;
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

        public void HideSidebar(Component owner)
        {
            if (sidebarOwner == owner)
                HideSidebar();
        }

        public void HideSidebar()
        {
            sidebarOwner = null;
            sidebarContent = null;
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

            if (!sidebarOwner.gameObject.activeInHierarchy || sidebarContent == null || (sidebarHideAt > 0 && Time.unscaledTime >= sidebarHideAt))
            {
                HideSidebar();
                return;
            }

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
        internal UI_GamePanel_Royal courtController;
        internal UI_GamePanel_Hud hudController;
        internal IGameUiNavigation navigation;
        internal GameUiSessionHandle sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal UI_GamePanel_WorldInteraction worldController;
        internal IGameBuildingUi buildingUi;
        bool isOpen;
        public RectTransform DetailsRows => Block<UI_GamePanel_BuildingDetails_Block_其他>().Rows;
        public bool IsOpen => isOpen;

        internal void BindPresenter(GameUiSessionHandle session, GameUiCommandWriter commands, IGameUiNavigation navigation, IGameBuildingUi buildingUi, UI_GamePanel_Royal court, UI_GamePanel_Hud hud, UI_GamePanel_Soldier soldier, UI_GamePanel_WorldInteraction world)
        {
            sessionController = session ?? throw new ArgumentNullException(nameof(session));
            commandsController = commands ?? throw new ArgumentNullException(nameof(commands));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            Block<UI_GamePanel_BuildingDetails_Block_其他>().Initialize();
            this.buildingUi = buildingUi ?? throw new ArgumentNullException(nameof(buildingUi));
            courtController = court ?? throw new ArgumentNullException(nameof(court));
            hudController = hud ?? throw new ArgumentNullException(nameof(hud));
            soldierController = soldier ?? throw new ArgumentNullException(nameof(soldier));
            worldController = world ?? throw new ArgumentNullException(nameof(world));
        }

        internal void Initialize()
        {
            Close.onClick.AddListener(() => Hide());
            Move.onClick.AddListener(() =>
            {
                worldController.BeginMoveBuilding();
                if (worldController.HasBuildingPlacement)
                    Hide();
            });
            Demolish.onClick.AddListener(ConfirmDemolition);
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
            RefreshBuildingCard(selected);
            Block<UI_GamePanel_BuildingDetails_Block_其他>().Refresh(selected);
            refresh.NextPanel = 0;
        }

        internal bool Hide()
        {
            var wasOpen = isOpen || gameObject.activeSelf;
            isOpen = false;
            Block<UI_GamePanel_BuildingDetails_Block_其他>().Clear();
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
            BuildingExperienceState bExperience = sessionController.em.GetComponentData<BuildingExperienceState>(entity);
            var stats = sessionController.em.GetComponentData<BuildingHousingStats>(entity);
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
            card.Move.gameObject.SetActive(session.Phase == Phase.Day && b.Stage == LifeStage.Operational);
            card.Move.interactable = BuildingPlacementCommands.CheckMove(sessionController.em, sessionController.root, entity).Allowed;
            card.Demolish.gameObject.SetActive(session.Phase == Phase.Day && stats.IsCore == 0);
            baseOutputBlock.Refresh(entity);
            card.ExperienceHover.Refresh(entity);
            card.Block<UI_GamePanel_BuildingDetails_Block_驻军>().Refresh(entity);
            var pos = EntityState.Position(sessionController.em, entity);
            card.Footer.text = $"(x: {pos.x:0.#}, y: {pos.y:0.#}, z: {pos.z:0.#})  移动力: {BuildingRangeOps.ActionPower(sessionController.em, sessionController.root, entity)}";
            card.SetWarnings(BuildingWarnings(entity));
            bool skins = sessionController.em.HasBuffer<BuildingVisualSlot>(entity) && sessionController.em.GetBuffer<BuildingVisualSlot>(entity).Length > 0;
            UI_GamePanel_BuildingDetails.Bind(card.Style, skins ? () => BuildingSkins(id.Id) : null);
            workforceBlock.Refresh(entity, can && normal);
            plantingBlock.Refresh(entity, can && normal);
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
            if (bFarming.Crop.IsValid && bWorkforce.Workers < BuildingDefinitions.Get(sessionController.em, sessionController.root, DefinitionOf(e)).Capabilities.Farming.RequiredWorkers)
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

        void ConfirmDemolition()
        {
            var entity = WorldQueries.Find(sessionController.em, worldSelection.SelectedEntityId);
            if (entity == Entity.Null || !sessionController.em.HasComponent<Building>(entity) ||
                sessionController.em.GetComponentData<BuildingHousingStats>(entity).IsCore != 0 ||
                sessionController.em.GetComponentData<Session>(sessionController.root).Phase != Phase.Day)
                return;

            var id = sessionController.em.GetComponentData<Identity>(entity);
            var workforce = sessionController.em.GetComponentData<BuildingWorkforceState>(entity);
            var housing = sessionController.em.GetComponentData<BuildingHousingState>(entity);
            var lines = new List<string> { "此操作删除建筑。修复投入不返还，按已支付建造/升级成本计算返还。" };
            foreach (var slot in sessionController.em.GetBuffer<InventorySlot>(sessionController.root))
                if (slot.Provider == id.Id && slot.Count > 0)
                    lines.Add("原库存损失：" + ItemName(slot.Item) + " × " + slot.Count);
            foreach (var refund in BuildingCostOps.DemolitionRefund(sessionController.em, sessionController.root, entity))
                lines.Add($"返还 {ItemName(refund.Item)} × {refund.Amount}：存入 {refund.Stored}，空间不足损失 {refund.Lost}");
            var soldiers = GarrisonOps.GarrisonCount(sessionController.em, id.Id);
            var empty = 0;
            using (var buildings = WorldQueries.Entities<Building>(sessionController.em))
                foreach (var other in buildings)
                    if (other != entity && BuildingStatus.Operational(sessionController.em, other))
                        empty += math.max(0, sessionController.em.GetComponentData<BuildingGarrisonStats>(other).Capacity - GarrisonOps.GarrisonCount(sessionController.em, sessionController.em.GetComponentData<Identity>(other).Id));
            if (soldiers > 0)
                lines.Add($"{soldiers} 名士兵转待分配池；其他驻地空槽 {empty}，下一次入夜前未安排将解散。");
            if (workforce.Workers > 0)
                lines.Add(workforce.Workers + " 名工人失业。");
            if (housing.Population > 0)
                lines.Add("住宅中的 " + housing.Population + " 人将损失，请确认人口后果。");
            if (sessionController.em.GetComponentData<BuildingSanctumStats>(entity).Hero.IsValid)
                lines.Add("已招募的关联英雄死亡、经验清零并进入重招冷却。");
            if (WorkforceSettlement.Locked(sessionController.em, id.Id))
                lines.Add("在途远征将终止，进度、携带物资和奖励不保留。");

            buildingUi.ShowBuildingConfirmation("拆除 " + id.Name, lines,
                () => worldController.SubmitBuilding(new DemolishBuildingRequest { Building = id.Id }));
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

        internal void Refresh(Entity entity)
        {
            gameObject.SetActive(isOpen && !intelligence.IsOpen);
            if (!isOpen || intelligence.IsOpen)
                return;
            RefreshBuildingCard(entity);
            Block<UI_GamePanel_BuildingDetails_Block_其他>().Refresh(entity);
        }

        Landsong.ECS.Definitions.BuildingId DefinitionOf(Entity entity) => sessionController.em.GetComponentData<BuildingDefinitionRef>(entity).Definition;
        string ItemName(ItemId item) => item.IsValid ? ItemDefinitions.Get(sessionController.em, sessionController.root, item).Metadata.Name.ToString() : "—";
        internal static string EnvironmentName(int kind) => kind == 20 ? "美观" : kind == 30 ? "医疗" : kind == 40 ? "治安" : kind == 10 ? "生产/作物收益百分比" : "效果 " + kind;
        internal static string BuildingStageName(LifeStage stage) => stage == LifeStage.Construction ? "施工中" : stage == LifeStage.Ruined ? "建筑荒废" : stage == LifeStage.Repairing ? "修复中" : "正常运营";
        internal void ResetSession()
        {
            isOpen = false;
            Block<UI_GamePanel_BuildingDetails_Block_其他>().ResetSession();
            gameObject.SetActive(false);
        }
    }
}
