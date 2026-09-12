#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using UnityEditor;

namespace Landsong.ECS.Editor
{
    public static class ArchiveApplicationServiceVerification
    {
        static StringBuilder report;
        static int assertions;
        static void Check(bool valid, string label)
        { if (!valid) throw new InvalidOperationException("FAIL " + label); assertions++; report.AppendLine("PASS " + label); }
        static void Reject(Action action, string label)
        {
            bool rejected = false;
            try { action(); }
            catch (Exception error) when (error is InvalidOperationException || error is InvalidDataException || error is ArgumentException) { rejected = true; }
            Check(rejected, label);
        }

        [MenuItem("Landsong/ECS/Verification/Archive application service")]
        public static string Run()
        {
            report = new StringBuilder().AppendLine("Started: " + DateTimeOffset.Now.ToString("O")); assertions = 0;
            var parent = Path.GetFullPath("Library/LandsongEcs/VerificationRuns");
            var directory = Path.GetFullPath(Path.Combine(parent, "ArchiveService-" + Guid.NewGuid().ToString("N")));
            if (Path.GetDirectoryName(directory) != parent || Directory.Exists(directory)) throw new InvalidOperationException("Expected an unused owned fixture directory.");
            try
            {
                var store = new RunArchiveStore(directory);
                var archive = RunArchiveCodec.Decode(File.ReadAllBytes("Assets/Landsong/Scripts/Editor/Tests/Verification/Fixtures/Persistence/map-test2-day-v23.lsrun.bytes"));
                archive.RunId = Guid.NewGuid().ToString("N"); store.Write(archive); store.Write(archive);
                var other = archive.Copy(); other.RunId = Guid.NewGuid().ToString("N"); store.Write(other);
                string slot = store.SaveSlot(archive, true), otherSlot = store.SaveSlot(other, true);
                LoadMode(store, archive, slot);
                SaveMode(store, archive, other, slot, otherSlot);
                StaleConfirmation(store, archive, slot);
                SessionExpiry(store, archive, slot);
                report.AppendLine("Completed: " + DateTimeOffset.Now.ToString("O")); report.AppendLine("Assertions: " + assertions);
                return report.ToString();
            }
            catch (Exception error) { report.AppendLine(error.ToString()); throw; }
            finally
            {
                // Resolved above to a new exact fixture directory; never visits the player save store.
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
                Directory.CreateDirectory("Library/LandsongEcs");
                File.WriteAllText("Library/LandsongEcs/archive-application-service-verification.txt", report.ToString());
            }
        }

        static void LoadMode(RunArchiveStore store, RunArchive archive, string slot)
        {
            var loads = new List<(string run, string slot, bool backup)>();
            var service = new ArchiveApplicationService(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Load, Store = store,
                Load = (run, selected, backup) => loads.Add((run, selected, backup)) });
            var automatic = service.Describe(archive.RunId); var selected = service.Describe(archive.RunId, slot);
            Check(service.IsValid && !service.CanWrite && automatic.Valid && selected.Valid, "Load mode reads valid automatic and manual nodes without gameplay save permission");
            Reject(() => service.Create("outside gameplay"), "Load mode cannot create gameplay snapshots");
            Reject(() => service.Overwrite(selected), "Load mode cannot overwrite gameplay snapshots");
            service.Load(automatic, false); service.Load(selected, true);
            Check(loads.SequenceEqual(new[] { (archive.RunId, (string)null, false), (archive.RunId, slot, true) }), "Load mode routes the explicit run, slot and backup choice");
            Reject(() => service.Rename(automatic, "invalid"), "Automatic nodes cannot be renamed");
            Reject(() => service.Delete(automatic), "Automatic nodes cannot be deleted as slots");
            service.Rename(selected, "共享面板验收槽");
            Check(store.Describe(archive.RunId, slot).Name == "共享面板验收槽" && !service.Matches(selected), "Load browser metadata management invalidates the prior slot confirmation");
            Reject(() => service.Delete(selected), "Stale slot metadata cannot authorize deletion");
            string disposable = store.SaveSlot(archive, true);
            service.Delete(service.Describe(archive.RunId, disposable));
            Check(!store.Slots(archive.RunId).Contains(disposable) && File.Exists(store.RunPath(archive.RunId)) && store.Slots(archive.RunId).Contains(slot), "Deleting one explicit slot preserves the dynasty and its other slots");
        }

