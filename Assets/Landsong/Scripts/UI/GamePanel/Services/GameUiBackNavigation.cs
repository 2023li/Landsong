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
    public sealed class GameUiBackNavigation
    {
        readonly GameUiSessionHandle sessionController;
        readonly GameUiInputContext inputContext;
        readonly GamePanelNavigator navigator;
        readonly UI_GamePanel_BuildingActionBar buildingController;
        readonly UI_GamePanel_WorldInteraction worldController;
        readonly UI_GamePanel_Soldier soldierController;
        readonly UI_GamePanel_Portrait portraitController;
        readonly UI_GamePanel_PersonRequests requestsController;
        readonly UI_GamePanel_Marriage marriageController;
        public GameUiBackNavigation(GameUiSessionHandle sessionController, GameUiInputContext inputContext, GamePanelNavigator navigator, UI_GamePanel_BuildingActionBar buildingController, UI_GamePanel_WorldInteraction worldController, UI_GamePanel_Soldier soldierController, UI_GamePanel_Portrait portraitController, UI_GamePanel_PersonRequests requestsController, UI_GamePanel_Marriage marriageController)
        {
            this.sessionController = sessionController;
            this.inputContext = inputContext;
            this.navigator = navigator;
            this.buildingController = buildingController;
            this.worldController = worldController;
            this.soldierController = soldierController;
            this.portraitController = portraitController;
            this.requestsController = requestsController;
            this.marriageController = marriageController;
        }

        bool CloseBackOwner(GameUiInputOwner owner)
        {
            switch (owner)
            {
                case GameUiInputOwner.InventoryDetails:
                    navigator.InventoryWindow.CloseResourceDetails();
                    return true;
                case GameUiInputOwner.SoldierDetails:
                    if (soldierController.EquipmentSelectionOpen)
                        soldierController.CloseEquipmentSelection();
                    else
                        soldierController.CloseSoldierDetails();
                    return true;
                case GameUiInputOwner.Portrait:
                    portraitController.ClosePortrait();
                    return true;
                case GameUiInputOwner.PersonRequests:
                    requestsController.ClosePersonRequests();
                    return true;
                case GameUiInputOwner.Marriage:
                    marriageController.CloseMarriage();
                    return true;
                case GameUiInputOwner.Pause:
                    inputContext.PauseMenu.Escape();
                    return true;
                default:
                    return false;
            }
        }

        public bool HandleBackInput()
        {
            if (!sessionController.IsBound)
                return false;
            var input = inputContext.Policy.Capture();
            if (!input.CanHandleBack)
                return false;
            if (!CloseBackOwner(input.BackOwner(includeClosedPause: true)) && !buildingController.CancelBuildingInteraction())
                BackPanel();
            worldController.cameraDragging = false;
            worldController.touchBlocked = true;
            UiInputState.BackHandledFrame = Time.frameCount;
            return true;
        }

        public void BackPanel()
        {
            if (!sessionController.IsBound)
                return;
            var input = inputContext.Policy.Capture();
            if (!input.CanHandleBack || CloseBackOwner(input.BackOwner()))
                return;
            if (buildingController.CancelBuildingInteraction())
                return;
            if (navigator.InventoryWindow.IsDragging)
            {
                navigator.EndInventoryDrag();
                return;
            }

            navigator.GoBack();
        }
    }
}
