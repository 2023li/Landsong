#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
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
        IEnumerator BuildingCatalogUi(EcsGameView view, EntityManager em, Entity root)
        {
            var snapshot = SnapshotCodec.Capture(em, root); var bar = view.BuildingBar;
            try
            {
                Require(bar != null && !bar.gameObject.activeSelf, "Building catalogue starts closed");
                var trigger = view.GetComponentInParent<Canvas>().GetComponentsInChildren<Button>(true).First(b => b.GetComponentInChildren<TMP_Text>(true)?.text == "建筑");
                trigger.onClick.Invoke(); yield return WaitFor(() => bar.gameObject.activeSelf, "Building button expands bottom catalogue");
                var warehouse = Sim.FindDefinition(em, root, "b仓库"); Sim.Grant(em, root, warehouse);
                Sim.Grant(em, root, Sim.FindDefinition(em, root, "feature.Building"));
                yield return new WaitForSecondsRealtime(.4f);
                var blob = em.GetComponentData<ContentCatalog>(root).Value;
                var expected = 0; BuildingCategory union = BuildingCategory.None;
                for (var i = 0; i < blob.Value.Definitions.Length; i++) if (blob.Value.Definitions[i].Kind == ContentKind.Building && Sim.HasGrant(em, root, i)) { expected++; union |= blob.Value.Definitions[i].BuildingPolicy.Category; }
                Require(bar.Models.Count == expected && bar.VisibleCardCount == expected, "Only owned blueprints appear, including unaffordable cards");
                for (var bit = 0; bit < 10; bit++) { var category = (BuildingCategory)(1 << bit); Require(bar.HasCategory(category) == ((union & category) != 0), "Empty blueprint category hidden " + category); }
                var model = bar.Models.Single(m => m.Definition == warehouse);
                Require(model.Tooltip.Contains("放置成本") && model.Tooltip.Contains("每回合消耗") && model.Name == Sim.Definition(em, root, warehouse).Name.ToString(), "Tooltip contains canonical building name and both cost stages");
                var card = bar.CardButton(warehouse); ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(bar.Tooltip.gameObject.activeSelf && bar.TooltipText.text == model.Tooltip, "Pointer hover opens TMP tooltip");
                Canvas.ForceUpdateCanvases();
                bar.TooltipText.ForceMeshUpdate();
                Require(bar.TooltipText.textWrappingMode == TextWrappingModes.Normal && bar.TooltipText.textBounds.size.x <= bar.TooltipText.rectTransform.rect.width + 2, "Long Chinese building description wraps inside tooltip");
                Require(bar.CardScroll.viewport.rect.width > 300 && ((RectTransform)card.transform).rect.width >= 90, "Horizontal icon row retains usable layout width");
                yield return null;
                if (Application.isEditor) ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-catalog-ui.png");
                yield return new WaitForSecondsRealtime(.15f);
                ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                Require(!bar.Tooltip.gameObject.activeSelf, "Tooltip closes on pointer exit");
                var categoryToSelect = Enumerable.Range(0,10).Select(i => (BuildingCategory)(1 << i)).First(c => bar.HasCategory(c));
                bar.SelectCategory(categoryToSelect); yield return null;
                Require(bar.VisibleCardCount == bar.Models.Count(m => (m.Category & categoryToSelect) != 0), "Tab filters building icons without altering blueprint grants");
                bar.SelectCategory(BuildingCategory.None); yield return null;
                foreach (var cost in BuildingOps.CheckBuild(em, root, warehouse).Costs) InventoryOps.Remove(em, root, cost.Item, InventoryOps.Count(em, root, cost.Item));
                yield return new WaitForSecondsRealtime(.4f);
                card = bar.CardButton(warehouse); var blocked = BuildingOps.CheckBuild(em, root, warehouse);
                Require(!blocked.Allowed && !card.interactable && bar.Models.Single(m => m.Definition == warehouse).Tooltip.Contains(blocked.Reason), "Unaffordable daytime card remains visible with action reason");
                ExecuteEvents.Execute(card.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(bar.Tooltip.gameObject.activeSelf, "Disabled daytime building still accepts tooltip hover");
                var session = em.GetComponentData<Session>(root); session.Phase = Phase.Night; session.Paused = 1; em.SetComponentData(root, session);
                yield return new WaitForSecondsRealtime(.4f);
                Require(!bar.gameObject.activeSelf && !bar.Tooltip.gameObject.activeSelf, "Night hides building catalogue and tooltip without obstructing battle HUD");
                session.Phase = Phase.Day; em.SetComponentData(root, session);
                // Placement validates independently when clicking; provide the exact quoted cost to the owned fixture.
                foreach (var cost in BuildingOps.CheckBuild(em, root, warehouse).Costs) InventoryOps.Add(em, root, cost.Item, cost.Amount);
                yield return new WaitForSecondsRealtime(.4f);
                Require(BuildingOps.CheckBuild(em, root, warehouse).Allowed, "Owned warehouse fixture has enough placement materials");
                bar.CardButton(warehouse).onClick.Invoke(); Require(view.HasBuildingPlacement, "Building icon enters original map placement workflow"); view.CancelBuildingInteraction();
                trigger.onClick.Invoke(); Require(!bar.gameObject.activeSelf && !bar.Tooltip.gameObject.activeSelf, "Building button collapses tray and tooltip");
                foreach (var legacy in view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Text>(true)) Require(false, "Unexpected legacy Text: " + legacy.name);
                view.NameInput.text = "测试国库"; Require(view.NameInput.textComponent.text.Contains("测试国库"), "TMP rename input updates displayed Chinese text");
                Require(view.NameInput.textViewport != null, "TMP rename has caret viewport");
            }
            finally
            {
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, snapshot)); view.CancelBuildingInteraction();
                bar.gameObject.SetActive(false); view.OpenPanel("经济");
            }
        }
    }
}
#endif
