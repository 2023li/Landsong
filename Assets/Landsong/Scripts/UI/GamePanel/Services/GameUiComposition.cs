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
    internal static class GameUiComposition
    {
        internal static void CreateServices(UI_GamePanel view)
        {
            if (view.navigator != null)
                return;
            view.navigator = new GamePanelNavigator(view.sessionController, view.inputContext, view.refresh, view.intelligence, view.commandsController, view.FeaturePanels, view.NavigationButtons, view.buildingController, view.buildingDetailsController, view.worldController, view.technologyController, view.questController, view.courtController, view.talentController, view.policyController, view.historyController);
            view.backNavigation = new GameUiBackNavigation(view.sessionController, view.inputContext, view.navigator, view.buildingController, view.worldController, view.soldierController, view.portraitController, view.requestsController, view.marriageController);
            view.refreshLoop = new GameUiRefreshLoop(view.sessionController, view.inputContext, view.refresh, view.intelligence, view.commandsController, view.navigator, view.buildingController, view.worldController, view.technologyController, view.questController, view.courtController, view.royalFounding, view.talentController, view.policyController, view.hudController, view.soldierController, view.portraitController, view.requestsController, view.marriageController);
            view.navigator.BindBack(view.backNavigation.BackPanel);
            var release = new List<Action>
            {
                view.worldController.ResetSession,
                view.worldController.WorldPresentation.ClearViews,
                view.InventoryWindow.EndInventoryDrag,
                view.InventoryWindow.ResetSession,
                view.EconomyWindow.ResetSession,
                view.GarrisonWindow.ResetSession,
                view.IntelligenceWindow.ResetSession,
                view.expeditionController.ResetSession,
                view.hudController.ResetSession,
                view.technologyController.ResetSession,
                view.buildingController.ResetSession,
                view.historyController.ResetSession,
                view.buildingController.ClearRows,
                view.talentController.ResetSession,
                view.policyController.ResetSession,
                view.courtController.ResetSession,
                view.royalFounding.ResetSession,
                view.questController.ClearSessionViews,
                view.soldierController.CloseSoldierDetails,
                view.portraitController.ClosePortrait,
                view.marriageController.CloseMarriage,
                view.requestsController.ClosePersonRequests
            };
            release.InsertRange(10, view.FeaturePanels.Select(panel => (Action)panel.ClearAllRows));
            view.lifetime = new GameUiSessionLifetime(view.sessionController, view.refresh, view.worldSelection, view.intelligence, view.commandsController, view.navigator, release.ToArray());
        }

        internal static void BindControllers(UI_GamePanel view)
        {
            if (view.worldController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：UI_GamePanel_WorldInteraction");
            if (view.buildingController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：UI_GamePanel_BuildingActionBar");
            if (view.buildingDetailsController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：UI_GamePanel_BuildingDetails");
            if (view.technologyController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameTechnologyController");
            if (view.questController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameQuestController");
            if (view.courtController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameCourtController");
            if (view.royalFounding == null)
                throw new InvalidOperationException("游戏面板未配置王室拥立弹窗。");
            if (view.marriageController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameMarriageController");
            if (view.portraitController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GamePortraitController");
            if (view.requestsController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GamePersonRequestsController");
            if (view.soldierController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameSoldierController");
            if (view.hudController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameHudController");
            if (view.historyController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameHistoryController");
            if (view.expeditionController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GameExpeditionController");
            if (view.phaseController == null)
                throw new InvalidOperationException("游戏面板未配置控制器：GamePhaseController");
            if (view.talentController == null || view.policyController == null)
                throw new InvalidOperationException("未配置人才或政策控制器。");
            CreateServices(view);
            view.buildingController.worldSelection = view.worldSelection;
            view.buildingController.intelligence = view.intelligence;
            view.buildingController.refresh = view.refresh;
            view.buildingDetailsController.worldSelection = view.worldSelection;
            view.buildingDetailsController.intelligence = view.intelligence;
            view.buildingDetailsController.refresh = view.refresh;
            view.talentController.intelligence = view.intelligence;
            view.talentController.refresh = view.refresh;
            view.policyController.intelligence = view.intelligence;
            view.policyController.refresh = view.refresh;
            view.courtController.intelligence = view.intelligence;
            view.courtController.refresh = view.refresh;
            view.expeditionController.worldSelection = view.worldSelection;
            view.expeditionController.refresh = view.refresh;
            view.historyController.refresh = view.refresh;
            view.hudController.intelligence = view.intelligence;
            view.hudController.refresh = view.refresh;
            view.marriageController.intelligence = view.intelligence;
            view.marriageController.refresh = view.refresh;
            view.phaseController.refresh = view.refresh;
            view.portraitController.refresh = view.refresh;
            view.questController.intelligence = view.intelligence;
            view.questController.refresh = view.refresh;
            view.requestsController.refresh = view.refresh;
            view.soldierController.refresh = view.refresh;
            view.technologyController.refresh = view.refresh;
            view.worldController.worldSelection = view.worldSelection;
            view.worldController.intelligence = view.intelligence;
            view.worldController.refresh = view.refresh;
            view.commandsController.refresh = view.refresh;
            view.GarrisonWindow.WorldSelection = view.worldSelection;
            view.InventoryWindow.inputContext = view.inputContext;
            view.InventoryWindow.economy = view.EconomyWindow;
            view.buildingController.inputContext = view.inputContext;
            view.buildingController.inventory = view.InventoryWindow;
            view.buildingController.economy = view.EconomyWindow;
            view.buildingDetailsController.economy = view.EconomyWindow;
            view.hudController.inputContext = view.inputContext;
            view.hudController.garrison = view.GarrisonWindow;
            view.hudController.intelligenceWindow = view.IntelligenceWindow;
            view.hudController.backgroundGroup = view.InterfaceGroup;
            view.marriageController.inputContext = view.inputContext;
            view.marriageController.inventory = view.InventoryWindow;
            view.requestsController.inputContext = view.inputContext;
            view.requestsController.inventory = view.InventoryWindow;
            view.portraitController.inputContext = view.inputContext;
            view.portraitController.inventory = view.InventoryWindow;
            view.soldierController.inputContext = view.inputContext;
            view.technologyController.inputContext = view.inputContext;
            view.worldController.inputContext = view.inputContext;
            view.talentController.sessionController = view.sessionController;
            view.talentController.commandsController = view.commandsController;
            view.talentController.navigation = view.navigator;
            view.talentController.buildingController = view.buildingController;
            view.policyController.sessionController = view.sessionController;
            view.policyController.commandsController = view.commandsController;
            view.policyController.navigation = view.navigator;
            view.policyController.buildingController = view.buildingController;
            view.commandsController.sessionController = view.sessionController;
            view.commandsController.inputContext = view.inputContext;
            view.inputContext.PauseMenu = view.PauseMenu;
            view.inputContext.Policy = new GameUiInputPolicy(view.sessionController, view.intelligence, view.navigator, view.inputContext, view.buildingController, view.soldierController, view.portraitController, view.requestsController, view.marriageController, view.InventoryWindow, view.royalFounding);
            view.worldController.sessionController = view.sessionController;
            view.worldController.buildingController = view.buildingController;
            view.worldController.hudController = view.hudController;
            view.worldController.navigation = view.navigator;
            view.worldController.commandsController = view.commandsController;
            view.buildingController.navigation = view.navigator;
            view.buildingController.sessionController = view.sessionController;
            view.buildingController.worldController = view.worldController;
            view.buildingController.commandsController = view.commandsController;
            view.buildingController.hudController = view.hudController;
            view.buildingDetailsController.BindPresenter(view.sessionController, view.commandsController, view.navigator, view.buildingController, view.courtController, view.hudController, view.soldierController, view.worldController);
            view.technologyController.navigation = view.navigator;
            view.technologyController.buildingController = view.buildingController;
            view.technologyController.sessionController = view.sessionController;
            view.technologyController.commandsController = view.commandsController;
            view.questController.sessionController = view.sessionController;
            view.questController.buildingController = view.buildingController;
            view.questController.commandsController = view.commandsController;
            view.questController.navigation = view.navigator;
            view.questController.hudController = view.hudController;
            view.courtController.sessionController = view.sessionController;
            view.courtController.commandsController = view.commandsController;
            view.courtController.buildingController = view.buildingController;
            view.courtController.marriageController = view.marriageController;
            view.courtController.navigation = view.navigator;
            view.courtController.requestsController = view.requestsController;
            view.marriageController.sessionController = view.sessionController;
            view.marriageController.courtController = view.courtController;
            view.marriageController.navigation = view.navigator;
            view.marriageController.soldierController = view.soldierController;
            view.marriageController.requestsController = view.requestsController;
            view.marriageController.portraitController = view.portraitController;
            view.marriageController.buildingController = view.buildingController;
            view.marriageController.worldController = view.worldController;
            view.marriageController.commandsController = view.commandsController;
            view.portraitController.sessionController = view.sessionController;
            view.portraitController.navigation = view.navigator;
            view.portraitController.marriageController = view.marriageController;
            view.portraitController.requestsController = view.requestsController;
            view.portraitController.courtController = view.courtController;
            view.portraitController.soldierController = view.soldierController;
            view.portraitController.buildingController = view.buildingController;
            view.portraitController.worldController = view.worldController;
            view.portraitController.commandsController = view.commandsController;
            view.requestsController.soldierController = view.soldierController;
            view.requestsController.marriageController = view.marriageController;
            view.requestsController.portraitController = view.portraitController;
            view.requestsController.sessionController = view.sessionController;
            view.requestsController.navigation = view.navigator;
            view.requestsController.buildingController = view.buildingController;
            view.requestsController.worldController = view.worldController;
            view.requestsController.courtController = view.courtController;
            view.requestsController.expeditionController = view.expeditionController;
            view.requestsController.commandsController = view.commandsController;
            view.soldierController.buildingController = view.buildingController;
            view.soldierController.sessionController = view.sessionController;
            view.soldierController.navigation = view.navigator;
            view.soldierController.marriageController = view.marriageController;
            view.soldierController.requestsController = view.requestsController;
            view.soldierController.portraitController = view.portraitController;
            view.soldierController.commandsController = view.commandsController;
            view.hudController.sessionController = view.sessionController;
            view.hudController.worldController = view.worldController;
            view.hudController.navigation = view.navigator;
            view.hudController.commandsController = view.commandsController;
            view.hudController.questController = view.questController;
            view.hudController.buildingController = view.buildingController;
            view.hudController.historyController = view.historyController;
            view.historyController.worldController = view.worldController;
            view.historyController.navigation = view.navigator;
            view.historyController.sessionController = view.sessionController;
            view.expeditionController.sessionController = view.sessionController;
            view.expeditionController.buildingController = view.buildingController;
            view.expeditionController.courtController = view.courtController;
            view.expeditionController.commandsController = view.commandsController;
            view.phaseController.sessionController = view.sessionController;
            view.phaseController.commandsController = view.commandsController;
            view.phaseController.navigation = view.navigator;
        }

        internal static void InitializePanelWindows(UI_GamePanel view)
        {
            foreach (var panel in view.FeaturePanels)
            {
                panel.Bind(view.sessionController, view.commandsController, view.navigator, view.refresh);
                if (panel is UI_GamePanel_Economy economy)
                    economy.Buildings = view.buildingController;
                if (panel is UI_GamePanel_Inventory inventory)
                {
                    inventory.Buildings = view.buildingController;
                    inventory.Feedback = view.hudController;
                }

                if (panel is UI_GamePanel_Garrison garrison)
                {
                    garrison.Buildings = view.buildingController;
                    garrison.Soldiers = view.soldierController;
                    garrison.World = view.worldController;
                }

                if (panel is UI_GamePanel_Intelligence intelligencePanel)
                {
                    intelligencePanel.World = view.worldController;
                }
            }

            view.navigator.RefreshPanelVisibility();
            view.royalFounding.Bind(view.sessionController, view.commandsController);
        }
    }
}
