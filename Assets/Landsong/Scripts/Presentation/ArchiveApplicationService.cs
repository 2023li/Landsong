using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Landsong.ECS.Persistence;

namespace Landsong.ECS.Presentation
{
    /// <summary>Save permissions, file operations and stale confirmation checks live outside the view.</summary>
    public sealed class ArchiveApplicationService
    {
        readonly ArchiveOpenRequest request;
        readonly RunArchiveStore store;
        public ArchiveApplicationService(ArchiveOpenRequest request)
        {
            this.request = request ?? throw new ArgumentNullException(nameof(request));
            store = request.Store ?? CheckpointSystem.DefaultStore;
            if (request.Mode == ArchiveOpenMode.Save && (string.IsNullOrEmpty(request.CurrentRun) || request.CanWrite == null || request.Save == null || request.ManageSlot == null))
                throw new InvalidOperationException("保存界面缺少有效的当前王朝和写入能力。");
        }
        public bool IsValid => request.IsSessionValid == null || request.IsSessionValid();
        public bool CanWrite => IsValid && request.Mode == ArchiveOpenMode.Save && request.CanWrite();
        public string CurrentRun => request.CurrentRun;
        public string[] Runs() { EnsureValid(); return store.Runs(); }
        public string[] Slots(string run) { EnsureRun(run); return store.Slots(run); }
        public string[] Histories() { EnsureValid(); return store.Histories(); }
        public string History(string id) { EnsureValid(); return store.History(id); }
        public ArchiveListing Describe(string run, string slot = null)
        { EnsureRun(run); var listing = store.Describe(run, slot); if (slot == null) listing.Stamp = RunStamp(run); return listing; }
        public bool HasBackup(ArchiveListing info) { EnsureRun(info.Run); return File.Exists((info.Slot == null ? store.RunPath(info.Run) : store.SlotPath(info.Run, info.Slot)) + ".bak"); }
        public byte[] Preview(ArchiveListing info)
        {
            EnsureRun(info.Run); if (info.Slot == null) return null;
            var path = store.PreviewPath(info.Run, info.Slot);
            if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024) return null;
            return File.ReadAllBytes(path);
        }
        public bool Matches(ArchiveListing info)
        {
            if (!IsValid) return false;
            try { EnsureRun(info.Run); return (info.Slot == null ? RunStamp(info.Run) : store.SlotStamp(info.Run, info.Slot)) == info.Stamp; }
            catch (IOException) { return false; }
            catch (InvalidOperationException) { return false; }
        }
        public void Load(ArchiveListing info, bool backup)
        {
            EnsureCurrent(info);
            if (request.Mode == ArchiveOpenMode.Save && !CanWrite) throw new InvalidOperationException("当前阶段不能载入存档。");
            if (request.Load == null) throw new InvalidOperationException("没有配置载入操作。");
            request.Load(info.Run, info.Slot, backup);
        }
        public void Create(string name) { EnsureWrite(); request.Save(null, 0, name); }
        public void Overwrite(ArchiveListing info) { EnsureWrite(); EnsureCurrent(info); if (info.Slot == null) throw new InvalidOperationException("自动节点不能作为独立槽覆盖。"); request.Save(info.Slot, info.Stamp, null); }
        public void Rename(ArchiveListing info, string name)
        {
            EnsureCurrent(info); if (info.Slot == null) throw new InvalidOperationException("自动节点不能重命名。");
            if (request.Mode == ArchiveOpenMode.Save) { EnsureWrite(); request.ManageSlot(info.Slot, name, info.Stamp, false); }
            else store.RenameSlot(info.Run, info.Slot, name, info.Stamp);
        }
        public void Delete(ArchiveListing info)
        {
            EnsureCurrent(info); if (info.Slot == null) throw new InvalidOperationException("自动节点不能作为独立槽删除。");
            if (request.Mode == ArchiveOpenMode.Save) { EnsureWrite(); request.ManageSlot(info.Slot, null, info.Stamp, true); }
            else store.DeleteSlot(info.Run, info.Slot, info.Stamp);
        }
        void EnsureValid() { if (!IsValid) throw new InvalidOperationException("游戏会话已结束，请重新打开存档界面。"); }
        void EnsureRun(string run)
        {
            EnsureValid();
            if (request.Mode == ArchiveOpenMode.Save && run != request.CurrentRun) throw new InvalidOperationException("只能修改当前王朝的存档。");
        }
        void EnsureWrite() { EnsureValid(); if (!CanWrite) throw new InvalidOperationException("当前阶段不能保存或修改存档。"); }
        void EnsureCurrent(ArchiveListing info) { EnsureRun(info.Run); if (!Matches(info)) throw new InvalidOperationException("存档已变化，请刷新后重新确认。"); }
        ulong RunStamp(string run)
        {
            using var hash = SHA256.Create(); var buffer = new byte[65536];
            foreach (var suffix in new[] { "", ".bak" })
            {
                var path = store.RunPath(run) + suffix;
                var header = Encoding.UTF8.GetBytes(suffix + ":" + (File.Exists(path) ? new FileInfo(path).Length : -1) + ";");
                hash.TransformBlock(header, 0, header.Length, header, 0);
                if (!File.Exists(path)) continue;
                using var stream = File.OpenRead(path); int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0) hash.TransformBlock(buffer, 0, read, buffer, 0);
            }
            hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0); return BitConverter.ToUInt64(hash.Hash, 0);
        }
    }
}
