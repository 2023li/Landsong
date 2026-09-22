#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Landsong.ECS.Definitions;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
using TMPro;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator IntelligenceUi(UI_GamePanel view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root);
            var settings = em.GetComponentData<NightSettings>(root);
            var checkpoints = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>();
            var archive = checkpoints.Export(root);
            var position = view.WorldInteraction.Camera.transform.position;
            float zoom = view.WorldInteraction.Camera.orthographicSize;
            var test = settings;
            test.FirstInvasion = 1;
            test.InvasionChance = 1;
            test.FirstBoss = 99999;
            em.SetComponentData(root, test);
            var s = em.GetComponentData<Session>(root);
            GameClock sClock = em.GetComponentData<GameClock>(root);
            SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
            NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
            PersistenceGate sPersistence = em.GetComponentData<PersistenceGate>(root);
            sClock.Turn = 8;
            s.Phase = Phase.Day;
            sPersistence.CheckpointPending = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sControl);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            EntityState.Set(em, root, new NightPlanState { BossDefinition = EnemyId.None });
            NightOps.Plan(em, root, false);
            EntityState.Set(em, root, new RecoveryState { Turn = sClock.Turn, KnownIntel = 100 });
            IntelOps.Refresh(em, root);
            var button = view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.GetComponentInChildren<TMP_Text>(true)?.text.StartsWith("情报") == true);
            yield return new WaitForSecondsRealtime(.3f);
            Require(button.GetComponentInChildren<TMP_Text>().text.Contains("•"), "12 actual intelligence unread badge");
            button.onClick.Invoke();
            yield return WaitFor(() => view.Hud.InIntelligenceMode && em.GetComponentData<IntelligenceModeState>(root).Enabled != 0 && view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "当晚军情"), "12 dedicated intelligence button opens isolated TMP panel");
            Require(!view.Hud.Advance.interactable && view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains(" × ")), "12 high daytime aggregates and disabled phase button");
            Require(!view.BuildingDetails.Name.gameObject.activeInHierarchy && !view.Quests.QuestHudRows.gameObject.activeInHierarchy, "12 unrelated rename and quest controls hidden in intelligence mode");
            yield return WaitFor(() => !IntelOps.Read(em, root).Unread, "12 rendered report acknowledges unread");
            var pending = em.GetBuffer<QueuedGameplayRequest>(root).Length;
            view.Commands.TryQueue(new AdvanceRequest());
            view.Commands.TryQueue(new WakeHeroRequest());
            Require(em.GetBuffer<QueuedGameplayRequest>(root).Length == pending, "12 presentation refuses gameplay input while viewing");
            var areas = IntelOps.Read(em, root).Areas;
            var area = areas.First(a => !a.Target);
            var ray = new Ray(view.WorldInteraction.Camera.transform.position, view.WorldInteraction.Camera.transform.forward);
            var plane = new Plane(Vector3.up, (Vector3)area.Center);
            if (plane.Raycast(ray, out float distance))
                view.WorldInteraction.Camera.transform.position += (Vector3)area.Center - ray.GetPoint(distance);
            view.WorldInteraction.Camera.orthographicSize = 24;
            if (Application.isEditor)
            {
                yield return new WaitForSecondsRealtime(.3f);
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/intelligence-intelligence-" + map + ".png");
                yield return new WaitForEndOfFrame();
            }

            {
                s = em.GetComponentData<Session>(root);
                sClock = em.GetComponentData<GameClock>(root);
                sControl = em.GetComponentData<SimulationControl>(root);
                sNight = em.GetComponentData<NightRuntimeState>(root);
                sPersistence = em.GetComponentData<PersistenceGate>(root);
            }

            s.Phase = Phase.Night;
            sNight.Duration = 120;
            sNight.Intelligence = 100;
            sControl.Paused = 0;
            {
                em.SetComponentData(root, s);
                em.SetComponentData(root, sClock);
                em.SetComponentData(root, sControl);
                em.SetComponentData(root, sNight);
                em.SetComponentData(root, sPersistence);
            }

            var plan = NightPlanOps.State(em, root);
            plan.ClockStarted = plan.AnySpawned = 1;
            plan.CombatElapsed = 0;
            EntityState.Set(em, root, plan);
            yield return WaitFor(() => NightPlanOps.State(em, root).CombatElapsed > .1f, "12 combat clock continues while intelligence is open");
            view.PauseMenu.OpenButton.onClick.Invoke();
            yield return WaitFor(() => view.PauseMenu.IsOpen && em.GetComponentData<SimulationControl>(root).Paused != 0, "12 pause retains priority over intelligence");
            Require(view.Hud.InIntelligenceMode, "12 closing pause can return to the existing intelligence mode");
            view.PauseMenu.ResumeButton.onClick.Invoke();
            yield return WaitFor(() => !view.PauseMenu.IsOpen && em.GetComponentData<SimulationControl>(root).Paused == 0, "12 pause resumes into intelligence");
            ClickIn(view.PrimaryRows, "退出情报模式");
            yield return WaitFor(() => !view.Hud.InIntelligenceMode && em.GetComponentData<IntelligenceModeState>(root).Enabled == 0, "12 explicit exit releases both input gates");
            em.SetComponentData(root, settings);
            checkpoints.Import(root, archive, false);
            SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original));
            view.WorldInteraction.Camera.transform.position = position;
            view.WorldInteraction.Camera.orthographicSize = zoom;
            view.OpenPanel(GamePanelId.Building);
            yield return null;
        }

        // Bounded development-only repeated-load probe: separate font atlas residency from total native allocations.
        // Stable observations do not prove FontEngine/ComputeBuffer shutdown diagnostics are harmless.
        IEnumerator FontReloadProbe()
        {
            int warmFonts = 0, warmAtlases = 0;
            long warmBytes = 0;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                EcsSceneFlow.Begin(new EcsSceneFlow.Request(VerificationMap.Id));
                yield return WaitFor(() => EcsSceneFlow.GameReady, "TMP repeated-load probe " + cycle, true);
                yield return null;
                var view = FindFirstObjectByType<UI_GamePanel>();
                ObserveGameLifetime(view, "repeated load " + cycle);
                view.OpenPanel(GamePanelId.Intelligence);
                yield return new WaitForSecondsRealtime(.3f);
                view.OpenPanel(GamePanelId.Building);
                yield return new WaitForSecondsRealtime(.3f);
                EcsSceneFlow.ReturnToMenu();
                yield return WaitFor(() => MenuReady, "TMP probe returns to menu");
                yield return Resources.UnloadUnusedAssets();
                GC.Collect();
                yield return null;
                RequireGameLifetimeReleased("repeated return " + cycle);
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                var textures = fonts.SelectMany(f => f.atlasTextures ?? Array.Empty<Texture2D>()).Where(t => t != null).Distinct().ToArray();
                long bytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
                Debug.Log("[ECS FONT PROBE] cycle=" + cycle + " fonts=" + fonts.Length + " atlases=" + textures.Length + " atlasBytes=" + bytes + " totalAllocated=" + Profiler.GetTotalAllocatedMemoryLong());
                if (cycle == 0)
                {
                    warmFonts = fonts.Length;
                    warmAtlases = textures.Length;
                    warmBytes = bytes;
                }
                else
                    Require(fonts.Length <= warmFonts && textures.Length <= warmAtlases && bytes <= warmBytes, "TMP repeated identical loads do not accumulate font assets or atlas textures");
                Require(Released(), "TMP repeated-load releases simulation ownership");
                RequirePresentationReleased();
            }
        }
    }
}
#endif
