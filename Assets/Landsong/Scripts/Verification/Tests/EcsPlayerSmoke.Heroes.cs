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

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator HeroUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            PopulationState sPopulation = em.GetComponentData<PopulationState>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            sPopulation.BasePopulation += 100;
            {
                em.SetComponentData(root, sPopulation);
                em.SetComponentData(root, sNight);
            }

            var definition = BuildingDefinitions.Find(em, root, "b泰坦神殿");
            var grid = em.GetComponentData<GridData>(root);
            Entity temple = Entity.Null;
            for (int y = 15; y < grid.Value.Value.Size.y - 15 && temple == Entity.Null; y++)
                for (int x = 15; x < grid.Value.Value.Size.x - 15; x++)
                    if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0))
                    {
                        temple = BuildingCreation.Create(em, root, definition, new int2(x, y), 0, 1, true);
                        break;
                    }

            Require(temple != Entity.Null, "Hero UI legal temple fixture");
            ulong siteId = em.GetComponentData<Identity>(temple).Id;
            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            Entity core = Entity.Null;
            using (var buildings = WorldQueries.Entities<Building>(em))
                foreach (var e in buildings)
                    if (em.GetComponentData<BuildingHousingStats>(e).IsCore != 0)
                        core = e;
            for (int i = 0; i < 30; i++)
                em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = em.GetComponentData<Identity>(core).Id, Index = 5000 + i, SlotType = StorageSlotId.None, Item = gold, Count = ItemDefinitions.Get(em, root, gold).MaximumStack });
            BuildingWorkforceState bWorkforce = em.GetComponentData<BuildingWorkforceState>(temple);
            BuildingSanctumState bSanctum = em.GetComponentData<BuildingSanctumState>(temple);
            bWorkforce.Workers = 30;
            bSanctum.Offering = 1;
            bWorkforce.Subsidy = 1;
            {
                em.SetComponentData(temple, bWorkforce);
                em.SetComponentData(temple, bSanctum);
            }

            view.Commands.TryQueue(new RecruitHeroRequest { Sanctum = siteId });
            Entity FindHero()
            {
                using var units = WorldQueries.Entities<Hero>(em);
                foreach (var e in units)
                    if (em.GetComponentData<Hero>(e).Sanctum == siteId)
                        return e;
                return Entity.Null;
            }

            yield return WaitFor(() => FindHero() != Entity.Null, "Hero recruitment command from presentation");
            ulong heroId = em.GetComponentData<Identity>(FindHero()).Id;
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night || view.Panel == GamePanelId.NightConfirmation, "Hero night preparation or confirmation: " + view.Hud.Message.text);
            if (view.Panel == GamePanelId.NightConfirmation)
            {
                ClickRow(view, "确认放弃以上物资与士兵并入夜");
            }

            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Hero UI enters actual night after loss consent");
            {
                sPopulation = em.GetComponentData<PopulationState>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
            }

            sNight.Duration = 120;
            {
                em.SetComponentData(root, sPopulation);
                em.SetComponentData(root, sNight);
            }

            Transform hud = view.Hud.BattleHud.transform;
            yield return WaitFor(() => hud.gameObject.activeInHierarchy && view.Hud.heroButtons.TryGetValue(heroId, out var card) && card.Select.interactable, "Independent hero HUD available");
            Require(!view.Hud.HeroSelection.gameObject.activeSelf, "Hero selection bar excludes heroes that have not awakened");
            view.Hud.heroButtons[heroId].Select.onClick.Invoke();
            yield return WaitFor(() => view.Buildings.SelectedBuildingId == siteId && view.Buildings.ActionButton("details")?.gameObject.activeInHierarchy == true, "Sleeping portrait selects its temple");
            view.Buildings.ActionButton("details").onClick.Invoke();
            temple = WorldQueries.Find(em, siteId); // Night entry publishes rebuilt entities from its settlement transaction.
            Require(HeroOps.HeroAvailability(em, root, temple, true).Length == 0, "Hero wake fixture is available: " + HeroOps.HeroAvailability(em, root, temple, true));
            Button WakeButton() => view.BuildingDetails.DetailsRows.GetComponentsInChildren<Button>().FirstOrDefault(button => button.interactable && button.GetComponentInChildren<Text>()?.text == "唤醒英雄");
            yield return WaitFor(() => WakeButton() != null, "Selected temple details show wake quote");
            WakeButton().onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Combatant>(WorldQueries.Find(em, heroId)).Deployed != 0, "Real temple UI wakes hero");
            yield return WaitFor(() => view.Hud.heroSelectionItems.TryGetValue(heroId, out var item) && item.gameObject.activeInHierarchy, "Awakened hero appears in the hero selection bar");
            view.Hud.heroSelectionItems[heroId].Select.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<HeroSelection>(root).SelectedHero == WorldQueries.Find(em, heroId), "Portrait selects active hero");
            var hero = WorldQueries.Find(em, heroId);
            var from = EntityState.Position(em, hero);
            float3 destination = from;
            using (var reach = new NightSpatialOps.Reach(em, root, from))
            {
                for (int x = 2; x <= 5; x++)
                {
                    var candidate = from + new float3(x, 0, 0);
                    if (GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, candidate)) && reach.Point(candidate))
                    {
                        destination = candidate;
                        break;
                    }
                }
            }

            Require(math.distance(from, destination) > 1, "Hero test has reachable movement destination");
            view.Commands.TryQueue(new MoveHeroRequest { Destination = destination });
            yield return WaitFor(() => math.distance(from, EntityState.Position(em, WorldQueries.Find(em, heroId))) > .3f, "Selected hero moves through actual DBP navigation");
            view.Commands.TryQueue(new SelectHeroRequest());
            yield return WaitFor(() => em.GetComponentData<HeroSelection>(root).SelectedHero == Entity.Null, "Cancel hero selection through command");
            Require(math.all(em.GetComponentData<Combatant>(hero).Home == destination), "Unselected hero keeps player anchor");
            Require(hud.GetComponentsInChildren<Text>().Any(label => label.text.Contains("本夜 +0")), "Peaceful HUD shows zero combat XP after awakening");
            view.BuildingDetails.Close.onClick.Invoke();
            while (view.Buildings.CancelBuildingInteraction())
            {
            }

            Require(!view.Buildings.BuildingRangesVisible && !view.Buildings.BuildingActionBar.gameObject.activeSelf, "Hero UI releases temple selection and range overlays");
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/heroes-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            view.OpenPanel(GamePanelId.Building);
            yield return null;
        }
    }
}
#endif
