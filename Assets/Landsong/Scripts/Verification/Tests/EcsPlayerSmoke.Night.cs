#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Landsong.ECS.Definitions;
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
        IEnumerator NightUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            yield return PeacefulAdvanceUi(view, em, root);
            var original = SnapshotCodec.Capture(em, root);
            var settings = em.GetComponentData<NightSettings>(root);
            Require(settings.DawnSeconds == 3, "Production game content owns the three-second dawn contract");
            Require(settings.BattleVictoryCaptionAt == 2 && settings.BattleCelebrationAt == 4 && settings.BattleAdvanceAt == 7, "Production game content owns the two-two-three second victory contract");
            var testSettings = settings;
            testSettings.FirstInvasion = 1;
            testSettings.InvasionChance = 1;
            testSettings.FirstBoss = 99999;
            em.SetComponentData(root, testSettings);
            EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
            NightOps.Plan(em, root, false);
            SimulationEvents.Emit(em, root, EventKind.DayCheckpoint, "");
            view.OpenPanel(GamePanelId.Building);
            yield return null;
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Night, "Night UI advances from day through deployment");
            yield return WaitFor(() => NightPlanOps.State(em, root).AnySpawned != 0, "Real enemy entry after preparation");
            Entity movingEnemy = Entity.Null;
            using (var actors = WorldQueries.Entities<Combatant>(em))
                foreach (var e in actors)
                    if (em.GetComponentData<Combatant>(e).Faction == 1 && EntityState.Alive(em, e))
                    {
                        movingEnemy = e;
                        break;
                    }

            Require(movingEnemy != Entity.Null, "Live hostile exists after entry");
            var entryPosition = EntityState.Position(em, movingEnemy);
            yield return WaitFor(() => em.Exists(movingEnemy) && Unity.Mathematics.math.distance(entryPosition, EntityState.Position(em, movingEnemy)) > .1f, "DBP and navigation move enemy toward reachable target");
            Require(!view.Hud.Moon.text.Contains("秒") && !view.Hud.Moon.text.Contains("/"), "Moon UI reveals progress only, no exact combat seconds");
            Require(!HasRow(view.PrimaryRows, "平安夜速度 1×（切换 2×）"), "Combat UI does not offer 2x");
            view.Commands.TryQueue(new PauseRequest());
            yield return WaitFor(() => em.GetComponentData<SimulationControl>(root).Paused != 0, "Pause command applied");
            var time = em.GetComponentData<GameClock>(root).Time;
            yield return new WaitForSecondsRealtime(.3f);
            Require(time == em.GetComponentData<GameClock>(root).Time, "Actual player loop pause freezes battle");
            view.OpenPanel(GamePanelId.Intelligence);
            yield return new WaitForSecondsRealtime(.4f);
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/night-planning-night-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            view.Commands.TryQueue(new PauseRequest());
            yield return WaitFor(() => em.GetComponentData<SimulationControl>(root).Paused == 0, "Resume real battle");
            // Bound this UI test; full wave/timeout/retreat rules are checked in the isolated verification.
            var waves = em.GetBuffer<NightWave>(root);
            for (int i = 0; i < waves.Length; i++)
            {
                var w = waves[i];
                if (w.Spawned == 0)
                    w.Spawned = 2;
                waves[i] = w;
            }

            using (var enemies = WorldQueries.Entities<Combatant>(em))
                foreach (var e in enemies)
                    if (em.GetComponentData<Combatant>(e).Faction == 1)
                        CombatOps.ApplyDamage(em, root, new DamageRequest { Target = e, Amount = 999999 });
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Celebration, "Combat completion enters celebration");
            var turn = em.GetComponentData<GameClock>(root).Turn;
            Require(!view.Hud.Advance.gameObject.activeSelf, "Victory keeps advancement hidden during its opening beats");
            ClickIn(view.PrimaryRows, "退出情报模式");
            yield return WaitFor(() => !view.Hud.InIntelligenceMode && view.Hud.NightCaption.gameObject.activeSelf && view.Hud.NightCaption.text.Contains("胜利属于我们"), "Victory caption appears two seconds after the last enemy falls");
            Require(!view.Hud.Advance.gameObject.activeSelf, "Victory caption does not expose next-stage before the cheer sequence");
            Entity celebratingSoldier = Entity.Null;
            yield return WaitFor(() =>
            {
                using var soldiers = WorldQueries.Entities<Soldier>(em);
                foreach (var soldier in soldiers)
                    if (EntityState.Alive(em, soldier) && em.GetComponentData<Combatant>(soldier).Deployed != 0 && em.GetComponentData<VisualState>(soldier).Celebrating == (byte)NightEndPose.Celebrate)
                    {
                        celebratingSoldier = soldier;
                        return true;
                    }
                return false;
            }, "Soldiers begin cheering two seconds after the victory caption");
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/night-victory-caption.png");
                yield return new WaitForEndOfFrame();
            }

            yield return WaitFor(() => view.Hud.Advance.gameObject.activeSelf && view.Hud.Advance.interactable && em.GetComponentData<VisualState>(celebratingSoldier).Celebrating == 0, "Next-stage appears three seconds after cheering and releases the cheer pose");
            var patrolStart = EntityState.Position(em, celebratingSoldier);
            yield return WaitFor(() => em.GetComponentData<Steering>(celebratingSoldier).Moving != 0 || Unity.Mathematics.math.distance(patrolStart, EntityState.Position(em, celebratingSoldier)) > .1f, "Soldiers resume patrol while victory waits for player advancement");
            Require(em.GetComponentData<Session>(root).Phase == Phase.Celebration, "Victory waits for manual advancement while patrol continues");
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetComponentData<GameClock>(root).Turn == turn + 1, "Victory next-stage enters the next dawn without replaying the timeout closure", timeoutSeconds: 10);
            Require(em.GetComponentData<GameClock>(root).DawnRemaining > 0, "Dawn has its own clock after the night completes");
            Require(view.Panel != GamePanelId.BattleReport, "Report is not forcibly opened");
            view.OpenPanel(GamePanelId.BattleReport);
            yield return null;
            Require(!HasRow(view.PrimaryRows, "确认战报 · 下一回合") && em.GetBuffer<BattleHistoryEntry>(root).Length > 0, "Battle report is archived without confirmation");
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/night-planning-report-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            em.SetComponentData(root, settings);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            SimulationEvents.Emit(em, root, EventKind.DayCheckpoint, "");
            view.OpenPanel(GamePanelId.Building);
            yield return null;
            Require(original.SequenceEqual(SnapshotCodec.Capture(em, root)), "Night UI fixture restores exact original day, no player save IO");
        }

        IEnumerator PeacefulAdvanceUi(UI_GamePanel view, EntityManager em, Entity root)
        {
            var original = SnapshotCodec.Capture(em, root);
            var settings = em.GetComponentData<NightSettings>(root);
            var caption = view.Hud.NightPresentation.PeacefulNightCaption;
            try
            {
                var fixture = settings;
                fixture.FirstInvasion = fixture.FirstBoss = 99999;
                em.SetComponentData(root, fixture);
                EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
                NightOps.Plan(em, root, false);
                view.OpenPanel(GamePanelId.Building);
                yield return null;
                view.Hud.Advance.onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Deployment && !view.Hud.Advance.gameObject.activeSelf, "Preparation hides the next-phase button");
                Require(view.Hud.MoonProgress.gameObject.activeSelf && view.Hud.MoonProgress.value < 1, "Night progress covers preparation through night closure");
                Require(!view.Hud.NightCaption.gameObject.activeSelf, "Preparation has no premature peaceful subtitle");
                view.Commands.TryQueue(new PauseRequest());
                yield return WaitFor(() => em.GetComponentData<SimulationControl>(root).Paused != 0, "Preparation can pause");
                var pausedTime = em.GetComponentData<GameClock>(root).PhaseTime;
                yield return new WaitForSecondsRealtime(.25f);
                Require(em.GetComponentData<GameClock>(root).PhaseTime == pausedTime && !view.Hud.NightCaption.gameObject.activeSelf, "Paused preparation cannot release its notice");
                view.Commands.TryQueue(new PauseRequest());
                yield return WaitFor(() => view.Hud.NightCaption.gameObject.activeSelf, "Formal night displays its delayed peaceful caption");
                Require(em.GetComponentData<GameClock>(root).PhaseTime >= fixture.NightPreparationSeconds + view.Hud.NightPresentation.NightCaptionDelay && view.Hud.NightCaption.text == caption, "Peaceful subtitle uses authored text at the configured time");
                Require(!view.Hud.Advance.gameObject.activeSelf, "Peaceful advance stays hidden when the caption first appears");
                yield return WaitFor(() => view.Hud.Advance.interactable, "Peaceful advance appears after its own two-second delay");
                Require(em.GetComponentData<GameClock>(root).PhaseTime >= fixture.NightPreparationSeconds + view.Hud.NightPresentation.NightCaptionDelay + view.Hud.NightPresentation.PeacefulAdvanceDelay, "Peaceful button delay starts after the caption delay");
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/night-peaceful-caption.png");
                    yield return new WaitForEndOfFrame();
                }

                view.Hud.NightPresentation.PeacefulNightCaption = "可配置的平安夜字幕";
                yield return WaitFor(() => view.Hud.NightCaption.text == "可配置的平安夜字幕", "Changing the caption configuration changes the actual TMP subtitle");
                view.Hud.NightPresentation.PeacefulNightCaption = caption;
                Entity soldier = Entity.Null;
                using (var units = WorldQueries.Entities<Soldier>(em))
                    foreach (var unit in units)
                        if (EntityState.Alive(em, unit) && em.GetComponentData<Combatant>(unit).Deployed != 0)
                        {
                            soldier = unit;
                            break;
                        }

                Require(soldier != Entity.Null, "Night contains a visible soldier for actual return verification");
                var returningActor = em.GetComponentData<Combatant>(soldier);
                var home = WorldQueries.Find(em, returningActor.HomeId);
                Require(home != Entity.Null && BuildingStatus.Operational(em, home), "Return probe has an operational garrison");
                var placement = em.GetComponentData<BuildingPlacementState>(home);
                var grid = em.GetComponentData<GridData>(root);
                var exitProbe = GridOps.Position(grid, placement.Cell + new Unity.Mathematics.int2(placement.Size.x, 0), new Unity.Mathematics.int2(1));
                Require(NavigationOps.TryNearestOpenOnSurface(em, root, exitProbe, 12, placement.Surface, placement.Elevation, out var homeExit), "Return probe resolves its garrison exit");
                var from = EntityState.Position(em, soldier);
                bool placed = false;
                using (var reach = new NightSpatialOps.Reach(em, root, homeExit, returningActor.Profile.BodyRadius))
                    foreach (var node in em.GetBuffer<SurfaceNavNode>(root))
                    {
                        float distance = Unity.Mathematics.math.distance(homeExit, node.Position);
                        if (node.Open == 0 || distance < 8 || distance > 12 || !reach.Point(node.Position))
                            continue;
                        var transform = em.GetComponentData<Unity.Transforms.LocalTransform>(soldier);
                        transform.Position = node.Position;
                        em.SetComponentData(soldier, transform);
                        placed = true;
                        break;
                    }

                Require(placed, "Return probe is placed on a reachable surface away from its garrison");
                var turn = em.GetComponentData<GameClock>(root).Turn;
                from = EntityState.Position(em, soldier);
                view.Hud.Advance.onClick.Invoke();
                yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Day, "Peaceful skip enters dawn directly");
                Require(em.GetComponentData<GameClock>(root).DawnRemaining > 0 && !view.Hud.Advance.gameObject.activeSelf, "Dawn deliberately hides next-night advancement");
                Require(em.GetComponentData<Combatant>(soldier).Deployed != 0 && em.GetComponentData<VisualState>(soldier).Visible != 0 && Unity.Mathematics.math.distance(from, EntityState.Position(em, soldier)) < 2, "Skipping never teleports or immediately hides the soldier");
                yield return WaitFor(() => em.GetComponentData<GameClock>(root).DawnRemaining == 0 && view.Hud.Advance.interactable, "Next-stage button returns when the configured dawn ends", timeoutSeconds: settings.DawnSeconds + 7);
                yield return WaitFor(() => em.GetComponentData<Combatant>(soldier).Deployed == 0, "Background return completes within its deadline", timeoutSeconds: 35);
                Require(em.GetComponentData<GameClock>(root).Turn == turn + 1 && em.Exists(soldier) && em.GetComponentData<Combatant>(soldier).Deployed == 0 && em.GetComponentData<VisualState>(soldier).Visible == 0, "Arrived soldiers hide while their persistent identity survives dawn");
            }
            finally
            {
                view.Hud.NightPresentation.PeacefulNightCaption = caption;
                em.SetComponentData(root, settings);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
                SimulationEvents.Emit(em, root, EventKind.DayCheckpoint, "");
                view.OpenPanel(GamePanelId.Building);
            }
        }
    }
}
#endif
