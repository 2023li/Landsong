#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator HudPanelsUi(UI_GamePanel view, EntityManager em, Entity root)
        {
            var original = SnapshotCodec.Capture(em, root);
            var canvas = view.GetComponentInParent<Canvas>();
            var primary = view.GetListPanel(view.Panel).PrimaryScroll.gameObject;
            var secondary = view.GarrisonWindow.SecondaryRows.GetComponentInParent<ScrollRect>(true).gameObject;
            try
            {
                yield return new WaitForSecondsRealtime(.4f);
                Require(view.HudRoot!=null&&view.FeatureRoot!=null&&view.BuildingRoot!=null&&view.ModalRoot!=null,"Game scene binds explicit UI ownership layers");
                Require(view.PauseMenu.transform.parent==view.ModalRoot&&view.PauseMenu.gameObject==view.PauseMenu.Overlay&&view.GetComponent<UI_GamePanel_PausePop>()==null,"Pause menu controller belongs to its modal panel instead of the UI root");
                Require(view.FeaturePanels.Select(p=>p.PrimaryRows).Distinct().Count()==view.FeaturePanels.Length,"Every function owns a separate content container");
                Require(view.FeaturePanels.All(p=>p.transform.parent==view.FeatureRoot),"Feature windows are scene-owned children of the feature layer");
                Require(view.Buildings.BuildingDetailsPanel.transform.parent==view.FeatureRoot,"Building details belongs to the feature panel layer");
                Require(view.GarrisonWindow.PrimaryRows.IsChildOf(view.GarrisonWindow.transform)&&view.GarrisonWindow.SecondaryRows.IsChildOf(view.GarrisonWindow.transform),"Assigned and pending soldiers share only their garrison owner");
                Require(view.Buildings.NameInput.transform.IsChildOf(view.Buildings.BuildingDetailsPanel.transform),"Building name input belongs to details from scene initialization");
                Require(!view.Buildings.BuildingPlacementPanel.activeSelf,"Placement hint is hidden outside placement");
                Require(view.Buildings.BuildingHint.transform.IsChildOf(view.Buildings.BuildingPlacementPanel.transform)&&view.Buildings.BuildingPlacementPanel.transform.parent==view.BuildingRoot,"Placement background and text have one visibility owner");
                Require(!view.IsPanelOpen && !primary.activeInHierarchy && !secondary.activeInHierarchy, "Entering Game leaves function scroll panels closed");
                Require(!view.Technology.TechnologyButton.gameObject.activeSelf, "Research HUD respects the technology license");
                foreach (var permission in new[] { "feature.Inventory", "feature.Building", "feature.Expedition", ResearchOps.FeatureId })
                    FeatureOps.Unlock(em, root, Sim.FindDefinition(em, root, new FixedString128Bytes(permission)));
                yield return WaitFor(() => view.Technology.TechnologyButton.gameObject.activeSelf, "Research HUD appears after license");
                Require(view.Technology.CurrentResearchDefinition == -1 && view.Technology.ResearchHudName.text == "尚未选择科技", "Empty research queue has a clear selectable HUD state");
                Require(view.Technology.TechnologyButton == view.Technology.ResearchHud.Open && view.Technology.ResearchHud.transform.IsChildOf(view.HudRoot) && !view.Technology.ResearchHud.transform.IsChildOf(view.FeatureRoot), "Research card belongs to the HUD independently of feature windows");
                foreach (var panel in new[] { GamePanelId.Economy, GamePanelId.Inventory, GamePanelId.Garrison, GamePanelId.Expedition, GamePanelId.Talent, GamePanelId.Royal, GamePanelId.Policy, GamePanelId.History, GamePanelId.BattleReport })
                {
                    view.OpenPanel(panel);
                    primary = panel==GamePanelId.Royal ? view.Court.RoyalOverviewRoot.gameObject : view.GetListPanel(panel).PrimaryScroll.gameObject;
                    yield return new WaitForSecondsRealtime(.3f);
                    Require(view.IsPanelOpen && (panel==GamePanelId.Royal?view.Court.RoyalDetails!=null&&view.Court.RoyalDetails.gameObject.activeInHierarchy:primary.activeInHierarchy) && view.PanelCloseButton.interactable, panel + " opens a dismissible function panel");
                    if (panel == GamePanelId.Garrison) Require(secondary.activeInHierarchy, "Military opens both coordinated scroll views");
                    Require(view.FeaturePanels.All(p=>p.gameObject.activeSelf==(p.PanelId==panel)),"Opening a feature activates exactly its registered root and hides unrelated windows");
                    var scroll = view.PrimaryRows.GetComponentInParent<ScrollRect>(true);
                    scroll.verticalNormalizedPosition = 0;
                    Require(!view.PanelCloseButton.transform.IsChildOf(scroll.content), "Close button remains outside scrolling content: " + panel);
                    if (panel == GamePanelId.Inventory && Application.isEditor)
                    {
                        ScreenCapture.CaptureScreenshot("Library/LandsongEcs/function-panel-inventory.png");
                        yield return null;
                    }
                    view.PanelCloseButton.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.4f);
                    Require(!view.IsPanelOpen && !primary.activeInHierarchy && !secondary.activeInHierarchy, panel + " stays closed across periodic refresh");
                    Require(!canvas.GetComponentsInChildren<UI_GamePanel_CourtGraph>().Any(), "Closing also hides associated family or policy graph");
                }
                view.OpenPanel(GamePanelId.Quest);
                yield return WaitFor(() => view.Quests.QuestWindow != null && view.Quests.QuestWindow.activeSelf, "Quest window opens");
                Require(view.Quests.QuestPanel.Close == view.GetListPanel(GamePanelId.Quest).CloseButton, "Quest header uses the close button bound by its feature owner before first render");
                view.Quests.QuestPanel.Close.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.Quests.QuestWindow.activeSelf && !primary.activeInHierarchy && !view.IsPanelOpen, "Quest header closes to unobstructed gameplay");
                view.Buildings.ToggleBuildingCatalog();
                yield return new WaitForSecondsRealtime(.3f);
                Require(view.Buildings.BuildingBar.gameObject.activeSelf, "Building function opens its bottom catalog");
                view.Buildings.BuildingBar.CloseButton.onClick.Invoke();
                Require(!view.IsPanelOpen && !view.Buildings.BuildingBar.gameObject.activeSelf && !primary.activeInHierarchy, "Building close does not reopen the default scroll panel");
                view.OpenPanel(GamePanelId.Intelligence);
                yield return WaitFor(() => em.GetComponentData<Session>(root).IntelligenceMode != 0, "Intelligence mode entered through command");
                view.PanelCloseButton.onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<Session>(root).IntelligenceMode == 0, "Closing intelligence exits authoritative read-only map mode");
                Require(!primary.activeInHierarchy && !view.IsPanelOpen, "Intelligence close restores unobstructed gameplay");

                int first = Sim.FindDefinition(em, root, new FixedString128Bytes("TN_3_1_启蒙"));
                int wood = Sim.FindDefinition(em, root, new FixedString128Bytes("TN_4_2_木工术"));
                Require(ResearchOps.Plan(em, root, wood) == ResultCode.Success, "HUD fixture creates an actual prerequisite research plan");
                var session = em.GetComponentData<Session>(root); session.ResearchPoints = 2; em.SetComponentData(root, session);
                ResearchOps.Settle(em, root);
                yield return WaitFor(() => view.Technology.CurrentResearchDefinition == first && Mathf.Approximately(view.Technology.ResearchHudProgress.rectTransform.anchorMax.x, .4f), "HUD reads the current technology and actual 2/5 progress");
                Require(view.Technology.ResearchHudName.text == "启蒙" && !string.IsNullOrWhiteSpace(view.Technology.ResearchHudEffects.text), "HUD shows technology name and effects or description");
                var beforeOpen = SnapshotCodec.Capture(em, root);
                view.Technology.TechnologyButton.onClick.Invoke();
                yield return WaitFor(() => view.Technology.TechnologyTree != null && view.Technology.TechnologyTree.gameObject.activeSelf && view.Technology.TechnologyTree.Selected == first, "Clicking research HUD opens and renders the current technology selection");
                Require(view.Technology.TechnologyTree.Selected == first && !primary.activeInHierarchy, "HUD opens and focuses current research without the primary scroll view");
                view.Technology.TechnologyTree.CloseButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.IsPanelOpen && !primary.activeInHierarchy && !view.Technology.TechnologyTree.gameObject.activeSelf, "Technology close does not reopen a default panel");
                Require(beforeOpen.SequenceEqual(SnapshotCodec.Capture(em, root)), "Opening and closing research changes no research points or gameplay state");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/research-hud.png");
                    yield return null;
                }
                ResearchOps.Command(em, root, first, true);
                yield return WaitFor(() => view.Technology.CurrentResearchDefinition == wood, "Cancelling the first entry advances HUD to blocked successor");
                Require(view.Technology.ResearchHudEffects.text.Contains("木材加工厂"), "HUD lists the technology's actual building unlock effects");
                Require(ResearchOps.Plan(em, root, wood) == ResultCode.Success, "Replanning retains previously invested research");
                session = em.GetComponentData<Session>(root); session.ResearchPoints = 3; em.SetComponentData(root, session); ResearchOps.Settle(em, root);
                yield return WaitFor(() => view.Technology.CurrentResearchDefinition == wood && view.Technology.ResearchHudName.text == "木工术", "Research completion advances HUD to the next technology");
                session = em.GetComponentData<Session>(root); session.ResearchPoints = 8; em.SetComponentData(root, session); ResearchOps.Settle(em, root);
                yield return WaitFor(() => view.Technology.CurrentResearchDefinition == -1, "Finishing the queue restores empty research HUD");
            }
            finally
            {
                if (view.Buildings.BuildingConfirmPanel != null) view.Buildings.BuildingConfirmPanel.SetActive(false);
                view.ClosePanel();
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            }
            yield return WaitFor(() => !view.Technology.TechnologyButton.gameObject.activeSelf, "Restoring an earlier save refreshes HUD licensing");
            yield return QuestUi(view,em,root,"Map_Test2");
            yield return BuildingDetailsUi(view,em,root);
            yield return GarrisonUi(view,em,root);
            yield return RoyalFamilyUi(view,em,root);
            yield return PortraitsUi(view,em,root);
            yield return TechnologyUi(view, em, root, "Map_Test2");
        }
    }
}
#endif
