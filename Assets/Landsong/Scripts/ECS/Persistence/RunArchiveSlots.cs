using System;
using System.IO;
using System.Linq;
using System.Text;

namespace Landsong.ECS.Persistence
{
    public sealed partial class RunArchiveStore
    {
        string SlotsDirectory(string run) => Path.Combine(RunDirectory(run), "slots");
        string SlotPointer(string run) => Path.Combine(RunDirectory(run), "active-slot.txt");
        public string SlotPath(string run, string slot)
        {
            if (!Guid.TryParseExact(slot, "N", out _))
                throw new InvalidDataException("存档槽 ID 无效。");
            return Path.Combine(SlotsDirectory(run), slot + ".lsrun");
        }

        void CheckLiving(string run)
        {
            if (ManagedFileExists(HistoryPath(run)))
                throw new InvalidDataException("王朝已结束，不能访问其存档槽。");
        }

        public string[] Slots(string run)
        {
            CheckLiving(run);
            var directory = SlotsDirectory(run);
            return Directory.Exists(directory) ? Directory.GetFiles(directory).Select(p => p.EndsWith(".lsrun" + BackupSuffix, StringComparison.Ordinal) ? p.Substring(0, p.Length - BackupSuffix.Length) : p).Where(p => p.EndsWith(".lsrun", StringComparison.Ordinal) && Guid.TryParseExact(Path.GetFileNameWithoutExtension(p), "N", out _)).Distinct().OrderByDescending(File.GetLastWriteTimeUtc).Select(Path.GetFileNameWithoutExtension).ToArray() : Array.Empty<string>();
        }

        public string ActiveSlot(string run)
        {
            CheckLiving(run);
            var path = SlotPointer(run);
            if (!ManagedFileExists(path))
                return null;
            var slot = ReadManagedText(path).Trim();
            return Guid.TryParseExact(slot, "N", out _) && Slots(run).Contains(slot) ? slot : null;
        }

        public void SelectSlot(string run, string slot)
        {
            CheckLiving(run);
            SlotPath(run, slot);
            if (!Slots(run).Contains(slot))
                throw new FileNotFoundException("存档槽不存在。");
            AtomicWrite(SlotPointer(run), Encoding.UTF8.GetBytes(slot));
        }

        public string SaveSlot(RunArchive data, bool create)
        {
            CheckLiving(data.RunId);
            var slot = create ? null : ActiveSlot(data.RunId);
            slot ??= Guid.NewGuid().ToString("N");
            var path = SlotPath(data.RunId, slot);
            var bytes = RunArchiveCodec.Encode(data);
            PreserveCorruptSlot(data.RunId, slot);
            // Slot files never participate in automatic checkpoint writes.
            AtomicWrite(path, bytes);
            SelectSlot(data.RunId, slot);
            return slot;
        }

        public RunArchive ReadSlot(string run, string slot, out bool backup)
        {
            CheckLiving(run);
            var path = SlotPath(run, slot);
            backup = false;
            try
            {
                return ReadOwned(path, run);
            }
            catch (Exception e)when (e is IOException || e is InvalidDataException || e is ArgumentException)
            {
                var result = ReadOwned(path + BackupSuffix, run);
                backup = true;
                return result;
            }
        }

        void DeleteSlots(string run)
        {
            var directory = SlotsDirectory(run);
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.GetFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (name.Length < 38 || !Guid.TryParseExact(name.Substring(0, 32), "N", out _))
                        continue;
                    var suffix = name.Substring(32);
                    if (new[]
                    {
                        ".lsrun",
                        ".lsrun" + BackupSuffix,
                        ".lsrun.tmp",
                        ".lsrun.name",
                        ".lsrun.name" + BackupSuffix,
                        ".lsrun.name.tmp",
                        ".lsrun.png",
                        ".lsrun.png" + BackupSuffix,
                        ".lsrun.png.tmp"
                    }.Contains(suffix) || suffix.StartsWith(".lsrun.corrupt-", StringComparison.Ordinal))
                        DeleteManagedFile(path);
                }

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }

            foreach (var suffix in new[]
            {
                "",
                BackupSuffix,
                ".tmp"
            }

            )
            {
                var path = SlotPointer(run) + suffix;
                if (ManagedFileExists(path))
                    DeleteManagedFile(path);
            }
        }
    }
}
