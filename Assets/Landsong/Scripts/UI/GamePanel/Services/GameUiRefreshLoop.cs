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
    public sealed class GameUiRefreshLoop
    {
        readonly GameUiSessionHandle sessionController;
        readonly GameUiInputContext inputContext;
        readonly GameUiRefreshScheduler refresh;
        readonly IntelligenceViewState intelligence;
        readonly GameUiCommandWriter commandsController;
        readonly GamePanelNavigator navigator;
        readonly UI_GamePanel_BuildingActionBar buildingController;
        readonly UI_GamePanel_WorldInteraction worldController;
        readonly UI_GamePanel_Technology technologyController;
        readonly UI_GamePanel_Quest questController;
        readonly UI_GamePanel_Court courtController;
        readonly UI_GamePanel_Talent talentController;
        readonly UI_GamePanel_Policy policyController;
        readonly UI_GamePanel_Hud hudController;
        readonly UI_GamePanel_Soldier soldierController;
        readonly UI_GamePanel_Portrait portraitController;
        readonly UI_GamePanel_PersonRequests requestsController;
        readonly UI_GamePanel_Marriage marriageController;
        Phase observedPhase;
        public GameUiRefreshLoop(GameUiSessionHandle sessionController, GameUiInputContext inputContext, GameUiRefreshScheduler refresh, IntelligenceViewState intelligence, GameUiCommandWriter commandsController, GamePanelNavigator navigator, UI_GamePanel_BuildingActionBar buildingController, UI_GamePanel_WorldInteraction worldController, UI_GamePanel_Technology technologyController, UI_GamePanel_Quest questController, UI_GamePanel_Court courtController, UI_GamePanel_Talent talentController, UI_GamePanel_Policy policyController, UI_GamePanel_Hud hudController, UI_GamePanel_Soldier soldierController, UI_GamePanel_Portrait portraitController, UI_GamePanel_PersonRequests requestsController, UI_GamePanel_Marriage marriageController)
        {
            this.sessionController = sessionController;
            this.inputContext = inputContext;
            this.refresh = refresh;
            this.intelligence = intelligence;
            this.commandsController = commandsController;
            this.navigator = navigator;
            this.buildingController = buildingController;
            this.worldController = worldController;
            this.technologyController = technologyController;
            this.questController = questController;
            this.courtController = courtController;
            this.talentController = talentController;
            this.policyController = policyController;
            this.hudController = hudController;
            this.soldierController = soldierController;
            this.portraitController = portraitController;
            this.requestsController = requestsController;
            this.marriageController = marriageController;
        }

        internal void Update()
        {
            if (!sessionController.IsBound || !EcsSceneFlow.GameReady)
                return;
            var state = sessionController.em.GetComponentData<Session>(sessionController.root);
            GameClock stateClock = sessionController.em.GetComponentData<GameClock>(sessionController.root);
            SimulationControl stateControl = sessionController.em.GetComponentData<SimulationControl>(sessionController.root);
            NightRuntimeState stateNight = sessionController.em.GetComponentData<NightRuntimeState>(sessionController.root);
            IntelligenceModeState stateIntelligenceMode = sessionController.em.GetComponentData<IntelligenceModeState>(sessionController.root);
            PersistenceGate statePersistence = sessionController.em.GetComponentData<PersistenceGate>(sessionController.root);
            DynastyIdentity stateDynasty = sessionController.em.GetComponentData<DynastyIdentity>(sessionController.root);
            refresh.Observe(state, stateClock, stateControl, stateNight, stateIntelligenceMode, statePersistence, InterfaceSettings.Revision, sessionController.em.GetBuffer<GameEvent>(sessionController.root, true).Length > 0 || sessionController.em.GetBuffer<ResearchCompletedEvent>(sessionController.root, true).Length > 0 || sessionController.em.GetBuffer<ItemPickupEvent>(sessionController.root, true).Length > 0, UI_GamePanel_InteractionLock.Revision);
            hudController.ConsumeInterfaceEvents(state);
            technologyController.RefreshResearchHud();
            marriageController.RefreshMarriageEvents();
            requestsController.RefreshPersonRequests();
            portraitController.RefreshPortraitCustomization();
            worldController.Input();
            if (refresh.TakeHudRefresh(Time.unscaledTime))
                RefreshHud(state, stateClock, stateControl, stateNight, stateIntelligenceMode, statePersistence, stateDynasty);
            bool editing = inputContext.TextFocused || navigator.InventoryWindow.IsDragging || navigator.InventoryWindow.IsEditing;
            // Pointer ownership belongs to each configured row; holding the mouse never freezes the HUD.
            if (refresh.NeedsPanelRefresh(Time.unscaledTime, true, editing))
            {
                RefreshVisibleContent(state, stateControl, stateNight);
                refresh.MarkRendered(Time.unscaledTime);
            }
        }

        internal void Refresh()
        {
            if (!sessionController.IsBound)
                return;
            var s = sessionController.em.GetComponentData<Session>(sessionController.root);
            GameClock sClock = sessionController.em.GetComponentData<GameClock>(sessionController.root);
            SimulationControl sControl = sessionController.em.GetComponentData<SimulationControl>(sessionController.root);
            NightRuntimeState sNight = sessionController.em.GetComponentData<NightRuntimeState>(sessionController.root);
            IntelligenceModeState sIntelligenceMode = sessionController.em.GetComponentData<IntelligenceModeState>(sessionController.root);
            PersistenceGate sPersistence = sessionController.em.GetComponentData<PersistenceGate>(sessionController.root);
            DynastyIdentity sDynasty = sessionController.em.GetComponentData<DynastyIdentity>(sessionController.root);
            RefreshHud(s, sClock, sControl, sNight, sIntelligenceMode, sPersistence, sDynasty);
            RefreshVisibleContent(s, sControl, sNight);
            refresh.MarkRendered(Time.unscaledTime);
        }

        void RefreshHud(Session s, GameClock sClock, SimulationControl sControl, NightRuntimeState sNight, IntelligenceModeState sIntelligenceMode, PersistenceGate sPersistence, DynastyIdentity sDynasty)
        {
            if (intelligence.IsOpen && sIntelligenceMode.Enabled == 0)
            {
                bool queued = false;
                foreach (var command in sessionController.em.GetBuffer<QueuedGameplayRequest>(sessionController.root))
                    if (command.Kind == CommandKind.IntelligenceMode && sessionController.em.GetComponentData<SetIntelligenceModeRequest>(command.Payload).Enabled)
                        queued = true;
                if (!queued)
                {
                    intelligence.IsOpen = false;
                    navigator.ClosePanel();
                    navigator.IntelligenceWindow.ResetView();
                }
            }

            hudController.RefreshIntelligenceBadge();
            if (observedPhase == Phase.GameOver && s.Phase != Phase.GameOver && s.Phase != Phase.Ended)
            {
                navigator.Panel = GamePanelId.Building;
                navigator.ClosePanel();
                hudController.Message.text = "";
                navigator.GarrisonWindow.SelectedSoldier = 0;
                worldController.EndBuildingPlacement();
                if (buildingController.BuildingConfirmPanel != null)
                    buildingController.BuildingConfirmPanel.SetActive(false);
                worldController.rangeRevision = -1;
            }

            observedPhase = s.Phase;
            hudController.RefreshHeader(s, sClock, sControl, sNight, sPersistence, sDynasty);
            hudController.RefreshHeroHud();
            if (s.Phase == Phase.GameOver || s.Phase == Phase.Ended)
            {
                navigator.Panel = GamePanelId.DynastyEnd;
                navigator.IsPanelOpen = true;
                intelligence.IsOpen = false;
            }

            technologyController.RefreshTechnologyAccess();
            navigator.RefreshFeatureAccess();
        }

        void RefreshVisibleContent(Session s, SimulationControl sControl, NightRuntimeState sNight)
        {
            soldierController.RefreshSoldierDetails();
            navigator.RefreshPanelVisibility();
            var featurePanel = navigator.IsPanelOpen ? navigator.FindPanel(navigator.Panel) : null;
            var listPanel = featurePanel as UI_GamePanel_List;
            if (listPanel != null)
                listPanel.BeginRender();
            buildingController.RefreshBuildingCatalog();
            navigator.RefreshPanelVisibility();
            buildingController.RefreshBuildingSelection();
            if (intelligence.IsOpen)
                hudController.Selection.text = "情报模式：WASD / 滚轮调整镜头；退出后恢复操作。";
            questController.RefreshQuestTracking();
            if (listPanel != null && !intelligence.IsOpen && s.Phase == Phase.Night && sNight.Kind == NightKind.Peaceful)
                listPanel.Row(sNight.Speed == 2 ? "平安夜速度 2×（切回 1×）" : "平安夜速度 1×（切换 2×）", sControl.Paused == 0 ? () => commandsController.TryQueue(new SetNightSpeedRequest { Speed = sNight.Speed == 2 ? 1 : 2 }) : null);
            if (listPanel != null && s.Phase == Phase.Day && sessionController.em.GetBuffer<BattleReportEntry>(sessionController.root).Length > 0 && navigator.Panel != GamePanelId.BattleReport)
                listPanel.Row("查看上一晚结算（含被盗物资）", () => navigator.OpenPanel(GamePanelId.BattleReport));
            if (listPanel != null)
            {
                listPanel.Render();
                listPanel.EndRender();
            }

            if (featurePanel != null && listPanel == null)
                featurePanel.Render();
            courtController.RefreshCourtPresentation();
            talentController.RefreshPresentation();
            policyController.RefreshPresentation();
        }
    }
}
