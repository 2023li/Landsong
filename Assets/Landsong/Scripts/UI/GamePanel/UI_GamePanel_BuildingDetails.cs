using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using Landsong.ECS.Authoring;
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

        public T Block<T>() where T : UI_GamePanel_BuildingDetails_Block
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
        [LabelText("工人信息模板"), Required]
        public UI_GamePanel_WorkerInfoRow WorkerInfoTemplate;
        [LabelText("劳动力模板"), Required]
        public UI_GamePanel_WorkforceRow WorkforceTemplate;

        internal GameUiCommandWriter commandsController;
        internal UI_GamePanel_Court courtController;
        internal UI_GamePanel_Hud hudController;
        internal IGameUiNavigation navigation;
        internal UI_GamePanel_RowRenderer rowsController;
        internal GameUiSession sessionController;
        internal UI_GamePanel_Soldier soldierController;
        internal IGameBuildingUi buildingUi;
        internal UI_GamePanel_WorkforceScale workforceScale;
        internal bool showSupplySources;
        internal bool showAttractionSources;
        internal bool showSpatialSources;
        bool isOpen;

        public RectTransform DetailsRows => Block<UI_GamePanel_BuildingDetails_Block_其他>().Rows;
        public bool IsOpen => isOpen;
        public UI_GamePanel_WorkforceScale WorkforceScale => workforceScale;

        internal void BindPresenter(GameUiSession session, GameUiCommandWriter commands, IGameUiNavigation navigation,
            UI_GamePanel_RowRenderer rows, IGameBuildingUi buildingUi, UI_GamePanel_Court court,
            UI_GamePanel_Hud hud, UI_GamePanel_Soldier soldier)
        {
            sessionController = session ?? throw new ArgumentNullException(nameof(session));
            commandsController = commands ?? throw new ArgumentNullException(nameof(commands));
            this.navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
            rowsController = rows ?? throw new ArgumentNullException(nameof(rows));
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
                    commandsController.TryQueue(CommandRequests.RenameBuilding(BuildingId, value));
            });
            gameObject.SetActive(false);
        }

        internal void Open()
        {
            var selected = Sim.Find(sessionController.em, sessionController.selected);
            if (selected == Entity.Null || !sessionController.em.HasComponent<Building>(selected))
                return;
            isOpen = true;
            gameObject.SetActive(true);
            sessionController.nextRefresh = 0;
        }

        internal bool Hide()
        {
            var wasOpen = isOpen || gameObject.activeSelf;
            isOpen = false;
            rowsController.Clear(DetailsRows);
            gameObject.SetActive(false);
            sessionController.nextRefresh = 0;
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
            var stats = sessionController.em.GetComponentData<BuildingStats>(entity);
            var d = Sim.Definition(sessionController.em, sessionController.root, id.Definition);
            var session = sessionController.em.GetComponentData<Session>(sessionController.root);
            bool can = session.Phase == Phase.Day && session.Paused == 0 && session.CheckpointPending == 0;
            bool normal = Sim.Operational(sessionController.em, entity);
            card.Select(id.Id, id.Name.ToString());
            card.Name.interactable = can;
            card.Icon.sprite = buildingUi.BuildingSource(id.Definition)?.Icon;
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

                buildingUi.ConfirmBuildingCommand(CommandKind.Upgrade);
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
                    can && normal && planted ? () => buildingUi.ShowBuildingConfirmation("铲除 " + sessionController.Name(b.Crop), new[] { "失去当前作物与进度，不返种植费用。" }, () => commandsController.Send(CommandKind.ClearCrop, id.Id)) : null,
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
                warnings.Add("维护未满足：" + buildingUi.CostText(maintenance));
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
            var icon = buildingUi.BuildingSource(definition)?.Icon;
            if (icon != null)
                return icon;
            var harvest = Sim.Rule(sessionController.em, sessionController.root, definition, RuleKind.RewardItem);
            return harvest.Target >= 0 ? buildingUi.BuildingSource(harvest.Target)?.Icon : null;
        }

        internal void BuildingSkins(ulong key)
        {
            var e = Sim.Find(sessionController.em, key);
            if (e == Entity.Null || !sessionController.em.HasBuffer<BuildingVisualSlot>(e))
                return;
            var skins = new HashSet<string>();
            var b = sessionController.em.GetComponentData<Building>(e);
            bool can = courtController.CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0 && Sim.Operational(sessionController.em, e);
            buildingUi.ShowBuildingChoices("选择建筑皮肤", (rows, close) =>
            {
                foreach (var slot in sessionController.em.GetBuffer<BuildingVisualSlot>(e))
                    if (slot.Purpose == BuildingVisualPurpose.Operational && skins.Add(slot.Skin.ToString()))
                    {
                        var skin = slot.Skin.ToString();
                        rowsController.Row((skin == b.Skin.ToString() ? "✓ " : "") + (skin.Length == 0 ? "默认" : skin), can ? () =>
                        {
                            close();
                            commandsController.TryQueue(CommandRequests.ChangeBuildingSkin(key, skin));
                        } : null, parent: rows, key: "building-skin:" + key + ":" + skin);
                    }
                rowsController.Row("关闭", close, parent: rows);
            });
        }

        internal void BuildingCrops(ulong key)
        {
            var e = Sim.Find(sessionController.em, key);
            if (e == Entity.Null)
                return;
            var b = sessionController.em.GetComponentData<Building>(e);
            var d = Sim.Definition(sessionController.em, sessionController.root, sessionController.em.GetComponentData<Identity>(e).Definition);
            buildingUi.ShowBuildingChoices("选择作物", (rows, close) =>
            {
                if (b.Crop >= 0)
                    rowsController.Row("已种植 " + sessionController.Name(b.Crop) + "，更换前请先使用 X 铲除。", parent: rows);
                var seen = new HashSet<int>();
                for (int i = 0; i < d.RuleCount; i++)
                {
                    var r = Sim.GetRule(sessionController.em, sessionController.root, d.RuleStart + i);
                    if (!EconomyOps.Matches(r, RuleKind.Crop, b.Level) || !seen.Add(r.Target))
                        continue;
                    int crop = r.Target;
                    var costs = BuildingCostOps.Rules(sessionController.em, sessionController.root, crop, RuleKind.PlacementCost, 1);
                    bool can = courtController.CourtDay && sessionController.em.GetComponentData<Session>(sessionController.root).Paused == 0 && Sim.Operational(sessionController.em, e) && b.Crop < 0 && BuildingCostOps.CanPay(sessionController.em, sessionController.root, costs);
                    rowsController.Row(sessionController.Name(crop) + " · " + buildingUi.CostText(costs) + " · " + Sim.Definition(sessionController.em, sessionController.root, crop).Duration + " 回合", can ? () =>
                    {
                        close();
                        commandsController.TryQueue(CommandRequests.PlantCrop(key, crop));
                    } : null, parent: rows, key: "building-crop:" + key + ":" + crop);
                }
                rowsController.Row("关闭", close, parent: rows);
            });
        }

        internal void Refresh(Entity entity)
        {
            gameObject.SetActive(isOpen && !sessionController.intel);
            if (!isOpen || sessionController.intel)
                return;
            var b = sessionController.em.GetComponentData<Building>(entity);
            var stats = sessionController.em.GetComponentData<BuildingStats>(entity);
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var d = Sim.Definition(sessionController.em, sessionController.root, id.Definition);
            var day = sessionController.em.GetComponentData<Session>(sessionController.root).Phase == Phase.Day;
            var normal = b.Stage == LifeStage.Operational;
            rowsController.Clear(DetailsRows);
            void Detail(string text, Action action = null, string workers = null, string key = null,
                [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
            {
                if (workers == null)
                    rowsController.Row(text, action, parent: DetailsRows);
                else
                {
                    var row = rowsController.Item(WorkerInfoTemplate, text, action, DetailsRows,
                        key: "building:" + id.Id + ":worker:" + workers + ":" + (key ?? sourceLine.ToString()));
                    if (row == null || !row.CanRebind) return;
                    this.ConfigureSidebar(row.WorkerHover, () => WorkerDetails(entity, workers));
                }
            }

            RefreshBuildingCard(entity);
            Detail("查看本建筑账本 / 参考预测", () => navigation.OpenEconomy(id.Id));
            var source = buildingUi.BuildingSource(id.Definition);
            if (!string.IsNullOrEmpty(source?.Description))
                Detail(source.Description);
            var provider = ResourceNetworkOps.Provider(sessionController.em, sessionController.root, entity);
            if (b.Stage == LifeStage.Construction)
            {
                Detail($"施工 {b.Progress}/{d.Duration} 回合");
                Detail("下期材料：" + buildingUi.CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.ConstructionCost, b.Progress + 1)));
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
                    Detail("开始修复（" + duration + " 回合）", () => buildingUi.ConfirmBuildingCommand(CommandKind.Repair));
                return;
            }

            var maintenance = BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Maintenance, b.Level);
            if (maintenance.Count > 0)
                Detail("每回合维护：" + buildingUi.CostText(maintenance) + (b.Maintained != 0 ? " · 已满足" : " · 未满足"));
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
                Detail("原料：" + buildingUi.CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, id.Definition, RuleKind.Input, b.Level)), workers: "生产");
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
                Detail("供奉：" + buildingUi.CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, stats.HeroDefinition, RuleKind.Supply, 1)) + " · 唤醒：" + buildingUi.CostText(BuildingCostOps.Rules(sessionController.em, sessionController.root, stats.HeroDefinition, RuleKind.WakeCost, 1)));
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
        internal string WorkerDetails(Entity entity, string module = "全部") => WorkerEfficiencyOps.Describe(sessionController.em, sessionController.root, entity, module);

        internal void WorkforceDetails(Entity entity, bool editable)
        {
            var id = sessionController.em.GetComponentData<Identity>(entity);
            var q = WorkforceOps.Quote(sessionController.em, sessionController.root, entity);
            void Detail(string label, Action action = null) => rowsController.Row(label, action, parent: DetailsRows);
            Detail($"岗位 · 实际 {q.Workers}/{q.Capacity} · 当前可稳定 {q.CurrentStable} · 目标 {q.Target} · 付款后预计稳定 {q.PlannedStable}");
            Detail($"每回合预计补贴：-{q.SubsidyCost} {sessionController.Name(q.Gold)} · 当前正常库存 {q.Stock}" + (q.Stock < q.SubsidyCost ? "（不足，付款失败不会获得加成）" : ""));
            Detail(q.PaidTurn > 0 ? $"最近白天 {q.PaidTurn} 已付补贴：{q.Paid} {sessionController.Name(q.Gold)}" : "尚未支付过岗位补贴");
            Detail("目标只设置未来补贴，不立即扣钱或招人。低于环境稳定人数时不补贴，也不会强制裁员。结算先维护再算补贴，实际费用可能变化。");
            var row = rowsController.Item(WorkforceTemplate, "", parent: DetailsRows, key: "workforce:" + id.Id);
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
                    rowsController.Row($"{s.Label} · {label}：{s.Value:+0.##;-0.##;0}", s.Building != 0 ? () => buildingUi.FocusBuilding(s.Building) : null, parent: DetailsRows, key: "attraction:" + id.Id + ":" + s.Building + ":" + s.Definition + ":" + s.Label);
                }

                Detail($"未截断合计 {q.Raw:0.##} → 环境吸引力 {q.Natural:0.##}（0～100）\n已付补贴加成 {q.Paid * q.PerGold:0.##} → 当前 {q.Current:0.##}；计划付款后 {q.Planned:0.##}");
            }
        }

        internal void RepairDetails(Entity entity, Action<string, Action> detail)
        {
            var q = BuildingCostOps.QuoteRepair(sessionController.em, sessionController.root, entity);
            detail((sessionController.em.GetComponentData<Building>(entity).Stage == LifeStage.Repairing ? "已冻结修复总额：" : "拟定修复总额：") + buildingUi.CostText(q.Total) + " · 尚需 " + buildingUi.CostText(q.Remaining), null);
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
                    detail($"{sessionController.EntityName(s.Source)} · {s.Group} · 配置 {s.Amount} / 计入 {s.Applied} · {s.Reason}", () => buildingUi.FocusBuilding(s.Source));
                }
            }
        }

        internal void ResetSession()
        {
            isOpen = false;
            showSupplySources = showAttractionSources = showSpatialSources = false;
            workforceScale = null;
            gameObject.SetActive(false);
        }

    }
}
