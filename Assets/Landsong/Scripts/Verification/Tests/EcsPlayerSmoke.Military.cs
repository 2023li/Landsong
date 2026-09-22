#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Landsong.ECS.Definitions;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator SoldierUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            PopulationState statePopulation = em.GetComponentData<PopulationState>(root);
            NightRuntimeState stateNight = em.GetComponentData<NightRuntimeState>(root);
            statePopulation.BasePopulation += 100;
            {
                em.SetComponentData(root, statePopulation);
                em.SetComponentData(root, stateNight);
            }

            var definition = BuildingDefinitions.Find(em, root, "b驻军营地");
            var palaceDefinition = BuildingDefinitions.Find(em, root, "b王宫");
            var grid = em.GetComponentData<GridData>(root);
            Entity palace = Entity.Null;
            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var building in buildings)
                    if (em.GetComponentData<BuildingDefinitionRef>(building).Definition == palaceDefinition)
                    {
                        palace = building;
                        break;
                    }
            Require(palace != Entity.Null && BuildingRangeOps.ActionPower(em, root, palace) == 20 && BuildingDefinitions.Get(em, root, palaceDefinition).PlacementAndVisuals.SpawnExclusionPadding == 30, "Runtime palace catalog contains saved action power and patrol movement budget");
            var palaceId = em.GetComponentData<Identity>(palace).Id;
            Entity home = Entity.Null;
            for (int y = 15; y < grid.Value.Value.Size.y - 15 && home == Entity.Null; y++)
                for (int x = 15; x < grid.Value.Value.Size.x - 15; x++)
                    if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0))
                    {
                        home = BuildingCreation.Create(em, root, definition, new int2(x, y), 0, 1, true);
                        break;
                    }

            Require(home != Entity.Null, "Military UI fixture has legal garrison");
            ulong homeId = em.GetComponentData<Identity>(home).Id;
            InventoryOps.Add(em, root, em.GetComponentData<CurrencySettings>(root).Gold, 200);
            view.OpenPanel(GamePanelId.Garrison);
            bool Prefix(RectTransform rows, string prefix) => rows.GetComponentsInChildren<Button>().Any(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true);
            void ClickPrefix(RectTransform rows, string prefix)
            {
                var button = rows.GetComponentsInChildren<Button>().First(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true);
                button.onClick.Invoke();
            }

            Button HomeButton(string prefix)
            {
                bool current = false;
                foreach (var button in view.PrimaryRows.GetComponentsInChildren<Button>())
                {
                    var label = button.GetComponentInChildren<Text>()?.text ?? "";
                    if (label.StartsWith("【"))
                        current = label.Contains("#" + homeId + "】");
                    if (current && button.interactable && label.StartsWith(prefix, StringComparison.Ordinal))
                        return button;
                }

                return null;
            }

            yield return WaitFor(() => Prefix(view.PrimaryRows, "招募数量 1"), "Military management panel visible");
            Require(view.PrimaryRows.GetComponentInParent<ScrollRect>() != view.SecondaryRows.GetComponentInParent<ScrollRect>(), "Garrison and pending lists are independent scroll views");
            ClickPrefix(view.PrimaryRows, "招募数量 1");
            yield return WaitFor(() => Prefix(view.PrimaryRows, "招募数量 2"), "Recruit quantity changes in native UI");
            HomeButton("募兵 ").onClick.Invoke();
            Require(view.Buildings.BuildingConfirmPanel.activeSelf, "Recruitment confirmation shows complete costs");
            ClickIn(view.Buildings.BuildingConfirmRows, "确认");
            yield return WaitFor(() => SoldierOps.UnassignedCount(em) == 2, "Confirmed UI recruits complete quantity into pending pool");
            ulong[] pending;
            using (var all = WorldQueries.OrderedEntities<Soldier>(em))
                pending = all.ToArray().Where(e => em.GetComponentData<Soldier>(e).Garrison == 0).Select(e => em.GetComponentData<Identity>(e).Id).ToArray();
            for (int i = 0; i < pending.Length; i++)
            {
                ulong id = pending[i];
                int slot = i + 1;
                Button PendingButton() => view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(c => c.PersonId == id)?.Select;
                yield return WaitFor(() => PendingButton() != null, "Pending recruit appears by stable identity");
                PendingButton().onClick.Invoke();
                yield return WaitFor(() => HomeButton("槽 " + slot + " · 空位") != null, "Selected recruit enables specific camp slot");
                HomeButton("槽 " + slot + " · 空位").onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<Soldier>(WorldQueries.Find(em, id)).Garrison == homeId, "Recruit assigned to selected camp");
            }

            var first = GarrisonOps.AtSlot(em, homeId, 1);
            ulong firstId = em.GetComponentData<Identity>(first).Id;
            UI_GamePanel_SoldierItem FirstCard() => view.PrimaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(c => c.PersonId == firstId);
            yield return WaitFor(() => FirstCard() != null, "Real first slot displayed");
            FirstCard().Details.onClick.Invoke();
            Require(view.soldierController.SoldierDetailsOpen, "Occupied camp slot opens soldier details");
            view.soldierController.SoldierDetailsName.onEndEdit.Invoke("军团验收先锋");
            yield return WaitFor(() => em.GetComponentData<Identity>(first).Name.ToString() == "军团验收先锋", "Native name input submits soldier rename");
            view.soldierController.CloseSoldierDetails();
            FirstCard().Remove.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).Garrison == 0 && view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().Any(c => c.PersonId == firstId), "Unassigned soldier appears only in pending list");
            HomeButton("一键填充空槽").onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).Garrison == homeId, "One-click fill restores vacant slot");
            yield return WaitFor(() => FirstCard() != null && FirstCard().Dismiss.interactable, "Assigned card exposes immediate dismissal");
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldiers-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            var captured = SnapshotCodec.Capture(em, root);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, captured));
            first = WorldQueries.Find(em, firstId);
            Require(em.GetComponentData<Identity>(first).Name.ToString() == "军团验收先锋" && em.GetComponentData<Soldier>(first).Slot == 1, "Real UI name and slot survive reconstruction");
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Deployment, "Military fixture enters night preparation");
            {
                statePopulation = em.GetComponentData<PopulationState>(root);
                stateNight = em.GetComponentData<NightRuntimeState>(root);
            }

            stateNight.Duration = 120;
            {
                em.SetComponentData(root, statePopulation);
                em.SetComponentData(root, stateNight);
            }

            first = WorldQueries.Find(em, firstId);
            yield return WaitFor(() => em.GetComponentData<Combatant>(first).Deployed != 0, "Soldier automatically deploys during preparation");
            Entity PalaceSoldier()
            {
                using var soldiers = WorldQueries.OrderedEntities<Soldier>(em);
                foreach (var unit in soldiers)
                    if (em.GetComponentData<Soldier>(unit).Garrison == palaceId && EntityState.Alive(em, unit))
                        return unit;
                return Entity.Null;
            }
            yield return WaitFor(() => PalaceSoldier() != Entity.Null && em.GetComponentData<Combatant>(PalaceSoldier()).Deployed != 0, "Initial palace soldier deploys during preparation");
            var palaceSoldier = PalaceSoldier();
            var palacePlacement = em.GetComponentData<BuildingPlacementState>(WorldQueries.Find(em, palaceId));
            var palacePosition = EntityState.Position(em, palaceSoldier);
            var palaceExitProbe = GridOps.Position(grid, palacePlacement.Cell + new int2(palacePlacement.Size.x, 0), new int2(1));
            Require(NavigationOps.TryNearestOpenOnSurface(em, root, palaceExitProbe, 12, palacePlacement.Surface, palacePlacement.Elevation, out var palaceExit), "Palace patrol has an exit on its own surface and elevation");
            bool palaceLayer = false;
            foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                palaceLayer |= node.Open != 0 && node.Surface == palacePlacement.Surface && node.Elevation == palacePlacement.Elevation && math.all(node.Cell == GridOps.Cell(grid, palacePosition)) && math.abs(node.Position.y + math.dot(node.Gradient, palacePosition.xz - node.Position.xz) - palacePosition.y) <= .55f;
            Require(palaceLayer, "Palace soldier deploys on the palace surface and elevation");
            var position = EntityState.Position(em, first);
            yield return WaitFor(() => math.distance(position, EntityState.Position(em, first)) > .2f, "Soldier DBP starts patrol during preparation");
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Preparation patrol continues into the night phase");
            yield return WaitFor(() => em.GetComponentData<Steering>(palaceSoldier).Moving != 0 && math.distance(palacePosition, em.GetComponentData<Steering>(palaceSoldier).Destination) > .2f, "Palace receives a weighted patrol destination");
            using (var palaceReach = new NightSpatialOps.Reach(em, root, palaceExit))
            {
                var patrolCost = palaceReach.Distance(em.GetComponentData<Steering>(palaceSoldier).Destination);
                Require(patrolCost > 6 && patrolCost <= 30.001f, "Palace patrol destination consumes the configured movement budget and exceeds the former value");
            }
            yield return WaitFor(() => math.distance(palacePosition, EntityState.Position(em, palaceSoldier)) > .5f && em.GetComponentData<NavigationState>(palaceSoldier).Failed == 0, "Palace soldier follows the weighted patrol route", timeoutSeconds: 30);
            view.OpenPanel(GamePanelId.Garrison);
            yield return WaitFor(() => HomeButton("召回所属士兵") != null, "Garrison-level recall UI available at night");
            HomeButton("召回所属士兵").onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).RecallState == 2, "Actual DBP navigation returns recalled soldier home");
            Require(em.GetComponentData<Combatant>(first).Deployed == 0, "Returned soldier no longer deploys in same night");
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            view.OpenPanel(GamePanelId.Building);
            yield return null;
        }
    }
}
#endif
