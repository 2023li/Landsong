#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Persistence;
using Landsong.ECS.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Landsong.ECS.Editor
{
    public static class InterfaceArchiveVerification
    {
        static StringBuilder log;static int count;
        static void Check(bool ok,string name){if(!ok)throw new InvalidOperationException("FAIL "+name);count++;log.AppendLine("PASS "+name);}
        static void Reject(Action action,string name){bool rejected=false;try{action();}catch(IOException){rejected=true;}catch(InvalidDataException){rejected=true;}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
        [MenuItem("Landsong/ECS/Verification/InterfaceArchive")]
        public static string Run()
        {
            log=new StringBuilder();count=0;
            try{Preferences();Simulation();log.AppendLine("Assertions: "+count);return log.ToString();}
            catch(Exception error){log.AppendLine(error.ToString());throw;}
            finally{Directory.CreateDirectory("Library/LandsongEcs");File.WriteAllText("Library/LandsongEcs/interface-verification.txt",log.ToString());}
        }
        static void Preferences()
        {
            var p=new InterfacePreferences {Master=float.NaN,CameraSpeed=float.PositiveInfinity,UiScale=9,Width=-1,Height=100,Forward=Key.Escape};p.Validate();
            Check(p.Master==1&&p.CameraSpeed==18&&p.UiScale==1.4f&&p.Width==0&&p.Height==0,"Preference finite/range normalization");
            Check(p.Forward==Key.W&&!InterfacePreferences.AllowedKey(Key.Escape)&&!InterfacePreferences.AllowedKey(Key.R),"Reserved hotkeys stay reserved");
            p.Forward=Key.D;p.Validate();Check(p.Forward==Key.W,"Duplicate keyboard bindings reset safely");
            var round=InterfaceSettings.Decode(JsonUtility.ToJson(new InterfacePreferences {Music=.3f,ReducedMotion=true,Forward=Key.UpArrow}));
            Check(round.Music==.3f&&round.ReducedMotion&&round.Forward==Key.UpArrow,"Preferences JSON roundtrip without touching player preferences");
            Check(InterfaceSettings.Decode("bad json").Master==1,"Corrupt settings safely use defaults");
            Check(RunArchiveStore.DisplayName(" <国库>\n ")=="国库","Names strip rich text delimiters and control characters");
            Check(RunArchiveStore.DisplayName(new string('名',100)).Length==32,"Names have bounded UTF-8 safe length");
            Check(RunArchiveStore.DisplayName(" ")=="未命名存档","Empty slot name fallback");
        }
        static void Simulation()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");using var blobs=new BlobAssetStore(128);using var world=new World("Wave fourteen isolated",WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world,scene.GetRootGameObjects(),blobs);var em=world.EntityManager;var root=Sim.Root(em);GameLoopSystem.Initialize(em,root);
                var s=em.GetComponentData<Session>(root);s.CheckpointPending=0;s.DynastyName="验收王朝";em.SetComponentData(root,s);
                var original=SnapshotCodec.Capture(em,root);var summary=SnapshotCodec.ReadSummary(original);
                Check(summary.Map=="Map_Test2"&&summary.Session.DynastyName.ToString()=="验收王朝","Archive summary reads authored map, turn and dynasty");
                Reject(()=>SnapshotCodec.ReadSummary(new byte[4]),"Malformed summary rejected");
                var core=Sim.OrderedEntities<Building>(em);var source=core[0];core.Dispose();var id=em.GetComponentData<Identity>(source);var gold=em.GetComponentData<GameSettings>(root).Gold;
                Sim.Emit(em,root,EventKind.Message,"测试来源消息",id.Id);Sim.Emit(em,root,EventKind.Message,"测试来源消息",id.Id);
                var message=em.GetBuffer<HistoryEntry>(root)[em.GetBuffer<HistoryEntry>(root).Length-1];Check(message.Count==2&&message.Source==id.Id&&message.SourceName.Equals(id.Name),"Same-turn identical messages merge with stable source snapshot");
                var renamed=id;renamed.Name="之后改名";em.SetComponentData(source,renamed);Check(message.SourceName.Equals(id.Name),"History source name never follows later rename");em.SetComponentData(source,id);
                int before=em.GetBuffer<HistoryEntry>(root).Length;Sim.Emit(em,root,EventKind.Theft,"隐藏货物",id.Id,definition:gold,amount:5);
                Check(em.GetBuffer<HistoryEntry>(root).Length==before,"Theft payload cannot enter live message history");
                var nonspatial=em.CreateEntity();em.AddComponentData(nonspatial,new Identity {Id=9999,Name="非空间来源"});Sim.Emit(em,root,EventKind.Message,"无坐标消息",9999);Check(em.GetBuffer<HistoryEntry>(root)[em.GetBuffer<HistoryEntry>(root).Length-1].HasPosition==0,"Nonspatial talent/royal source has no phantom position");em.DestroyEntity(nonspatial);before=em.GetBuffer<HistoryEntry>(root).Length;
                EconomyJournalOps.Begin(em,root,true);using(EconomyJournalOps.For(em,root,source,EconomyReason.Production))EconomyJournalOps.Record(em,root,gold,10);EconomyJournalOps.End(em,root);
                Check(em.GetBuffer<HistoryEntry>(root).Length==before,"Forecast does not publish phantom history");
                EconomyJournalOps.Begin(em,root,false);using(EconomyJournalOps.For(em,root,source,EconomyReason.NaturalLoss))EconomyJournalOps.Record(em,root,gold,-2);EconomyJournalOps.End(em,root);
                Check(em.GetBuffer<HistoryEntry>(root).Length==before+1&&em.GetBuffer<HistoryEntry>(root)[before].Delta==-2,"Committed settlement history preserves cause and amount");
                using(HistoryOps.ForCommand(em,root,new Command {Kind=CommandKind.Discard,Target=id.Id}))
                {
                    using(var transaction=new InventoryTransaction(em,root)){EconomyJournalOps.Record(em,root,gold,-3);}
                    Check(em.GetBuffer<HistoryEntry>(root).Length==before+1,"Rejected resource transaction removes history rows");
                    EconomyJournalOps.Record(em,root,gold,-3);Check(em.GetBuffer<HistoryEntry>(root)[before+1].Text.ToString()=="主动丢弃","Manual resource history has explicit reason");
                }
                EconomyJournalOps.Begin(em,root,false);using(EconomyJournalOps.For(em,root,source,EconomyReason.CapacityTransfer))EconomyJournalOps.Record(em,root,gold,4);EconomyJournalOps.End(em,root);
                Check(em.GetBuffer<HistoryEntry>(root)[em.GetBuffer<HistoryEntry>(root).Length-1].Transfer==1,"Internal capacity transfers are not economic income");
                using(HistoryOps.ForCommand(em,root,new Command {Kind=CommandKind.StorePending}))EconomyJournalOps.Record(em,root,gold,4);
                Check(em.GetBuffer<HistoryEntry>(root)[em.GetBuffer<HistoryEntry>(root).Length-1].Transfer==1,"Manual pending storage is not economic income");
                foreach(var kind in new[]{CommandKind.StorePending,CommandKind.StorePendingSlot,CommandKind.Harvest})
                {
                    using(HistoryOps.ForCommand(em,root,new Command {Kind=kind}))
                    {
                        var context=em.GetComponentData<ManualHistoryContext>(root);
                        context.Reason=kind==CommandKind.Harvest?"待存放转库":"Localized storage action";
                        em.SetComponentData(root,context);EconomyJournalOps.Record(em,root,gold,4);
                    }
                    var row=em.GetBuffer<HistoryEntry>(root)[em.GetBuffer<HistoryEntry>(root).Length-1];
                    Check(row.Transfer==(kind==CommandKind.Harvest?0:1),"Manual transfer classification follows command kind, independent of reason text: "+kind);
                    Check(em.GetComponentData<ManualHistoryContext>(root).Active==0,"Manual history context is released after "+kind);
                }
                var saved=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,saved));Check(saved.SequenceEqual(SnapshotCodec.Capture(em,root)),"History survives exact snapshot roundtrip");
                foreach(var point in new[]{"root-reset","root-published"})
                {Reject(()=>SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original),probe:stage=>{if(stage==point)throw new InvalidOperationException("owned fault");}),"History restore fault "+point);Check(saved.SequenceEqual(SnapshotCodec.Capture(em,root)),"History/root rollback exact "+point);}
                var invalid=SnapshotCodec.Decode(em,root,saved);invalid.History[0].Position=new float3(float.NaN);Reject(()=>SnapshotCodec.Restore(em,root,invalid),"Nonfinite history position rejected before publication");
                for(int i=0;i<2200;i++)em.GetBuffer<HistoryEntry>(root).Add(new HistoryEntry {Turn=1,Item=-1,Count=1,Text="有界历史"});HistoryOps.Trim(em,root);Check(em.GetBuffer<HistoryEntry>(root).Length==HistoryOps.Limit,"History bounded independently of UI reading");
                SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));
                var cameraObject=new GameObject("Owned camera",typeof(Camera));try{var camera=cameraObject.GetComponent<Camera>();camera.transform.rotation=Quaternion.Euler(55,40,0);camera.transform.position=new Vector3(100000,30,100000);var grid=em.GetComponentData<GridData>(root);var clamped=UI_GamePanel_WorldInteraction.ClampCameraPosition(camera,grid,camera.transform.position);Check(math.all(math.isfinite((float3)clamped))&&clamped.x<100000&&clamped.z<100000,"Camera focus clamped to authored map rectangle");}finally{UnityEngine.Object.DestroyImmediate(cameraObject);}
                Archives(world,root);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Archives(World world,Entity root)
        {
            string directory=Path.Combine(Path.GetTempPath(),"Landsong-Wave14-"+Guid.NewGuid().ToString("N"));var store=new RunArchiveStore(directory);var checkpoint=world.GetOrCreateSystemManaged<CheckpointSystem>();checkpoint.Store=store;var archive=checkpoint.Export(root);
            try
            {
                store.Write(archive);var first=store.SaveSlot(archive,true);var second=store.SaveSlot(archive,true);var other=archive.Copy();other.RunId=Guid.NewGuid().ToString("N");store.Write(other);var foreign=store.SaveSlot(other,true);
                Check(store.Runs().Length==2&&store.Slots(archive.RunId).Length==2,"Cross-dynasty browser lists independent ownership");
                var described=store.Describe(archive.RunId,first);Check(described.Valid&&described.Turn==1&&described.Dynasty=="验收王朝"&&described.Stamp!=0,"Slot metadata from checksum-validated archive");
                var bytes=File.ReadAllBytes(store.SlotPath(archive.RunId,first));store.RenameSlot(archive.RunId,first,"国库",described.Stamp);Check(store.Describe(archive.RunId,first).Name=="国库"&&bytes.SequenceEqual(File.ReadAllBytes(store.SlotPath(archive.RunId,first))),"Rename changes only display metadata");
                Reject(()=>store.DeleteSlot(archive.RunId,first,described.Stamp),"Stale delete confirmation refused after rename");
                var fresh=store.Describe(archive.RunId,first);store.BeforeCommit=_=>throw new IOException("owned IO fault");Reject(()=>store.OverwriteSlot(archive,first,fresh.Stamp),"Overwrite IO failure propagated");store.BeforeCommit=null;
                Check(bytes.SequenceEqual(File.ReadAllBytes(store.SlotPath(archive.RunId,first))),"Failed overwrite preserves original bytes");
                store.OverwriteSlot(archive,first,fresh.Stamp);Check(File.Exists(store.SlotPath(archive.RunId,first)+".bak")&&store.ActiveSlot(archive.RunId)==first,"Confirmed overwrite retains backup and selects slot");
                File.WriteAllBytes(store.SlotPath(archive.RunId,first),new byte[]{1,2,3});var corrupt=store.Describe(archive.RunId,first);Check(corrupt.Valid&&corrupt.Backup,"Damaged primary is explicitly described as backup recovery");
                store.SaveSlot(archive,false);Check(Directory.GetFiles(Path.GetDirectoryName(store.SlotPath(archive.RunId,first)),first+".lsrun.corrupt-*").Length==1&&store.ReadBackup(archive.RunId,first)!=null,"Quick save preserves damaged primary and usable backup");
                File.Delete(store.SlotPath(archive.RunId,first));Check(store.Slots(archive.RunId).Contains(first)&&store.Describe(archive.RunId,first).Valid,"Orphan valid backup remains discoverable");
                store.OverwriteSlot(archive,first,store.SlotStamp(archive.RunId,first));store.AtomicWrite(store.PreviewPath(archive.RunId,first),new byte[]{1});
                store.DeleteSlot(archive.RunId,first,store.SlotStamp(archive.RunId,first));Check(!store.Slots(archive.RunId).Contains(first)&&store.ActiveSlot(archive.RunId)==null&&!File.Exists(store.PreviewPath(archive.RunId,first)),"Delete current slot removes only its owned files and pointer");
                Check(store.Read(archive.RunId,out _)!=null&&store.ReadSlot(archive.RunId,second,out _)!=null&&store.ReadSlot(other.RunId,foreign,out _)!=null,"Delete preserves automatic nodes, other slots and other dynasty");
                Reject(()=>store.SlotPath(archive.RunId,"../escape"),"Slot path traversal rejected");Reject(()=>store.RunDirectory("../escape"),"Run path traversal rejected");
                store.End(archive.RunId,"验收王朝",123,"验收原因");Check(store.Histories().Contains(archive.RunId)&&store.History(archive.RunId).Contains("123")&&store.History(archive.RunId).Contains("验收原因"),"Dynasty tombstone preserves name, longevity and cause");
                Check(!store.Runs().Contains(archive.RunId)&&store.ReadSlot(other.RunId,foreign,out _)!=null,"Ending hides only ended dynasty and protects foreign saves");
                Reject(()=>store.ReadSlot(archive.RunId,second,out _),"Ended dynasty copies cannot revive through slot browser");
            }
            finally{if(Directory.Exists(directory))Directory.Delete(directory,true);}
        }
    }
}
#endif
