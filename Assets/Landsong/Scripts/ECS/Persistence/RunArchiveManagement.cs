using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Landsong.ECS.Persistence
{
    public sealed class ArchiveListing
    {
        public string Run, Slot, Name, Dynasty, Map, Error; public int Turn; public Phase Phase; public DateTime Saved; public bool Backup, Valid; public ulong Stamp;
        public string Stage => Phase==Phase.Day?"白天":Phase==Phase.Deployment?"夜晚开始":Phase==Phase.GameOver?"王朝终局":"节点";
    }
    public sealed partial class RunArchiveStore
    {
        public string[] Runs()
        {
            var root=Path.Combine(DirectoryPath,"runs");return Directory.Exists(root)?Directory.GetDirectories(root).Select(Path.GetFileName).Where(id=>Guid.TryParseExact(id,"N",out _)&&!File.Exists(HistoryPath(id))).OrderBy(id=>id).ToArray():Array.Empty<string>();
        }
        public string[] Histories()
        {
            var root=Path.Combine(DirectoryPath,"history");return Directory.Exists(root)?Directory.GetFiles(root,"*.txt").Where(p=>Guid.TryParseExact(Path.GetFileNameWithoutExtension(p),"N",out _)).OrderByDescending(File.GetLastWriteTimeUtc).Select(p=>Path.GetFileNameWithoutExtension(p)).ToArray():Array.Empty<string>();
        }
        public string History(string run) => File.ReadAllText(HistoryPath(run));
        public string PreviewPath(string run,string slot)=>SlotPath(run,slot)+".png";
        string NamePath(string run,string slot)=>SlotPath(run,slot)+".name";
        public static string DisplayName(string name)
        {
            if(string.IsNullOrWhiteSpace(name))return "未命名存档";
            var clean=new string(name.Trim().Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').Take(32).ToArray());return string.IsNullOrWhiteSpace(clean)?"未命名存档":clean;
        }
        public ulong SlotStamp(string run,string slot)
        {
            CheckLiving(run);var path=SlotPath(run,slot);using var hash=SHA256.Create();var buffer=new byte[65536];
            foreach(var suffix in new[]{"",".bak",".name"}) {var p=path+suffix;var header=Encoding.UTF8.GetBytes(suffix+":"+(File.Exists(p)?new FileInfo(p).Length:-1)+";");hash.TransformBlock(header,0,header.Length,header,0);if(!File.Exists(p))continue;using var stream=File.OpenRead(p);int read;while((read=stream.Read(buffer,0,buffer.Length))>0)hash.TransformBlock(buffer,0,read,buffer,0);}
            hash.TransformFinalBlock(Array.Empty<byte>(),0,0);return BitConverter.ToUInt64(hash.Hash,0);
        }
        void CheckStamp(string run,string slot,ulong expected)
        {if(!Slots(run).Contains(slot)||expected==0||SlotStamp(run,slot)!=expected)throw new InvalidDataException("存档已变化，请刷新列表后重新确认。");}
        public void RenameSlot(string run,string slot,string name,ulong expected)
        {CheckStamp(run,slot,expected);AtomicWrite(NamePath(run,slot),Encoding.UTF8.GetBytes(DisplayName(name)));}
        public void DeleteSlot(string run,string slot,ulong expected)
        {
            CheckStamp(run,slot,expected);var path=SlotPath(run,slot);
            // Exact owned files only. Removing a slot never ends a dynasty or deletes its nodes.
            foreach(var suffix in new[]{"",".bak",".tmp",".name",".name.bak",".name.tmp",".png",".png.bak",".png.tmp"})if(File.Exists(path+suffix))File.Delete(path+suffix);
            foreach(var p in Directory.GetFiles(SlotsDirectory(run),slot+".lsrun.corrupt-*"))File.Delete(p);
            if(File.Exists(SlotPointer(run))&&File.ReadAllText(SlotPointer(run)).Trim()==slot)foreach(var suffix in new[]{"",".bak",".tmp"})if(File.Exists(SlotPointer(run)+suffix))File.Delete(SlotPointer(run)+suffix);
        }
        public void OverwriteSlot(RunArchive archive,string slot,ulong expected)
        {var bytes=RunArchiveCodec.Encode(archive);CheckStamp(archive.RunId,slot,expected);PreserveCorruptSlot(archive.RunId,slot);AtomicWrite(SlotPath(archive.RunId,slot),bytes);SelectSlot(archive.RunId,slot);}
        void PreserveCorruptSlot(string run,string slot)
        {var path=SlotPath(run,slot);if(!File.Exists(path))return;try{ReadOwned(path,run);}catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException){File.Move(path,path+".corrupt-"+Guid.NewGuid().ToString("N"));}}
        public void Activate(string run,string slot=null)
        {CheckLiving(run);if(slot!=null)SelectSlot(run,slot);AtomicWrite(Pointer,Encoding.UTF8.GetBytes(run));}
        public ArchiveListing Describe(string run,string slot=null,bool backupOnly=false)
        {
            var value=new ArchiveListing {Run=run,Slot=slot,Name=slot==null?"自动节点":File.Exists(NamePath(run,slot))?DisplayName(File.ReadAllText(NamePath(run,slot))):"独立存档",Saved=File.GetLastWriteTime(slot==null?RunPath(run):SlotPath(run,slot))};
            try
            {
                var archive=backupOnly?ReadBackup(run,slot):slot==null?Read(run,out value.Backup):ReadSlot(run,slot,out value.Backup);if(backupOnly)value.Backup=true;
                var summary=SnapshotCodec.ReadSummary(archive.Current);value.Map=summary.Map;value.Dynasty=summary.Session.DynastyName.ToString();value.Turn=summary.Session.Turn;value.Phase=archive.Recovery.AwaitingDecision!=0?Phase.GameOver:summary.Session.Phase;value.Valid=true;if(slot!=null)value.Stamp=SlotStamp(run,slot);if(value.Backup)value.Saved=File.GetLastWriteTime((slot==null?RunPath(run):SlotPath(run,slot))+".bak");
            }catch(Exception e)when(e is IOException||e is InvalidDataException||e is ArgumentException){value.Error=e.Message;if(slot!=null)value.Stamp=SlotStamp(run,slot);}
            return value;
        }
        public RunArchive ReadBackup(string run,string slot=null)
        {CheckLiving(run);return ReadOwned((slot==null?RunPath(run):SlotPath(run,slot))+".bak",run);}
    }
}
