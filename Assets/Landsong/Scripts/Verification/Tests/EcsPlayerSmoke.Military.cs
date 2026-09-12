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
using InputField = TMPro.TMP_InputField;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator SoldierUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            var state = em.GetComponentData<Session>(root); state.BasePopulation += 100; em.SetComponentData(root, state);
            int definition = Sim.FindDefinition(em,root,"b驻军营地");
            var grid = em.GetComponentData<GridData>(root); Entity home = Entity.Null;
            for (int y = 15; y < grid.Value.Value.Size.y - 15 && home == Entity.Null; y++) for (int x = 15; x < grid.Value.Value.Size.x - 15; x++) if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0)) { home = BuildingOps.Create(em, root, definition, new int2(x, y), 0, 1, true); break; }
            Require(home != Entity.Null, "Military UI fixture has legal garrison");
            ulong homeId = em.GetComponentData<Identity>(home).Id;
            InventoryOps.Add(em, root, em.GetComponentData<GameSettings>(root).Gold, 200);
            view.OpenPanel(GamePanelId.Garrison);
            bool Prefix(RectTransform rows, string prefix) => rows.GetComponentsInChildren<Button>().Any(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true);
            void ClickPrefix(RectTransform rows, string prefix)
            {
                var button = rows.GetComponentsInChildren<Button>().First(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true); button.onClick.Invoke();
            }
            Button HomeButton(string prefix)
            {
                bool current=false;foreach(var button in view.PrimaryRows.GetComponentsInChildren<Button>())
                {var label=button.GetComponentInChildren<Text>()?.text??"";if(label.StartsWith("【"))current=label.Contains("#"+homeId+"】");if(current&&button.interactable&&label.StartsWith(prefix,StringComparison.Ordinal))return button;}return null;
            }
            yield return WaitFor(() => Prefix(view.PrimaryRows, "招募数量 1"), "Military management panel visible");
            Require(view.PrimaryRows.GetComponentInParent<ScrollRect>() != view.SecondaryRows.GetComponentInParent<ScrollRect>(), "Garrison and pending lists are independent scroll views");
            ClickPrefix(view.PrimaryRows, "招募数量 1");
            yield return WaitFor(() => Prefix(view.PrimaryRows, "招募数量 2"), "Recruit quantity changes in native UI");
            HomeButton("募兵 ").onClick.Invoke();
            Require(view.Buildings.BuildingConfirmPanel.activeSelf, "Recruitment confirmation shows complete costs");
            ClickIn(view.Buildings.BuildingConfirmRows, "确认");
            yield return WaitFor(() => MilitaryOps.UnassignedCount(em) == 2, "Confirmed UI recruits complete quantity into pending pool");
            ulong[] pending;using(var all=Sim.OrderedEntities<Soldier>(em))pending=all.ToArray().Where(e=>em.GetComponentData<Soldier>(e).Garrison==0).Select(e=>em.GetComponentData<Identity>(e).Id).ToArray();
            for(int i=0;i<pending.Length;i++)
            {
                ulong id=pending[i];int slot=i+1;
                Button PendingButton()=>view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(c=>c.PersonId==id)?.Select;
                yield return WaitFor(()=>PendingButton()!=null,"Pending recruit appears by stable identity");PendingButton().onClick.Invoke();
                yield return WaitFor(()=>HomeButton("槽 "+slot+" · 空位")!=null,"Selected recruit enables specific camp slot");HomeButton("槽 "+slot+" · 空位").onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison==homeId,"Recruit assigned to selected camp");
            }
            var first = MilitaryOps.AtSlot(em, homeId, 1); ulong firstId = em.GetComponentData<Identity>(first).Id;
            UI_GamePanel_SoldierItem FirstCard()=>view.PrimaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(c=>c.PersonId==firstId);
            yield return WaitFor(()=>FirstCard()!=null,"Real first slot displayed");FirstCard().Details.onClick.Invoke();
            Require(view.soldierController.SoldierDetailsOpen,"Occupied camp slot opens soldier details");view.soldierController.SoldierDetailsName.onEndEdit.Invoke("军团验收先锋");
            yield return WaitFor(() => em.GetComponentData<Identity>(first).Name.ToString() == "军团验收先锋", "Native name input submits soldier rename");
            view.soldierController.CloseSoldierDetails();FirstCard().Remove.onClick.Invoke();
            yield return WaitFor(()=>em.GetComponentData<Soldier>(first).Garrison==0&&view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().Any(c=>c.PersonId==firstId),"Unassigned soldier appears only in pending list");
            HomeButton("一键填充空槽").onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).Garrison == homeId, "One-click fill restores vacant slot");
            yield return WaitFor(()=>FirstCard()!=null&&FirstCard().Dismiss.interactable,"Assigned card exposes immediate dismissal");
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldiers-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            var captured = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, captured)); first = Sim.Find(em, firstId);
            Require(em.GetComponentData<Identity>(first).Name.ToString() == "军团验收先锋" && em.GetComponentData<Soldier>(first).Slot == 1, "Real UI name and slot survive reconstruction");
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Military fixture enters actual night");
            state = em.GetComponentData<Session>(root); state.NightDuration = 120; em.SetComponentData(root, state); first = Sim.Find(em, firstId);
            yield return WaitFor(() => em.GetComponentData<Combatant>(first).Deployed != 0, "Soldier automatically deploys in player loop");
            var position = Sim.Position(em, first);
            yield return WaitFor(() => math.distance(position, Sim.Position(em, first)) > .2f, "Soldier DBP automatically patrols");
            view.OpenPanel(GamePanelId.Garrison);
            yield return WaitFor(() => HomeButton("召回所属士兵")!=null, "Garrison-level recall UI available at night");
            HomeButton("召回所属士兵").onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).RecallState == 2, "Actual DBP navigation returns recalled soldier home");
            Require(em.GetComponentData<Combatant>(first).Deployed == 0, "Returned soldier no longer deploys in same night");
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); view.OpenPanel(GamePanelId.Building);
            yield return null;
        }
    }
}
#endif
