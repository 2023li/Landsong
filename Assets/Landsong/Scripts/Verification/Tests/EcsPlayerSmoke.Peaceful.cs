#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using Landsong.ECS.Definitions;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator PeacefulUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            var checkpoint = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>();
            var archive = checkpoint.Export(root);
            var cameraPosition = view.WorldInteraction.Camera.transform.position;
            float zoom = view.WorldInteraction.Camera.orthographicSize;
            InventoryOps.Add(em, root, em.GetComponentData<CurrencySettings>(root).Gold, 100);
            Entity source = Entity.Null;
            using (var sites = WorldQueries.OrderedEntities<Building>(em))
                foreach (var site in sites)
                    if (PeacefulOps.Items(em, root, em.GetComponentData<Identity>(site).Id).Count > 0)
                    {
                        source = site;
                        break;
                    }

            Require(source != Entity.Null && NavigationOps.TryNearestOpen(em, root, EntityState.Position(em, source), 16, out _), "13 stocked source and legal responder position");
            NavigationOps.TryNearestOpen(em, root, EntityState.Position(em, source), 16, out var position);
            var unit = SoldierEntities.Spawn(em, root, SoldierId.FromIndex(0), position, false);
            EntityState.Set(em, unit, new Soldier());
            SoldierCombatants.Configure(em, root, unit, true, em.GetComponentData<Identity>(source).Id, position);
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            s.Phase = Phase.Night;
            sNight.Kind = NightKind.Peaceful;
            sClock.PhaseTime = 3;
            sClock.Time = 3;
            sNight.Duration = em.GetComponentData<NightSettings>(root).TotalNightSeconds;
            sPersistence.CheckpointPending = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sControl);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            NightResultOps.Reset(em, root);
            var visitorDefinition = OpportunityDefinitions.Find(em, root, "night.visitor");
            var visitorProfile = OpportunityDefinitions.Get(em, root, visitorDefinition).VisitorProfile;
            // Sample inside the authored window, not on its floating-point start boundary.
            sClock.PhaseTime = sNight.Duration * (visitorProfile.StartFraction + visitorProfile.EndFraction) * .5f;
            sClock.Time = sClock.PhaseTime;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sControl);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            var scheduler = em.GetComponentData<PeacefulState>(root);
            scheduler.Next = float.MaxValue;
            em.SetComponentData(root, scheduler);
            var visitor = PeacefulOps.TrySpawn(em, root, visitorDefinition, em.GetComponentData<Identity>(source).Id, 13579);
            Require(visitor != Entity.Null, $"13 actual peaceful visitor spawns; paused={sControl.Paused}, time={sClock.PhaseTime}/{sNight.Duration}, source={em.GetComponentData<BuildingPlacementState>(source).Cell}, operational={BuildingStatus.Operational(em, source)}, items={PeacefulOps.Items(em, root, em.GetComponentData<Identity>(source).Id).Count}, responder={PeacefulOps.Eligible(em, unit, OpportunityDefinitions.Get(em, root, visitorDefinition).VisitorProfile)}, position={position}");
            var escape = em.GetBuffer<VisitorPathPoint>(visitor);
            Require(escape.Length >= 4, "13 route leaves room for observable interception");
            em.SetComponentData(unit, LocalTransform.FromPosition(escape[3].Position));
            var ray = new Ray(view.WorldInteraction.Camera.transform.position, view.WorldInteraction.Camera.transform.forward);
            var plane = new Plane(Vector3.up, (Vector3)position);
            if (plane.Raycast(ray, out float distance))
                view.WorldInteraction.Camera.transform.position += (Vector3)position - ray.GetPoint(distance);
            view.WorldInteraction.Camera.orthographicSize = 10;
            view.OpenPanel(GamePanelId.Building);
            yield return WaitFor(() => view.Hud.NightHud.MarkerRoot.GetComponentsInChildren<UI_GamePanel_NightMarker>().Any(b => b.Select.interactable && b.Label.text.Contains("小偷")), "13 TMP marker is visible and clickable");
            var marker = view.Hud.NightHud.MarkerRoot.GetComponentsInChildren<UI_GamePanel_NightMarker>().First(b => b.Select.interactable && b.Label.text.Contains("小偷"));
            marker.Select.onClick.Invoke();
            yield return WaitFor(() => em.Exists(visitor) && em.GetComponentData<Opportunity>(visitor).Responder == unit, "13 UI submits real interception command");
            Require(em.GetComponentData<UnitOrder>(unit).Kind == OrderKind.Capture, "13 DBP receives non-damaging capture order");
            view.Commands.TryQueue(new PauseRequest());
            yield return WaitFor(() => em.GetComponentData<SimulationControl>(root).Paused != 0, "13 capture can be paused");
            var frozen = EntityState.Position(em, visitor);
            yield return new WaitForSecondsRealtime(.2f);
            Require(em.Exists(visitor) && math.distancesq(EntityState.Position(em, visitor), frozen) < .0001f, "13 visitor movement freezes in real loop");
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/peaceful-peaceful-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            view.Commands.TryQueue(new PauseRequest());
            yield return WaitFor(() => em.GetComponentData<SimulationControl>(root).Paused == 0, "13 resume capture");
            yield return WaitFor(() => !em.Exists(visitor), "13 visitor completes its real route or interception");
            using (var reports = em.GetBuffer<BattleReportEntry>(root).ToNativeArray(Unity.Collections.Allocator.Temp))
                Require(reports.Any(r => r.Kind == EventKind.TheftPrevented), "13 real DBP movement catches visitor without teleport or damage");
            Require(em.GetComponentData<Combatant>(unit).Participated == 0 && SoldierOps.SoldierBattleExperience(em, root, unit) == 0, "13 peaceful interception grants no combat experience");
            int privateRow = em.GetBuffer<BattleReportEntry>(root).Length;
            em.GetBuffer<BattleReportEntry>(root).Add(new BattleReportEntry { Kind = EventKind.Theft, Amount = 1, SourceName = "隐私验收来源" });
            view.OpenPanel(GamePanelId.BattleReport);
            yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("本夜战报尚未结算")), "13 retained report panel blocks current-night theft details");
            Require(!view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("隐私验收来源") || t.text.Contains("被盗物资")), "13 theft payload never leaks before report or dawn");
            em.GetBuffer<BattleReportEntry>(root).RemoveAt(privateRow);
            view.OpenPanel(GamePanelId.Building);
            var drop = LootEntities.Spawn(em, root, LootId.FromIndex(0), position, false);
            EntityState.Set(em, drop, new Loot { Item = em.GetComponentData<CurrencySettings>(root).Gold, Count = 7, Rarity = 3, SourceName = "验收 Boss" });
            EntityState.Set(em, drop, new VisualState { Visible = 1 });
            {
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
                sControl = em.GetComponentData<SimulationControl>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sPersistence = em.GetComponentData<PersistenceGate>(root);
            }

            sNight.Kind = NightKind.Invasion;
            s.Phase = Phase.Celebration;
            sClock.PhaseTime = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sControl);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            var gold = em.GetComponentData<CurrencySettings>(root).Gold;
            int priorStock = InventoryOps.Count(em, root, gold);
            yield return WaitFor(() => view.Hud.NightHud.MarkerRoot.GetComponentsInChildren<UI_GamePanel_NightMarker>().Any(b => b.Select.interactable && b.Label.text.Contains("特殊战利品")), "13 special loot marker can be clicked during cleanup");
            view.Hud.NightHud.MarkerRoot.GetComponentsInChildren<UI_GamePanel_NightMarker>().First(b => b.Select.interactable && b.Label.text.Contains("特殊战利品")).Select.onClick.Invoke();
            yield return WaitFor(() => !em.Exists(drop), "13 real HUD click claims special drop");
            Require(InventoryOps.Count(em, root, gold) == priorStock, "13 clicked special drop remains deferred until dawn");
            var automatic = LootEntities.Spawn(em, root, LootId.FromIndex(0), position, false);
            EntityState.Set(em, automatic, new Loot { Item = gold, Count = 3, Rarity = 2, SourceName = "自动收取验收" });
            EntityState.Set(em, automatic, new VisualState { Visible = 1 });
            yield return WaitFor(() => view.Hud.Advance.interactable, "13 victory allows manual cleanup");
            view.Hud.Advance.onClick.Invoke();
            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Day && !em.Exists(automatic), "13 loot auto-collected and dawn starts without report confirmation", timeoutSeconds: 30);
            view.OpenPanel(GamePanelId.BattleReport);
            yield return WaitFor(() => view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("阻止盗窃")) && !HasRow(view.PrimaryRows, "确认战报 · 下一回合"), "13 archived TMP report distinguishes prevention and rewards without a gate");
            if (Application.isEditor)
            {
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/peaceful-report-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            yield return WaitFor(() => em.GetComponentData<Session>(root).Phase == Phase.Day && em.GetBuffer<BattleHistoryEntry>(root).Length > 0, "13 automatic dawn commits history exactly once");
            checkpoint.Import(root, archive, false);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            view.WorldInteraction.Camera.transform.position = cameraPosition;
            view.WorldInteraction.Camera.orthographicSize = zoom;
            view.OpenPanel(GamePanelId.Building);
            yield return null;
        }
    }
}
#endif
