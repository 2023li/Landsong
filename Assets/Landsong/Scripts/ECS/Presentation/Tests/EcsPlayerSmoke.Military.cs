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
        IEnumerator SoldierUi(EcsGameView view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            var state = em.GetComponentData<Session>(root); state.BasePopulation += 100; em.SetComponentData(root, state);
            var catalog = em.GetComponentData<ContentCatalog>(root).Value; int definition = -1;
            for (int i = 0; i < catalog.Value.Definitions.Length; i++) if (catalog.Value.Definitions[i].Kind == ContentKind.Building && Sim.Rule(em, root, i, RuleKind.Garrison).Level >= 0) { definition = i; break; }
            var grid = em.GetComponentData<GridData>(root); Entity home = Entity.Null;
            for (int y = 15; y < grid.Value.Value.Size.y - 15 && home == Entity.Null; y++) for (int x = 15; x < grid.Value.Value.Size.x - 15; x++) if (GridOps.CanPlace(em, root, definition, new int2(x, y), 0)) { home = BuildingOps.Create(em, root, definition, new int2(x, y), 0, 1, true); break; }
            Require(home != Entity.Null, "Military UI fixture has legal garrison");
            ulong homeId = em.GetComponentData<Identity>(home).Id;
            InventoryOps.Add(em, root, em.GetComponentData<GameSettings>(root).Gold, 200);
            view.OpenPanel("驻军");
            bool Prefix(RectTransform rows, string prefix) => rows.GetComponentsInChildren<Button>().Any(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true);
            void ClickPrefix(RectTransform rows, string prefix)
            {
                var button = rows.GetComponentsInChildren<Button>().First(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith(prefix, StringComparison.Ordinal) == true); button.onClick.Invoke();
            }
            yield return WaitFor(() => Prefix(view.PrimaryRows, "招募数量 1"), "Military management panel visible");
            Require(view.PrimaryRows.GetComponentInParent<ScrollRect>() != view.SecondaryRows.GetComponentInParent<ScrollRect>(), "Garrison and pending lists are independent scroll views");
            ClickPrefix(view.PrimaryRows, "招募数量 1");
            yield return WaitFor(() => Prefix(view.PrimaryRows, "招募数量 2"), "Recruit quantity changes in native UI");
            ClickPrefix(view.PrimaryRows, "招募 " );
            Require(view.BuildingConfirmPanel.activeSelf, "Recruitment confirmation shows complete costs");
            ClickIn(view.BuildingConfirmRows, "确认");
            yield return WaitFor(() => MilitaryOps.GarrisonCount(em, homeId) == 2, "Confirmed UI recruits complete quantity");
            var first = MilitaryOps.AtSlot(em, homeId, 1); ulong firstId = em.GetComponentData<Identity>(first).Id;
            yield return WaitFor(() => Prefix(view.PrimaryRows, "槽 1 · "), "Real first slot displayed"); ClickPrefix(view.PrimaryRows, "槽 1 · ");
            yield return WaitFor(() => HasRow(view.SecondaryRows, "保存士兵姓名"), "Individual details and rename controls");
            var nameInput = view.SecondaryRows.GetComponentsInChildren<InputField>().Single(); nameInput.text = "军团验收先锋"; ClickIn(view.SecondaryRows, "保存士兵姓名");
            yield return WaitFor(() => em.GetComponentData<Identity>(first).Name.ToString() == "军团验收先锋", "Native name input submits soldier rename");
            ClickIn(view.SecondaryRows, "移入待分配池");
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).Garrison == 0 && Prefix(view.SecondaryRows, "军团验收先锋"), "Unassigned soldier appears in right list");
            ClickPrefix(view.PrimaryRows, "一键填充空槽");
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).Garrison == homeId, "One-click fill restores vacant slot");
            yield return WaitFor(() => HasRow(view.SecondaryRows, "解散士兵…"), "Dismiss control is present");
            ClickIn(view.SecondaryRows, "解散士兵…"); ClickIn(view.BuildingConfirmRows, "取消"); Require(em.Exists(first), "Cancel dismissal preserves soldier");
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldiers-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            var captured = SnapshotCodec.Capture(em, root); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, captured)); first = Sim.Find(em, firstId);
            Require(em.GetComponentData<Identity>(first).Name.ToString() == "军团验收先锋" && em.GetComponentData<Soldier>(first).Slot == 1, "Real UI name and slot survive reconstruction");
            view.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Military fixture enters actual night");
            state = em.GetComponentData<Session>(root); state.NightDuration = 120; em.SetComponentData(root, state); first = Sim.Find(em, firstId);
            yield return WaitFor(() => em.GetComponentData<Combatant>(first).Deployed != 0, "Soldier automatically deploys in player loop");
            var position = Sim.Position(em, first);
            yield return WaitFor(() => math.distance(position, Sim.Position(em, first)) > .2f, "Soldier DBP automatically patrols");
            view.OpenPanel("驻军");
            yield return WaitFor(() => Prefix(view.PrimaryRows, "召回所属士兵"), "Garrison-level recall UI available at night");
            ClickPrefix(view.PrimaryRows, "召回所属士兵");
            yield return WaitFor(() => em.GetComponentData<Soldier>(first).RecallState == 2, "Actual DBP navigation returns recalled soldier home");
            Require(em.GetComponentData<Combatant>(first).Deployed == 0, "Returned soldier no longer deploys in same night");
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); view.OpenPanel("建筑");
            yield return null;
        }
    }
}
#endif
