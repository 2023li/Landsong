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
        const string Format = "LANDSONG-RUN-ES3";
        const int Version = 4;
        const int MaxBytes = 256 * 1024 * 1024;

        public sealed class ArchiveEnvelope
        {
            public string Format;
            public int Version;
            public byte[] Payload;
            public byte[] Sha256;
        }

        public sealed class ArchivePayload
        {
            public string RunId;
            public int Turn, LossCount, KnownIntel;
            public uint Seed;
            public byte AwaitingDecision, Extinction;
            public ulong IntelFingerprint, IntelReadFingerprint;
            public byte[] Current, Day, Dusk, Manual, ManualDay;
        }

        public static byte[] Encode(RunArchive archive)
        {
            Validate(archive);
            var payload = new ArchivePayload
            {
                RunId = archive.RunId,
                Turn = archive.Recovery.Turn,
                LossCount = archive.Recovery.LossCount,
                KnownIntel = archive.Recovery.KnownIntel,
                Seed = archive.Recovery.Seed,
                AwaitingDecision = archive.Recovery.AwaitingDecision,
                Extinction = archive.Recovery.Extinction,
                IntelFingerprint = archive.Recovery.IntelFingerprint,
                IntelReadFingerprint = archive.Recovery.IntelReadFingerprint,
                Current = archive.Current,
                Day = archive.Day,
                // ES3 3.5.26's JSON byte[] reader cannot round-trip a null value.
                // Empty arrays are an unambiguous disk sentinel because every real snapshot is non-empty.
                Dusk = archive.Dusk ?? Array.Empty<byte>(),
                Manual = archive.Manual ?? Array.Empty<byte>(),
                ManualDay = archive.ManualDay ?? Array.Empty<byte>()
            };
            var payloadBytes = ES3.Serialize(payload, SerializationSettings(false));
            using var hash = SHA256.Create();
            var bytes = ES3.Serialize(new ArchiveEnvelope
            {
                Format = Format,
                Version = Version,
                Payload = payloadBytes,
                Sha256 = hash.ComputeHash(payloadBytes)
            }, SerializationSettings(true));
            if (bytes.Length > MaxBytes)
                throw new InvalidDataException("王朝记录超过容量限制。");
            return bytes;
        }

        public static RunArchive Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 16 || bytes.Length > MaxBytes)
                throw new InvalidDataException("王朝记录已截断或超过容量限制。");
            try
            {
                var envelope = ES3.Deserialize<ArchiveEnvelope>(bytes, SerializationSettings(true));
                if (envelope == null || envelope.Format != Format || envelope.Version != Version || envelope.Payload == null
                    || envelope.Payload.Length > MaxBytes || envelope.Sha256 == null || envelope.Sha256.Length != 32)
                    throw new InvalidDataException("不是当前 ES3 基准版本的王朝记录。");
                using var hash = SHA256.Create();
                if (!hash.ComputeHash(envelope.Payload).SequenceEqual(envelope.Sha256))
                    throw new InvalidDataException("王朝记录校验失败。");
                var payload = ES3.Deserialize<ArchivePayload>(envelope.Payload, SerializationSettings(false));
                if (payload == null)
                    throw new InvalidDataException("王朝记录内容为空。");
                var result = new RunArchive
                {
                    RunId = payload.RunId,
                    Recovery = new RecoveryState
                    {
                        Turn = payload.Turn,
                        LossCount = payload.LossCount,
                        KnownIntel = payload.KnownIntel,
                        Seed = payload.Seed,
                        AwaitingDecision = payload.AwaitingDecision,
                        Extinction = payload.Extinction,
                        IntelFingerprint = payload.IntelFingerprint,
                        IntelReadFingerprint = payload.IntelReadFingerprint
                    },
                    Current = payload.Current,
                    Day = payload.Day,
                    Dusk = OptionalSnapshot(payload.Dusk),
                    Manual = OptionalSnapshot(payload.Manual),
                    ManualDay = OptionalSnapshot(payload.ManualDay)
                };
                Validate(result);
                return result;
            }
            catch (OutOfMemoryException)
            {
                throw;
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception error)
            {
                throw new InvalidDataException("王朝记录不是有效的 ES3 数据。", error);
            }
        }

        static ES3Settings SerializationSettings(bool compress) => new ES3Settings(false)
        {
            compressionType = compress ? ES3.CompressionType.Gzip : ES3.CompressionType.None,
            encryptionType = ES3.EncryptionType.None,
            format = ES3.Format.JSON,
            prettyPrint = false,
            referenceMode = ES3.ReferenceMode.ByValue,
            memberReferenceMode = ES3.ReferenceMode.ByValue,
            typeChecking = true,
            safeReflection = true
        };

        static byte[] OptionalSnapshot(byte[] bytes) => bytes == null || bytes.Length == 0 ? null : bytes;

        static void Validate(RunArchive archive)
        {
            if (archive == null || !Guid.TryParseExact(archive.RunId, "N", out _) || archive.Current == null || archive.Day == null || (archive.Manual == null) != (archive.ManualDay == null))
                throw new InvalidDataException("王朝身份或节点不完整。");
            var r = archive.Recovery;
            if (r.Turn < 1 || r.LossCount < 0 || r.KnownIntel < 0 || r.KnownIntel > 100 || r.AwaitingDecision > 1 || r.Extinction > 1 || (r.Extinction != 0 && r.AwaitingDecision == 0) || (r.LossCount > 0 && r.Seed == 0) || (r.AwaitingDecision != 0 && r.Extinction == 0 && (r.LossCount == 0 || archive.Dusk == null)))
                throw new InvalidDataException("恢复元数据无效。");
        }

    }

    // Only owned files under validated per-run directories can be removed.
    public sealed partial class RunArchiveStore
    {
        // Easy Save 3's persistent backup suffix (ES3IO.backupFileSuffix).
        public const string BackupSuffix = ".bac";
        public readonly string DirectoryPath;
        public Action<string> BeforeCommit; // Instance-scoped fault injection for isolated tests.
        string Pointer => Path.Combine(DirectoryPath, "active-run.txt");

        public RunArchiveStore(string directory)
        {
            DirectoryPath = Path.GetFullPath(directory);
        }

        public string RunDirectory(string id)
        {
            if (!Guid.TryParseExact(id, "N", out _))
                throw new InvalidDataException("王朝 ID 无效。");
            return Path.Combine(DirectoryPath, "runs", id);
        }

        public string RunPath(string id) => Path.Combine(RunDirectory(id), "state.lsrun");
        string HistoryPath(string id)
        {
            RunDirectory(id);
            return Path.Combine(DirectoryPath, "history", id + ".txt");
        }

        public bool HasContinue => ManagedFileExists(Pointer);

        public RunArchive ReadContinue(out bool backup)
        {
            if (!ManagedFileExists(Pointer))
                throw new FileNotFoundException("没有可继续的王朝。");
            var id = ReadManagedText(Pointer).Trim();
            return Read(id, out backup);
        }

        public RunArchive Read(string id, out bool backup)
        {
            var path = RunPath(id);
            backup = false;
            if (ManagedFileExists(HistoryPath(id)))
                throw new InvalidDataException("该王朝已经结束，不能恢复其副本。");
            Exception primary;
            try
            {
                return ReadOwned(path, id);
            }
            catch (Exception error)when (error is IOException || error is InvalidDataException || error is ArgumentException)
            {
                primary = error;
            }

            try
            {
                var result = ReadOwned(path + BackupSuffix, id);
                backup = true;
                return result;
            }
            catch (Exception error)when (error is IOException || error is InvalidDataException || error is ArgumentException)
            {
                throw new InvalidDataException("正式记录及备份均无法读取，原文件未修改。", primary);
            }
        }

        RunArchive ReadOwned(string path, string id)
        {
            if (new FileInfo(path).Length > 256L * 1024 * 1024)
                throw new InvalidDataException("王朝记录超过容量限制。");
            var archive = RunArchiveCodec.Decode(ES3.LoadRawBytes(FileSettings(path)));
            if (archive.RunId != id)
                throw new InvalidDataException("王朝记录归属不匹配。");
            return archive;
        }

        public void Write(RunArchive archive)
        {
            var bytes = RunArchiveCodec.Encode(archive);
            if (ManagedFileExists(HistoryPath(archive.RunId)))
                throw new InvalidDataException("不能覆盖已结束的王朝。");
            var path = RunPath(archive.RunId);
            if (ManagedFileExists(path))
            {
                try
                {
                    ReadOwned(path, archive.RunId);
                }
                catch (Exception error)when (error is IOException || error is InvalidDataException || error is ArgumentException)
                {
                    RenameManagedFile(path, path + ".corrupt-" + Guid.NewGuid().ToString("N"));
                }
            }

            AtomicWrite(path, bytes);
            AtomicWrite(Pointer, Encoding.UTF8.GetBytes(archive.RunId));
        }

        public void End(string id, string dynastyName, int turn, string reason = "聚落核心失守")
        {
            var directory = RunDirectory(id);
            var history = HistoryPath(id);
            // Durable tombstone first: interruption cannot resurrect a completed dynasty.
            if (!ManagedFileExists(history))
                AtomicWrite(history, Encoding.UTF8.GetBytes(dynastyName + " 延续了 " + turn + " 回合，覆灭于" + reason + "。"));
            DeleteSlots(id);
            if (Directory.Exists(directory))
            {
                foreach (var path in Directory.EnumerateFiles(directory))
                {
                    var name = Path.GetFileName(path);
                    if (name == "state.lsrun" || name == "state.lsrun.bac" || name == "state.lsrun.tmp" || name.StartsWith("state.lsrun.corrupt-", StringComparison.Ordinal))
                        DeleteManagedFile(path);
                }

                if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    Directory.Delete(directory);
            }

            if (ManagedFileExists(Pointer) && ReadManagedText(Pointer).Trim() == id)
            {
                DeleteManagedFile(Pointer);
                if (ManagedFileExists(Pointer + BackupSuffix))
                    DeleteManagedFile(Pointer + BackupSuffix);
            }
        }

        public void AtomicWrite(string path, byte[] bytes)
        {
            path = OwnedPath(path);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var settings = FileSettings(path);
            BeforeCommit?.Invoke(path);
            if (ES3.FileExists(settings))
                ES3.CreateBackup(settings);
            ES3.SaveRaw(bytes, settings);
        }

        public bool ManagedFileExists(string path) => ES3.FileExists(FileSettings(path));

        public byte[] ReadManagedBytes(string path, long maximumBytes = 256L * 1024 * 1024)
        {
            path = OwnedPath(path);
            if (!ManagedFileExists(path))
                throw new FileNotFoundException("受管存档文件不存在。", path);
            if (new FileInfo(path).Length > maximumBytes)
                throw new InvalidDataException("受管存档文件超过容量限制。");
            return ES3.LoadRawBytes(FileSettings(path));
        }

        public string ReadManagedText(string path) => Encoding.UTF8.GetString(ReadManagedBytes(path, 1024 * 1024));

        public DateTime ManagedTimestamp(string path) => ES3.GetTimestamp(FileSettings(path));

        void DeleteManagedFile(string path) => ES3.DeleteFile(FileSettings(path));

        void RenameManagedFile(string path, string destination) => ES3.RenameFile(FileSettings(path), FileSettings(destination));

        string OwnedPath(string path)
        {
            var full = Path.GetFullPath(path);
            var root = DirectoryPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("存档路径不属于当前受管目录。");
            return full;
        }

        ES3Settings FileSettings(string path) => new ES3Settings(OwnedPath(path), ES3.Location.File)
        {
            compressionType = ES3.CompressionType.None,
            encryptionType = ES3.EncryptionType.None,
            autoCacheDefaultFile = false,
            autoCacheFileOnLoad = false
        };

    }
}
