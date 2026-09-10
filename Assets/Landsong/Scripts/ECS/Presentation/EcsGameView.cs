using System;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    // Presentation-only adapter: every gameplay change is submitted as an ECS Command.
    public sealed partial class EcsGameView : MonoBehaviour
    {
        public Camera Camera;
        public Text Status, Selection, Message, Moon;
        public Slider MoonProgress;
        public Button Advance;
        public RectTransform PrimaryRows, SecondaryRows;
        public GameObject RowTemplate;
        public InputField NameInput;
        public Button Rename;
        public Mesh OverlayMesh;
        public Material OverlayMaterial;
        public GamePauseMenu PauseMenu;
        public string Panel = "建筑";
        EntityManager em;
        Entity root;
        ulong selected, selectedSoldier, request;
        float nextRefresh;
        bool intel;
        Phase observedPhase;
        float clickTime;
        readonly StringBuilder text = new StringBuilder();
        public void OpenPanel(string panel)
            => OpenPanelCore(panel, true);
        void OpenPanelCore(string panel, bool remember)
        {
            if(SoldierDetailsOpen||MarriageOpen||PersonRequestsOpen||PortraitOpen)return;
            if(panel!="王室")RestoreRoyalPrimary();
            if (PauseMenu != null && PauseMenu.IsOpen) return;
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) return;
            if (panel == "存档" && PauseMenu != null) { PauseMenu.Open(); return; }
            if (panel == "科技" && (root == Entity.Null || !em.Exists(root) || !ResearchOps.Unlocked(em, root))) return;
            if ((panel == "库存" || panel == "远征") && (root == Entity.Null || !em.Exists(root) || !FeatureOps.Unlocked(em, root, PanelFeature(panel)))) return;
            bool enteringIntel = panel == "情报";
            if (intel != enteringIntel) Send(CommandKind.IntelligenceMode, argument: enteringIntel ? 1 : 0);
            if (enteringIntel) { EndBuildingPlacement(); EndInventoryDrag(); intelligenceWave = 0; intelligenceView = null; if (BuildingConfirmPanel != null) BuildingConfirmPanel.SetActive(false); }
            if (remember && IsPanelOpen && Panel != panel) { if (panelHistory.Count >= 16) panelHistory.Clear(); panelHistory.Push(Panel); }
            if (Panel != panel) { EndInventoryDrag(); if (panel != "建筑") EndBuildingPlacement(); if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null); }
            Panel = panel; IsPanelOpen = true; intel = enteringIntel; nextRefresh = 0;
            if (panel != "建筑") { showBuildingDetails = false; if (BuildingDetailsPanel != null) BuildingDetailsPanel.SetActive(false); }
            buildingBarOpen = panel == "建筑";
            if (BuildingBar != null && panel != "建筑") BuildingBar.gameObject.SetActive(false);
            if (TechnologyTree != null && panel != "科技") TechnologyTree.gameObject.SetActive(false);
            if (QuestWindow != null && panel != "任务") QuestWindow.SetActive(false);
            if(courtGraph!=null&&panel!="王室"&&panel!="人才"&&panel!="政策")courtGraph.gameObject.SetActive(false);
            RefreshPanelVisibility();
        }
        void Start()
        {
            Advance.onClick.AddListener(RequestAdvance);
            Rename.onClick.AddListener(() => Send(CommandKind.Rename, selected, text: NameInput.text));
            InitializeBuildings(); InitializeFeatureButtons(); InitializeIntelligence();
            InitializeInterface();
            InitializePanelWindows(); InitializeResearchHud();
        }
        void RequestAdvance()
        {
            if (root == Entity.Null || !em.Exists(root)) return;
            if (intel) return;
            if (HasBuildingPlacement) EndBuildingPlacement();
            if (em.GetComponentData<Session>(root).Phase == Phase.Report) { OpenPanel("战报"); return; }
            Send(CommandKind.Advance);
        }
        void Update()
        {
            if (!EcsSceneFlow.GameReady) return;
            var world = World.DefaultGameObjectInjectionWorld;
            if (world == null || !world.IsCreated) return;
            em = world.EntityManager; root = Sim.Root(em);
            if (root == Entity.Null || !em.HasComponent<SimulationReady>(root)) return;
            ConsumeInterfaceEvents(em.GetComponentData<Session>(root));
            RefreshResearchHud();
            RefreshMarriageEvents();
            RefreshPersonRequests();
            RefreshPortraitCustomization();
            Input();
            if (TextFocused || inventoryDragging || inventoryAmountInput != null && inventoryAmountInput.isFocused || militaryNameInput != null && militaryNameInput.isFocused || workforceScale != null && workforceScale.Interacting) return;
            if (Time.unscaledTime < nextRefresh) return;
            // Do not replace a button between pointer-down and EventSystem's pointer-up/click dispatch.
            if (Mouse.current != null && (Mouse.current.leftButton.isPressed || Mouse.current.leftButton.wasReleasedThisFrame)) return;
            nextRefresh = Time.unscaledTime + .25f; Refresh();
        }
        public void Send(CommandKind kind, ulong target = 0, ulong other = 0, int definition = -1, int amount = 0, int argument = 0, string text = null, Vector3 position = default)
        {
            if(SoldierDetailsOpen&&kind!=CommandKind.RenameSoldier&&kind!=CommandKind.Pause)return;
            if (!EcsSceneFlow.GameReady) return;
            if((MarriageOpen||PersonRequestsOpen||PortraitOpen) && kind!=CommandKind.Pause)return;
            if (root == Entity.Null || !em.Exists(root)) return;
            if (intel && kind != CommandKind.IntelligenceMode && kind != CommandKind.ReadIntelligence && kind != CommandKind.CameraMoved && kind != CommandKind.CameraZoomed && kind != CommandKind.Pause && kind != CommandKind.Save && kind != CommandKind.Load) return;
            if (PauseMenu != null && PauseMenu.IsOpen && kind != CommandKind.Pause && kind != CommandKind.Save && kind != CommandKind.Load) return;
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf && kind != CommandKind.Pause) return;
            var command = new Command { Kind = kind, RequestId = ++request, Target = target, Other = other, Definition = definition, Amount = amount, Argument = argument, Position = position, Text = new FixedString128Bytes(kind == CommandKind.Rename || kind == CommandKind.RenameSoldier ? BuildingOps.SanitizeName(text) : text ?? "") };
            em.GetBuffer<Command>(root).Add(command); nextRefresh = 0;
        }
        string Name(int definition) => Sim.ValidDefinition(em, root, definition) ? Sim.Definition(em, root, definition).Name.ToString() : "—";
        string EntityName(ulong id) { var e = Sim.Find(em, id); return e == Entity.Null ? "无驻地" : em.GetComponentData<Identity>(e).Name.ToString(); }
        void Input()
        {
            var mouse = Mouse.current; var keyboard = Keyboard.current;
            if(SoldierDetailsOpen){if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)CloseSoldierDetails();return;}
            if(PortraitOpen){if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)ClosePortrait();return;}
            if(PersonRequestsOpen){if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)ClosePersonRequests();return;}
            if(MarriageOpen){if(keyboard!=null&&keyboard.escapeKey.wasPressedThisFrame)CloseMarriage();return;}
            if (PauseMenu != null && keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { PauseMenu.Escape(); cameraDragging=false; touchBlocked=true; return; }
            if (PauseMenu != null && PauseMenu.IsOpen) return;
            if (BuildingConfirmPanel != null && BuildingConfirmPanel.activeSelf) { if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) CancelBuildingInteraction(); return; }
            if (Panel == "科技" || Panel == "任务") { if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) OpenPanel("建筑"); return; }
            if (keyboard != null && !TextFocused)
            {
                if (keyboard.escapeKey.wasPressedThisFrame) { if (!CancelBuildingInteraction() && intel) OpenPanel("建筑"); }
                if (!intel && keyboard[InterfaceSettings.Current.Pause].wasPressedThisFrame) Send(CommandKind.Pause);
                if (!intel) HeroHotkeys(keyboard);
            }
            if (CameraInput(mouse,keyboard)) return;
            var ray = Camera.ScreenPointToRay(mouse.position.ReadValue());
            if (!GroundPoint(ray, out var point)) return;
            if (intel) return;
            if (BuildingInput(mouse, point)) return;
            if (mouse.rightButton.wasPressedThisFrame)
            {
                Send(CommandKind.MoveHero, position: point);
            }
            if (!mouse.leftButton.wasPressedThisFrame) return;
            WorldClick(point);
        }
        bool GroundPoint(Ray ray, out Vector3 point)
        {
            var found = GridOps.RaycastSurface(em.GetComponentData<GridData>(root), ray.origin, ray.direction, out var hit);
            point = hit; return found;
        }
        Entity Hit(Vector3 point, bool enemiesOnly)
        {
            var nearest = Entity.Null; var score = float.MaxValue;
            using var all = Sim.Entities<Identity>(em);
            foreach (var e in all)
            {
                if (!em.HasComponent<LocalTransform>(e)) continue;
                // Restored people have a transform for snapshot symmetry, not a world click target.
                if (!em.HasComponent<Building>(e) && !em.HasComponent<Combatant>(e) && !em.HasComponent<Loot>(e) && !em.HasComponent<Opportunity>(e)) continue;
                if (enemiesOnly && (!em.HasComponent<Combatant>(e) || em.GetComponentData<Combatant>(e).Faction != 1 || !Sim.Alive(em, e))) continue;
                if (em.HasComponent<VisualState>(e) && em.GetComponentData<VisualState>(e).Visible == 0) continue;
                var position = Sim.Position(em, e);
                var delta = math.abs(position.xz - new float2(point.x, point.z));
                var radius = new float2(.8f);
                if (em.HasComponent<Loot>(e) || em.HasComponent<Opportunity>(e)) radius = new float2(1.5f);
                if (em.HasComponent<Building>(e)) radius = (float2)em.GetComponentData<Building>(e).Size * em.GetComponentData<GridData>(root).CellSize * .5f;
                if (math.any(delta > radius)) continue;
                var value = math.lengthsq(delta) + (em.HasComponent<Building>(e) ? 10 : 0);
                if (em.HasComponent<Loot>(e) || em.HasComponent<Opportunity>(e)) value -= 50;
                if (value < score) { nearest = e; score = value; }
            }
            return nearest;
        }
        void Refresh()
        {
            RefreshSoldierDetails();
            var s = em.GetComponentData<Session>(root); var population = Sim.Population(em, root);
            if (intel && s.IntelligenceMode == 0)
            {
                bool queued = false; foreach (var command in em.GetBuffer<Command>(root)) if (command.Kind == CommandKind.IntelligenceMode && command.Argument == 1) queued = true;
                if (!queued) { intel = false; ClosePanel(); intelligenceView = null; }
            }
            RefreshIntelligenceBadge();
            if (observedPhase == Phase.GameOver && s.Phase != Phase.GameOver && s.Phase != Phase.Ended)
            {
                Panel = "建筑"; ClosePanel(); Message.text = ""; selectedSoldier = 0;
                EndBuildingPlacement(); if (BuildingConfirmPanel != null) BuildingConfirmPanel.SetActive(false);
                rangeRevision = -1;
            }
            observedPhase = s.Phase;
            Status.text = PresentationText.Get("Gameplay/gameplay.ecs.turn_status","{0}　白天 {1} / 夜晚 {1}　{2}　人口 {3}（空闲 {4}）",s.DynastyName.ToString(),s.Turn,PresentationText.Source(PhaseName(s.Phase)),population,math.max(0,population-Sim.Employed(em)));
            Advance.GetComponentInChildren<Text>().text = s.Phase == Phase.Report ? "今晚战报" : "下一阶段";
            Advance.interactable = !intel && s.Paused == 0 && s.CheckpointPending == 0 && (s.Phase == Phase.Day || s.Phase == Phase.Report);
            MoonProgress.gameObject.SetActive(s.Phase != Phase.Day); MoonProgress.SetValueWithoutNotify(NightOps.Progress(em, root));
            Moon.text = s.Paused != 0 ? "已暂停" : s.Phase == Phase.Night ? "月亮进度" : PhaseName(s.Phase);
            RefreshHeroHud();
            if (s.Phase == Phase.GameOver || s.Phase == Phase.Ended) { Panel = "王朝终局"; IsPanelOpen = true; intel = false; }
            RefreshTechnologyAccess(); RefreshFeatureAccess();
            reconcilingRows = true;
            Clear(PrimaryRows); Clear(SecondaryRows);
            RefreshBuildingCatalog();
            RefreshPanelVisibility();
            Details();
            var namedBuilding = Sim.Find(em, selected);
            bool naming = !intel && s.Phase == Phase.Day && Panel == "建筑" && namedBuilding != Entity.Null && em.HasComponent<Building>(namedBuilding);
            NameInput.gameObject.SetActive(showBuildingDetails && !intel); Rename.gameObject.SetActive(false);
            if (intel) Selection.text = "情报模式：WASD / 滚轮调整镜头；退出后恢复操作。";
            RefreshQuestTracking();
            if (!intel && s.Phase == Phase.Night && s.NightKind == NightKind.Peaceful) Row(s.NightSpeed == 2 ? "平安夜速度 2×（切回 1×）" : "平安夜速度 1×（切换 2×）", s.Paused == 0 ? () => Send(CommandKind.NightSpeed, amount: s.NightSpeed == 2 ? 1 : 2) : null);
            if (s.Phase == Phase.Day && em.GetBuffer<BattleReportEntry>(root).Length > 0 && Panel != "战报") Row("查看上一晚结算（含被盗物资）", () => OpenPanel("战报"));
            if (IsPanelOpen) switch (Panel)
            {
                case "经济": Economy(); break;
                case "历史": HistoryRows(); break;
                case "建筑": BuildMenu(); break;
                case "库存": Inventory(); break;
                case "驻军": Military(); break;
                case "科技": Research(); break;
                case "任务": Quests(); break;
                case "远征": Expeditions(); break;
                case "人才": Talents(); break;
                case "王室": Royals(); break;
                case "政策": Policies(); break;
                case "情报": Intelligence(); break;
                case "战报": Report(); break;
                case "王朝终局": GameOver(); break;
                case "入夜确认": ConfirmNight(); break;
            }
            FinishRows();
            RefreshCourtPresentation();
        }
        static string PhaseName(Phase p) => p == Phase.Day ? "白天建造" : p == Phase.Night ? "夜晚" : p == Phase.Deployment ? "出勤" : p == Phase.Retreat ? "敌军撤离" : p == Phase.Celebration ? "战后收尾" : p == Phase.Report ? "今晚战报" : p == Phase.GameOver || p == Phase.Ended ? "王朝终局" : "结算";
        static string ResultName(ResultCode r) => r == ResultCode.PreparationFailed ? "准备失败，当前进度已保留；请检查 Console 后重试" : r == ResultCode.ConfirmationRequired ? "请确认结算后的待清空内容" : r == ResultCode.WrongPhase ? "当前阶段不能执行此操作" : r == ResultCode.InsufficientResources ? "资源不足" : r == ResultCode.InsufficientPopulation ? "空闲人口或工人不足" : r == ResultCode.NoCapacity ? "容量不足" : r == ResultCode.InvalidPlacement ? "占地、地形或通行条件不符" : r == ResultCode.MissingResearch ? "需要前置科技" : r == ResultCode.QuestOverflow ? "任务超出可承接数量" : "当前条件不满足（" + r + "）";
        readonly System.Collections.Generic.Dictionary<RectTransform, System.Collections.Generic.Stack<GameObject>> rowPools = new System.Collections.Generic.Dictionary<RectTransform, System.Collections.Generic.Stack<GameObject>>();
        bool reconcilingRows;
        readonly System.Collections.Generic.Dictionary<RectTransform,int> rowCursors = new System.Collections.Generic.Dictionary<RectTransform,int>();
        readonly System.Collections.Generic.Dictionary<RectTransform,System.Collections.Generic.List<GameObject>> stableRows = new System.Collections.Generic.Dictionary<RectTransform,System.Collections.Generic.List<GameObject>>();
        void Clear(RectTransform rows)
        {
            if (reconcilingRows)
            {
                if (!stableRows.TryGetValue(rows,out var list)) { list=new System.Collections.Generic.List<GameObject>(); stableRows.Add(rows,list); }
                list.Clear(); for(int i=0;i<rows.childCount;i++) if(rows.GetChild(i).gameObject!=RowTemplate)list.Add(rows.GetChild(i).gameObject);
                rowCursors[rows]=0; if(rowPools.TryGetValue(rows,out var oldPool))oldPool.Clear(); return;
            }
            if (!rowPools.TryGetValue(rows, out var pool)) rowPools.Add(rows, pool = new System.Collections.Generic.Stack<GameObject>());
            for (var i = rows.childCount - 1; i >= 0; i--) { var child = rows.GetChild(i).gameObject; if (child == RowTemplate || !child.activeSelf) continue; child.SetActive(false); pool.Push(child); }
        }
        GameObject Row(string label, Action action = null, bool right = false, RectTransform parent = null)
        {
            var rows = parent != null ? parent : right ? SecondaryRows : PrimaryRows;
            GameObject instance;
            if(reconcilingRows && rowCursors.TryGetValue(rows,out var cursor))
            { var list=stableRows[rows]; if(cursor<list.Count)instance=list[cursor];else {instance=Instantiate(RowTemplate,rows);list.Add(instance);}rowCursors[rows]=cursor+1; }
            else instance = rowPools.TryGetValue(rows, out var pool) && pool.Count > 0 ? pool.Pop() : Instantiate(RowTemplate, rows);
            var inventoryGrid = instance.transform.Find("InventoryGrid"); if (inventoryGrid != null) inventoryGrid.gameObject.SetActive(false);
            var quantityEditor = instance.transform.Find("InventoryQuantity"); if (quantityEditor != null) quantityEditor.gameObject.SetActive(false);
            var workforceEditor = instance.transform.Find("WorkforceScale"); if (workforceEditor != null) workforceEditor.gameObject.SetActive(false);
            var questEditor = instance.transform.Find("QuestQuantity"); if (questEditor != null) questEditor.gameObject.SetActive(false);
            var personPortrait=instance.transform.Find("Person portrait");if(personPortrait!=null){personPortrait.gameObject.SetActive(false);instance.GetComponentInChildren<Text>().margin=new Vector4(8,4,8,4);}
            var militaryEditor = instance.transform.Find("MilitaryName"); if (militaryEditor != null) militaryEditor.gameObject.SetActive(false);
            if(!reconcilingRows)instance.transform.SetAsLastSibling(); if(!instance.activeSelf)instance.SetActive(true);
            var textComponent = instance.GetComponentInChildren<Text>(); bool changed=textComponent.text!=label; if(changed)textComponent.text = label; textComponent.enableAutoSizing = false; textComponent.textWrappingMode = TMPro.TextWrappingModes.Normal;
            if(changed && EventSystem.current!=null && EventSystem.current.currentSelectedGameObject==instance)EventSystem.current.SetSelectedGameObject(null);
            var layout = instance.GetComponent<LayoutElement>(); if (layout == null) layout = instance.AddComponent<LayoutElement>();
            var preferred = textComponent.GetPreferredValues(label, Mathf.Max(200, rows.rect.width - 32), float.PositiveInfinity);
            layout.preferredHeight = Mathf.Max(38, preferred.y + 18); layout.minHeight = layout.preferredHeight;
            var icon = instance.transform.Find("Icon"); if (icon != null) icon.gameObject.SetActive(false);
            var button = instance.GetComponent<Button>(); button.onClick.RemoveAllListeners(); button.interactable = action != null;
            if (action != null) button.onClick.AddListener(() => action());
            return instance;
        }
        void FinishRows()
        {
            foreach(var pair in rowCursors) {var list=stableRows[pair.Key];for(int i=pair.Value;i<list.Count;i++)if(list[i].activeSelf)list[i].SetActive(false);}
            rowCursors.Clear();reconcilingRows=false;
        }
        void ForDefinitions(ContentKind kind, Action<int, ContentDefinition> action)
        {
            var blob = em.GetComponentData<ContentCatalog>(root).Value;
            for (var i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Kind == kind) action(i, blob.Value.Definitions[i]);
        }
        void Details() => RefreshBuildingDetails();
        void BuildMenu() => BuildCatalogRows();
        void Inventory()
        {
            InventoryRows();
        }
        void Military()
        {
            MilitaryRows();
        }
        void ConfirmAbandonQuest(Identity id)
        {
            var lines = new System.Collections.Generic.List<string> { "已提交物资不返还。惩罚只扣正常库存中现有的数量，不形成债务，也不扣待存放池。" };
            foreach (var cost in ProgressionOps.FailureCosts(em, root, id.Definition))
                lines.Add(Name(cost.Item) + "：最多扣除 " + cost.Amount + "，当前实际可扣 " + math.min(cost.Amount, InventoryOps.Count(em, root, cost.Item)));
            ShowBuildingConfirmation("确认放弃任务？" + id.Name, lines, () => Send(CommandKind.AbandonQuest, id.Id));
        }
        void Talents() => TalentRows();
        void Royals() => RoyalRows();
        void Policies() => PolicyRows();
        public static string Direction(int value) => IntelOps.Direction(value);
        void Report()
        {
            var phase = em.GetComponentData<Session>(root).Phase;
            if (phase != Phase.Day && phase != Phase.Report) { Row("本夜战报尚未结算；被盗明细只在结算时显示。"); return; }
            var rows = new System.Collections.Generic.List<BattleReportEntry>(); foreach (var entry in em.GetBuffer<BattleReportEntry>(root)) rows.Add(entry);
            foreach (var line in NightReportOps.Lines(em, root, rows)) Row(line);
            Row(em.GetComponentData<Session>(root).Phase == Phase.Day ? "收益已结算，溢出物资进入待存放池。" : "特殊掉落已全部记录；确认战报后先提交战损，再发放收益，放不下的进入待存放池。");
            if (em.GetComponentData<Session>(root).Phase == Phase.Report) Row("确认战报 · 下一回合", () => Send(CommandKind.Advance));
            if (em.GetComponentData<Session>(root).Phase == Phase.Day) ReportHistoryRows();
        }
        void GameOver()
        {
            if (em.GetComponentData<Session>(root).Phase == Phase.Ended)
            {
                Row("王朝已结束，记录已保留。存档已永久删除。");
                Row("返回主菜单", EcsSceneFlow.ReturnToMenu); return;
            }
            if (CourtOps.State(em, root).Extinction != 0) Row("王朝绝嗣：无在世直系后代，不能重试。确认结束后才删除存档。");
            else { Row("聚落核心失守"); Row("回到本回合白天", () => Send(CommandKind.RetryDay)); Row("重新开始本夜", () => Send(CommandKind.RetryDusk)); }
            Row("结束王朝（永久删除存档）", () => { Clear(PrimaryRows); Row("确认结束并永久删除，仅保留王朝记录", () => Send(CommandKind.EndDynasty)); Row("取消", () => OpenPanel("王朝终局")); nextRefresh = float.PositiveInfinity; });
        }
        void ConfirmNight()
        {
            if (!em.HasComponent<NightEntryReview>(root) || em.GetComponentData<NightEntryReview>(root).Token == 0)
            { Row("请点击下一阶段，重新检查入夜条件。"); return; }
            var token = em.GetComponentData<NightEntryReview>(root).Token;
            Row("按白天结算后的结果预览：以下待存放物资将清空，未驻扎士兵将解散。尚未扣款或推进回合。");
            foreach (var loss in em.GetBuffer<NightEntryLoss>(root))
                Row(loss.Soldier == 0 ? Name(loss.Item) + " × " + loss.Amount : loss.Name + " #" + loss.Soldier + " 将解散");
            Row("返回白天调整后请重新检查；原确认不会放弃后来新增的物资或士兵。");
            Row("返回整理库存", () => OpenPanel("库存")); Row("返回安排驻军", () => OpenPanel("驻军"));
            Row("重新检查", () => Send(CommandKind.Advance));
            Row("确认放弃以上物资与士兵并入夜", () => { Send(CommandKind.Advance, other: token, argument: 1); OpenPanel("建筑"); });
        }
        void LateUpdate()
        {
            RefreshInterfaceBarrier();
            if (root == Entity.Null || !em.Exists(root) || OverlayMesh == null || OverlayMaterial == null) return;
            var grid = em.GetComponentData<GridData>(root);
            void Draw(float3 position, Vector3 size, Color color)
            {
                if(InterfaceSettings.Current.HighContrast){color.a=Mathf.Max(.8f,color.a);size.x=Mathf.Max(.16f,size.x);size.z=Mathf.Max(.16f,size.z);}
                var properties = new MaterialPropertyBlock(); properties.SetColor("_BaseColor", color);
                Graphics.DrawMesh(OverlayMesh, Matrix4x4.TRS((Vector3)position + Vector3.up * .07f, Quaternion.identity, size), OverlayMaterial, 0, Camera, 0, properties, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            }
            var selectedEntity = Sim.Find(em, selected);
            if (!intel) DrawBuildingInteraction(Draw);
            if (!intel && em.GetComponentData<Session>(root).Phase == Phase.Night)
                foreach (var wave in em.GetBuffer<NightWave>(root)) if (wave.Warned != 0 && wave.Spawned == 0 && wave.Region >= 0 && wave.Region < em.GetBuffer<SpawnRegion>(root).Length)
                { var region = em.GetBuffer<SpawnRegion>(root)[wave.Region]; Draw(region.Center, new Vector3(region.Size.x, .1f, region.Size.z), new Color(1, .65f, .1f, .4f)); }
            if (!intel && selectedEntity != Entity.Null && em.HasComponent<Building>(selectedEntity)) { var b = em.GetComponentData<Building>(selectedEntity); Draw(Sim.Position(em, selectedEntity), new Vector3(b.Size.x * grid.CellSize, .06f, b.Size.y * grid.CellSize), new Color(.2f, .7f, 1, .4f)); }
            using (var projectiles = Sim.Entities<Projectile>(em)) foreach (var e in projectiles)
            {
                var p = em.GetComponentData<Projectile>(e); if (p.Mode != ProjectileMode.Ground) continue;
                float radius = math.max(.2f, p.Radius); var center = p.Landing; center.y = GridOps.Position(grid, GridOps.Cell(grid, center), new int2(1)).y + .08f;
                for (int i = 0; i < 24; i++) { float angle = i * math.PI / 12; Draw(center + new float3(math.cos(angle), 0, math.sin(angle)) * radius, new Vector3(.15f, .06f, .15f), new Color(1, .2f, .05f, .8f)); }
            }
            DrawIntelligence(Draw);
            DrawNightResults(Draw);

        }
    }
}
