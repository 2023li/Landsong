#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
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
        IEnumerator IntelligenceUi(EcsGameView view, EntityManager em, Entity root, string map)
        {
            var original = SnapshotCodec.Capture(em, root); var settings = em.GetComponentData<GameSettings>(root);
            var checkpoints = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>(); var archive = checkpoints.Export(root);
            var position = view.Camera.transform.position; float zoom = view.Camera.orthographicSize;
            var test = settings; test.FirstInvasion = 1; test.InvasionChance = 1; test.FirstBoss = 99999; em.SetComponentData(root, test);
            var s = em.GetComponentData<Session>(root); s.Turn = 8; s.Phase = Phase.Day; s.CheckpointPending = 0; em.SetComponentData(root, s);
            Sim.Set(em, root, new NightPlanState { BossDefinition = -1 }); NightOps.Plan(em, root, false);
            Sim.Set(em, root, new RecoveryState { Turn = s.Turn, KnownIntel = 100 }); IntelOps.Refresh(em, root);
            var button = view.GetComponentInParent<Canvas>().GetComponentsInChildren<UnityEngine.UI.Button>(true).First(b => b.GetComponentInChildren<TMP_Text>(true)?.text.StartsWith("情报") == true);
            yield return new WaitForSecondsRealtime(.3f);
            Require(button.GetComponentInChildren<TMP_Text>().text.Contains("•"), "12 actual intelligence unread badge");
            button.onClick.Invoke();
            yield return WaitFor(() => view.InIntelligenceMode && em.GetComponentData<Session>(root).IntelligenceMode != 0 && view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text == "当晚军情"), "12 dedicated intelligence button opens isolated TMP panel");
            Require(!view.Advance.interactable && view.PrimaryRows.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains(" × ")), "12 high daytime aggregates and disabled phase button");
            Require(!view.NameInput.gameObject.activeInHierarchy && !view.QuestHudRows.gameObject.activeInHierarchy, "12 unrelated rename and quest controls hidden in intelligence mode");
            yield return WaitFor(() => !IntelOps.Read(em, root).Unread, "12 rendered report acknowledges unread");
            var pending = em.GetBuffer<Command>(root).Length; view.Send(CommandKind.Advance); view.Send(CommandKind.WakeHero);
            Require(em.GetBuffer<Command>(root).Length == pending, "12 presentation refuses gameplay input while viewing");
            var areas = IntelOps.Read(em, root).Areas; var area = areas.First(a => !a.Target);
            var ray = new Ray(view.Camera.transform.position, view.Camera.transform.forward); var plane = new Plane(Vector3.up, (Vector3)area.Center);
            if (plane.Raycast(ray, out float distance)) view.Camera.transform.position += (Vector3)area.Center - ray.GetPoint(distance);
            view.Camera.orthographicSize = 24;
            if (Application.isEditor) { yield return new WaitForSecondsRealtime(.3f); ScreenCapture.CaptureScreenshot("Library/LandsongEcs/intelligence-intelligence-" + map + ".png"); yield return new WaitForEndOfFrame(); }
            s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; s.NightDuration = 120; s.IntelAtNight = 100; s.Paused = 0; em.SetComponentData(root, s);
            var plan = NightPlanOps.State(em, root); plan.ClockStarted = plan.AnySpawned = 1; plan.CombatElapsed = 0; Sim.Set(em, root, plan);
            yield return WaitFor(() => NightPlanOps.State(em, root).CombatElapsed > .1f, "12 combat clock continues while intelligence is open");
            view.PauseMenu.OpenButton.onClick.Invoke();
            yield return WaitFor(() => view.PauseMenu.IsOpen && em.GetComponentData<Session>(root).Paused != 0, "12 pause retains priority over intelligence");
            Require(view.InIntelligenceMode, "12 closing pause can return to the existing intelligence mode");
            view.PauseMenu.ResumeButton.onClick.Invoke();
            yield return WaitFor(() => !view.PauseMenu.IsOpen && em.GetComponentData<Session>(root).Paused == 0, "12 pause resumes into intelligence");
            ClickIn(view.PrimaryRows, "退出情报模式");
            yield return WaitFor(() => !view.InIntelligenceMode && em.GetComponentData<Session>(root).IntelligenceMode == 0, "12 explicit exit releases both input gates");
            checkpoints.Import(root, archive, false); SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, original)); em.SetComponentData(root, settings);
            view.Camera.transform.position = position; view.Camera.orthographicSize = zoom; view.OpenPanel("建筑"); yield return null;
        }
        // Bounded development-only repeated-load probe: separate font atlas residency from total native allocations.
        // Stable observations do not prove FontEngine/ComputeBuffer shutdown diagnostics are harmless.
        IEnumerator FontReloadProbe()
        {
            int warmFonts = 0, warmAtlases = 0; long warmBytes = 0;
            for (int cycle = 0; cycle < 3; cycle++)
            {
                EcsSceneFlow.Begin(new EcsSceneFlow.Request("Map_Test2"));
                yield return WaitFor(() => EcsSceneFlow.GameReady, "TMP repeated-load probe " + cycle, true);
                yield return null; var view = FindFirstObjectByType<EcsGameView>();
                view.OpenPanel("情报"); yield return new WaitForSecondsRealtime(.3f); view.OpenPanel("建筑"); yield return new WaitForSecondsRealtime(.3f);
                EcsSceneFlow.ReturnToMenu(); yield return WaitFor(() => FindFirstObjectByType<EcsMainMenu>() != null, "TMP probe returns to menu");
                yield return Resources.UnloadUnusedAssets(); GC.Collect(); yield return null;
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>(); var textures = fonts.SelectMany(f => f.atlasTextures ?? Array.Empty<Texture2D>()).Where(t => t != null).Distinct().ToArray();
                long bytes = textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
                Debug.Log("[ECS FONT PROBE] cycle=" + cycle + " fonts=" + fonts.Length + " atlases=" + textures.Length + " atlasBytes=" + bytes + " totalAllocated=" + Profiler.GetTotalAllocatedMemoryLong());
                if (cycle == 0) { warmFonts = fonts.Length; warmAtlases = textures.Length; warmBytes = bytes; }
                else Require(fonts.Length <= warmFonts && textures.Length <= warmAtlases && bytes <= warmBytes, "TMP repeated identical loads do not accumulate font assets or atlas textures");
                Require(Released(), "TMP repeated-load releases simulation ownership");
                RequirePresentationReleased();
            }
        }
    }
}
#endif
