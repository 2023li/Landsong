#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        static void ClickContains(RectTransform rows, string text)
        {
            var button = rows.GetComponentsInChildren<Button>().FirstOrDefault(b => b.interactable && b.GetComponentInChildren<Text>()?.text.Contains(text) == true);
            Require(button != null, "Usable row contains " + text); button.onClick.Invoke();
        }
        static IEnumerator InvitationExpeditionUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            Require(!FeatureOps.Unlocked(em, root, "Inventory") && !FeatureOps.Unlocked(em, root, "Building") && !FeatureOps.Unlocked(em, root, "Expedition"), "Formal player starts without invitations-expeditions permissions");
            view.OpenPanel(GamePanelId.Inventory); Require(view.Panel != GamePanelId.Inventory, "Inventory panel rejects direct locked entry"); view.OpenPanel(GamePanelId.Expedition); Require(view.Panel != GamePanelId.Expedition, "Expedition panel rejects direct locked entry");
            yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<Text>().Any(t => t.text.Contains("建造许可尚未解锁")), "Locked construction menu explains tutorial path");
            foreach (var permission in new[] { "feature.Inventory", "feature.Building", "feature.Expedition" }) FeatureOps.Unlock(em, root, Sim.FindDefinition(em, root, new FixedString128Bytes(permission)));
            Entity Create(string name)
            {
                var definition = Sim.FindDefinition(em, root, new FixedString128Bytes(name)); var g = em.GetComponentData<GridData>(root);
                for (var i = 0; i < g.Value.Value.Cells.Length; i++) { var cell = g.Value.Value.Min + new int2(i % g.Value.Value.Size.x, i / g.Value.Value.Size.x); if (!GridOps.CanPlace(em, root, definition, cell, 0)) continue; var e = BuildingOps.Create(em, root, definition, cell, 0, 1, true); var b = em.GetComponentData<Building>(e); b.Workers = b.StableWorkers = em.GetComponentData<BuildingStats>(e).JobCapacity; em.SetComponentData(e, b); return e; }
                throw new InvalidOperationException("Missing UI fixture placement " + name);
            }
            var state = em.GetComponentData<Session>(root); state.BasePopulation = 100; em.SetComponentData(root, state);
            var market = Create("b市场"); var marketId = em.GetComponentData<Identity>(market).Id;
            InventoryOps.Add(em, root, em.GetComponentData<GameSettings>(root).Gold, 50);
            // PlayerHome also has an invitation slot. Select the fixture source explicitly,
            // rather than assuming the first equally labelled button belongs to the market.
            view.Quests.OpenInvitations(marketId);
            yield return WaitFor(() => view.Quests.QuestPoolRows != null && view.Quests.QuestPoolRows.gameObject.activeInHierarchy && view.Quests.QuestPoolRows.GetComponentsInChildren<Button>().Any(b => b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith("立即邀约") == true), "Actual UGUI invitation source pool");
            var mainlineName = em.GetComponentData<Identity>(Sim.Find(em, QuestOps.Tracking(em, root).Target)).Name.ToString();
            Require(view.Quests.QuestListRows.GetComponentsInChildren<Text>().Any(t => t.text.Contains(mainlineName)), "Active mainline remains visible beside invitation pool");
            var bytes = SnapshotCodec.Capture(em, root); ClickContains(view.Quests.QuestPoolRows, "立即邀约"); ClickIn(view.Buildings.BuildingConfirmRows, "取消"); Require(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Cancelled paid invitation is mutation free");
            ClickContains(view.Quests.QuestPoolRows, "立即邀约"); ClickIn(view.Buildings.BuildingConfirmRows, "确认");
            var expectedCommand = false; var commandDiagnostic = "";
            foreach (var queued in em.GetBuffer<Command>(root)) { commandDiagnostic += queued.Kind + " target=" + queued.Target + " arg=" + queued.Argument + "; "; if (queued.Kind == CommandKind.RecruitQuest && queued.Target == marketId && queued.Argument == 0) expectedCommand = true; }
            Require(expectedCommand, "Invitation confirmation queues expected source " + marketId + " and slot; queue=" + commandDiagnostic + "; ready=" + EcsSceneFlow.GameReady + "; message=" + view.Hud.Message.text);
            yield return WaitFor(() => em.GetBuffer<Command>(root).Length == 0, "Paid invitation command processed");
            Require(QuestOfferOps.Offered(em, marketId, 0) != Entity.Null, "Paid invite button creates offer; " + view.Hud.Message.text + "; source=" + QuestOfferOps.Quote(em, root, market, 0).Reason + "; phase=" + em.GetComponentData<Session>(root).Phase);
            yield return WaitFor(() => view.Quests.QuestPoolRows.GetComponentsInChildren<Button>().Any(b => b.interactable && b.GetComponentInChildren<Text>()?.text.Contains("查看邀约") == true), "New invitation displayed under source");
            ClickContains(view.Quests.QuestPoolRows, "查看邀约"); yield return WaitFor(() => HasRow(view.Quests.QuestDetailRows, "签约"), "Invitation detail opens without auto acceptance");
            ClickContains((RectTransform)view.Quests.QuestWindow.transform, "取消来源筛选"); yield return new WaitForSecondsRealtime(.4f);
            for(var type=0;type<4;type++)view.Quests.QuestTypeToggle(type).isOn=type==3; yield return new WaitForSecondsRealtime(.4f);
            Require(!view.Quests.QuestPoolRows.GetComponentsInChildren<Text>().Any(t => t.text.Contains("市场（")), "Independent source filter hides nonmatching trade source");
            for(var type=0;type<4;type++)view.Quests.QuestTypeToggle(type).isOn=true;
            if (Application.isEditor) { yield return new WaitForSecondsRealtime(.4f); ScreenCapture.CaptureScreenshot("Library/LandsongEcs/invitations-expeditions-invitations-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            var post = Create("b陆上远征所"); var postId = em.GetComponentData<Identity>(post).Id;
            view.OpenPanel(GamePanelId.Expedition); yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<Button>().Any(b => b.interactable && b.GetComponentInChildren<Text>()?.text.Contains("陆上远征所 LV") == true), "Expedition lists available source buildings");
            ClickContains(view.PrimaryRows, "陆上远征所 LV"); ClickContains(view.PrimaryRows, "近郊侦察 · 需驻地");
            yield return WaitFor(() => HasRow(view.PrimaryRows, "确认派遣"), "Crew and destination preview enables departure");
            ClickRow(view, "按可用人数填满"); yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<Text>().Any(t => t.text.Contains("出征人数 15")), "Expedition crew selector uses actual stable workforce cap");
            Require(view.PrimaryRows.GetComponentsInChildren<Text>().Any(t => t.text.Contains("失败预计伤亡")) && view.PrimaryRows.GetComponentsInChildren<Text>().Any(t => t.text.Contains("成功物品奖励")), "Preview explains probability rewards casualties pension before departure");
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/invitations-expeditions-expedition-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            bytes = SnapshotCodec.Capture(em, root); ClickRow(view, "确认派遣"); ClickIn(view.Buildings.BuildingConfirmRows, "取消"); Require(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Cancel expedition does not spend or lock workers");
            ClickRow(view, "确认派遣"); ClickIn(view.Buildings.BuildingConfirmRows, "确认"); yield return WaitFor(() => EconomyOps.WorkforceLocked(em, postId), "Real UGUI dispatch locks source workforce");
            yield return WaitFor(() => HasRow(view.PrimaryRows, "放弃远征"), "Travelling party exposes abandon action");
            ClickRow(view, "放弃远征"); ClickIn(view.Buildings.BuildingConfirmRows, "取消"); Require(EconomyOps.WorkforceLocked(em, postId), "Abandon cancellation retains party");
            ClickRow(view, "放弃远征"); ClickIn(view.Buildings.BuildingConfirmRows, "确认"); yield return WaitFor(() => !EconomyOps.WorkforceLocked(em, postId), "Confirmed abandon returns workers without casualties");
            yield return WaitFor(() => HasRow(view.PrimaryRows, "确认派遣"), "Source can depart again after abandonment");
            ClickRow(view, "确认派遣"); ClickIn(view.Buildings.BuildingConfirmRows, "确认"); yield return WaitFor(() => EconomyOps.WorkforceLocked(em, postId), "Second UGUI departure accepted");
            ulong journeyId = 0;
            using (var all = Sim.OrderedEntities<Expedition>(em)) foreach (var e in all)
            {
                var j = em.GetComponentData<Expedition>(e); if (j.Site != postId) continue;
                // Deterministic fixture advances only this result; the domain suite tests actual travel/RNG.
                j.SuccessChance = 1; j.Arrival = em.GetComponentData<Session>(root).Turn + 1; em.SetComponentData(e, j); journeyId = em.GetComponentData<Identity>(e).Id;
            }
            ExpeditionOps.Settle(em, root);
            yield return WaitFor(() => HasRow(view.PrimaryRows, "领取远征奖励"), "Successful arrival displays actual claim action");
            bytes = SnapshotCodec.Capture(em, root); ClickRow(view, "领取远征奖励"); ClickIn(view.Buildings.BuildingConfirmRows, "取消"); Require(bytes.SequenceEqual(SnapshotCodec.Capture(em, root)), "Reward cancellation retains unclaimed result and inventory");
            var wood = Sim.FindDefinition(em, root, new FixedString128Bytes("原木")); var woodBefore = InventoryOps.Count(em, root, wood);
            ClickRow(view, "领取远征奖励"); ClickIn(view.Buildings.BuildingConfirmRows, "确认"); yield return WaitFor(() => em.GetBuffer<Command>(root).Length == 0, "Reward claim command processed");
            Require(Sim.Find(em, journeyId) == Entity.Null, "Confirmed reward claim consumes result; " + view.Hud.Message.text + "; paused=" + em.GetComponentData<Session>(root).Paused + "; menu=" + (view.PauseMenu != null && view.PauseMenu.IsOpen));
            Require(InventoryOps.Count(em, root, wood) == woodBefore + 15, "Actual UGUI claim pays frozen full-crew reward once");
            view.OpenPanel(GamePanelId.Building); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Wave-eight UGUI fixture restores exact original day and permissions");
        }
    }
}
#endif
