#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator PauseMenuUi(EcsGameView view, EntityManager em, Entity root)
        {
            var live = SnapshotCodec.Capture(em, root);
            var checkpoint = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>(); var original = checkpoint.Export(root); var originalStore = checkpoint.Store;
            string temporary = Path.Combine(Path.GetTempPath(), "Landsong-Pause-UI-" + Guid.NewGuid().ToString("N")); var store = new RunArchiveStore(temporary); checkpoint.Store = store;
            var inputBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            var keyboard = InputSystem.AddDevice<Keyboard>(); var menu = view.PauseMenu;
            IEnumerator EscapeKey()
            { InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape)); for(int frame=0;frame<4;frame++){keyboard.MakeCurrent();yield return null;} InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null; }
            try
            {
                Require(menu != null && !menu.IsOpen, "Scene pause menu starts hidden");
                yield return null; // Let action bindings finish resolving the newly attached virtual keyboard before its first event.
                yield return EscapeKey();yield return null;
                var escapeState=em.GetComponentData<Session>(root);
                Require(menu.IsOpen&&escapeState.Paused!=0,"Real Esc opens and pauses ECS (open="+menu.IsOpen+", phase="+escapeState.Phase+", paused="+escapeState.Paused+", pending="+escapeState.CheckpointPending+", keyboard="+(Keyboard.current==keyboard)+", view="+view.isActiveAndEnabled+")");
                var commandCount = em.GetBuffer<Command>(root).Length; view.Send(CommandKind.Advance); Require(em.GetBuffer<Command>(root).Length == commandCount, "Modal rejects gameplay command submission");
                menu.SaveButton.onClick.Invoke(); Require(menu.SavesPage.activeSelf, "Save opens independent slot page"); menu.CreateSlotButton.onClick.Invoke();
                string run = em.GetComponentData<RunPersistence>(root).RunId.ToString();
                yield return WaitFor(() => store.Slots(run).Length == 1 && em.GetComponentData<Session>(root).CheckpointPending == 0, "Create slot writes isolated complete archive");
                string first = store.ActiveSlot(run); var originalSlot = File.ReadAllBytes(store.SlotPath(run, first));
                menu.CreateSlotButton.onClick.Invoke(); yield return WaitFor(() => store.Slots(run).Length == 2 && em.GetComponentData<Session>(root).CheckpointPending == 0, "Second save creates independent slot");
                Require(originalSlot.SequenceEqual(File.ReadAllBytes(store.SlotPath(run, first))), "Independent save preserves earlier slot");
                menu.SavesBackButton.onClick.Invoke(); menu.QuickSaveButton.onClick.Invoke(); yield return null; yield return null;
                Require(store.Slots(run).Length == 2 && store.ActiveSlot(run) != first, "Quick-save button retains current target without a third slot");
                var changed = em.GetComponentData<Session>(root); var dynasty = changed.DynastyName; changed.DynastyName = "未保存的测试改名"; em.SetComponentData(root, changed);
                var knowledge = em.GetComponentData<RecoveryState>(root); knowledge.KnownIntel = 75; em.SetComponentData(root, knowledge);
                menu.SaveButton.onClick.Invoke(); var slotButton = menu.SlotRows.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.GetComponentInChildren<TMPro.TextMeshProUGUI>().text.Contains(first.Substring(0, 6)));
                slotButton.onClick.Invoke(); Require(menu.ConfirmPage.activeSelf && em.GetComponentData<Session>(root).DynastyName.Equals(changed.DynastyName), "Slot load waits for confirmation");
                menu.ConfirmButton.onClick.Invoke(); yield return WaitFor(() => em.GetComponentData<Session>(root).DynastyName.Equals(dynasty) && store.ActiveSlot(run) == first, "Confirmed slot load restores state and selects quick-save target");
                Require(IntelOps.Known(em, root) >= 75, "Manual slot rewind retains acquired intelligence in the same night plan");
                menu.SettingsButton.onClick.Invoke(); Require(menu.SettingsPage.activeSelf && menu.Volume != null && menu.Fullscreen != null, "Settings page exposes working controls");
                yield return EscapeKey(); Require(menu.MainPage.activeSelf && menu.IsOpen, "Esc from settings returns to pause menu");
                menu.MenuButton.onClick.Invoke(); Require(menu.ConfirmPage.activeSelf && EcsSceneFlow.GameReady, "Return to Start requires confirmation"); menu.CancelButton.onClick.Invoke();
                menu.QuitButton.onClick.Invoke(); Require(menu.ConfirmPage.activeSelf && EcsSceneFlow.GameReady, "Quit requires confirmation"); menu.CancelButton.onClick.Invoke();
                if (Application.isEditor) { ScreenCapture.CaptureScreenshot("Library/LandsongEcs/pause-menu-Map_Test2.png"); yield return new WaitForEndOfFrame(); }
                yield return EscapeKey(); yield return WaitFor(() => !menu.IsOpen && em.GetComponentData<Session>(root).Paused == 0, "Esc resumes previously running game");
                view.Send(CommandKind.Pause); yield return WaitFor(() => em.GetComponentData<Session>(root).Paused != 0, "Fixture independently pauses"); menu.OpenButton.onClick.Invoke(); menu.ResumeButton.onClick.Invoke();
                yield return WaitFor(() => !menu.IsOpen, "Resume closes menu"); Require(em.GetComponentData<Session>(root).Paused != 0, "Menu preserves pre-existing pause");
                var s = em.GetComponentData<Session>(root); s.Phase = Phase.Night; s.NightKind = NightKind.Invasion; s.Paused = 0; s.NightDuration = 120; s.PhaseTime = 0; em.SetComponentData(root, s); em.GetBuffer<NightWave>(root).Clear();
                yield return EscapeKey(); yield return WaitFor(() => menu.IsOpen && em.GetComponentData<Session>(root).Paused != 0, "Night pause opens"); float time = em.GetComponentData<Session>(root).PhaseTime;
                yield return new WaitForSecondsRealtime(.3f); Require(em.GetComponentData<Session>(root).PhaseTime == time && !menu.QuickSaveButton.interactable, "Night clock freezes and save is disabled");
                view.Send(CommandKind.Save, argument: 1); yield return null; yield return null; Require(store.Slots(run).Length == 2, "Night save rejected by authority, not just UI");
                menu.Close(); yield return WaitFor(() => !menu.IsOpen, "Night pause closes safely");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = inputBackground;
                checkpoint.Store = originalStore; checkpoint.Import(root, original, false);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, live));
                if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
            }
            Require(live.SequenceEqual(SnapshotCodec.Capture(em, root)), "Pause fixture restores exact live day, not only last archived node");
        }
    }
}
#endif
