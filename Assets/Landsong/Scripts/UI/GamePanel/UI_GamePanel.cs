using Moyo.Unity;
using System.Threading.Tasks;
using System.Collections.Generic;
using Unity.Entities;
using System;
using System.Linq;
using Landsong.Content;
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
    public sealed class UI_GamePanel : UIPanelBase
    {
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("建筑控制器")]
        internal UI_GamePanel_BuildingActionBar buildingController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("建筑详情控制器")]
        internal UI_GamePanel_BuildingDetails buildingDetailsController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("人才控制器")]
        internal UI_GamePanel_Talent talentController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("政策控制器")]
        internal UI_GamePanel_Policy policyController;
        internal readonly GameUiCommandWriter commandsController = new GameUiCommandWriter();
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("王室控制器")]
        internal UI_GamePanel_Royal courtController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("王室拥立弹窗")]
        internal UI_GamePanel_RoyalFounding royalFounding;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("远征控制器")]
        internal UI_GamePanel_Expedition expeditionController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("历史控制器")]
        internal UI_GamePanel_History historyController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("界面导航")]
        internal UI_GamePanel_Navigation navigationPanel;
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
        internal readonly GameUiSessionHandle sessionController = new GameUiSessionHandle();
        internal readonly GameUiRefreshScheduler refresh = new GameUiRefreshScheduler();
        internal readonly WorldSelectionState worldSelection = new WorldSelectionState();
        internal readonly IntelligenceViewState intelligence = new IntelligenceViewState();
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("士兵控制器")]
        internal UI_GamePanel_Soldier soldierController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("科技控制器")]
        internal UI_GamePanel_Technology technologyController;
        [SerializeField]
        [Sirenix.OdinInspector.LabelText("世界控制器")]
        internal UI_GamePanel_WorldInteraction worldController;
        internal readonly GameUiInputContext inputContext = new GameUiInputContext();
        public EventSystem InputEvents { get => inputContext.Events; private set => inputContext.Events = value; }
        public GameUiInputPolicy InputPolicy => inputContext.Policy;

        public void OpenPanelFromEvent(int panelValue) => OpenPanel(GamePanelNavigator.ParseEventTarget(panelValue));
        public void OpenPanel(GamePanelId panel) => navigator?.OpenPanel(panel);
        public void ClosePanel() => navigator?.ClosePanel();
        public void BackPanel() => backNavigation?.BackPanel();
        public bool HandleBackInput() => backNavigation?.HandleBackInput() ?? false;
        internal void BindControllers() => GameUiComposition.BindControllers(this);
        internal void ValidateInspectorConfiguration() => GameUiConfiguration.Validate(this);
        internal void Update() => refreshLoop?.Update();
        internal void Refresh() => refreshLoop?.Refresh();
        internal GamePanelNavigator navigator;
        internal GameUiBackNavigation backNavigation;
        internal GameUiRefreshLoop refreshLoop;
        [Sirenix.OdinInspector.LabelText("通用功能面板集合")]
        public UI_GamePanel_View[] FeaturePanels;
        public RectTransform PrimaryRows => Panel == GamePanelId.Royal ? courtController.PrimaryRows : ActiveListPanel?.PrimaryRows;
        public RectTransform SecondaryRows => GarrisonWindow.SecondaryRows;

        [Sirenix.OdinInspector.LabelText("暂停菜单")]
        public UI_GamePanel_PausePop PauseMenu;
        [Sirenix.OdinInspector.LabelText("界面组")]
        public CanvasGroup InterfaceGroup;
        [NonSerialized]
        public CanvasScaler InterfaceScaler;
        public GamePanelId Panel => navigator?.Panel ?? GamePanelId.Building;

        public void InitializeView()
        {
            // The pause popup must stay active so Awake can bind its buttons;
            // its CanvasGroup and overlay image control whether it is visible.
            if (PauseMenu != null && !PauseMenu.gameObject.activeSelf)
                PauseMenu.gameObject.SetActive(true);
            if (initialized)
                return;
            BindControllers();
            ValidateInspectorConfiguration();
            buildingDetailsController.RefreshInitialPreview();
            hudController.PauseButton.onClick.AddListener(PauseMenu.Open);
            hudController.Advance.onClick.AddListener(hudController.RequestAdvance);
            buildingController.InitializeBuildings();
            navigator.InitializeFeatureButtons();
            hudController.InitializeIntelligence();
            navigationPanel.Back.onClick.AddListener(navigator.BackPanel);
            navigationPanel.History.onClick.AddListener(() => historyController.OpenHistory());
            GameUiComposition.InitializePanelWindows(this);
            technologyController.InitializeResearchHud();
            initialized = true;
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
        public bool TextFocused => UiInputState.TextFocused;

        [Header("游戏界面归属")]
        [Sirenix.OdinInspector.LabelText("信息栏根对象")]
        public RectTransform HudRoot;
        [Sirenix.OdinInspector.LabelText("建筑根对象")]
        public RectTransform BuildingRoot;
        [Sirenix.OdinInspector.LabelText("功能根对象")]
        public RectTransform FeatureRoot;
        [Sirenix.OdinInspector.LabelText("弹窗根对象")]
        public RectTransform ModalRoot;
        public bool IsPanelOpen => navigator?.IsPanelOpen ?? false;
        public Button PanelCloseButton => GetPanel(Panel).CloseButton;
        internal UI_GamePanel_List ActiveListPanel => FindPanel(Panel) as UI_GamePanel_List;
        public UI_GamePanel_History HistoryWindow => (UI_GamePanel_History)GetPanel(GamePanelId.History);
        public UI_GamePanel_Inventory InventoryWindow => (UI_GamePanel_Inventory)GetPanel(GamePanelId.Inventory);
        public UI_GamePanel_Garrison GarrisonWindow => (UI_GamePanel_Garrison)GetListPanel(GamePanelId.Garrison);
        public UI_GamePanel_Intelligence IntelligenceWindow => (UI_GamePanel_Intelligence)GetListPanel(GamePanelId.Intelligence);

        public UI_GamePanel_View GetPanel(GamePanelId id) => FeaturePanels.First(p => p.PanelId == id);
        internal UI_GamePanel_View FindPanel(GamePanelId id) => FeaturePanels.FirstOrDefault(p => p.PanelId == id);
        public UI_GamePanel_List GetListPanel(GamePanelId id) => (UI_GamePanel_List)GetPanel(id);
        internal void LocateGarrison(ulong id) => GarrisonWindow.LocateGarrison(id);
        internal void OpenHistory(ulong source = 0) => HistoryWindow.OpenHistory(source);
        internal void EndInventoryDrag()
        {
            if (FeaturePanels != null && FeaturePanels.Length > 0)
                InventoryWindow.EndInventoryDrag();
        }

        bool initialized;
        internal GameUiSessionLifetime lifetime;
        public GameUiSessionHandle Session => sessionController;
        public GameUiCommandWriter Commands => commandsController;
        public UI_GamePanel_BuildingActionBar Buildings => buildingController;
        public UI_GamePanel_BuildingDetails BuildingDetails => buildingDetailsController;
        public UI_GamePanel_Technology Technology => technologyController;
        public UI_GamePanel_Quest Quests => questController;
        public UI_GamePanel_Royal Court => courtController;
        public UI_GamePanel_RoyalFounding RoyalFounding => royalFounding;
        public UI_GamePanel_Talent Talents => talentController;
        public UI_GamePanel_Policy Policies => policyController;
        public UI_GamePanel_WorldInteraction WorldInteraction => worldController;
        public UI_GamePanel_Hud Hud => hudController;

        public void BindSession(EntityManager manager, Entity sessionRoot, Camera camera, CanvasScaler scaler, EventSystem events)
        {
            if (camera == null || scaler == null || events == null)
                throw new InvalidOperationException("游戏会话缺少相机、画布缩放器或输入系统。");
            UnbindSession();
            BindControllers();
            try
            {
                lifetime.Bind(() =>
                {
                    sessionController.Bind(manager, sessionRoot);
                    refresh.Reset();
                    worldSelection.Reset();
                    intelligence.Reset();
                    commandsController.ResetSequence();
                    worldController.Camera = camera;
                    InterfaceScaler = scaler;
                    InputEvents = events;
                    InitializeView();
                    worldController.BindSession();
                    refresh.NextPanel = 0;
                });
            }
            catch
            {
                worldController.Camera = null;
                InterfaceScaler = null;
                InputEvents = null;
                throw;
            }
        }

        public void UnbindSession()
        {
            try
            {
                lifetime?.Unbind();
            }
            finally
            {
                if (worldController != null)
                    worldController.Camera = null;
                InterfaceScaler = null;
                InputEvents = null;
            }
        }

        void LateUpdate()
        {
            if (sessionController.IsBound)
                worldController.LifecycleLateUpdate();
        }

        void OnDisable()
        {
            if (lifetime?.IsBound == true)
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
