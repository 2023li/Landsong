#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class PauseMenuVerification
    {
        [MenuItem("Landsong/ECS/Verify Game pause menu")]
        public static string Run()
        {
            var log = new StringBuilder(); int count = 0;
            void Check(bool valid, string label) { if (!valid) throw new InvalidOperationException("FAIL " + label); count++; log.AppendLine("PASS " + label); }
            var temporary = Path.Combine(Path.GetTempPath(), "Landsong-Pause-" + Guid.NewGuid().ToString("N"));
            try
            {
                var store = new RunArchiveStore(temporary); string run = Guid.NewGuid().ToString("N"), other = Guid.NewGuid().ToString("N");
                var data = new RunArchive { RunId = run, Current = new byte[] { 1 }, Day = new byte[] { 1 }, Manual = new byte[] { 1 }, ManualDay = new byte[] { 1 }, Recovery = new RecoveryState { Turn = 1 } };
                store.Write(data); string first = store.SaveSlot(data, true); var firstBytes = File.ReadAllBytes(store.SlotPath(run, first));
                Check(store.Slots(run).Length == 1 && store.ActiveSlot(run) == first, "First independent save creates and selects a slot");
                data.Current = data.Manual = new byte[] { 2 }; string second = store.SaveSlot(data, true);
                Check(second != first && store.Slots(run).Length == 2, "Second save is independent");
                Check(firstBytes.SequenceEqual(File.ReadAllBytes(store.SlotPath(run, first))), "New save leaves prior slot unchanged");
                data.Current = data.Manual = new byte[] { 3 }; Check(store.SaveSlot(data, false) == second && store.Slots(run).Length == 2, "Quick save overwrites current slot only");
                Check(store.ReadSlot(run, second, out _).Current[0] == 3 && store.ReadSlot(run, first, out _).Current[0] == 1, "Slot payloads remain distinct");
                var secondBytes = File.ReadAllBytes(store.SlotPath(run, second)); data.Current = new byte[] { 4 }; store.Write(data);
                Check(secondBytes.SequenceEqual(File.ReadAllBytes(store.SlotPath(run, second))), "Automatic checkpoint cannot overwrite explicit slot");
                store.SelectSlot(run, first); Check(store.ActiveSlot(run) == first, "Loading can select prior slot as quick-save target");
                store.BeforeCommit = path => { if (path == store.SlotPath(run, first)) throw new IOException("Injected slot commit failure"); }; bool failed = false;
                try { store.SaveSlot(data, false); } catch (IOException) { failed = true; } finally { store.BeforeCommit = null; }
                Check(failed && firstBytes.SequenceEqual(File.ReadAllBytes(store.SlotPath(run, first))) && store.ActiveSlot(run) == first, "Failed overwrite preserves slot and active target");
                File.WriteAllBytes(store.SlotPath(run, second), new byte[] { 0 }); var recovered = store.ReadSlot(run, second, out bool backup);
                Check(backup && recovered.Current[0] == 2, "Corrupt primary slot falls back to its backup");
                bool rejected = false; try { store.SlotPath(run, "../state"); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Slot traversal is rejected");
                var otherData = data.Copy(); otherData.RunId = other; store.Write(otherData); string otherSlot = store.SaveSlot(otherData, false);
                Check(store.Slots(other).Length == 1, "Quick save without active slot creates one");
                store.End(run, "隔离测试", 1); Check(!Directory.Exists(Path.Combine(store.RunDirectory(run), "slots")) && File.Exists(store.SlotPath(other, otherSlot)), "End removes only owned dynasty slots");
                rejected = false; try { store.ReadSlot(run, first, out _); } catch (InvalidDataException) { rejected = true; } Check(rejected, "Ended dynasty cannot resurrect from slot");
                var scene = EditorSceneManager.OpenPreviewScene(EcsSceneFlow.Game);
                try
                {
                    var view = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EcsGameView>(true)).Single(); var menu = view.PauseMenu;
                    Check(menu != null && menu.View == view && !menu.Overlay.activeSelf, "Scene-authored pause panel starts hidden");
                    Check(new UnityEngine.Object[] { menu.OpenButton, menu.SaveButton, menu.QuickSaveButton, menu.SettingsButton, menu.ResumeButton, menu.MenuButton, menu.QuitButton, menu.Volume, menu.Fullscreen, menu.SlotRows, menu.SlotTemplate }.All(o => o != null), "Six actions settings and slots are wired");
                    Check(menu.Overlay.GetComponent<Canvas>().overrideSorting && menu.Overlay.GetComponent<Canvas>().sortingOrder == 500, "Pause overlay renders above dynamic HUD");
                }
                finally { EditorSceneManager.ClosePreviewScene(scene); }
                log.AppendLine("Assertions: " + count); return log.ToString();
            }
            finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, true); Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/pause-menu-verification.txt", log.ToString()); }
        }
    }
}
#endif
