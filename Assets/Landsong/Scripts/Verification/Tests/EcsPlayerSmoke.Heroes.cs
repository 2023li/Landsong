#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
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
            var original = SnapshotCodec.Capture(em, root); var s = em.GetComponentData<Session>(root); s.BasePopulation += 100; em.SetComponentData(root, s);
            int definition = Sim.FindDefinition(em, root, "b泰坦神殿"); var grid = em.GetComponentData<GridData>(root); Entity temple = Entity.Null;
            for (int y = 15; y < grid.Value.Value.Size.y - 15 && temple == Entity.Null; y++) for (int x = 15; x < grid.Value.Value.Size.x - 15; x++) if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0)) { temple = BuildingOps.Create(em, root, definition, new int2(x, y), 0, 1, true); break; }
            Require(temple != Entity.Null, "Hero UI legal temple fixture"); ulong siteId = em.GetComponentData<Identity>(temple).Id;
            int gold = em.GetComponentData<GameSettings>(root).Gold; Entity core = Entity.Null; using (var buildings = Sim.Entities<Building>(em)) foreach (var e in buildings) if (em.GetComponentData<BuildingStats>(e).IsCore != 0) core = e;
            for (int i = 0; i < 30; i++) em.GetBuffer<InventorySlot>(root).Add(new InventorySlot { Provider = em.GetComponentData<Identity>(core).Id, Index = 5000 + i, SlotType = -1, Item = gold, Count = Sim.Definition(em, root, gold).Capacity });
            var b = em.GetComponentData<Building>(temple); b.Workers = 30; b.Offering = 1; b.Subsidy = 1; em.SetComponentData(temple, b);
            view.Commands.Send(CommandKind.RecruitHero, siteId);
            Entity FindHero() { using var units = Sim.Entities<Hero>(em); foreach (var e in units) if (em.GetComponentData<Hero>(e).Sanctum == siteId) return e; return Entity.Null; }
            yield return WaitFor(() => FindHero() != Entity.Null, "Hero recruitment command from presentation");
            ulong heroId = em.GetComponentData<Identity>(FindHero()).Id;
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night || view.Panel == GamePanelId.NightConfirmation, "Hero night preparation or confirmation: " + view.Hud.Message.text);
            if (view.Panel == GamePanelId.NightConfirmation) { ClickRow(view, "确认放弃以上物资与士兵并入夜"); }
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Hero UI enters actual night after loss consent");
            s = em.GetComponentData<Session>(root); s.NightDuration = 120; em.SetComponentData(root, s);
            Transform hud = view.Hud.BattleHud.transform;
            yield return WaitFor(() => hud.gameObject.activeInHierarchy && hud.GetComponentsInChildren<Button>().Any(button => button.interactable), "Independent hero HUD available");
            hud.GetComponentsInChildren<Button>().First(button => button.interactable).onClick.Invoke();
            Button WakeButton() => view.Buildings.BuildingDetailsRows.GetComponentsInChildren<Button>().FirstOrDefault(button => button.interactable && button.GetComponentInChildren<Text>()?.text == "唤醒英雄");
            yield return WaitFor(() => WakeButton() != null, "Sleeping portrait opens temple with wake quote"); WakeButton().onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Combatant>(Sim.Find(em, heroId)).Deployed != 0, "Real temple UI wakes hero");
            yield return new WaitForSecondsRealtime(.5f); hud.GetComponentsInChildren<Button>().First(button => button.interactable).onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).SelectedHero == Sim.Find(em, heroId), "Portrait selects active hero");
            var hero = Sim.Find(em, heroId); var from = Sim.Position(em, hero); float3 destination = from;
            using (var reach = new NightSpatialOps.Reach(em, root, from))
            { for (int x = 2; x <= 5; x++) { var candidate = from + new float3(x, 0, 0); if (GridOps.Traversable(grid, em.GetBuffer<Occupancy>(root), GridOps.Cell(grid, candidate)) && reach.Point(candidate)) { destination = candidate; break; } } }
            Require(math.distance(from, destination) > 1, "Hero test has reachable movement destination"); view.Commands.Send(CommandKind.MoveHero, position: destination);
            yield return WaitFor(() => math.distance(from, Sim.Position(em, Sim.Find(em, heroId))) > .3f, "Selected hero moves through actual DBP navigation");
            view.Commands.Send(CommandKind.SelectHero); yield return WaitFor(() => em.GetComponentData<Session>(root).SelectedHero == Entity.Null, "Cancel hero selection through command");
            Require(math.all(em.GetComponentData<Combatant>(hero).Home == destination), "Unselected hero keeps player anchor");
            Require(hud.GetComponentsInChildren<Text>().Any(label => label.text.Contains("本夜 +0")), "Peaceful HUD shows zero combat XP after awakening");
            view.Buildings.BuildingDetailsClose.onClick.Invoke();
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/heroes-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); view.OpenPanel(GamePanelId.Building); yield return null;
        }
    }
}
#endif
