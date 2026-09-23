#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Landsong.ECS.Definitions;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        static IEnumerator BuildingDetailsUi(UI_GamePanel view, EntityManager em, Entity root)
        {
            var original = SnapshotCodec.Capture(em, root);
            var originalCameraPosition = view.WorldInteraction.Camera.transform.position;
            try
            {
                BuildingId Def(string name) => BuildingDefinitions.Find(em, root, new FixedString128Bytes(name));
                var session = em.GetComponentData<Session>(root);
                GameClock sessionClock = em.GetComponentData<GameClock>(root);
                SimulationControl sessionControl = em.GetComponentData<SimulationControl>(root);
                PopulationState sessionPopulation = em.GetComponentData<PopulationState>(root);
                PersistenceGate sessionPersistence = em.GetComponentData<PersistenceGate>(root);
                session.Phase = Phase.Day;
                sessionControl.Paused = 0;
                sessionPersistence.CheckpointPending = 0;
                sessionPopulation.BasePopulation = 100;
                {
                    em.SetComponentData(root, session);
                    em.SetComponentData(root, sessionClock);
                    em.SetComponentData(root, sessionControl);
                    em.SetComponentData(root, sessionPopulation);
                    em.SetComponentData(root, sessionPersistence);
                }

                var definition = Def("b农田");
                var grid = em.GetComponentData<GridData>(root);
                Entity farm = Entity.Null;
                for (int i = 0; i < grid.Value.Value.Cells.Length; i++)
                {
                    var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                    if (GridOps.CanPlace(em, root, definition, cell, 0))
                    {
                        farm = BuildingCreation.Create(em, root, definition, cell, 0, 1, true);
                        break;
                    }
                }

                Require(farm != Entity.Null, "Building detail fixture has real farm");
                ulong key = em.GetComponentData<Identity>(farm).Id;
                var state = em.GetComponentData<Building>(farm);
                BuildingWorkforceState stateWorkforce = em.GetComponentData<BuildingWorkforceState>(farm);
                BuildingFarmingState stateFarming = em.GetComponentData<BuildingFarmingState>(farm);
                BuildingExperienceState stateExperience = em.GetComponentData<BuildingExperienceState>(farm);
                BuildingMaintenanceState stateMaintenance = em.GetComponentData<BuildingMaintenanceState>(farm);
                stateWorkforce.SubsidyBudget = stateWorkforce.PaidSubsidy = 0;
                stateWorkforce.Workers = 0;
                {
                    em.SetComponentData(farm, state);
                    em.SetComponentData(farm, stateWorkforce);
                    em.SetComponentData(farm, stateFarming);
                    em.SetComponentData(farm, stateExperience);
                    em.SetComponentData(farm, stateMaintenance);
                }

                view.OpenPanel(GamePanelId.Building);
                view.Buildings.SelectBuilding(key);
                view.Buildings.FocusBuilding(key); // The first buildable fixture cell can be outside the initial camera view.
                yield return WaitFor(() => view.Buildings.BuildingActionBar.gameObject.activeSelf && !view.BuildingDetails.gameObject.activeSelf, "Selecting building opens only its world-anchored action bar");
                view.Buildings.BuildingDetailsButton.onClick.Invoke();
                yield return WaitFor(() => view.BuildingDetails.gameObject.activeSelf && view.BuildingDetails.BuildingId == key, "Action bar details button opens redesigned detail card");
                var card = view.BuildingDetails;
                var baseOutput = card.Block<UI_GamePanel_BuildingDetails_Block_基础产出>();
                var workforce = card.Block<UI_GamePanel_BuildingDetails_Block_岗位>();
                var planting = card.Block<UI_GamePanel_BuildingDetails_Block_种植>();
                Require(view.Buildings.BuildingRangesVisible && card.Footer.text.Contains("移动力"), "Selection defaults to range overlays and fixed coordinates/action power: " + view.Buildings.BuildingRangesVisible + " / " + card.Footer.text);
                Require(view.Buildings.transform.parent == view.BuildingRoot && card.transform.parent == view.FeatureRoot, "Building action bar uses the world overlay layer while details belongs directly to the feature layer");
                Require(card.Name.transform.IsChildOf(card.transform) && !view.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b => b.name == "Rename"), "Editable building name is inside detail header without old rename toolbar");
                card.Name.text = "春耕园";
                card.Name.onEndEdit.Invoke(card.Name.text);
                yield return WaitFor(() => em.GetComponentData<Identity>(farm).Name.ToString() == "春耕园", "Name field commits player rename through ECS");
                ExecuteEvents.Execute(card.Warning.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(card.Tooltip.activeSelf && card.TooltipText.text.Contains("没有工人"), "Warning hover explains actual building abnormalities");
                ExecuteEvents.Execute(card.Warning.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                Require(!card.Tooltip.activeSelf, "Warning leaves without lingering overlay");
                var gold = WorkforceOps.Quote(em, root, farm).Gold;
                int stock = InventoryOps.Count(em, root, gold);
                workforce.Increase.onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<BuildingWorkforceState>(farm).SubsidyBudget == 1, "Left arrow increases subsidy budget by one");
                Require(InventoryOps.Count(em, root, gold) == stock && em.GetComponentData<BuildingWorkforceState>(farm).PaidSubsidy == 0, "Budget setting neither pays immediately nor adds effective attraction");
                yield return new WaitForSecondsRealtime(.3f);
                var q = WorkforceOps.Quote(em, root, farm);
                Require(Mathf.Approximately(workforce.SubsidyFill.rectTransform.anchorMax.x, q.Planned / 100) && workforce.SubsidyFill.rectTransform.anchorMax.x > workforce.SubsidyFill.rectTransform.anchorMin.x && workforce.SubsidyFill.color.g > .75f, "Yellow subsidy preview immediately reflects the configured unpaid budget");
                Require(!baseOutput.gameObject.activeSelf, "Farm without base output removes the entire output block from layout");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/subsidy-preview.png");
                    yield return null;
                }

                {
                    state = em.GetComponentData<Building>(farm);
                    stateWorkforce = em.GetComponentData<BuildingWorkforceState>(farm);
                    stateFarming = em.GetComponentData<BuildingFarmingState>(farm);
                    stateExperience = em.GetComponentData<BuildingExperienceState>(farm);
                    stateMaintenance = em.GetComponentData<BuildingMaintenanceState>(farm);
                }

                stateWorkforce.PaidSubsidy = 1;
                stateWorkforce.PaidSubsidyTurn = sessionClock.Turn;
                stateWorkforce.Workers = 1;
                {
                    em.SetComponentData(farm, state);
                    em.SetComponentData(farm, stateWorkforce);
                    em.SetComponentData(farm, stateFarming);
                    em.SetComponentData(farm, stateExperience);
                    em.SetComponentData(farm, stateMaintenance);
                }

                view.Refresh(); // Direct fixture projection; production budget commands below still refresh through events.
                yield return new WaitForSecondsRealtime(.3f);
                Require(workforce.SubsidyFill.rectTransform.anchorMax.x > workforce.SubsidyFill.rectTransform.anchorMin.x && workforce.JobTicks.Count(t => t.gameObject.activeSelf) == Mathf.Min(10, q.Capacity), "Paid attraction and at most ten proportional job markers rendered");
                workforce.Decrease.onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<BuildingWorkforceState>(farm).SubsidyBudget == 0, "Right arrow reduces future subsidy without erasing paid benefit");
                yield return new WaitForSecondsRealtime(.3f);
                Require(Mathf.Approximately(workforce.SubsidyFill.rectTransform.anchorMin.x, workforce.SubsidyFill.rectTransform.anchorMax.x) && em.GetComponentData<BuildingWorkforceState>(farm).PaidSubsidy == 1, "Removing future budget clears the yellow preview without erasing the paid benefit");
                ExecuteEvents.Execute(workforce.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                var attraction = WorkforceOps.Quote(em, root, farm);
                Require(card.Sidebar.activeSelf && card.SidebarText.text == $"基础吸引力：{attraction.Natural:0.#}\n\n补贴吸引力：{attraction.Planned - attraction.Natural:0.#}", "Workforce hover shows base and subsidy attraction in the details sidebar");
                ExecuteEvents.Execute(workforce.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                card.Upgrade.onClick.Invoke();
                Require(!view.Buildings.BuildingConfirmPanel.activeSelf && !string.IsNullOrEmpty(view.Hud.Message.text), "Gray upgrade remains clickable and reports missing requirements");
                {
                    state = em.GetComponentData<Building>(farm);
                    stateWorkforce = em.GetComponentData<BuildingWorkforceState>(farm);
                    stateFarming = em.GetComponentData<BuildingFarmingState>(farm);
                    stateExperience = em.GetComponentData<BuildingExperienceState>(farm);
                    stateMaintenance = em.GetComponentData<BuildingMaintenanceState>(farm);
                }

                stateExperience.Experience = 100000;
                stateWorkforce.Workers = em.GetComponentData<BuildingWorkforceStats>(farm).Capacity;
                stateMaintenance.Maintained = 1;
                {
                    em.SetComponentData(farm, state);
                    em.SetComponentData(farm, stateWorkforce);
                    em.SetComponentData(farm, stateFarming);
                    em.SetComponentData(farm, stateExperience);
                    em.SetComponentData(farm, stateMaintenance);
                }

                int maximumLevel = BuildingDefinitions.Get(em, root, definition).MaximumLevel;
                BuildingBlueprints.Grant(em, root, definition, maximumLevel);
                foreach (var cost in BuildingUpgradeCommands.Check(em, root, farm).Costs)
                    InventoryOps.Add(em, root, cost.Item, cost.Amount);
                view.Refresh(); // XP, blueprint and inventory were populated directly by this fixture.
                yield return new WaitForSecondsRealtime(.3f);
                if (maximumLevel > 1)
                {
                    Require(BuildingUpgradeCommands.Check(em, root, farm).Allowed, "Upgrade fixture satisfies actual requirements");
                    card.Upgrade.onClick.Invoke();
                    Require(view.Buildings.BuildingConfirmPanel.activeSelf && view.Buildings.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("费用")), "Upgrade opens material confirmation");
                    view.Buildings.CancelBuildingInteraction();
                }

                if (card.Style.interactable)
                {
                    card.Style.onClick.Invoke();
                    Require(view.Buildings.BuildingConfirmPanel.activeSelf && view.Buildings.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("选择建筑皮肤")), "Style opens separate skin choices");
                    view.Buildings.CancelBuildingInteraction();
                }

                var crop = BuildingDefinitions.Get(em, root, definition).Capabilities.Farming.Crops[0].Crop;
                Require(crop.IsValid, "Farm fixture exposes a crop");
                for (int i = 0; i < CropDefinitions.Get(em, root, crop).PlantingCosts.Length; i++)
                {
                    var cost = CropDefinitions.Get(em, root, crop).PlantingCosts[i];
                    InventoryOps.Add(em, root, cost.Item, cost.Quantity);
                }

                planting.Select.onClick.Invoke();
                Require(view.Buildings.BuildingConfirmPanel.activeSelf, "Circular crop button opens crop choices");
                var cropRows = view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>();
                Require(cropRows.Any(row => row.Identity.StartsWith("domain:building-crop:")), "Crop choices use stable domain identities");
                for (int repeat = 0; repeat < 3; repeat++)
                {
                    // Reopen both while visible and after closing; neither path may append rows.
                    planting.Select.onClick.Invoke();
                    Require(cropRows.SequenceEqual(view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>()), "Repeated crop click reuses exactly the same rows");
                    ClickIn(view.Buildings.BuildingConfirmRows, "关闭");
                    planting.Select.onClick.Invoke();
                    Require(cropRows.SequenceEqual(view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>()), "Closed crop choices reopen without duplicate buttons");
                }

                if (card.Style.interactable)
                {
                    card.Style.onClick.Invoke();
                    var skinRows = view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>();
                    Require(skinRows.All(row => !row.Identity.StartsWith("domain:building-crop:")), "Skin choices retire crop rows");
                    card.Style.onClick.Invoke();
                    Require(skinRows.SequenceEqual(view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>()), "Repeated skin click reuses exactly the same rows");
                }

                view.Buildings.ShowBuildingConfirmation("回归确认", Array.Empty<string>(), () =>
                {
                });
                Require(view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>().Length == 3, "Ordinary confirmation retires all crop and skin choices");
                view.Buildings.CancelBuildingInteraction();
                planting.Select.onClick.Invoke();
                Require(view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UI_GamePanel_Row>().Length == cropRows.Length, "Switching back to crops restores one complete choice list");
                string cropName = CropDefinitions.Get(em, root, crop).Metadata.Name.ToString();
                view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UnityEngine.UI.Button>().First(button => button.interactable && button.GetComponentInChildren<TMP_Text>().text.StartsWith(cropName + " · ")).onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<BuildingFarmingState>(farm).Crop == crop, "Crop choice dispatches planting command");
                yield return new WaitForSecondsRealtime(.3f);
                Require(planting.Label.text.Contains(cropName) && planting.Icon.transform.IsChildOf(planting.Select.transform) && planting.Icon.sprite != null, "Chosen crop is shown on circle child icon and maturity label");
                var beforeHover = SnapshotCodec.Capture(em, root);
                ExecuteEvents.Execute(planting.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(card.Sidebar.activeSelf && card.SidebarText.text.Contains("1工人") && card.SidebarText.text.Contains("2工人") && card.SidebarText.text.Contains("3工人") && card.SidebarText.text.Contains("作物正常生长") && card.SidebarText.text.Contains("作物产量 +50%") && card.SidebarText.text.Contains(cropName) && card.SidebarText.text.Contains("全生长期") && card.SidebarText.text.Contains("生长暂停"), "Planting hover displays farm worker tiers, growth threshold and whole-cycle bonus");
                Require(beforeHover.SequenceEqual(SnapshotCodec.Capture(em, root)), "Worker tooltip does not mutate simulation or consume crop RNG");
                ExecuteEvents.Execute(planting.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                ExecuteEvents.Execute(card.Sidebar, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.25f);
                Require(card.Sidebar.activeSelf, "Building sidebar remains open when pointer enters it for scrolling");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/worker-tiers.png");
                    yield return null;
                }

                ExecuteEvents.Execute(card.Sidebar, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.25f);
                Require(!card.Sidebar.activeSelf, "Building sidebar closes after leaving module and sidebar");
                {
                    state = em.GetComponentData<Building>(farm);
                    stateWorkforce = em.GetComponentData<BuildingWorkforceState>(farm);
                    stateFarming = em.GetComponentData<BuildingFarmingState>(farm);
                    stateExperience = em.GetComponentData<BuildingExperienceState>(farm);
                    stateMaintenance = em.GetComponentData<BuildingMaintenanceState>(farm);
                }

                stateFarming.Progress = 1;
                {
                    em.SetComponentData(farm, state);
                    em.SetComponentData(farm, stateWorkforce);
                    em.SetComponentData(farm, stateFarming);
                    em.SetComponentData(farm, stateExperience);
                    em.SetComponentData(farm, stateMaintenance);
                }

                view.Refresh();
                yield return new WaitForSecondsRealtime(.3f);
                Require(planting.Fill.rectTransform.anchorMax.x > 0, "Crop bar reads authoritative maturity progress");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-details.png");
                    yield return null;
                }

                planting.Clear.onClick.Invoke();
                Require(view.Buildings.BuildingConfirmPanel.activeSelf, "Crop X requires removal confirmation");
                ClickIn(view.Buildings.BuildingConfirmRows, "确认");
                yield return WaitFor(() => !em.GetComponentData<BuildingFarmingState>(farm).Crop.IsValid, "Confirmed crop removal clears actual crop");
                {
                    state = em.GetComponentData<Building>(farm);
                    stateWorkforce = em.GetComponentData<BuildingWorkforceState>(farm);
                    stateFarming = em.GetComponentData<BuildingFarmingState>(farm);
                    stateExperience = em.GetComponentData<BuildingExperienceState>(farm);
                    stateMaintenance = em.GetComponentData<BuildingMaintenanceState>(farm);
                }

                state.Level = maximumLevel;
                stateExperience.Experience = 100000;
                {
                    em.SetComponentData(farm, state);
                    em.SetComponentData(farm, stateWorkforce);
                    em.SetComponentData(farm, stateFarming);
                    em.SetComponentData(farm, stateExperience);
                    em.SetComponentData(farm, stateMaintenance);
                }

                BuildingLevelConfiguration.Apply(em, root, farm, false);
                view.Refresh();
                yield return new WaitForSecondsRealtime(.3f);
                Require(card.Experience.text == "MAX" && !card.Upgrade.gameObject.activeSelf, "Full XP at final level displays MAX without upgrade action");
                var warehouseDef = Def("b仓库");
                Entity warehouse = Entity.Null;
                for (int i = 0; i < grid.Value.Value.Cells.Length; i++)
                {
                    var cell = grid.Value.Value.Min + new int2(i % grid.Value.Value.Size.x, i / grid.Value.Value.Size.x);
                    if (GridOps.CanPlace(em, root, warehouseDef, cell, 0))
                    {
                        warehouse = BuildingCreation.Create(em, root, warehouseDef, cell, 0, 1, true);
                        break;
                    }
                }

                Require(warehouse != Entity.Null, "Upgrade fixture creates multilevel warehouse");
                ulong warehouseId = em.GetComponentData<Identity>(warehouse).Id;
                BuildingWorkforceState wbWorkforce = em.GetComponentData<BuildingWorkforceState>(warehouse);
                BuildingExperienceState wbExperience = em.GetComponentData<BuildingExperienceState>(warehouse);
                BuildingMaintenanceState wbMaintenance = em.GetComponentData<BuildingMaintenanceState>(warehouse);
                wbExperience.Experience = 100000;
                wbWorkforce.Workers = em.GetComponentData<BuildingWorkforceStats>(warehouse).Capacity;
                wbMaintenance.Maintained = 1;
                {
                    em.SetComponentData(warehouse, wbWorkforce);
                    em.SetComponentData(warehouse, wbExperience);
                    em.SetComponentData(warehouse, wbMaintenance);
                }

                var grants = em.GetBuffer<BlueprintUnlock>(root);
                for (int i = grants.Length - 1; i >= 0; i--)
                    if (grants[i].Building == warehouseDef)
                        grants.RemoveAt(i);
                BuildingBlueprints.Grant(em, root, warehouseDef, 1);
                view.Buildings.SelectBuilding(warehouseId);
                view.Buildings.BuildingDetailsButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                card.Upgrade.onClick.Invoke();
                Require(!view.Buildings.BuildingConfirmPanel.activeSelf && view.Hud.Message.text.Contains("下一等级蓝图"), "Full XP without higher blueprint stays gray and explains missing blueprint");
                Require(baseOutput.gameObject.activeSelf, "Selecting warehouse restores populated base output block");
                ExecuteEvents.Execute(baseOutput.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                Require(card.Sidebar.activeSelf && card.SidebarText.text.Contains("库存槽") && !card.SidebarText.text.Contains(cropName), "Warehouse hover shows its own worker effects without stale crop data");
                card.HideSidebar();
                BuildingBlueprints.Grant(em, root, warehouseDef, 2);
                foreach (var cost in BuildingUpgradeCommands.Check(em, root, warehouse).Costs)
                    InventoryOps.Add(em, root, cost.Item, cost.Amount);
                view.Refresh(); // Show the newly authored fixture license and material availability.
                Require(BuildingUpgradeCommands.Check(em, root, warehouse).Allowed, "Warehouse upgrade fixture meets authoritative requirements");
                yield return new WaitForSecondsRealtime(.3f);
                card.Upgrade.onClick.Invoke();
                Require(view.Buildings.BuildingConfirmPanel.activeSelf && view.Buildings.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("费用")), "Usable upgrade opens quoted material confirmation");
                var beforeUpgrade = SnapshotCodec.Capture(em, root);
                view.Buildings.CancelBuildingInteraction();
                Require(beforeUpgrade.SequenceEqual(SnapshotCodec.Capture(em, root)), "Cancel upgrade does not spend materials or XP");
                card.Upgrade.onClick.Invoke();
                ClickIn(view.Buildings.BuildingConfirmRows, "确认");
                yield return WaitFor(() => em.GetComponentData<Building>(warehouse).Level == 2, "Upgrade confirmation changes real building level");
                using (var reach = BuildingRangeOps.Reach(em, root, farm, Allocator.Temp))
                {
                    var provider = ResourceNetworkOps.Provider(em, root, farm);
                    if (provider != Entity.Null)
                    {
                        var path = BuildingRangeOps.ProviderPath(em, root, provider, reach);
                        Require(path.Count > 0 && path.Skip(1).Select((cell, i) => math.csum(math.abs(cell - path[i]))).All(gap => gap == 1), "Resource overlay uses contiguous path to actual selected provider");
                    }
                }

                ExecuteEvents.Execute(workforce.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.Quests.QuestTracking.gameObject.activeSelf, "Building details suppress quest tracking before local close");
                card.Close.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.BuildingDetails.gameObject.activeSelf && !card.Sidebar.activeSelf, "Building detail X remains closed across refresh and clears its sidebar");
                Require(view.Quests.QuestTracking.gameObject.activeInHierarchy, "Closing building details restores quest HUD during idle day without another command");
                view.Buildings.SelectBuilding(warehouseId);
                view.Buildings.BuildingDetailsButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);
                Require(view.BuildingDetails.gameObject.activeSelf && !view.Quests.QuestTracking.gameObject.activeSelf, "Reopened building details hide quest tracking again");
                for (int i = 0; i < 4 && view.Buildings.CancelBuildingInteraction(); i++)
                {
                }

                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.BuildingDetails.gameObject.activeSelf && view.Quests.QuestTracking.gameObject.activeInHierarchy, "Cancelling building details restores quest HUD without relying on periodic idle rebuilds");
            }
            finally
            {
                view.Buildings.BuildingConfirmPanel.SetActive(false);
                view.BuildingDetails.Close.onClick.Invoke();
                view.ClosePanel();
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                view.WorldInteraction.Camera.transform.position = originalCameraPosition;
            }
        }
    }
}
#endif
