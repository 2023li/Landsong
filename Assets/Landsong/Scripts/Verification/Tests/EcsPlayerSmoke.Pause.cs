#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using Landsong.ECS.Definitions;
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
        IEnumerator PauseMenuUi(UI_GamePanel view, EntityManager em, Entity root)
        {
            var live = SnapshotCodec.Capture(em, root);
            var checkpoint = World.DefaultGameObjectInjectionWorld.GetExistingSystemManaged<CheckpointSystem>();
            var original = checkpoint.Export(root);
            var originalStore = checkpoint.Store;
            string temporary = Path.Combine(Path.GetTempPath(), "Landsong-Pause-UI-" + Guid.NewGuid().ToString("N"));
            var store = new RunArchiveStore(temporary);
            checkpoint.Store = store;
            var inputBackground = InputSystem.settings.backgroundBehavior;
            var editorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.EnableDevice(keyboard);
            var menu = view.PauseMenu;
            IEnumerator EscapeKey()
            {
                keyboard.MakeCurrent();
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
                for (int frame = 0; frame < 4; frame++)
                {
                    keyboard.MakeCurrent();
                    yield return null;
                }

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                yield return null;
            }

            try
            {
                Require(menu != null && !menu.IsOpen, "Scene pause menu starts hidden");
                yield return null; // Let action bindings finish resolving the newly attached virtual keyboard before its first event.
                yield return EscapeKey();
                yield return null;
                var escapeState = em.GetComponentData<Session>(root);
                SimulationControl escapeStateControl = em.GetComponentData<SimulationControl>(root);
                PersistenceGate escapeStatePersistence = em.GetComponentData<PersistenceGate>(root);
                Require(menu.IsOpen && escapeStateControl.Paused != 0, "Real Esc opens and pauses ECS (open=" + menu.IsOpen + ", phase=" + escapeState.Phase + ", paused=" + escapeStateControl.Paused + ", pending=" + escapeStatePersistence.CheckpointPending + ", keyboard=" + (Keyboard.current == keyboard) + ", view=" + view.isActiveAndEnabled + ")");
                var commandCount = em.GetBuffer<QueuedGameplayRequest>(root).Length;
                view.Commands.TryQueue(new AdvanceRequest());
                Require(em.GetBuffer<QueuedGameplayRequest>(root).Length == commandCount, "Modal rejects gameplay command submission");
                menu.SaveButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_SavePanel>() != null, "Save opens shared slot panel");
                ObserveSharedArchives("Game pause archives");
                var browser = Shared<UI_SavePanel>();
                ClickArchive(browser, "创建独立存档");
                string run = em.GetComponentData<RunPersistence>(root).RunId.ToString();
                yield return WaitFor(() => store.Slots(run).Length == 1 && em.GetComponentData<PersistenceGate>(root).CheckpointPending == 0, "Create slot writes isolated complete archive");
                string first = store.ActiveSlot(run);
                var originalSlot = File.ReadAllBytes(store.SlotPath(run, first));
                browser.Refresh();
                ClickArchive(browser, "创建独立存档");
                yield return WaitFor(() => store.Slots(run).Length == 2 && em.GetComponentData<PersistenceGate>(root).CheckpointPending == 0, "Second save creates independent slot");
                Require(originalSlot.SequenceEqual(File.ReadAllBytes(store.SlotPath(run, first))), "Independent save preserves earlier slot");
                yield return CloseShared<UI_SavePanel>();
                menu.QuickSaveButton.onClick.Invoke();
                yield return null;
                yield return null;
                Require(store.Slots(run).Length == 2 && store.ActiveSlot(run) != first, "Quick-save button retains current target without a third slot");
                DynastyIdentity changedDynasty = em.GetComponentData<DynastyIdentity>(root);
                var dynasty = changedDynasty.Name;
                changedDynasty.Name = "未保存的测试改名";
                {
                    em.SetComponentData(root, changedDynasty);
                }

                var knowledge = em.GetComponentData<RecoveryState>(root);
                knowledge.KnownIntel = 75;
                em.SetComponentData(root, knowledge);
                store.RenameSlot(run, first, "暂停验收槽一", store.SlotStamp(run, first));
                menu.SaveButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_SavePanel>() != null, "Shared slots reopen");
                browser = Shared<UI_SavePanel>();
                ClickArchive(browser, "暂停验收槽一");
                ClickArchive(browser, "载入此记录");
                yield return WaitFor(() => Shared<UI_ConfirmPanel>() != null, "Slot load waits for shared confirmation");
                Require(em.GetComponentData<DynastyIdentity>(root).Name.Equals(changedDynasty.Name), "Unconfirmed slot load preserves current dynasty name");
                yield return ConfirmShared(true);
                yield return WaitFor(() => em.GetComponentData<DynastyIdentity>(root).Name.Equals(dynasty) && store.ActiveSlot(run) == first, "Confirmed slot load restores state and selects quick-save target");
                Require(IntelOps.Known(em, root) >= 75, "Manual slot rewind retains acquired intelligence in the same night plan");
                yield return CloseShared<UI_SavePanel>();
                menu.SettingsButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_SettingPanel>() != null, "Pause opens the shared settings panel");
                ObserveSharedSettings("Game pause settings");
                yield return EscapeKey();
                Require(Shared<UI_SettingPanel>() == null && menu.IsOpen, "Esc closes settings and returns focus to pause menu");
                menu.MenuButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_ConfirmPanel>() != null, "Return to Start requires shared confirmation");
                Require(EcsSceneFlow.GameReady, "Unconfirmed return preserves gameplay");
                yield return ConfirmShared(false);
                menu.QuitButton.onClick.Invoke();
                yield return WaitFor(() => Shared<UI_ConfirmPanel>() != null, "Quit requires shared confirmation");
                Require(EcsSceneFlow.GameReady, "Unconfirmed quit preserves gameplay");
                yield return ConfirmShared(false);
                if (Application.isEditor)
                {
                    ScreenCapture.CaptureScreenshot("Library/LandsongEcs/pause-menu.png");
                    yield return new WaitForEndOfFrame();
                }

                yield return EscapeKey();
                yield return WaitFor(() => !menu.IsOpen && em.GetComponentData<SimulationControl>(root).Paused == 0, "Esc resumes previously running game");
                view.Commands.TryQueue(new PauseRequest());
                yield return WaitFor(() => em.GetComponentData<SimulationControl>(root).Paused != 0, "Fixture independently pauses");
                menu.OpenButton.onClick.Invoke();
                menu.ResumeButton.onClick.Invoke();
                yield return WaitFor(() => !menu.IsOpen, "Resume closes menu");
                Require(em.GetComponentData<SimulationControl>(root).Paused != 0, "Menu preserves pre-existing pause");
                var s = em.GetComponentData<Session>(root);
                GameClock sClock = em.GetComponentData<GameClock>(root);
                SimulationControl sControl = em.GetComponentData<SimulationControl>(root);
                NightRuntimeState sNight = em.GetComponentData<NightRuntimeState>(root);
                s.Phase = Phase.Night;
                sNight.Kind = NightKind.Invasion;
                sControl.Paused = 0;
                sNight.Duration = 120;
                sClock.PhaseTime = 0;
                {
                    em.SetComponentData(root, s);
                    em.SetComponentData(root, sClock);
                    em.SetComponentData(root, sControl);
                    em.SetComponentData(root, sNight);
                }

                em.GetBuffer<NightWave>(root).Clear();
                yield return EscapeKey();
                yield return WaitFor(() => menu.IsOpen && em.GetComponentData<SimulationControl>(root).Paused != 0, "Night pause opens");
                float time = em.GetComponentData<GameClock>(root).PhaseTime;
                yield return new WaitForSecondsRealtime(.3f);
                Require(em.GetComponentData<GameClock>(root).PhaseTime == time && !menu.QuickSaveButton.interactable, "Night clock freezes and save is disabled");
                view.Commands.TryQueue(new CreateSaveRequest());
                yield return null;
                yield return null;
                Require(store.Slots(run).Length == 2, "Night save rejected by authority, not just UI");
                menu.Close();
                yield return WaitFor(() => !menu.IsOpen, "Night pause closes safely");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.backgroundBehavior = inputBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = editorInputBehavior;
                checkpoint.Store = originalStore;
                checkpoint.Import(root, original, false);
                SnapshotCodec.Restore(em, root, SnapshotCodec.Decode(em, root, live));
                if (Directory.Exists(temporary))
                    Directory.Delete(temporary, true);
            }

            Require(live.SequenceEqual(SnapshotCodec.Capture(em, root)), "Pause fixture restores exact live day, not only last archived node");
        }
    }
}
#endif
