using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Landsong.ECS.Persistence
{
    // Disk envelope only. Snapshots are nodes, not a second live simulation.
    public sealed class RunArchive
    {
        public string RunId;
        public RecoveryState Recovery;
        public byte[] Current, Day, Dusk, Manual, ManualDay;
        public RunArchive Copy() => (RunArchive)MemberwiseClone();
    }
    public static class RunArchiveCodec
    {
        const int Version = 3;
        const int MaxBytes = 256 * 1024 * 1024;
        public static byte[] Encode(RunArchive archive)
        {
            Validate(archive);
            using var payload = new MemoryStream();
            using (var writer = new BinaryWriter(payload, Encoding.UTF8, true))
            {
                writer.Write("LANDSONG-RUN"); writer.Write(Version); writer.Write(archive.RunId);
                writer.Write(archive.Recovery.Turn); writer.Write(archive.Recovery.LossCount); writer.Write(archive.Recovery.KnownIntel);
                writer.Write(archive.Recovery.Seed); writer.Write(archive.Recovery.AwaitingDecision); writer.Write(archive.Recovery.Extinction);
                writer.Write(archive.Recovery.IntelFingerprint); writer.Write(archive.Recovery.IntelReadFingerprint);
                Write(writer, archive.Current); Write(writer, archive.Day); Write(writer, archive.Dusk); Write(writer, archive.Manual); Write(writer, archive.ManualDay);
            }
            var bytes = payload.ToArray();
            if (bytes.Length > MaxBytes) throw new InvalidDataException("王朝记录超过容量限制。");
            using var hash = SHA256.Create(); return bytes.Concat(hash.ComputeHash(bytes)).ToArray();
        }
        public static RunArchive Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 40 || bytes.Length > MaxBytes + 32) throw new InvalidDataException("王朝记录已截断或超过容量限制。");
            var length = bytes.Length - 32; using var hash = SHA256.Create();
            if (!hash.ComputeHash(bytes, 0, length).SequenceEqual(bytes.Skip(length))) throw new InvalidDataException("王朝记录校验失败。");
            using var reader = new BinaryReader(new MemoryStream(bytes, 0, length, false));
            if (reader.ReadString() != "LANDSONG-RUN" || reader.ReadInt32() != Version) throw new InvalidDataException("不是当前基准版本的王朝记录。");
            var result = new RunArchive { RunId = reader.ReadString() };
            result.Recovery = new RecoveryState { Turn = reader.ReadInt32(), LossCount = reader.ReadInt32(), KnownIntel = reader.ReadInt32(), Seed = reader.ReadUInt32(), AwaitingDecision = reader.ReadByte(), Extinction = reader.ReadByte() };
            result.Recovery.IntelFingerprint = reader.ReadUInt64(); result.Recovery.IntelReadFingerprint = reader.ReadUInt64();
            result.Current = Read(reader); result.Day = Read(reader); result.Dusk = Read(reader); result.Manual = Read(reader); result.ManualDay = Read(reader);
            if (reader.BaseStream.Position != length) throw new InvalidDataException("王朝记录存在多余数据。");
            Validate(result); return result;
        }
        static void Validate(RunArchive archive)
        {
            if (archive == null || !Guid.TryParseExact(archive.RunId, "N", out _) || archive.Current == null || archive.Day == null || (archive.Manual == null) != (archive.ManualDay == null)) throw new InvalidDataException("王朝身份或节点不完整。");
            var r = archive.Recovery;
            if (r.Turn < 1 || r.LossCount < 0 || r.KnownIntel < 0 || r.KnownIntel > 100 || r.AwaitingDecision > 1 || r.Extinction > 1 || (r.Extinction != 0 && r.AwaitingDecision == 0) || (r.LossCount > 0 && r.Seed == 0) || (r.AwaitingDecision != 0 && r.Extinction == 0 && (r.LossCount == 0 || archive.Dusk == null))) throw new InvalidDataException("恢复元数据无效。");
        }
        static void Write(BinaryWriter writer, byte[] bytes) { writer.Write(bytes?.Length ?? -1); if (bytes != null) writer.Write(bytes); }
        static byte[] Read(BinaryReader reader)
        {
            var n = reader.ReadInt32(); if (n == -1) return null;
            if (n < 1 || n > MaxBytes || n > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("节点长度无效。");
            return reader.ReadBytes(n);
        }
    }
    // Only owned files under validated per-run directories can be removed.
    public sealed partial class RunArchiveStore
    {
        public readonly string DirectoryPath;
        public Action<string> BeforeCommit; // Instance-scoped fault injection for isolated tests.
        string Pointer => Path.Combine(DirectoryPath, "active-run.txt");
        public RunArchiveStore(string directory) { DirectoryPath = Path.GetFullPath(directory); }
        public string RunDirectory(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _)) throw new InvalidDataException("王朝 ID 无效。");
            return Path.Combine(DirectoryPath, "runs", id);
        }
        public string RunPath(string id) => Path.Combine(RunDirectory(id), "state.lsrun");
        string HistoryPath(string id) { RunDirectory(id); return Path.Combine(DirectoryPath, "history", id + ".txt"); }
        public bool HasContinue => File.Exists(Pointer);
        public RunArchive ReadContinue(out bool backup)
        {
            if (!File.Exists(Pointer)) throw new FileNotFoundException("没有可继续的王朝。");
            return Read(File.ReadAllText(Pointer).Trim(), out backup);
        }
        public RunArchive Read(string id, out bool backup)
        {
            var path = RunPath(id); backup = false;
            if (File.Exists(HistoryPath(id))) throw new InvalidDataException("该王朝已经结束，不能恢复其副本。");
            Exception primary;
            try { return ReadOwned(path, id); }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException) { primary = error; }
            try { var result = ReadOwned(path + ".bak", id); backup = true; return result; }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException) { throw new InvalidDataException("正式记录及备份均无法读取，原文件未修改。", primary); }
        }
        static RunArchive ReadOwned(string path, string id)
        {
            if (new FileInfo(path).Length > 256L * 1024 * 1024 + 32) throw new InvalidDataException("王朝记录超过容量限制。");
            var archive = RunArchiveCodec.Decode(File.ReadAllBytes(path));
            if (archive.RunId != id) throw new InvalidDataException("王朝记录归属不匹配。");
            return archive;
        }
        public void Write(RunArchive archive)
        {
            var bytes = RunArchiveCodec.Encode(archive);
            if (File.Exists(HistoryPath(archive.RunId))) throw new InvalidDataException("不能覆盖已结束的王朝。");
            var path = RunPath(archive.RunId);
            if (File.Exists(path))
            {
                try { ReadOwned(path, archive.RunId); }
                catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException)
                { File.Move(path, path + ".corrupt-" + Guid.NewGuid().ToString("N")); }
            }
            AtomicWrite(path, bytes); AtomicWrite(Pointer, Encoding.UTF8.GetBytes(archive.RunId));
        }
        public void End(string id, string dynastyName, int turn, string reason = "聚落核心失守")
        {
            var directory = RunDirectory(id); var history = HistoryPath(id);
            // Durable tombstone first: interruption cannot resurrect a completed dynasty.
            if (!File.Exists(history)) AtomicWrite(history, Encoding.UTF8.GetBytes(dynastyName + " 延续了 " + turn + " 回合，覆灭于" + reason + "。"));
            DeleteSlots(id);
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (name == "state.lsrun" || name == "state.lsrun.bak" || name == "state.lsrun.tmp" || name.StartsWith("state.lsrun.corrupt-", StringComparison.Ordinal)) File.Delete(path);
                }
                if (!Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
            if (File.Exists(Pointer) && File.ReadAllText(Pointer).Trim() == id)
            { File.Delete(Pointer); if (File.Exists(Pointer + ".bak")) File.Delete(Pointer + ".bak"); }
        }
        public void AtomicWrite(string path, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)); var temporary = path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            BeforeCommit?.Invoke(path);
            if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        }
    }
}
