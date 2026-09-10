using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Authoring;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        [Header("Building workflow (presentation only)")]
        public GameCatalogAsset BuildingCatalog;
        public RectTransform BuildingToolbar, BuildingDetailsRows, BuildingConfirmRows;
        public GameObject BuildingDetailsPanel, BuildingConfirmPanel;
        public Button BuildingDetailsButton, BuildingMoveButton, BuildingRangeButton, BuildingUpgradeButton, BuildingRepairButton, BuildingDemolishButton, BuildingDetailsClose;
        public Text BuildingHint;
        int buildDefinition = -1, buildRotation;
        ulong movingBuilding;
        int2? roadStart;
        bool showBuildingDetails, showBuildingRange;
        GameObject buildingGhost;
        readonly List<(Vector3 position, Vector3 size, Color color)> buildingOverlays = new List<(Vector3, Vector3, Color)>();
        int rangeRevision = -1; ulong rangeBuilding; float nextRangeRefresh;
        public ulong SelectedBuildingId => selected;
        public bool HasBuildingPlacement => buildDefinition >= 0 || movingBuilding != 0;

        void InitializeBuildings()
        {
            if (BuildingDetailsButton == null) return;
            BuildingDetailsButton.onClick.AddListener(() => { showBuildingDetails = !showBuildingDetails; nextRefresh = 0; });
            BuildingDetailsClose.onClick.AddListener(() => { showBuildingDetails = false; Clear(BuildingDetailsRows); BuildingDetailsPanel.SetActive(false); });
            BuildingMoveButton.onClick.AddListener(BeginMoveBuilding);
            BuildingRangeButton.onClick.AddListener(() => { showBuildingRange = !showBuildingRange; rangeRevision = -1; nextRefresh = 0; });
            BuildingUpgradeButton.onClick.AddListener(() => ConfirmBuildingCommand(CommandKind.Upgrade));
            BuildingRepairButton.onClick.AddListener(() => ConfirmBuildingCommand(CommandKind.Repair));
            BuildingDemolishButton.onClick.AddListener(() => ConfirmBuildingCommand(CommandKind.Demolish));
            InitializeBuildingCatalog();
            InitializeBuildingCard();
            BuildingToolbar.gameObject.SetActive(false); BuildingDetailsPanel.SetActive(false); BuildingConfirmPanel.SetActive(false);
        }
        public bool CancelBuildingInteraction()
        {
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) { BuildingConfirmPanel.SetActive(false); return true; }
            if (HasBuildingPlacement) { EndBuildingPlacement(); return true; }
            if (showBuildingRange) { showBuildingRange = false; buildingOverlays.Clear(); return true; }
            if (showBuildingDetails) { showBuildingDetails = false; BuildingDetailsPanel.SetActive(false); return true; }
            return false;
        }
        void EndBuildingPlacement()
        {
            buildDefinition = -1; movingBuilding = 0; roadStart = null;
            if (buildingGhost != null) Destroy(buildingGhost); buildingGhost = null; nextRefresh = 0;
            if (BuildingHint != null) BuildingHint.text = "";
        }
        void OnDisable() { if (buildingGhost != null) Destroy(buildingGhost); buildingGhost = null; EndInventoryDrag(); }
        void BeginMoveBuilding()
        {
            var entity = Sim.Find(em, selected); var quote = BuildingOps.CheckMove(em, root, entity);
            if (!quote.Allowed) { Message.text = quote.Reason; return; }
            EndBuildingPlacement(); movingBuilding = selected; var b = em.GetComponentData<Building>(entity); buildRotation = b.Rotation;
            CreateBuildingGhost(em.GetComponentData<Identity>(entity).Definition, b.Level, b.Skin.ToString());
        }
        void BeginBuildingPlacement(int definition)
        {
            var quote = BuildingOps.CheckBuild(em, root, definition);
            if (!quote.Allowed) { Message.text = quote.Reason; return; }
            EndBuildingPlacement(); buildDefinition = definition; buildRotation = 0;
            CreateBuildingGhost(definition, 1, Sim.Definition(em, root, definition).DefaultSkin.ToString());
        }
        ContentSource BuildingSource(int definition)
        {
            if (BuildingCatalog == null || !Sim.ValidDefinition(em, root, definition)) return null;
            var id = Sim.Definition(em, root, definition).Id.ToString(); var index = BuildingCatalog.Find(id);
            return index < 0 ? null : BuildingCatalog.Definitions[index].Data;
        }
        void CreateBuildingGhost(int definition, int level, string skin)
        {
            var source = BuildingSource(definition); if (source?.Prefab == null) return;
            buildingGhost = Instantiate(source.Prefab); buildingGhost.name = "BuildingPlacementPreview"; buildingGhost.hideFlags = HideFlags.DontSave;
            foreach (var collider in buildingGhost.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var behaviour in buildingGhost.GetComponentsInChildren<Behaviour>(true)) behaviour.enabled = false;
            var slot = BuildingVisualResolver.Select(buildingGhost, LifeStage.Operational, level, 1, skin, true);
            foreach (var renderer in buildingGhost.GetComponentsInChildren<MeshRenderer>(true))
            {
                var owner = renderer.GetComponentInParent<BuildingVisualSlotAuthoring>(); renderer.enabled = owner == null || owner == slot;
                var part = renderer.GetComponentInParent<BuildingVisualPartAuthoring>();
                if (part != null && !string.IsNullOrEmpty(part.CropId)) renderer.enabled = false;
                renderer.gameObject.layer = 2;
            }
        }
        bool BuildingInput(Mouse mouse, Vector3 point)
            => BuildingPointer(point, mouse.leftButton.wasPressedThisFrame, mouse.leftButton.wasReleasedThisFrame, mouse.rightButton.wasPressedThisFrame);
        bool BuildingPointer(Vector3 point, bool leftPressed, bool leftReleased, bool rightPressed)
        {
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) return true;
            if (!HasBuildingPlacement) return false;
            if (em.GetComponentData<Session>(root).Phase != Phase.Day || intel) { EndBuildingPlacement(); return true; }
            var moving = Sim.Find(em, movingBuilding); if (movingBuilding != 0 && !Sim.Operational(em, moving)) { EndBuildingPlacement(); return true; }
            if (rightPressed) { EndBuildingPlacement(); return true; }
            var definition = movingBuilding == 0 ? buildDefinition : em.GetComponentData<Identity>(moving).Definition;
            var grid = em.GetComponentData<GridData>(root); var cell = GridOps.Cell(grid, point); var d = Sim.Definition(em, root, definition);
            if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && d.BuildingPolicy.CanRotate != 0) buildRotation = (buildRotation + 1) % 4;
            var size = (buildRotation & 1) == 0 ? d.Size : d.Size.yx; var position = GridOps.Position(grid, cell, size);
            var quote = movingBuilding == 0 ? BuildingOps.CheckBuild(em, root, definition, cell, buildRotation) : BuildingOps.CheckMove(em, root, moving, cell, buildRotation);
            RoadPlan road = null;
            if (movingBuilding == 0 && BuildingRoadOps.IsRoad(em, root, definition) && roadStart.HasValue) { road = BuildingRoadOps.Plan(em, root, definition, roadStart.Value, cell); quote = road.Quote; }
            if (buildingGhost != null)
            {
                buildingGhost.transform.SetPositionAndRotation((Vector3)position + Vector3.up * .04f, Quaternion.Euler(0, buildRotation * 90, 0));
                var tint = new MaterialPropertyBlock(); tint.SetColor("_BaseColor", quote.Allowed ? new Color(.5f, 1, .65f, .65f) : new Color(1, .35f, .3f, .65f));
                foreach (var renderer in buildingGhost.GetComponentsInChildren<MeshRenderer>()) renderer.SetPropertyBlock(tint);
            }
            var action = movingBuilding == 0 ? "放置" : "移动";
            if (BuildingHint != null) BuildingHint.text = $"{action} {d.Name} · {size.x}×{size.y} · {CostText(quote.Costs)}" + (movingBuilding == 0 ? "" : $" · 经验 -{quote.ExperienceLoss}") + "\n" + (quote.Allowed ? "左键确认位置；R 旋转；右键/Esc 取消" : quote.Reason);
            if (movingBuilding == 0 && BuildingRoadOps.IsRoad(em, root, definition))
            {
                if (!roadStart.HasValue && leftPressed) { roadStart = cell; return true; }
                if (roadStart.HasValue && road != null)
                {
                    var finishedDrag = leftReleased && math.any(cell != roadStart.Value);
                    if (finishedDrag || leftPressed)
                    {
                        if (!quote.Allowed) { Message.text = quote.Reason; return true; }
                        var command = new Command { Kind = CommandKind.BuildRoad, Definition = definition, Position = GridOps.Position(grid, roadStart.Value, new int2(1)), EndPosition = point };
                        ShowBuildingConfirmation("铺设道路 · 新建 " + road.NewCells.Count + " 格（已有道路不收费）", new[] { CostText(quote.Costs) }, () => { SubmitBuilding(command); EndBuildingPlacement(); });
                    }
                }
                return true;
            }
            if (!leftPressed) return true;
            if (!quote.Allowed) { Message.text = quote.Reason; return true; }
            var pendingCommand = new Command { Kind = movingBuilding == 0 ? CommandKind.Build : CommandKind.MoveBuilding, Target = movingBuilding, Definition = definition, Position = point, Argument = buildRotation };
            if (movingBuilding == 0) SubmitBuilding(pendingCommand);
            else ShowBuildingConfirmation("确认移动 " + em.GetComponentData<Identity>(moving).Name, new[] { "材料：" + CostText(quote.Costs), "当前经验减少 " + quote.ExperienceLoss + "，等级/库存/岗位/驻军等身份状态保留。" }, () => { SubmitBuilding(pendingCommand); EndBuildingPlacement(); });
            return true;
        }
        void SubmitBuilding(Command command)
        {
            if (PauseMenu != null && PauseMenu.IsOpen || intel) return;
            command.RequestId = ++request; em.GetBuffer<Command>(root).Add(command); nextRefresh = 0; rangeRevision = -1;
        }
        string CostText(IEnumerable<BuildingCost> costs)
        {
            var lines = costs.Select(c => Name(c.Item) + " × " + c.Amount).ToArray(); return lines.Length == 0 ? "无资源费用" : string.Join("、", lines);
        }
        void ShowBuildingConfirmation(string title, IEnumerable<string> lines, Action confirm)
        {
            if (BuildingConfirmPanel == null) { Message.text = "建筑确认面板未接线，请执行建筑 UI 安装工具。"; return; }
            foreach (var label in BuildingConfirmPanel.GetComponentsInChildren<Text>(true)) if (!label.transform.IsChildOf(BuildingConfirmRows)) label.text = "确认操作";
            Clear(BuildingConfirmRows); Row(title, parent: BuildingConfirmRows);
            foreach (var line in lines) Row(line, parent: BuildingConfirmRows);
            Row("确认", () => { BuildingConfirmPanel.SetActive(false); confirm(); }, parent: BuildingConfirmRows);
            Row("取消", () => BuildingConfirmPanel.SetActive(false), parent: BuildingConfirmRows);
            var modalCanvas=BuildingConfirmPanel.GetComponent<Canvas>();if(modalCanvas==null)modalCanvas=BuildingConfirmPanel.AddComponent<Canvas>();modalCanvas.overrideSorting=true;modalCanvas.sortingOrder=400;
            if(BuildingConfirmPanel.GetComponent<GraphicRaycaster>()==null)BuildingConfirmPanel.AddComponent<GraphicRaycaster>();
            var modalGroup=BuildingConfirmPanel.GetComponent<CanvasGroup>();if(modalGroup==null)modalGroup=BuildingConfirmPanel.AddComponent<CanvasGroup>();modalGroup.ignoreParentGroups=true;
            BuildingConfirmPanel.SetActive(true); BuildingConfirmPanel.transform.SetAsLastSibling();
        }
        void ConfirmBuildingCommand(CommandKind kind)
        {
            var entity = Sim.Find(em, selected); if (entity == Entity.Null || !em.HasComponent<Building>(entity)) return;
            var id = em.GetComponentData<Identity>(entity); var b = em.GetComponentData<Building>(entity); var lines = new List<string>();
            var title = "";
            if (kind == CommandKind.Upgrade)
            {
                var quote = BuildingOps.CheckUpgrade(em, root, entity); if (!quote.Allowed) { Message.text = quote.Reason; return; }
                title = "升级 " + id.Name + " → LV" + (b.Level + 1); lines.Add("费用：" + CostText(quote.Costs));
                lines.Add("保留建筑身份与现有状态，按新等级更新能力和模型。");
            }
            else if (kind == CommandKind.Repair)
            {
                if (b.Stage != LifeStage.Ruined || b.RuinPending != 0) return;
                var costs = BuildingCostOps.RepairTotal(em, root, entity, out var turns); title = "修复 " + id.Name;
                lines.Add("共 " + turns + " 回合，逐回合扣费，无需工人。"); lines.Add("总费用：" + CostText(costs));
                lines.Add("先用待存放材料，再用正常库存；普通库存断连或当期材料不足时暂停，不吞进度。");
                var payment = BuildingCostOps.QuoteRepair(em, root, entity);
                foreach (var c in payment.Payments) lines.Add($"首期 {Name(c.Item)}：待存放 {c.Pending} + 普通库存 {c.Normal}，缺口 {c.Missing}");
                lines.Add(payment.Reason + "；开始修复不会立即扣除材料。");
                lines.Add("完工保留等级/名称/皮肤，不自动招工或召回驻军；库存为空，住房黎明迁入 2 人（不超过容量）。");
            }
            else
            {
                if (em.GetComponentData<BuildingStats>(entity).IsCore != 0) return; title = "拆除 " + id.Name;
                lines.Add("此操作删除建筑。修复投入不返还，按已支付建造/升级成本计算返还。");
                foreach (var slot in em.GetBuffer<InventorySlot>(root)) if (slot.Provider == id.Id && slot.Count > 0) lines.Add("原库存损失：" + Name(slot.Item) + " × " + slot.Count);
                foreach (var refund in BuildingCostOps.DemolitionRefund(em, root, entity)) lines.Add($"返还 {Name(refund.Item)} × {refund.Amount}：存入 {refund.Stored}，空间不足损失 {refund.Lost}");
                var soldiers = MilitaryOps.GarrisonCount(em, id.Id); var empty = 0;
                using (var buildings = Sim.Entities<Building>(em)) foreach (var other in buildings) if (other != entity && Sim.Operational(em, other)) empty += math.max(0, em.GetComponentData<BuildingStats>(other).Garrison - MilitaryOps.GarrisonCount(em, em.GetComponentData<Identity>(other).Id));
                if (soldiers > 0) lines.Add($"{soldiers} 名士兵转待分配池；其他驻地空槽 {empty}，下一次入夜前未安排将解散。");
                if (b.Workers > 0) lines.Add(b.Workers + " 名工人失业。");
                if (b.Population > 0) lines.Add("住宅中的 " + b.Population + " 人将损失，请确认人口后果。");
                if (em.GetComponentData<BuildingStats>(entity).HeroDefinition >= 0) lines.Add("已招募的关联英雄死亡、经验清零并进入重招冷却。");
                if (EconomyOps.WorkforceLocked(em, id.Id)) lines.Add("在途远征将终止，进度、携带物资和奖励不保留。");
            }
            ShowBuildingConfirmation(title, lines, () => SubmitBuilding(new Command { Kind = kind, Target = id.Id, Argument = 1 }));
        }
        void BuildCatalogRows()
        {
            if (!FeatureOps.Unlocked(em, root, "Building")) Row("建造许可尚未解锁。先完成镜头与收集材料的主线任务；地图上的资源堆仍可采集。", () => OpenPanel("任务"));
        }
        void RefreshBuildingDetails()
        {
            var entity = Sim.Find(em, selected); var has = entity != Entity.Null && em.HasComponent<Building>(entity);
            if (BuildingToolbar != null) BuildingToolbar.gameObject.SetActive(has && !HasBuildingPlacement && !intel);
            if (!has) { Selection.text = "点击建筑显示操作条；WASD 镜头，滚轮缩放。"; if (BuildingDetailsPanel != null) BuildingDetailsPanel.SetActive(false); return; }
            var b = em.GetComponentData<Building>(entity); var stats = em.GetComponentData<BuildingStats>(entity); var id = em.GetComponentData<Identity>(entity); var d = Sim.Definition(em, root, id.Definition);
            var day = em.GetComponentData<Session>(root).Phase == Phase.Day; var normal = b.Stage == LifeStage.Operational;
            Selection.text = $"{id.Name} · LV{b.Level} · {BuildingStageName(b.Stage)}";
            if (BuildingToolbar == null) return;
            BuildingMoveButton.gameObject.SetActive(day && normal); BuildingMoveButton.interactable = BuildingOps.CheckMove(em, root, entity).Allowed;
            BuildingUpgradeButton.gameObject.SetActive(day && normal && b.Level < d.Level); BuildingUpgradeButton.interactable = BuildingOps.CheckUpgrade(em, root, entity).Allowed;
            BuildingRepairButton.gameObject.SetActive(day && b.Stage == LifeStage.Ruined); BuildingRepairButton.interactable = b.RuinPending == 0;
            BuildingDemolishButton.gameObject.SetActive(day && stats.IsCore == 0);
            BuildingDetailsPanel.SetActive(showBuildingDetails && !intel);
            NameInput.interactable = day; Rename.interactable = day;
            if (!showBuildingDetails || intel) return;
            Clear(BuildingDetailsRows);
            void Detail(string text, Action action = null) => Row(text, action, parent: BuildingDetailsRows);
            RefreshBuildingCard(entity);
            Detail("查看本建筑账本 / 参考预测", () => OpenEconomy(id.Id));
            var source = BuildingSource(id.Definition); if (!string.IsNullOrEmpty(source?.Description)) Detail(source.Description);
            if(stats.IsCore==0){var h=em.GetComponentData<Health>(entity);Detail($"耐久 {h.Current:0}/{h.Maximum:0}");}
            var provider=ResourceNetworkOps.Provider(em,root,entity);
            if (b.Stage == LifeStage.Construction) { Detail($"施工 {b.Progress}/{d.Duration} 回合"); Detail("下期材料：" + CostText(BuildingCostOps.Rules(em, root, id.Definition, RuleKind.ConstructionCost, b.Progress + 1))); if (provider == Entity.Null) Detail("需要从正常库存支付的施工：断连时暂停。"); }
            if (b.Stage == LifeStage.Ruined || b.Stage == LifeStage.Repairing)
            {
                BuildingCostOps.RepairTotal(em, root, entity, out var duration);
                Detail(b.Stage == LifeStage.Ruined ? "荒废：无建筑功能；修复无需工人。" : $"修复 {b.Progress}/{b.RepairDuration} 回合，材料不足或断连暂停。");
                if (b.RuinPending != 0) Detail($"本夜已失效，黎明提交居民损失 {b.Population} / 工人失业 {b.Workers}。库存已标记损失。");
                RepairDetails(entity, Detail);
                if (day && b.Stage == LifeStage.Ruined) Detail("开始修复（" + duration + " 回合）", () => ConfirmBuildingCommand(CommandKind.Repair));
                return;
            }
            var maintenance = BuildingCostOps.Rules(em, root, id.Definition, RuleKind.Maintenance, b.Level); if (maintenance.Count > 0) Detail("每回合维护：" + CostText(maintenance) + (b.Maintained != 0 ? " · 已满足" : " · 未满足"));
            if (stats.JobCapacity > 0)
            {
                CompactWorkforceActions(entity, day && normal);
            }

            if (stats.MaxPopulation > 0)
            {
                Detail($"居住 {b.Population}/{stats.MaxPopulation} · 增长进度 {b.Growth} · 税收进度 {b.TaxProgress} · 连续缺粮 {b.FoodFailures}");
                if (b.DeferredResidents > 0) Detail("修复迁入人口将在黎明加入：" + b.DeferredResidents);
                foreach (var food in em.GetBuffer<FoodSelection>(entity)) Detail("上次实际食物：" + Name(food.Item) + " × " + food.Amount);
            }
            var production = Sim.Rule(em, root, id.Definition, RuleKind.Production, b.Level);
            if (production.Level >= 0) { Detail($"生产周期进度 {b.ProductionProgress}/{production.Amount} · 所需工人 {production.B}"); Detail("原料：" + CostText(BuildingCostOps.Rules(em, root, id.Definition, RuleKind.Input, b.Level))); }
            for (var i = 0; i < d.RuleCount; i++)
            {
                var r = Sim.GetRule(em, root, d.RuleStart + i); if (r.Level != 0 && r.Level != b.Level) continue;
                if (r.Kind == RuleKind.ProductionTier) Detail("产品：" + Name(r.Target) + " × " + r.Amount + " · 工人 " + r.B + (r.C > 0 ? "～" + r.C : " 以上") + (b.Workers >= r.B && (r.C <= 0 || b.Workers <= r.C) ? "（当前档）" : ""));
                if (r.Kind == RuleKind.Food) Detail($"食谱：{Name(r.Target)} · {r.Amount} 种 · 每居民每种 {math.max(1, r.B)}");
                if (r.Kind == RuleKind.Environment) Detail($"环境条件 {EnvironmentName(r.B)} ≥ {r.Amount} · 当前 {EconomyOps.SpatialValue(em, root, entity, r.B):0.#}");
                if (r.Kind == RuleKind.SpatialEffect) Detail($"作用 {EnvironmentName(r.B)} +{r.Amount} · 范围 {r.Value} 格 · 工人 ≥{r.C}");
                if (r.Kind == RuleKind.Market) Detail("市场结算比例：" + r.Value + " · 本回合归因价值 " + b.MarketValue);
                if (r.Kind == RuleKind.Harvest) Detail("剩余采集次数：" + b.HarvestRemaining, day && normal ? () => Send(CommandKind.Harvest, id.Id) : null);

            }
            if (b.Crop >= 0)
            {
                Detail($"作物 {Name(b.Crop)} · 生长 {b.CropProgress}/{Sim.Definition(em, root, b.Crop).Duration}");
                if (day && normal) { Detail("收获", () => Send(CommandKind.Harvest, id.Id)); Detail(b.AutoHarvest != 0 ? "关闭自动收获" : "开启自动收获", () => Send(CommandKind.AutoHarvest, id.Id, amount: b.AutoHarvest == 0 ? 1 : 0)); }
            }
            if (stats.Garrison > 0)
            {
                if (normal && (em.GetComponentData<Session>(root).Phase == Phase.Night || em.GetComponentData<Session>(root).Phase == Phase.Retreat)) { Detail("召回所属士兵", () => Send(CommandKind.RecallGarrison, id.Id)); Detail("取消途中召回", () => Send(CommandKind.RecallGarrison, id.Id, argument: 1)); }
            }
            if (stats.BellRadius > 0) Detail("警铃集结范围 " + stats.BellRadius, !day && normal ? () => Send(CommandKind.Bell, id.Id) : null);
            if (stats.Intelligence > 0) Detail("情报贡献 " + stats.Intelligence, () => OpenPanel("情报"));
            if (Sim.Rule(em, root, id.Definition, RuleKind.ExpeditionSite, b.Level).Level >= 0) Detail(EconomyOps.WorkforceLocked(em, id.Id) ? "远征在途，岗位/移动/升级锁定" : "打开远征", () => OpenPanel("远征"));
            if (stats.HeroDefinition >= 0)
            {
                var hero = Sim.Definition(em, root, stats.HeroDefinition); Detail($"{hero.Name} · 招募 {hero.Cost} 金币 · 人口 {hero.Population} · 神殿所需工人 {stats.RequiredWorkers}");
                Detail("供奉：" + CostText(BuildingCostOps.Rules(em, root, stats.HeroDefinition, RuleKind.Supply, 1)) + " · 唤醒：" + CostText(BuildingCostOps.Rules(em, root, stats.HeroDefinition, RuleKind.WakeCost, 1)));
                Detail($"当前工人 {b.Workers}/{stats.RequiredWorkers} · 持续供奉{(b.Offering == 0 ? "关闭" : "开启")} · 本回合供奉{(b.PaidOfferingTurn == em.GetComponentData<Session>(root).Turn ? "已支付" : "未支付")}");
                Detail("供奉在平安夜也消耗资源；唤醒另外付费，未实际参战不获得战斗经验。英雄阵亡后经验清零，冷却结束重招支付完整费用。缺工不会杀死英雄，神殿荒废/拆除会。");
                bool wake = !day; string reason = MilitaryOps.HeroAvailability(em, root, entity, wake);
                Detail((wake ? "唤醒英雄" : "招募英雄") + (reason.Length > 0 ? " · " + reason : ""), reason.Length == 0 ? () => Send(wake ? CommandKind.WakeHero : CommandKind.RecruitHero, id.Id) : null);
                if (normal && day) Detail(b.Offering == 0 ? "开启持续供奉" : "关闭持续供奉", () => Send(CommandKind.Offering, id.Id, amount: b.Offering == 0 ? 1 : 0));
            }
        }
        static string EnvironmentName(int kind) => kind == 20 ? "美观" : kind == 30 ? "医疗" : kind == 40 ? "治安" : kind == 10 ? "生产/作物收益百分比" : "效果 " + kind;
        static string BuildingStageName(LifeStage stage) => stage == LifeStage.Construction ? "施工中" : stage == LifeStage.Ruined ? "建筑荒废" : stage == LifeStage.Repairing ? "修复中" : "正常运营";
        void FocusBuilding(ulong id)
        {
            var entity = Sim.Find(em, id); if (entity == Entity.Null) return;
            LocateHistory(id,Sim.Position(em,entity));
            selected = id; nextRefresh = 0;
        }
        void DrawBuildingInteraction(Action<float3, Vector3, Color> draw)
        {
            var entity = Sim.Find(em, selected); var grid = em.GetComponentData<GridData>(root);
            if (entity != Entity.Null && em.HasComponent<Building>(entity) && BuildingToolbar != null && BuildingToolbar.gameObject.activeSelf)
            {
                var screen = Camera.WorldToScreenPoint(Sim.Position(em, entity) + new float3(0, 2, 0)); var parent = (RectTransform)BuildingToolbar.parent;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out var local))
                { local.x = Mathf.Clamp(local.x, parent.rect.xMin + 320, parent.rect.xMax - 320); local.y = Mathf.Clamp(local.y + 30, parent.rect.yMin + 50, parent.rect.yMax - 100); BuildingToolbar.anchoredPosition = local; }
            }
            if (showBuildingRange && !HasBuildingPlacement && (Panel=="建筑"||showBuildingDetails) && entity != Entity.Null && em.HasComponent<Building>(entity) && !intel)
            {
                if (rangeBuilding != selected || rangeRevision != grid.Revision || Time.unscaledTime >= nextRangeRefresh)
                {
                    rangeBuilding = selected; rangeRevision = grid.Revision; nextRangeRefresh = Time.unscaledTime + 1; buildingOverlays.Clear();
                    using var distances = BuildingRangeOps.Reach(em, root, entity, Allocator.Temp);
                    for (var i = 0; i < distances.Length; i++) if (math.isfinite(distances[i])) { var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x); buildingOverlays.Add((GridOps.Position(grid, cell, new int2(1)), new Vector3(grid.CellSize, .035f, grid.CellSize), new Color(.1f, .65f, 1, .22f))); }
                    var b = em.GetComponentData<Building>(entity); var def = em.GetComponentData<Identity>(entity).Definition; var d = Sim.Definition(em, root, def);
                    for (var i = 0; i < d.RuleCount; i++) { var r = Sim.GetRule(em, root, d.RuleStart + i); if (!EconomyOps.Matches(r, RuleKind.SpatialEffect, b.Level)) continue; var radius = (int)math.ceil(r.Value); for (var y = -radius; y < b.Size.y + radius; y++) for (var x = -radius; x < b.Size.x + radius; x++) { var gap = math.max(0, -x) + math.max(0, x - b.Size.x + 1) + math.max(0, -y) + math.max(0, y - b.Size.y + 1); if (gap > r.Value || GridOps.Index(grid, b.Cell + new int2(x, y)) < 0) continue; buildingOverlays.Add((GridOps.Position(grid, b.Cell + new int2(x, y), new int2(1)), new Vector3(grid.CellSize * .7f, .05f, grid.CellSize * .7f), new Color(.8f, .3f, 1, .35f))); } }
                    var provider = ResourceNetworkOps.Provider(em, root, entity); var path=BuildingRangeOps.ProviderPath(em,root,provider,distances);ResourcePathCellCount=path.Count;foreach(var pathCell in path)buildingOverlays.Add((GridOps.Position(grid,pathCell,new int2(1)),new Vector3(grid.CellSize*.28f,.085f,grid.CellSize*.28f),new Color(.15f,1,.3f,.9f))); if (provider != Entity.Null) { var p = em.GetComponentData<Building>(provider); buildingOverlays.Add((Sim.Position(em, provider), new Vector3(p.Size.x * grid.CellSize, .1f, p.Size.y * grid.CellSize), new Color(.2f, 1, .3f, .65f))); }
                }
                foreach (var overlay in buildingOverlays) draw(overlay.position, overlay.size, overlay.color);
            }
            if (!HasBuildingPlacement || Mouse.current == null) return;
            if (!GroundPoint(Camera.ScreenPointToRay(Mouse.current.position.ReadValue()), out var point)) return;
            var moving = Sim.Find(em, movingBuilding); if (movingBuilding != 0 && moving == Entity.Null) return;
            var definition = movingBuilding == 0 ? buildDefinition : em.GetComponentData<Identity>(moving).Definition; var cellAt = GridOps.Cell(grid, point); var definitionData = Sim.Definition(em, root, definition); var size = (buildRotation & 1) == 0 ? definitionData.Size : definitionData.Size.yx;
            var quote = movingBuilding == 0 ? BuildingOps.CheckBuild(em, root, definition, cellAt, buildRotation) : BuildingOps.CheckMove(em, root, moving, cellAt, buildRotation);
            if (roadStart.HasValue && movingBuilding == 0) { var plan = BuildingRoadOps.Plan(em, root, definition, roadStart.Value, cellAt); foreach (var cell in plan.Path) draw(GridOps.Position(grid, cell, new int2(1)), new Vector3(grid.CellSize, .08f, grid.CellSize), plan.Quote.Allowed ? Color.green : Color.red); }
            else draw(GridOps.Position(grid, cellAt, size), new Vector3(size.x * grid.CellSize, .08f, size.y * grid.CellSize), quote.Allowed ? new Color(.2f, 1, .3f, .5f) : new Color(1, .2f, .1f, .5f));
        }
    }
}