        static void SaveMode(RunArchiveStore store, RunArchive archive, RunArchive other, string slot, string otherSlot)
        {
            bool canWrite = true;
            var saves = new List<(string slot, ulong stamp, string label)>();
            var management = new List<(string slot, string label, ulong stamp, bool delete)>();
            int loads = 0;
            var request = new ArchiveOpenRequest { Mode = ArchiveOpenMode.Save, Store = store, CurrentRun = archive.RunId,
                CanWrite = () => canWrite, Save = (target, stamp, label) => saves.Add((target, stamp, label)),
                ManageSlot = (target, label, stamp, delete) => management.Add((target, label, stamp, delete)), Load = (_, _, _) => loads++ };
            var service = new ArchiveApplicationService(request); var selected = service.Describe(archive.RunId, slot); var automatic = service.Describe(archive.RunId);
            Check(service.CanWrite, "Live save mode has explicit save capability");
            service.Create("新建验收槽"); service.Overwrite(selected); service.Rename(selected, "重命名请求"); service.Delete(selected); service.Load(selected, false);
            Check(saves.SequenceEqual(new[] { ((string)null, 0UL, "新建验收槽"), (slot, selected.Stamp, (string)null) }), "Save service delegates create and overwrite using named slot and expected stamp");
            Check(management.SequenceEqual(new[] { (slot, "重命名请求", selected.Stamp, false), (slot, (string)null, selected.Stamp, true) }) && loads == 1, "Save service delegates rename/delete/load to current gameplay authority");
            Check(store.Describe(archive.RunId, slot).Name == "共享面板验收槽", "Save service does not bypass the supplied gameplay management callback");
            Reject(() => service.Overwrite(automatic), "Automatic node cannot be reinterpreted as a create-slot overwrite");
            int saveCount = saves.Count, manageCount = management.Count;
            var foreign = new ArchiveApplicationService(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Load, Store = store }).Describe(other.RunId, otherSlot);
            Reject(() => service.Describe(other.RunId), "Save scope cannot browse another dynasty as its current run");
            Reject(() => service.Slots(other.RunId), "Save scope cannot enumerate another dynasty's slots");
            Reject(() => service.Load(foreign, false), "Save scope cannot dispatch another dynasty's load");
            Reject(() => service.Overwrite(foreign), "Save scope cannot overwrite another dynasty's slot");
            Reject(() => service.Rename(foreign, "cross-run"), "Save scope cannot rename another dynasty's slot");
            Reject(() => service.Delete(foreign), "Save scope cannot delete another dynasty's slot");
            Check(!service.Matches(foreign), "A foreign confirmation target returns false instead of escaping the confirmation predicate");
            canWrite = false;
            Check(!service.CanWrite, "Phase change immediately revokes save permission");
            Reject(() => service.Create("locked"), "Locked phase rejects create");
            Reject(() => service.Overwrite(selected), "Locked phase rejects overwrite");
            Reject(() => service.Rename(selected, "locked"), "Locked phase rejects rename");
            Reject(() => service.Delete(selected), "Locked phase rejects delete");
            Reject(() => service.Load(selected, false), "Locked phase rejects load");
            Check(saves.Count == saveCount && management.Count == manageCount && loads == 1, "Rejected cross-run and phase changes never invoke authority callbacks");
            Reject(() => new ArchiveApplicationService(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Save, Store = store, CurrentRun = archive.RunId }), "Save mode rejects missing capability configuration");
        }

        static void StaleConfirmation(RunArchiveStore store, RunArchive archive, string slot)
        {
            int dispatches = 0;
            var service = new ArchiveApplicationService(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Load, Store = store, Load = (_, _, _) => dispatches++ });
            foreach (var path in new[] { store.RunPath(archive.RunId), store.RunPath(archive.RunId) + ".bak", store.SlotPath(archive.RunId, slot) })
            {
                var listing = service.Describe(archive.RunId, path == store.SlotPath(archive.RunId, slot) ? slot : null);
                var modified = RunArchiveCodec.Decode(File.ReadAllBytes(path)); modified.Recovery.KnownIntel = (modified.Recovery.KnownIntel + 1) % 101;
                var originalTime = File.GetLastWriteTimeUtc(path); long originalLength = new FileInfo(path).Length;
                File.WriteAllBytes(path, RunArchiveCodec.Encode(modified)); File.SetLastWriteTimeUtc(path, originalTime);
                Check(new FileInfo(path).Length == originalLength && File.GetLastWriteTimeUtc(path) == originalTime, "Stale-content fixture retains file length and timestamp: " + Path.GetFileName(path));
                Check(!service.Matches(listing), "Content digest detects stale primary, backup or slot: " + Path.GetFileName(path));
                Reject(() => service.Load(listing, false), "Stale file confirmation cannot dispatch load: " + Path.GetFileName(path));
                Check(service.Matches(service.Describe(archive.RunId, listing.Slot)), "Explicit refresh obtains the new content stamp: " + Path.GetFileName(path));
            }
            Check(dispatches == 0, "Every stale-file rejection happens before the application load callback");
        }

        static void SessionExpiry(RunArchiveStore store, RunArchive archive, string slot)
        {
            bool valid = true; int dispatched = 0;
            var service = new ArchiveApplicationService(new ArchiveOpenRequest { Mode = ArchiveOpenMode.Save, Store = store, CurrentRun = archive.RunId,
                IsSessionValid = () => valid, CanWrite = () => true, Save = (_, _, _) => dispatched++, ManageSlot = (_, _, _, _) => dispatched++, Load = (_, _, _) => dispatched++ });
            var selected = service.Describe(archive.RunId, slot); valid = false;
            Check(!service.IsValid && !service.CanWrite && !service.Matches(selected), "Ended gameplay session invalidates permissions and outstanding confirmations");
            Reject(() => service.Runs(), "Expired session cannot enumerate run metadata");
            Reject(() => service.Describe(archive.RunId, slot), "Expired session cannot reopen a slot");
            Reject(() => service.Create("expired"), "Expired session cannot create a slot");
            Reject(() => service.Overwrite(selected), "Expired session cannot overwrite a slot");
            Reject(() => service.Rename(selected, "expired"), "Expired session cannot rename a slot");
            Reject(() => service.Delete(selected), "Expired session cannot delete a slot");
            Reject(() => service.Load(selected, false), "Expired session cannot dispatch load");
            Check(dispatched == 0, "Expired service retains no usable mutation callback");
        }
    }
}
#endif
