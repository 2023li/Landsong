#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Landsong.ECS.Definitions;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator BuildingCatalogUi(UI_GamePanel view, EntityManager em, Entity root)
        {
            var snapshot = SnapshotCodec.Capture(em, root);
            var bar = view.Buildings.BuildingBar;
            try
            {
                Require(bar != null && !bar.gameObject.activeSelf, "Building catalogue starts closed");
                FeatureOps.Unlock(em, root, FeatureDefinitions.Find(em, root, "feature.Building"));
                view.Refresh();
                var trigger = view.BuildingFeatureButton;
                Require(trigger.interactable, "Building fixture unlocks navigation before clicking");
                trigger.onClick.Invoke();
                yield return WaitFor(() => bar.gameObject.activeSelf, "Building button expands bottom catalogue");
                var warehouse = BuildingDefinitions.Find(em, root, "b仓库");
                BuildingBlueprints.Grant(em, root, warehouse, 1);
                view.Refresh(); // Direct fixture mutations bypass the production command-result invalidation event.
                yield return new WaitForSecondsRealtime(.4f);
                var blob = em.GetComponentData<BuildingCatalog>(root).Value;
                var expected = 0;
                BuildingCategory union = BuildingCategory.None;
                for (var i = 0; i < blob.Value.Definitions.Length; i++)
                    if (BuildingBlueprints.Has(em, root, BuildingId.FromIndex(i)))
                    {
                        expected++;
                        union |= blob.Value.Definitions[i].PlacementAndVisuals.Category;
                    }

                Require(bar.Models.Count == expected && bar.VisibleCardCount == expected, "Only owned blueprints appear, including unaffordable cards");
                for (var bit = 0; bit < 10; bit++)
                {
                    var category = (BuildingCategory)(1 << bit);
                    Require(bar.HasCategory(category) == ((union & category) != 0), "Empty blueprint category hidden " + category);
                }

                var model = bar.Models.Single(m => m.Definition == warehouse);
                Require(model.Tooltip.Contains("放置成本") && model.Tooltip.Contains("每回合消耗") && model.Name == BuildingDefinitions.Get(em, root, warehouse).Metadata.Name.ToString(), "Tooltip contains canonical building name and both cost stages");
                var card = bar.CardButton(warehouse);
                ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(bar.Tooltip.gameObject.activeSelf && bar.TooltipText.text == model.Tooltip, "Pointer hover opens TMP tooltip");
                Canvas.ForceUpdateCanvases();
                bar.TooltipText.ForceMeshUpdate();
                Require(bar.TooltipText.textWrappingMode == TextWrappingModes.Normal && bar.TooltipText.textBounds.size.x <= bar.TooltipText.rectTransform.rect.width + 2, "Long Chinese building description wraps inside tooltip");
                Require(bar.CardScroll.viewport.rect.width > 300 && ((RectTransform)card.transform).rect.width >= 90, "Horizontal icon row retains usable layout width");
                yield return null;
                if (Application.isEditor)
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-catalog-ui.png");
                yield return new WaitForSecondsRealtime(.15f);
                ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                Require(!bar.Tooltip.gameObject.activeSelf, "Tooltip closes on pointer exit");
                var categoryToSelect = Enumerable.Range(0, 10).Select(i => (BuildingCategory)(1 << i)).First(c => bar.HasCategory(c));
                bar.SelectCategory(categoryToSelect);
                yield return null;
                Require(bar.VisibleCardCount == bar.Models.Count(m => (m.Category & categoryToSelect) != 0), "Tab filters building icons without altering blueprint grants");
                bar.SelectCategory(BuildingCategory.None);
                yield return null;
                foreach (var cost in BuildingPlacementCommands.CheckBuild(em, root, warehouse).Costs)
                    InventoryOps.Remove(em, root, cost.Item, InventoryOps.Count(em, root, cost.Item));
                view.Refresh(); // Render the fixture; the following real button actions retain normal refresh behavior.
                yield return new WaitForSecondsRealtime(.4f);
                card = bar.CardButton(warehouse);
                var blocked = BuildingPlacementCommands.CheckBuild(em, root, warehouse);
                Require(!blocked.Allowed && !card.interactable && bar.Models.Single(m => m.Definition == warehouse).Tooltip.Contains(blocked.Reason), "Unaffordable daytime card remains visible with action reason");
                ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(bar.Tooltip.gameObject.activeSelf, "Disabled daytime building still accepts tooltip hover");
                var session = em.GetComponentData<Session>(root);
                SimulationControl sessionControl = em.GetComponentData<SimulationControl>(root);
                session.Phase = Phase.Night;
                sessionControl.Paused = 1;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionControl);
                }

                yield return new WaitForSecondsRealtime(.4f);
                Require(!bar.gameObject.activeSelf && !bar.Tooltip.gameObject.activeSelf, "Night hides building catalogue and tooltip without obstructing battle HUD");
                session.Phase = Phase.Day;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionControl);
                }

                // Placement validates independently when clicking; provide the exact quoted cost to the owned fixture.
                foreach (var cost in BuildingPlacementCommands.CheckBuild(em, root, warehouse).Costs)
                    InventoryOps.Add(em, root, cost.Item, cost.Amount);
                yield return new WaitForSecondsRealtime(.4f);
                Require(BuildingPlacementCommands.CheckBuild(em, root, warehouse).Allowed, "Owned warehouse fixture has enough placement materials");
                bar.CardButton(warehouse).onClick.Invoke();
                Require(view.WorldInteraction.HasBuildingPlacement, "Building icon enters original map placement workflow");
                Require(view.Buildings.BuildingPlacementPanel.activeSelf && !string.IsNullOrWhiteSpace(view.Buildings.BuildingHint.text), "Placement begins with a visible contextual hint");
                view.Buildings.CancelBuildingInteraction();
                Require(!view.Buildings.BuildingPlacementPanel.activeSelf && string.IsNullOrEmpty(view.Buildings.BuildingHint.text), "Cancel placement removes both hint and stale text");
                trigger.onClick.Invoke();
                Require(!bar.gameObject.activeSelf && !bar.Tooltip.gameObject.activeSelf, "Building button collapses tray and tooltip");
                foreach (var legacy in view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Text>(true))
                    Require(false, "Unexpected legacy Text: " + legacy.name);
                view.BuildingDetails.Name.text = "测试国库";
                Require(view.BuildingDetails.Name.textComponent.text.Contains("测试国库"), "TMP rename input updates displayed Chinese text");
                Require(view.BuildingDetails.Name.textViewport != null, "TMP rename has caret viewport");
            }
            finally
            {
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot));
                view.Buildings.CancelBuildingInteraction();
                bar.gameObject.SetActive(false);
                view.OpenPanel(GamePanelId.Economy);
            }
        }
    }
}
#endif
