using Moyo.Unity;
using System.Threading.Tasks;
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
    public sealed class UI_GamePanel : UIPanelBase, IGameUiNavigation
    {
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("建筑控制器")]
        internal UI_GamePanel_Building buildingController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("人才控制器")]
        internal UI_GamePanel_Talent talentController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("政策控制器")]
        internal UI_GamePanel_Policy policyController;
        internal readonly GameUiCommandWriter commandsController = new GameUiCommandWriter();
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("王室控制器")]
        internal UI_GamePanel_Court courtController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("远征控制器")]
        internal UI_GamePanel_Expedition expeditionController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("历史控制器")]
        internal UI_GamePanel_History historyController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("信息栏控制器")]
        internal UI_GamePanel_Hud hudController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("婚姻控制器")]
        internal UI_GamePanel_Marriage marriageController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("阶段控制器")]
        internal UI_GamePanel_Phase phaseController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("肖像控制器")]
        internal UI_GamePanel_Portrait portraitController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("任务控制器")]
        internal UI_GamePanel_Quest questController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("请求控制器")]
        internal UI_GamePanel_PersonRequests requestsController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("条目容器控制器")]
        internal UI_GamePanel_RowRenderer rowsController;
        internal readonly GameUiSession sessionController = new GameUiSession();
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("士兵控制器")]
        internal UI_GamePanel_Soldier soldierController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("科技控制器")]
        internal UI_GamePanel_Technology technologyController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("世界控制器")]
        internal UI_GamePanel_WorldInteraction worldController;
        UI_GamePanel_List IGameUiNavigation.ActiveListPanel { get => ActiveListPanel; }

        void IGameUiNavigation.BackPanel() => BackPanel();
        void IGameUiNavigation.ClosePanel() => ClosePanel();
        void IGameUiNavigation.EndInventoryDrag() => EndInventoryDrag();
        UI_GamePanel_List IGameUiNavigation.GetListPanel(GamePanelId id) => GetListPanel(id);
        UI_GamePanel_Intelligence IGameUiNavigation.IntelligenceWindow { get => IntelligenceWindow; }

        CanvasGroup IGameUiNavigation.InterfaceGroup { get => InterfaceGroup; set => InterfaceGroup = value; }

        CanvasScaler IGameUiNavigation.InterfaceScaler { get => InterfaceScaler; set => InterfaceScaler = value; }

        bool IGameUiNavigation.IsPanelOpen { get => IsPanelOpen; set => IsPanelOpen = value; }

        void IGameUiNavigation.LocateGarrison(ulong id) => LocateGarrison(id);
        void IGameUiNavigation.OpenEconomy(ulong source) => OpenEconomy(source);
        void IGameUiNavigation.OpenPanel(GamePanelId panel) => OpenPanel(panel);
        GamePanelId IGameUiNavigation.Panel { get => Panel; set => Panel = value; }

        UI_GamePanel_PausePop IGameUiNavigation.PauseMenu { get => PauseMenu; set => PauseMenu = value; }

        RectTransform IGameUiNavigation.PrimaryRows { get => PrimaryRows; }


        RectTransform IGameUiNavigation.SecondaryRows { get => SecondaryRows; }

        bool IGameUiNavigation.TextFocused { get => TextFocused; }
        public EventSystem InputEvents { get; private set; }
        public GameUiInputPolicy InputPolicy { get; private set; }

        internal void BindControllers()
        {
            if (worldController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：UI_GamePanel_WorldInteraction");
            if (buildingController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameBuildingController");
            if (technologyController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameTechnologyController");
            if (questController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameQuestController");
            if (courtController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameCourtController");
            if (marriageController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameMarriageController");
            if (portraitController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GamePortraitController");
            if (requestsController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GamePersonRequestsController");
            if (soldierController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameSoldierController");
            if (hudController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameHudController");
            if (historyController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameHistoryController");
            if (expeditionController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameExpeditionController");
            if (phaseController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GamePhaseController");
            if (rowsController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameRowRenderer");
            if (talentController == null || policyController == null)
                throw new InvalidOperationException("未配置人才或政策控制器。");
            talentController.sessionController = sessionController;
            talentController.commandsController = commandsController;
            talentController.rowsController = rowsController;
            talentController.navigation = this;
            talentController.buildingController = buildingController;
            policyController.sessionController = sessionController;
            policyController.commandsController = commandsController;
            policyController.rowsController = rowsController;
            policyController.navigation = this;
            policyController.buildingController = buildingController;
            commandsController.sessionController = sessionController;
            commandsController.navigation = this;
            InputPolicy = new GameUiInputPolicy(sessionController, this, buildingController, soldierController, portraitController, requestsController, marriageController);
            worldController.sessionController = sessionController;
            worldController.buildingController = buildingController;
            worldController.hudController = hudController;
            worldController.navigation = this;
            worldController.commandsController = commandsController;
            buildingController.navigation = this;
            buildingController.sessionController = sessionController;
            buildingController.worldController = worldController;
            buildingController.commandsController = commandsController;
            buildingController.soldierController = soldierController;
            buildingController.marriageController = marriageController;
            buildingController.requestsController = requestsController;
            buildingController.hudController = hudController;
            buildingController.rowsController = rowsController;
            buildingController.courtController = courtController;
            technologyController.navigation = this;
            technologyController.buildingController = buildingController;
            technologyController.sessionController = sessionController;
            technologyController.rowsController = rowsController;
            technologyController.commandsController = commandsController;
            questController.sessionController = sessionController;
            questController.buildingController = buildingController;
            questController.commandsController = commandsController;
            questController.navigation = this;
            questController.rowsController = rowsController;
            questController.hudController = hudController;
            courtController.sessionController = sessionController;
            courtController.rowsController = rowsController;
            courtController.commandsController = commandsController;
            courtController.buildingController = buildingController;
            courtController.marriageController = marriageController;
            courtController.navigation = this;
            courtController.requestsController = requestsController;
            marriageController.sessionController = sessionController;
            marriageController.courtController = courtController;
            marriageController.navigation = this;
            marriageController.soldierController = soldierController;
            marriageController.requestsController = requestsController;
            marriageController.portraitController = portraitController;
            marriageController.buildingController = buildingController;
            marriageController.worldController = worldController;
            marriageController.commandsController = commandsController;
            portraitController.sessionController = sessionController;
            portraitController.navigation = this;
            portraitController.marriageController = marriageController;
            portraitController.requestsController = requestsController;
            portraitController.courtController = courtController;
            portraitController.soldierController = soldierController;
            portraitController.buildingController = buildingController;
            portraitController.worldController = worldController;
            portraitController.commandsController = commandsController;
            portraitController.rowsController = rowsController;
            requestsController.soldierController = soldierController;
            requestsController.marriageController = marriageController;
            requestsController.portraitController = portraitController;
            requestsController.sessionController = sessionController;
            requestsController.navigation = this;
            requestsController.buildingController = buildingController;
            requestsController.worldController = worldController;
            requestsController.courtController = courtController;
            requestsController.expeditionController = expeditionController;
            requestsController.commandsController = commandsController;
            soldierController.buildingController = buildingController;
            soldierController.sessionController = sessionController;
            soldierController.navigation = this;
            soldierController.marriageController = marriageController;
            soldierController.requestsController = requestsController;
            soldierController.portraitController = portraitController;
            soldierController.commandsController = commandsController;
            hudController.sessionController = sessionController;
            hudController.worldController = worldController;
            hudController.navigation = this;
            hudController.commandsController = commandsController;
            hudController.questController = questController;
            hudController.buildingController = buildingController;
            hudController.historyController = historyController;
            historyController.worldController = worldController;
            historyController.navigation = this;
            historyController.sessionController = sessionController;
            historyController.rowsController = rowsController;
            expeditionController.sessionController = sessionController;
            expeditionController.rowsController = rowsController;
            expeditionController.buildingController = buildingController;
            expeditionController.courtController = courtController;
            expeditionController.commandsController = commandsController;
            phaseController.sessionController = sessionController;
            phaseController.rowsController = rowsController;
            phaseController.commandsController = commandsController;
            phaseController.navigation = this;
            rowsController.navigation = this;
        }

        internal void ValidateInspectorConfiguration()
        {
            static void Need(UnityEngine.Object value, string name)
            {
                if (value == null)
                    throw new InvalidOperationException("ECS 游戏 UI 检查器引用缺失：" + name);
            }

            Need(HudRoot, nameof(HudRoot));
            Need(BuildingRoot, nameof(BuildingRoot));
            Need(FeatureRoot, nameof(FeatureRoot));
            Need(ModalRoot, nameof(ModalRoot));
            Need(rowsController.RowTemplate, nameof(rowsController.RowTemplate));
            Need(PauseMenu, nameof(PauseMenu));
            Need(InterfaceGroup, nameof(InterfaceGroup));
            Need(InterfaceScaler, nameof(InterfaceScaler));
            Need(worldController.Camera, nameof(worldController.Camera));
            Need(buildingController.BuildingBar, nameof(buildingController.BuildingBar));
            Need(buildingController.BuildingCard, nameof(buildingController.BuildingCard));
            Need(technologyController.TechnologyTree, nameof(technologyController.TechnologyTree));
            Need(courtController.CourtGraph, nameof(courtController.CourtGraph));
            Need(courtController.RoyalDetails, nameof(courtController.RoyalDetails));
            Need(questController.QuestPanel, nameof(questController.QuestPanel));
            Need(questController.QuestTracking, nameof(questController.QuestTracking));
            Need(soldierController.SoldierDetailsPanel, nameof(soldierController.SoldierDetailsPanel));
            Need(marriageController.MarriagePanel, nameof(marriageController.MarriagePanel));
            Need(requestsController.PersonRequestsPanel, nameof(requestsController.PersonRequestsPanel));
            Need(portraitController.PortraitPanel, nameof(portraitController.PortraitPanel));
            Need(technologyController.ResearchHud, nameof(technologyController.ResearchHud));
            Need(hudController.BattleHud, nameof(hudController.BattleHud));
            Need(hudController.NightHud, nameof(hudController.NightHud));
            Need(historyController.NavigationPanel, nameof(historyController.NavigationPanel));
            Need(worldController.WorldPresentation, nameof(worldController.WorldPresentation));
            Need(historyController.HistoryTools, nameof(historyController.HistoryTools));
            Need(historyController.HistoryFilter, nameof(historyController.HistoryFilter));
            Need(hudController.AdvanceLabel, nameof(hudController.AdvanceLabel));
            Need(hudController.MessageButton, nameof(hudController.MessageButton));
            Need(BuildingFeatureButton, nameof(BuildingFeatureButton));
            Need(InventoryFeatureButton, nameof(InventoryFeatureButton));
            Need(ExpeditionFeatureButton, nameof(ExpeditionFeatureButton));
            Need(hudController.IntelligenceButtonLabel, nameof(hudController.IntelligenceButtonLabel));
            Need(portraitController.BeautyEventButton, nameof(portraitController.BeautyEventButton));
            Need(portraitController.BeautyEventLabel, nameof(portraitController.BeautyEventLabel));
            Need(marriageController.MarriageEventButton, nameof(marriageController.MarriageEventButton));
            Need(marriageController.MarriageEventLabel, nameof(marriageController.MarriageEventLabel));
            Need(buildingController.BuildingConfirmTitle, nameof(buildingController.BuildingConfirmTitle));
            Need(buildingController.BuildingConfirmGroup, nameof(buildingController.BuildingConfirmGroup));
            buildingController.BuildingCard.ValidateConfiguration();
            buildingController.BuildingBar.ValidateConfiguration();
            technologyController.TechnologyTree.ValidateConfiguration();
            courtController.CourtGraph.ValidateConfiguration();
            talentController.CourtGraph.ValidateConfiguration();
            policyController.CourtGraph.ValidateConfiguration();
            courtController.RoyalDetails.ValidateConfiguration();
            questController.QuestPanel.ValidateConfiguration();
            soldierController.SoldierDetailsPanel.ValidateConfiguration();
            marriageController.MarriagePanel.ValidateConfiguration();
            requestsController.PersonRequestsPanel.ValidateConfiguration();
            portraitController.PortraitPanel.ValidateConfiguration();
            technologyController.ResearchHud.ValidateConfiguration();
            hudController.BattleHud.ValidateConfiguration();
            hudController.NightHud.ValidateConfiguration();
            historyController.NavigationPanel.ValidateConfiguration();
            rowsController.RowTemplate.ValidateConfiguration();
            buildingController.WorkerInfoTemplate.ValidateConfiguration();
            buildingController.WorkforceTemplate.ValidateConfiguration();
            questController.QuantityTemplate.ValidateConfiguration();
            portraitController.PortraitTemplate.ValidateConfiguration();
            if (FeaturePanels == null || FeaturePanels.Length == 0 || FeaturePanels.Any(p => p == null) || FeaturePanels.GroupBy(p => p.PanelId).Any(g => g.Key == GamePanelId.None || g.Count() != 1))
                throw new InvalidOperationException("FeaturePanels 必须检查器绑定且 PanelId 唯一。");
            foreach (var panel in FeaturePanels)
                if (panel.transform.parent != FeatureRoot)
                    throw new InvalidOperationException(panel.name + " 必须是功能面板根对象的直接子对象。");
            if (buildingController.BuildingCard.transform.parent != FeatureRoot)
                throw new InvalidOperationException("建筑详情必须是功能面板根对象的直接子对象。");
            foreach (var modal in new Component[]
            {
                PauseMenu,
                soldierController.SoldierDetailsPanel,
                marriageController.MarriagePanel,
                requestsController.PersonRequestsPanel,
                portraitController.PortraitPanel
            }

            )
                if (!modal.transform.IsChildOf(ModalRoot))
                    throw new InvalidOperationException(modal.name + " 必须位于模态面板根对象下。");
        }

        [Sirenix.OdinInspector.LabelText("功能面板集合")]
        public UI_GamePanel_List[] FeaturePanels;
        public RectTransform PrimaryRows => ActiveListPanel.PrimaryRows;
        public RectTransform SecondaryRows => GarrisonWindow.SecondaryRows;

        [Sirenix.OdinInspector.LabelText("暂停菜单")]
        public UI_GamePanel_PausePop PauseMenu;
        [Sirenix.OdinInspector.LabelText("界面组")]
        public CanvasGroup InterfaceGroup;
        [NonSerialized]
        public CanvasScaler InterfaceScaler;
        [Sirenix.OdinInspector.LabelText("面板")]
        public GamePanelId Panel = GamePanelId.Building;
        internal Phase observedPhase;
        public void InitializeView()
        {
            if (initialized)
                return;
            BindControllers();
            ValidateInspectorConfiguration();
            hudController.Advance.onClick.AddListener(hudController.RequestAdvance);
            buildingController.InitializeBuildings();
            InitializeFeatureButtons();
            hudController.InitializeIntelligence();
            historyController.InitializeInterface();
            InitializePanelWindows();
            technologyController.InitializeResearchHud();
            initialized = true;
        }

        internal void Update()
        {
            if (!sessionController.IsBound || !EcsSceneFlow.GameReady)
                return;
            var state = sessionController.em.GetComponentData<Session>(sessionController.root);
            var refresh = sessionController.Refresh;
            refresh.Observe(state, InterfaceSettings.Revision,
                sessionController.em.GetBuffer<GameEvent>(sessionController.root, true).Length > 0,
                UI_GamePanel_InteractionLock.Revision);
            hudController.ConsumeInterfaceEvents(state);
            technologyController.RefreshResearchHud();
            marriageController.RefreshMarriageEvents();
            requestsController.RefreshPersonRequests();
            portraitController.RefreshPortraitCustomization();
            worldController.Input();
            if (refresh.TakeHudRefresh(Time.unscaledTime)) RefreshHud(state);
            bool editing = TextFocused || InventoryWindow.IsDragging || InventoryWindow.IsEditing ||
                buildingController.workforceScale != null && buildingController.workforceScale.Interacting;
            // Pointer ownership belongs to each configured row; holding the mouse never freezes the HUD.
            if (refresh.NeedsPanelRefresh(Time.unscaledTime, true, editing))
            {
                RefreshVisibleContent(state);
                refresh.MarkRendered(Time.unscaledTime);
            }
        }

        internal void Refresh()
        {
            if (!sessionController.IsBound) return;
            var s = sessionController.em.GetComponentData<Session>(sessionController.root);
            RefreshHud(s);
            RefreshVisibleContent(s);
            sessionController.Refresh.MarkRendered(Time.unscaledTime);
        }

        void RefreshHud(Session s)
        {
            if (sessionController.intel && s.IntelligenceMode == 0)
            {
                bool queued = false;
                foreach (var command in sessionController.em.GetBuffer<Command>(sessionController.root))
                    if (command.Kind == CommandKind.IntelligenceMode && command.Argument == 1)
                        queued = true;
                if (!queued)
                {
                    sessionController.intel = false;
                    ClosePanel();
                    IntelligenceWindow.ResetView();
                }
            }

            hudController.RefreshIntelligenceBadge();
            if (observedPhase == Phase.GameOver && s.Phase != Phase.GameOver && s.Phase != Phase.Ended)
            {
                Panel = GamePanelId.Building;
                ClosePanel();
                hudController.Message.text = "";
                GarrisonWindow.SelectedSoldier = 0;
                worldController.EndBuildingPlacement();
                if (buildingController.BuildingConfirmPanel != null)
                    buildingController.BuildingConfirmPanel.SetActive(false);
                worldController.rangeRevision = -1;
            }

            observedPhase = s.Phase;
            hudController.RefreshHeader(s);
            hudController.RefreshHeroHud();
            if (s.Phase == Phase.GameOver || s.Phase == Phase.Ended)
            {
                Panel = GamePanelId.DynastyEnd;
                IsPanelOpen = true;
                sessionController.intel = false;
            }

            technologyController.RefreshTechnologyAccess();
            RefreshFeatureAccess();
        }

        void RefreshVisibleContent(Session s)
        {
            soldierController.RefreshSoldierDetails();
            rowsController.reconcilingRows = true;
            RefreshPanelVisibility();
            var listPanel = IsPanelOpen ? FindListPanel(Panel) : null;
            if (listPanel != null)
                rowsController.Clear(listPanel.PrimaryRows);
            if (IsPanelOpen && Panel == GamePanelId.Garrison)
                rowsController.Clear(SecondaryRows);
            buildingController.RefreshBuildingCatalog();
            RefreshPanelVisibility();
            buildingController.Details();
            buildingController.NameInput.gameObject.SetActive(buildingController.showBuildingDetails && !sessionController.intel);
            if (sessionController.intel)
                hudController.Selection.text = "情报模式：WASD / 滚轮调整镜头；退出后恢复操作。";
            questController.RefreshQuestTracking();
            if (IsPanelOpen && !sessionController.intel && s.Phase == Phase.Night && s.NightKind == NightKind.Peaceful)
                rowsController.Row(s.NightSpeed == 2 ? "平安夜速度 2×（切回 1×）" : "平安夜速度 1×（切换 2×）", s.Paused == 0 ? () => commandsController.TryQueue(CommandRequests.SetNightSpeed(s.NightSpeed == 2 ? 1 : 2)) : null);
            if (IsPanelOpen && s.Phase == Phase.Day && sessionController.em.GetBuffer<BattleReportEntry>(sessionController.root).Length > 0 && Panel != GamePanelId.BattleReport)
                rowsController.Row("查看上一晚结算（含被盗物资）", () => OpenPanel(GamePanelId.BattleReport));
            if (IsPanelOpen)
                ActiveListPanel.Render();
            rowsController.FinishRows();
            courtController.RefreshCourtPresentation();
            talentController.RefreshPresentation();
            policyController.RefreshPresentation();
        }

        [Serializable]
        public sealed class NavigationButtonBinding
        {
            [LabelText("目标面板")]
            public GamePanelId Target;
            [LabelText("导航按钮")]
            public Button Button;
        }

        [LabelText("功能导航按钮")]
        public NavigationButtonBinding[] NavigationButtons = Array.Empty<NavigationButtonBinding>();
        public Button BuildingFeatureButton => NavigationButton(GamePanelId.Building);
        public Button InventoryFeatureButton => NavigationButton(GamePanelId.Inventory);
        public Button ExpeditionFeatureButton => NavigationButton(GamePanelId.Expedition);
        Button NavigationButton(GamePanelId target) => NavigationButtons.FirstOrDefault(binding => binding.Target == target)?.Button;
        internal void InitializeFeatureButtons()
        {
            if (NavigationButtons == null || NavigationButtons.Length == 0 || NavigationButtons.Any(binding => binding == null || binding.Button == null || FindListPanel(binding.Target) == null)
                || NavigationButtons.Select(binding => binding.Button).Distinct().Count() != NavigationButtons.Length)
                throw new InvalidOperationException("功能导航必须显式配置有效面板与唯一按钮。");
        }

        internal void RefreshFeatureAccess()
        {
            foreach (var binding in NavigationButtons)
                binding.Button.interactable = GetListPanel(binding.Target).IsFeatureUnlocked(sessionController.em, sessionController.root);
            var active = FindListPanel(Panel);
            if (IsPanelOpen && active != null && !active.CanOpen(sessionController.em, sessionController.root))
                ClosePanel();
        }

        internal readonly Stack<GamePanelId> panelHistory = new Stack<GamePanelId>();
        public bool TextFocused => UiInputState.TextFocused;

        bool CloseBackOwner(GameUiInputOwner owner)
        {
            switch (owner)
            {
                case GameUiInputOwner.SoldierDetails: soldierController.CloseSoldierDetails(); return true;
                case GameUiInputOwner.Portrait: portraitController.ClosePortrait(); return true;
                case GameUiInputOwner.PersonRequests: requestsController.ClosePersonRequests(); return true;
                case GameUiInputOwner.Marriage: marriageController.CloseMarriage(); return true;
                case GameUiInputOwner.Pause: PauseMenu.Escape(); return true;
                default: return false;
            }
        }

        public bool HandleBackInput()
        {
            if (!sessionController.IsBound) return false;
            var input = InputPolicy.Capture();
            if (!input.CanHandleBack) return false;
            if (!CloseBackOwner(input.BackOwner(includeClosedPause: true)) && !buildingController.CancelBuildingInteraction())
                BackPanel();
            worldController.cameraDragging = false;
            worldController.touchBlocked = true;
            UiInputState.BackHandledFrame = Time.frameCount;
            return true;
        }

        public void BackPanel()
        {
            if (!sessionController.IsBound) return;
            var input = InputPolicy.Capture();
            if (!input.CanHandleBack || CloseBackOwner(input.BackOwner())) return;
            if (buildingController.CancelBuildingInteraction()) return;
            if (InventoryWindow.IsDragging) { EndInventoryDrag(); return; }
            if (panelHistory.Count > 0) OpenPanelCore(panelHistory.Pop(), false);
            else ClosePanel();
        }

        [Header("游戏界面归属")]
        [Sirenix.OdinInspector.LabelText("信息栏根对象")]
        public RectTransform HudRoot;
        [Sirenix.OdinInspector.LabelText("建筑根对象")]
        public RectTransform BuildingRoot;
        [Sirenix.OdinInspector.LabelText("功能根对象")]
        public RectTransform FeatureRoot;
        [Sirenix.OdinInspector.LabelText("弹窗根对象")]
        public RectTransform ModalRoot;
        // Unity's persistent Button event serializes enum values through an integer argument.
        // Invalid values are configuration errors; no text-to-panel compatibility path exists.
        public void OpenPanelFromEvent(int panelValue)
        {
            if (!Enum.IsDefined(typeof(GamePanelId), panelValue) || panelValue == (int)GamePanelId.None)
                throw new InvalidOperationException("导航事件包含无效面板标识：" + panelValue);
            OpenPanel((GamePanelId)panelValue);
        }
        public void OpenPanel(GamePanelId panel) => OpenPanelCore(panel, true);
        internal void OpenPanelCore(GamePanelId panel, bool remember)
        {
            if (!sessionController.IsBound || !InputPolicy.Capture().CanNavigate) return;
            if (panel == GamePanelId.Pause) { PauseMenu.Open(); return; }
            var destination = GetListPanel(panel);
            if (!destination.CanOpen(sessionController.em, sessionController.root)) return;
            bool enteringIntel = panel == GamePanelId.Intelligence;
            if (sessionController.intel != enteringIntel)
                commandsController.TryQueue(CommandRequests.SetIntelligenceMode(enteringIntel));
            if (enteringIntel)
            {
                worldController.EndBuildingPlacement();
                EndInventoryDrag();
                IntelligenceWindow.ResetView();
                if (buildingController.BuildingConfirmPanel != null)
                    buildingController.BuildingConfirmPanel.SetActive(false);
            }

            if (remember && IsPanelOpen && Panel != panel)
            {
                if (panelHistory.Count >= 16)
                    panelHistory.Clear();
                panelHistory.Push(Panel);
            }

            if (Panel != panel)
            {
                EndInventoryDrag();
                if (panel != GamePanelId.Building)
                    worldController.EndBuildingPlacement();
                if (InputEvents != null)
                    InputEvents.SetSelectedGameObject(null);
            }

            Panel = panel;
            IsPanelOpen = true;
            sessionController.intel = enteringIntel;
            sessionController.nextRefresh = 0;
            if (panel != GamePanelId.Building)
            {
                buildingController.showBuildingDetails = false;
                if (buildingController.BuildingDetailsPanel != null)
                    buildingController.BuildingDetailsPanel.SetActive(false);
            }

            buildingController.buildingBarOpen = panel == GamePanelId.Building;
            if (buildingController.BuildingBar != null && panel != GamePanelId.Building)
                buildingController.BuildingBar.gameObject.SetActive(false);
            if (technologyController.TechnologyTree != null && panel != GamePanelId.Technology)
                technologyController.TechnologyTree.gameObject.SetActive(false);
            if (questController.QuestWindow != null && panel != GamePanelId.Quest)
                questController.QuestWindow.SetActive(false);
            talentController.CourtGraph.gameObject.SetActive(panel == GamePanelId.Talent);
            policyController.CourtGraph.gameObject.SetActive(panel == GamePanelId.Policy);
            if (courtController.courtGraph != null && panel != GamePanelId.Royal)
                courtController.courtGraph.gameObject.SetActive(false);
            RefreshPanelVisibility();
        }

        public bool IsPanelOpen { get; private set; }
        public Button PanelCloseButton => ActiveListPanel.CloseButton;

        internal void InitializePanelWindows()
        {
            foreach (var panel in FeaturePanels)
            {
                panel.Bind(sessionController, commandsController, this, rowsController);
                if (panel is UI_GamePanel_Economy economy)
                    economy.Buildings = buildingController;
                if (panel is UI_GamePanel_Inventory inventory)
                {
                    inventory.Buildings = buildingController;
                    inventory.Feedback = hudController;
                }

                if (panel is UI_GamePanel_Garrison garrison)
                {
                    garrison.Buildings = buildingController;
                    garrison.Soldiers = soldierController;
                    garrison.World = worldController;
                }

                if (panel is UI_GamePanel_Intelligence intelligence)
                {
                    intelligence.Court = courtController;
                    intelligence.World = worldController;
                }
            }

            RefreshPanelVisibility();
        }

        public void ClosePanel()
        {
            if (!sessionController.IsBound || !InputPolicy.Capture().CanNavigate || Panel == GamePanelId.DynastyEnd) return;
            if (sessionController.intel)
                commandsController.TryQueue(CommandRequests.SetIntelligenceMode(false));
            sessionController.intel = false;
            IntelligenceWindow.ResetView();
            EndInventoryDrag();
            worldController.EndBuildingPlacement();
            IsPanelOpen = false;
            Panel = GamePanelId.Building;
            panelHistory.Clear();
            buildingController.buildingBarOpen = false;
            if (buildingController.BuildingBar != null)
                buildingController.BuildingBar.gameObject.SetActive(false);
            if (technologyController.TechnologyTree != null)
                technologyController.TechnologyTree.gameObject.SetActive(false);
            if (questController.QuestWindow != null)
                questController.QuestWindow.SetActive(false);
            if (courtController.courtGraph != null)
                courtController.courtGraph.gameObject.SetActive(false);
            talentController.CourtGraph.gameObject.SetActive(false);
            policyController.CourtGraph.gameObject.SetActive(false);
            if (historyController.historyTools != null)
                historyController.historyTools.gameObject.SetActive(false);
            if (InputEvents != null)
                InputEvents.SetSelectedGameObject(null);
            sessionController.nextRefresh = 0;
            RefreshPanelVisibility();
        }

        internal void RefreshPanelVisibility()
        {
            if (IsPanelOpen && portraitController.BeautyEventButton != null)
                portraitController.BeautyEventButton.gameObject.SetActive(false);
            bool buildingUnlocked = GetListPanel(GamePanelId.Building).IsFeatureUnlocked(sessionController.em, sessionController.root);
            bool primary = IsPanelOpen && Panel != GamePanelId.Technology && Panel != GamePanelId.Quest && (Panel != GamePanelId.Royal || courtController.royalOverview) && (Panel != GamePanelId.Building || !buildingUnlocked);
            foreach (var window in FeaturePanels)
                window.gameObject.SetActive(IsPanelOpen && window.PanelId == Panel);
            var active = FindListPanel(Panel);
            if (active != null)
                active.SetContentVisible(primary);
        }

        public UI_GamePanel_List GetListPanel(GamePanelId id) => FeaturePanels.First(p => p.PanelId == id);
        internal UI_GamePanel_List FindListPanel(GamePanelId id) => FeaturePanels.FirstOrDefault(p => p.PanelId == id);
        internal UI_GamePanel_List ActiveListPanel => FeaturePanels.FirstOrDefault(p => p.PanelId == Panel) ?? throw new InvalidOperationException("未配置功能面板：" + Panel);
        public UI_GamePanel_Economy EconomyWindow => (UI_GamePanel_Economy)GetListPanel(GamePanelId.Economy);
        public UI_GamePanel_Inventory InventoryWindow => (UI_GamePanel_Inventory)GetListPanel(GamePanelId.Inventory);
        public UI_GamePanel_Garrison GarrisonWindow => (UI_GamePanel_Garrison)GetListPanel(GamePanelId.Garrison);
        public UI_GamePanel_Intelligence IntelligenceWindow => (UI_GamePanel_Intelligence)GetListPanel(GamePanelId.Intelligence);

        internal void LocateGarrison(ulong id) => GarrisonWindow.LocateGarrison(id);
        internal void OpenEconomy(ulong source = 0) => EconomyWindow.OpenEconomy(source);
        internal void EndInventoryDrag()
        {
            if (FeaturePanels != null && FeaturePanels.Length > 0)
                InventoryWindow.EndInventoryDrag();
        }

        bool initialized;
        bool sessionBound;
        public GameUiSession Session => sessionController;
        public GameUiCommandWriter Commands => commandsController;
        public UI_GamePanel_Building Buildings => buildingController;
        public UI_GamePanel_Technology Technology => technologyController;
        public UI_GamePanel_Quest Quests => questController;
        public UI_GamePanel_Court Court => courtController;
        public UI_GamePanel_Talent Talents => talentController;
        public UI_GamePanel_Policy Policies => policyController;
        public UI_GamePanel_WorldInteraction WorldInteraction => worldController;
        public UI_GamePanel_Hud Hud => hudController;

        public void BindSession(EntityManager manager, Entity sessionRoot, Camera camera, CanvasScaler scaler, EventSystem events)
        {
            if (camera == null || scaler == null || events == null)
                throw new InvalidOperationException("游戏会话缺少相机、画布缩放器或输入系统。");
            if (sessionBound)
                UnbindSession();
            BindControllers();
            sessionController.Bind(manager, sessionRoot);
            sessionBound = true;
            worldController.Camera = camera;
            InterfaceScaler = scaler;
            InputEvents = events;
            try
            {
                InitializeView();
                worldController.BindSession();
                sessionController.nextRefresh = 0;
            }
            catch (Exception error)
            {
                try
                {
                    UnbindSession();
                }
                catch (Exception cleanupError)
                {
                    throw new AggregateException("游戏会话绑定及清理失败。", error, cleanupError);
                }

                throw;
            }
        }

        public void UnbindSession()
        {
            if (!sessionBound)
                return;
            sessionBound = false;
            var errors = new List<Exception>();
            void Clear(Action action)
            {
                try
                {
                    action();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            }

            try
            {
                if (worldController != null)
                {
                    Clear(worldController.LifecycleOnDestroy);
                    Clear(worldController.EndBuildingPlacement);
                    if (worldController.WorldPresentation != null)
                        Clear(worldController.WorldPresentation.ClearViews);
                }

                Clear(EndInventoryDrag);
                Clear(() => InventoryWindow.ResetSession());
                Clear(() => EconomyWindow.ResetSession());
                Clear(() => GarrisonWindow.ResetSession());
                if (hudController != null)
                    Clear(hudController.ResetSession);
                if (technologyController != null)
                    Clear(technologyController.ResetSession);
                if (buildingController != null)
                    Clear(buildingController.ResetSession);
                if (historyController != null)
                {
                    historyController.historyCategory = -1;
                    historyController.historyPage = historyController.historyFilterTurn = 0;
                    historyController.historySearch = "";
                    if (historyController.HistoryFilter != null)
                        Clear(() => historyController.HistoryFilter.SetTextWithoutNotify(""));
                }

                if (rowsController != null)
                    Clear(rowsController.ClearAll);
                if (talentController != null)
                    Clear(talentController.ResetSession);
                if (policyController != null)
                    Clear(policyController.ResetSession);
                if (courtController != null)
                {
                    courtController.courtPerson = 0;
                    courtController.royalOverview = false;
                    if (courtController.CourtGraph != null)
                        Clear(courtController.CourtGraph.ClearSession);
                }

                if (questController != null)
                    Clear(questController.ClearSessionViews);
                if (soldierController != null)
                    Clear(soldierController.CloseSoldierDetails);
                if (portraitController != null)
                    Clear(portraitController.ClosePortrait);
                if (marriageController != null)
                    Clear(marriageController.CloseMarriage);
                if (requestsController != null)
                    Clear(requestsController.ClosePersonRequests);
            }
            finally
            {
                IsPanelOpen = false;
                Panel = GamePanelId.Building;
                panelHistory.Clear();
                sessionController.Unbind();
                if (worldController != null)
                    worldController.Camera = null;
                InterfaceScaler = null;
                InputEvents = null;
            }

            if (errors.Count > 0)
                throw new AggregateException("游戏会话清理失败。", errors);
        }

        void LateUpdate()
        {
            if (sessionController.IsBound)
                worldController.LifecycleLateUpdate();
        }

        void OnDisable()
        {
            if (sessionBound)
            {
                worldController.EndBuildingPlacement();
                EndInventoryDrag();
            }
        }

        public override Task<bool> TryHandleBackAsync() => Task.FromResult(HandleBackInput());
        public override Task OnCloseAsync()
        {
            PauseMenu?.Unbind();
            UnbindSession();
            return base.OnCloseAsync();
        }

        public override Task OnReleaseAsync()
        {
            PauseMenu?.Unbind();
            UnbindSession();
            return base.OnReleaseAsync();
        }

        void OnDestroy()
        {
            UnbindSession();
        }
    }
}
