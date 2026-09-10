#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator NightUi(EcsGameView view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root); var settings = em.GetComponentData<GameSettings>(root);
            var testSettings = settings; testSettings.FirstInvasion = 1; testSettings.InvasionChance = 1; testSettings.FirstBoss = 99999; testSettings.RetreatSeconds = 5; em.SetComponentData(root, testSettings);
            Sim.Set(em, root, new NightPlanState { BossDefinition = -1 }); NightOps.Plan(em, root, false);
            Sim.Emit(em, root, EventKind.DayCheckpoint, "");
            view.OpenPanel("建筑"); yield return null; view.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Night UI advances from day through deployment");
            yield return WaitFor(() => NightPlanOps.State(em, root).ClockStarted != 0, "Real enemy entry and clock start");
            Entity movingEnemy = Entity.Null;
            using (var actors = Sim.Entities<Combatant>(em)) foreach (var e in actors) if (em.GetComponentData<Combatant>(e).Faction == 1 && Sim.Alive(em, e)) { movingEnemy = e; break; }
            Require(movingEnemy != Entity.Null, "Live hostile exists after entry"); var entryPosition = Sim.Position(em, movingEnemy);
            yield return WaitFor(() => em.Exists(movingEnemy) && Unity.Mathematics.math.distance(entryPosition, Sim.Position(em, movingEnemy)) > .1f, "DBP and navigation move enemy toward reachable target");
            Require(!view.Moon.text.Contains("秒") && !view.Moon.text.Contains("/"), "Moon UI reveals progress only, no exact combat seconds");
            Require(!HasRow(view.PrimaryRows, "平安夜速度 1×（切换 2×）"), "Combat UI does not offer 2x");
            view.Send(CommandKind.Pause); yield return WaitFor(() => em.GetComponentData<Session>(root).Paused != 0, "Pause command applied");
            var time = em.GetComponentData<Session>(root).Time; yield return new WaitForSecondsRealtime(.3f); Require(time == em.GetComponentData<Session>(root).Time, "Actual player loop pause freezes battle");
            view.OpenPanel("情报"); yield return new WaitForSecondsRealtime(.4f);
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/night-planning-night-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            view.Send(CommandKind.Pause); yield return WaitFor(() => em.GetComponentData<Session>(root).Paused == 0, "Resume real battle");
            // Bound this UI test; full wave/timeout/retreat rules are checked in the isolated verification.
            var waves = em.GetBuffer<NightWave>(root); for (int i = 0; i < waves.Length; i++) { var w = waves[i]; if (w.Spawned == 0) w.Spawned = 2; waves[i] = w; }
            using (var enemies = Sim.Entities<Combatant>(em)) foreach (var e in enemies) if (em.GetComponentData<Combatant>(e).Faction == 1) CombatOps.ApplyDamage(em, root, new DamageRequest { Target = e, Amount = 999999 });
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Celebration, "Combat completion enters celebration");
            var turn = em.GetComponentData<Session>(root).Turn;
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Report, "Full ten-second celebration reaches report gate");
            yield return WaitFor(() => view.Advance.GetComponentInChildren<Text>().text == "今晚战报", "Next-phase button becomes Tonight report");
            Require(!view.Advance.interactable, "Intelligence keeps report advancement locked until explicit exit");
            ClickIn(view.PrimaryRows, "退出情报模式");
            yield return WaitFor(() => !view.InIntelligenceMode && view.Advance.interactable, "Exit intelligence before opening tonight report");
            Require(view.Panel != "战报", "Report is not forcibly opened"); view.Advance.onClick.Invoke();
            yield return WaitFor(() => HasRow(view.PrimaryRows, "确认战报 · 下一回合"), "Native report button opens report confirmation");
            Require(em.GetComponentData<Session>(root).Turn == turn, "Opening report does not advance turn");
            if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/night-planning-report-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            ClickRow(view, "确认战报 · 下一回合"); yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetComponentData<Session>(root).Turn == turn + 1, "Report confirmation commits dawn");
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); em.SetComponentData(root, settings); Sim.Emit(em, root, EventKind.DayCheckpoint, "");
            view.OpenPanel("建筑"); yield return null;
            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Night UI fixture restores exact original day, no player save IO");
        }
    }
}
#endif
