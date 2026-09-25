using Landsong.ECS.Definitions;
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
    public sealed class GamePanelNavigator : IGameUiNavigation
    {
        readonly GameUiSessionHandle sessionController;
        readonly GameUiInputContext inputContext;
        readonly GameUiRefreshScheduler refresh;
        readonly IntelligenceViewState intelligence;
        readonly GameUiCommandWriter commandsController;
        readonly UI_GamePanel_View[] FeaturePanels;
        readonly UI_GamePanel.NavigationButtonBinding[] NavigationButtons;
        readonly UI_GamePanel_BuildingActionBar buildingController;
        readonly UI_GamePanel_BuildingDetails buildingDetailsController;
        readonly UI_GamePanel_WorldInteraction worldController;
        readonly UI_GamePanel_Technology technologyController;
        readonly UI_GamePanel_Quest questController;
        readonly UI_GamePanel_Royal courtController;
        readonly UI_GamePanel_Talent talentController;
        readonly UI_GamePanel_Policy policyController;
        readonly UI_GamePanel_History historyController;
        readonly Stack<GamePanelId> panelHistory = new Stack<GamePanelId>();
        Action backRequested;
        public GamePanelId Panel { get; internal set; } = GamePanelId.Building;
        public bool IsPanelOpen { get; internal set; }
        public UI_GamePanel_Inventory InventoryWindow => (UI_GamePanel_Inventory)GetPanel(GamePanelId.Inventory);
        public UI_GamePanel_Economy EconomyWindow => (UI_GamePanel_Economy)GetPanel(GamePanelId.Economy);
        public UI_GamePanel_Garrison GarrisonWindow => (UI_GamePanel_Garrison)GetListPanel(GamePanelId.Garrison);
        public UI_GamePanel_Intelligence IntelligenceWindow => (UI_GamePanel_Intelligence)GetListPanel(GamePanelId.Intelligence);

        public void BindBack(Action action) => backRequested = action;
        public void BackPanel() => backRequested?.Invoke();
        internal void GoBack()
        {
            if (panelHistory.Count > 0)
                OpenPanelCore(panelHistory.Pop(), false);
            else
                ClosePanel();
        }

        public void Reset()
        {
            IsPanelOpen = false;
            Panel = GamePanelId.Building;
            panelHistory.Clear();
        }

        public GamePanelNavigator(GameUiSessionHandle sessionController, GameUiInputContext inputContext, GameUiRefreshScheduler refresh, IntelligenceViewState intelligence, GameUiCommandWriter commandsController, UI_GamePanel_View[] FeaturePanels, UI_GamePanel.NavigationButtonBinding[] NavigationButtons, UI_GamePanel_BuildingActionBar buildingController, UI_GamePanel_BuildingDetails buildingDetailsController, UI_GamePanel_WorldInteraction worldController, UI_GamePanel_Technology technologyController, UI_GamePanel_Quest questController, UI_GamePanel_Royal courtController, UI_GamePanel_Talent talentController, UI_GamePanel_Policy policyController, UI_GamePanel_History historyController)
        {
            this.sessionController = sessionController;
            this.inputContext = inputContext;
            this.refresh = refresh;
            this.intelligence = intelligence;
            this.commandsController = commandsController;
            this.FeaturePanels = FeaturePanels;
            this.NavigationButtons = NavigationButtons;
            this.buildingController = buildingController;
            this.buildingDetailsController = buildingDetailsController;
            this.worldController = worldController;
            this.technologyController = technologyController;
            this.questController = questController;
            this.courtController = courtController;
            this.talentController = talentController;
            this.policyController = policyController;
            this.historyController = historyController;
        }

        internal void InitializeFeatureButtons()
        {
            if (NavigationButtons == null || NavigationButtons.Length == 0 || NavigationButtons.Any(binding => binding == null || binding.Button == null || binding.Target != GamePanelId.Building && FindPanel(binding.Target) == null) || NavigationButtons.Select(binding => binding.Button).Distinct().Count() != NavigationButtons.Length)
                throw new InvalidOperationException("功能导航必须显式配置有效目的地与唯一按钮。");
        }

        internal void RefreshFeatureAccess()
        {
            foreach (var binding in NavigationButtons)
                binding.Button.interactable = IsDestinationUnlocked(binding.Target);
            if (IsPanelOpen && !CanOpenDestination(Panel))
                ClosePanel();
        }

        bool IsDestinationUnlocked(GamePanelId panel) => panel == GamePanelId.Building ? FeatureUnlocks.Has(sessionController.em, sessionController.root, FeatureDefinitions.Find(sessionController.em, sessionController.root, new Unity.Collections.FixedString128Bytes("feature.Building"))) : GetPanel(panel).IsFeatureUnlocked(sessionController.em, sessionController.root);
        bool CanOpenDestination(GamePanelId panel) => panel == GamePanelId.Building ? FeatureUnlocks.Has(sessionController.em, sessionController.root, FeatureDefinitions.Find(sessionController.em, sessionController.root, new Unity.Collections.FixedString128Bytes("feature.Building"))) : GetPanel(panel).CanOpen(sessionController.em, sessionController.root);
        // Unity's persistent Button event serializes enum values through an integer argument.
        // Invalid values are configuration errors; no text-to-panel compatibility path exists.
        public static GamePanelId ParseEventTarget(int panelValue)
        {
            if (!Enum.IsDefined(typeof(GamePanelId), panelValue) || panelValue == (int)GamePanelId.None)
                throw new InvalidOperationException("导航事件包含无效面板标识：" + panelValue);
            return (GamePanelId)panelValue;
        }

        public void OpenPanel(GamePanelId panel) => OpenPanelCore(panel, true);
        internal void OpenPanelCore(GamePanelId panel, bool remember)
        {
            if (!sessionController.IsBound || !inputContext.Policy.Capture().CanNavigate)
                return;
            if (panel == GamePanelId.Pause)
            {
                inputContext.PauseMenu.Open();
                return;
            }

            if (!CanOpenDestination(panel))
                return;
            bool enteringIntel = panel == GamePanelId.Intelligence;
            if (intelligence.IsOpen != enteringIntel)
                commandsController.TryQueue(new SetIntelligenceModeRequest { Enabled = enteringIntel });
            if (enteringIntel)
            {
                worldController.EndBuildingPlacement();
                EndInventoryDrag();
                IntelligenceWindow.ResetView();
                if (buildingController.BuildingConfirmPanel != null)
                    buildingController.BuildingConfirmPanel.SetActive(false);
                buildingController.CropSelectionPanel?.Hide();
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
                if (inputContext.Events != null)
                    inputContext.Events.SetSelectedGameObject(null);
            }

            Panel = panel;
            IsPanelOpen = true;
            intelligence.IsOpen = enteringIntel;
            refresh.NextPanel = 0;
            if (panel != GamePanelId.Building)
            {
                buildingController.showBuildingActionBar = false;
                buildingDetailsController.Hide();
                if (buildingController.BuildingActionBar != null)
                    buildingController.BuildingActionBar.gameObject.SetActive(false);
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
            if (panel != GamePanelId.Royal)
                courtController.GraphRoot.gameObject.SetActive(false);
            RefreshPanelVisibility();
        }

        public void ClosePanel()
        {
            if (!sessionController.IsBound || !inputContext.Policy.Capture().CanNavigate || Panel == GamePanelId.DynastyEnd)
                return;
            if (intelligence.IsOpen)
                commandsController.TryQueue(new SetIntelligenceModeRequest { Enabled = false });
            intelligence.IsOpen = false;
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
            courtController.GraphRoot.gameObject.SetActive(false);
            talentController.CourtGraph.gameObject.SetActive(false);
            policyController.CourtGraph.gameObject.SetActive(false);
            if (historyController.historyTools != null)
                historyController.historyTools.gameObject.SetActive(false);
            if (inputContext.Events != null)
                inputContext.Events.SetSelectedGameObject(null);
            refresh.NextPanel = 0;
            RefreshPanelVisibility();
        }

        internal void RefreshPanelVisibility()
        {
            bool primary = IsPanelOpen && Panel != GamePanelId.Technology && Panel != GamePanelId.Quest && (Panel != GamePanelId.Royal || courtController.royalOverview);
            foreach (var window in FeaturePanels)
                window.gameObject.SetActive(IsPanelOpen && window.PanelId == Panel);
            var active = FindPanel(Panel);
            if (active != null)
                active.SetContentVisible(primary);
        }

        public UI_GamePanel_View GetPanel(GamePanelId id) => FeaturePanels.First(p => p.PanelId == id);
        internal UI_GamePanel_View FindPanel(GamePanelId id) => FeaturePanels.FirstOrDefault(p => p.PanelId == id);
        public UI_GamePanel_List GetListPanel(GamePanelId id) => (UI_GamePanel_List)GetPanel(id);
        internal void LocateGarrison(ulong id) => GarrisonWindow.LocateGarrison(id);
        internal void OpenEconomy(ulong source = 0) => EconomyWindow.OpenEconomy(source);
        internal void EndInventoryDrag()
        {
            if (FeaturePanels != null && FeaturePanels.Length > 0)
                InventoryWindow.EndInventoryDrag();
        }
    }
}
