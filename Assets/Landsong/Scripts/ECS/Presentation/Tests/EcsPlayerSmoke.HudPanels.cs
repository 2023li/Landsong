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
        IEnumerator HudPanelsUi(EcsGameView view, EntityManager em, Entity root)
        {
            var original = SnapshotCodec.Capture(em, root);
            var canvas = view.GetComponentInParent<Canvas>();
            var primary = view.PrimaryRows.parent.parent.gameObject;
            var secondary = view.SecondaryRows.parent.parent.gameObject;
            try
            {
                yield return new WaitForSecondsRealtime(.4f);
                Require(!view.IsPanelOpen && !primary.activeSelf && !secondary.activeSelf, "Entering Game leaves function scroll panels closed");
                Require(!view.TechnologyButton.gameObject.activeSelf, "Research HUD respects the technology license");
                foreach (var permission in new[] { "feature.Inventory", "feature.Building", "feature.Expedition", ResearchOps.FeatureId })
                    Sim.Grant(em, root, Sim.FindDefinition(em, root, new FixedString128Bytes(permission)));
                yield return WaitFor(() => view.TechnologyButton.gameObject.activeSelf, "Research HUD appears after license");
                Require(view.CurrentResearchDefinition == -1 && view.ResearchHudName.text == "尚未选择科技", "Empty research queue has a clear selectable HUD state");
                Require(view.TechnologyButton.transform.parent == canvas.transform, "Research card replaces the toolbar technology entry");
                foreach (var panel in new[] { "经济", "库存", "驻军", "远征", "人才", "王室", "政策", "历史", "战报" })
                {
                    view.OpenPanel(panel);
                    yield return new WaitForSecondsRealtime(.3f);
                    Require(view.IsPanelOpen && (panel=="王室"?view.RoyalDetails!=null&&view.RoyalDetails.gameObject.activeInHierarchy:primary.activeSelf) && view.PanelCloseButton.interactable, panel + " opens a dismissible function panel");
                    if (panel == "驻军") Require(secondary.activeSelf, "Military opens both coordinated scroll views");
                    var scroll = view.PrimaryRows.GetComponentInParent<ScrollRect>(true);
                    scroll.verticalNormalizedPosition = 0;
                    Require(!view.PanelCloseButton.transform.IsChildOf(scroll.content), "Close button remains outside scrolling content: " + panel);
                    if (panel == "库存" && Application.isEditor)
                    {
                        ScreenCapture.CaptureScreenshot("Library/LandsongEcs/function-panel-inventory.png");
                        yield return null;
                    }
                    view.PanelCloseButton.onClick.Invoke();
                    yield return new WaitForSecondsRealtime(.4f);
                    Require(!view.IsPanelOpen && !primary.activeSelf && !secondary.activeSelf, panel + " stays closed across periodic refresh");
                    Require(!canvas.GetComponentsInChildren<CourtPresentationView>().Any(), "Closing also hides associated family or policy graph");
                }
                view.OpenPanel("任务");
                yield return WaitFor(() => view.QuestWindow != null && view.QuestWindow.activeSelf, "Quest window opens");
                view.QuestCloseButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.QuestWindow.activeSelf && !primary.activeSelf && !view.IsPanelOpen, "Quest header closes to unobstructed gameplay");
                view.ToggleBuildingCatalog();
                yield return new WaitForSecondsRealtime(.3f);
                Require(view.BuildingBar.gameObject.activeSelf, "Building function opens its bottom catalog");
                view.BuildingBar.CloseButton.onClick.Invoke();
                Require(!view.IsPanelOpen && !view.BuildingBar.gameObject.activeSelf && !primary.activeSelf, "Building close does not reopen the default scroll panel");
                view.OpenPanel("情报");
                yield return WaitFor(() => em.GetComponentData<Session>(root).IntelligenceMode != 0, "Intelligence mode entered through command");
                view.PanelCloseButton.onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<Session>(root).IntelligenceMode == 0, "Closing intelligence exits authoritative read-only map mode");
                Require(!primary.activeSelf && !view.IsPanelOpen, "Intelligence close restores unobstructed gameplay");

                int first = Sim.FindDefinition(em, root, new FixedString128Bytes("TN_3_1_启蒙"));
                int wood = Sim.FindDefinition(em, root, new FixedString128Bytes("TN_4_2_木工术"));
                Require(ResearchOps.Plan(em, root, wood) == ResultCode.Success, "HUD fixture creates an actual prerequisite research plan");
                var session = em.GetComponentData<Session>(root); session.ResearchPoints = 2; em.SetComponentData(root, session);
                ResearchOps.Settle(em, root);
                yield return WaitFor(() => view.CurrentResearchDefinition == first && Mathf.Approximately(view.ResearchHudProgress.rectTransform.anchorMax.x, .4f), "HUD reads the current technology and actual 2/5 progress");
                Require(view.ResearchHudName.text == "启蒙" && !string.IsNullOrWhiteSpace(view.ResearchHudEffects.text), "HUD shows technology name and effects or description");
                var beforeOpen = SnapshotCodec.Capture(em, root);
                view.TechnologyButton.onClick.Invoke();
                yield return WaitFor(() => view.TechnologyTree != null && view.TechnologyTree.gameObject.activeSelf, "Clicking research HUD opens the technology panel");
                Require(view.TechnologyTree.Selected == first && !primary.activeSelf, "HUD opens and focuses current research without the primary scroll view");
                view.TechnologyTree.CloseButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.IsPanelOpen && !primary.activeSelf && !view.TechnologyTree.gameObject.activeSelf, "Technology close does not reopen a default panel");
                Require(beforeOpen.SequenceEqual(SnapshotCodec.Capture(em, root)), "Opening and closing research changes no research points or gameplay state");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/research-hud.png");
                    yield return null;
                }
                ResearchOps.Command(em, root, first, true);
                yield return WaitFor(() => view.CurrentResearchDefinition == wood, "Cancelling the first entry advances HUD to blocked successor");
                Require(view.ResearchHudEffects.text.Contains("木材加工厂"), "HUD lists the technology's actual building unlock effects");
                Require(ResearchOps.Plan(em, root, wood) == ResultCode.Success, "Replanning retains previously invested research");
                session = em.GetComponentData<Session>(root); session.ResearchPoints = 3; em.SetComponentData(root, session); ResearchOps.Settle(em, root);
                yield return WaitFor(() => view.CurrentResearchDefinition == wood && view.ResearchHudName.text == "木工术", "Research completion advances HUD to the next technology");
                session = em.GetComponentData<Session>(root); session.ResearchPoints = 8; em.SetComponentData(root, session); ResearchOps.Settle(em, root);
                yield return WaitFor(() => view.CurrentResearchDefinition == -1, "Finishing the queue restores empty research HUD");
            }
            finally
            {
                if (view.BuildingConfirmPanel != null) view.BuildingConfirmPanel.SetActive(false);
                view.ClosePanel();
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            }
            yield return WaitFor(() => !view.TechnologyButton.gameObject.activeSelf, "Restoring an earlier save refreshes HUD licensing");
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
