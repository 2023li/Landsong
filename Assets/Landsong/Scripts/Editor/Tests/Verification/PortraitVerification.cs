#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using Landsong.ECS.Authoring;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Landsong.ECS.Editor
{
    public static class PortraitVerification
    {
        static StringBuilder log;static int count;
        static void Check(bool ok,string label){if(!ok)throw new InvalidOperationException("FAIL "+label);count++;log.AppendLine("PASS "+label);}
        [MenuItem("Landsong/ECS/Verification/Portraits")]
        public static string Run()
        {
            log=new StringBuilder();count=0;
            try{Pixels();People();log.AppendLine("Assertions: "+count);return log.ToString();}
            catch(Exception e){log.AppendLine(e.ToString());throw;}
            finally{File.WriteAllText("Library/LandsongEcs/portraits-verification.txt",log.ToString());}
        }
        static void Pixels()
        {
            var config=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<PortraitConfig>("Assets/Landsong/ECSContent/PortraitConfig.asset"));
            try
            {
                Check(config.Settings.YouthAge==16,"Authored youth threshold defaults to 16");
                foreach(int size in new[]{32,64})
                {
                    config.Resolution=size;using var blob=PortraitLibraryBuilder.Build(config);
                    ref var lib=ref blob.Value;var dna=PortraitOps.Generate(ref lib,123,PersonGender.Female);
                    Check(PortraitOps.Valid(ref lib,dna,PersonGender.Female)&&dna.Parts[(int)PortraitPartType.FacialHair]==0,"Female generation respects part compatibility at "+size);
                    using var young=new NativeArray<Color32>(size*size,Allocator.Temp);using var old=new NativeArray<Color32>(size*size,Allocator.Temp);using var repeat=new NativeArray<Color32>(size*size,Allocator.Temp);
                    PortraitPixels.Compose(ref lib,dna,16,PersonGender.Female,young);PortraitPixels.Compose(ref lib,dna,75,PersonGender.Female,old);PortraitPixels.Compose(ref lib,dna,16,PersonGender.Female,repeat);
                    Check(young.ToArray().SequenceEqual(repeat.ToArray())&&young.ToArray().Any(p=>p.a==255),"Deterministic nonempty pixel composition at "+size);
                    Check(!young.ToArray().SequenceEqual(old.ToArray())&&dna.Hair.Equals(PortraitOps.Generate(ref lib,123,PersonGender.Female).Hair),"Procedural aging changes render without mutating inherited DNA at "+size);
                    PortraitPixels.Compose(ref lib,dna,15,PersonGender.Female,repeat);
                    Check(!young.ToArray().SequenceEqual(repeat.ToArray())&&repeat.ToArray().Any(p=>p.a==255),"Infant placeholder switches at configured youth age at "+size);
                    ulong key=PortraitPixels.Key(ref lib,dna,16,PersonGender.Female);Check(key==PortraitPixels.Key(ref lib,dna,25,PersonGender.Female)&&key!=PortraitPixels.Key(ref lib,dna,75,PersonGender.Female),"Cache uses appearance age bands at "+size);
                    var edited=dna;edited.Eyes=new Color32(1,2,3,255);Check(key!=PortraitPixels.Key(ref lib,edited,16,PersonGender.Female),"Edited color invalidates cache at "+size);
                }
                var red=new Color32(255,0,0,255);var transparent=new Color32(0,255,0,0);var blue=new Color32(0,0,255,128);
                Check(PortraitPixels.Blend(red,transparent).Equals(red),"Transparent pixels preserve background");
                Check(PortraitPixels.Blend(blue,red).Equals(red),"Opaque pixels replace background");
                var blended=PortraitPixels.Blend(red,blue);Check(blended.a==255&&blended.r==127&&blended.b==128,"Partial alpha uses straight alpha composition");
                void Invalid(Action edit,string name){edit();bool rejected=false;try{using var b=PortraitLibraryBuilder.Build(config);}catch(InvalidOperationException){rejected=true;}Check(rejected,name);}
                Invalid(()=>config.Resolution=48,"Non-project portrait resolution rejected");config.Resolution=64;
                Invalid(()=>config.Settings.YouthAge=0,"Invalid youth age rejected");config.Settings=PortraitSettings.Default;
                Invalid(()=>config.SkinColors=new[]{Color.clear},"Transparent palette rejected");
            }
            finally{UnityEngine.Object.DestroyImmediate(config);}
        }
        static void People()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity");using var store=new BlobAssetStore(128);using var world=new World("Portrait verification",WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world,scene.GetRootGameObjects(),store);var em=world.EntityManager;var root=Sim.Root(em);GameLoopSystem.Initialize(em,root);
                Check(PortraitOps.Ready(em,root),"Map baking creates immutable portrait library");
                var monarch=CourtOps.Monarch(em);Check(em.HasComponent<PortraitDNA>(monarch),"Initial royal receives persistent portrait");
                var child=DynastyOps.CreateRoyal(em,root,"丽质测试",2,15,em.GetComponentData<Identity>(monarch).Id);
                ulong id=em.GetComponentData<Identity>(child).Id;uint random=em.GetComponentData<Session>(root).RandomState;
                PortraitOps.Ensure(em,root,child);var before=em.GetComponentData<PortraitDNA>(child);PortraitOps.Ensure(em,root,child);
                Check(before.Equals(em.GetComponentData<PortraitDNA>(child))&&random==em.GetComponentData<Session>(root).RandomState,"Ensure preserves portrait and gameplay random state");
                var adult=em.GetComponentData<Royal>(child);adult.Age=16;em.SetComponentData(child,adult);
                Check(!PortraitOps.CanCustomize(em,root,child),"People without Beauty cannot customize");adult.Age=15;em.SetComponentData(child,adult);
                int beauty=Sim.FindDefinition(em,root,new FixedString128Bytes("gene.beauty"));
                Sim.Buffer<TraitEntry>(em,child);em.GetBuffer<TraitEntry>(child).Add(new TraitEntry{Definition=beauty,Revealed=1,Active=1});
                ResultCode Customize(PortraitDNA dna)=>GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.CustomizePortrait,Target=id,Other=dna.Seed,Text=new FixedString128Bytes(PortraitOps.Payload(dna))});
                Check(!PortraitOps.CanCustomize(em,root,child)&&Customize(before)!=ResultCode.Success,"Beauty cannot customize before youth threshold");
                var royal=em.GetComponentData<Royal>(child);royal.Age=16;em.SetComponentData(child,royal);PortraitOps.Announce(em,root);
                Check(PortraitOps.CanCustomize(em,root,child)&&PersonRequestOps.Pending(em,root,child).Any(r=>r.Kind==PersonRequestKind.Portrait),"Youth beauty becomes a pending personal request");
                int events=em.GetBuffer<HistoryEntry>(root).Length;PortraitOps.Announce(em,root);Check(events==em.GetBuffer<HistoryEntry>(root).Length,"Youth invitation emits once");
                var phase=em.GetComponentData<Session>(root);phase.Paused=1;em.SetComponentData(root,phase);
                Check(Customize(before)==ResultCode.Busy,"Paused command cannot consume customization");phase.Paused=0;phase.Phase=Phase.Night;em.SetComponentData(root,phase);
                Check(Customize(before)==ResultCode.WrongPhase,"Night command cannot consume customization");phase.Phase=Phase.Day;em.SetComponentData(root,phase);
                var obsolete=before;obsolete.Seed++;Check(Customize(obsolete)==ResultCode.Unavailable,"Stale preview seed is rejected");
                var draft=before;draft.Skin=new Color32(33,77,111,255);draft.Hair=new Color32(222,13,99,255);draft.Eyes=new Color32(13,244,127,255);
                var invalid=draft;invalid.Parts[0]=int.MaxValue;Check(Customize(invalid)==ResultCode.InvalidContent&&em.GetComponentData<PortraitDNA>(child).Customized==0,"Unknown part rejected atomically");
                Check(Customize(draft)==ResultCode.Success&&em.GetComponentData<PortraitDNA>(child).Skin.Equals(draft.Skin),"One ECS command commits editable base colors");
                Check(Customize(draft)!=ResultCode.Success&&!PersonRequestOps.Pending(em,root,child).Any(r=>r.Kind==PersonRequestKind.Portrait),"Completed customization cannot reopen or remain pending");
                var mate=DynastyOps.CreateRoyal(em,root,"配色测试",4,22);PortraitOps.Ensure(em,root,mate);
                var mateDNA=em.GetComponentData<PortraitDNA>(mate);mateDNA.Skin=draft.Skin;mateDNA.Hair=draft.Hair;mateDNA.Eyes=draft.Eyes;em.SetComponentData(mate,mateDNA);
                int inherited=0;bool fresh=true;for(int i=0;i<30;i++)
                {var baby=DynastyOps.CreateRoyal(em,root,"颜色子代",2,0,id);PortraitOps.Inherit(em,root,baby,child,mate);var dna=em.GetComponentData<PortraitDNA>(baby);if(dna.Skin.Equals(draft.Skin))inherited++;fresh&=dna.Customized==0;}
                Check(fresh,"Offspring never inherits used customization flag");
                Check(inherited>=20,"Children inherit edited parental base colors with rare independent palette choice");
                var bytes=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,bytes));child=Sim.Find(em,id);
                Check(bytes.SequenceEqual(SnapshotCodec.Capture(em,root))&&em.GetComponentData<PortraitDNA>(child).Customized==1,"Portrait and one-use flags roundtrip exactly");
                bool rollback=false;try{SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,bytes),probe:step=>{if(step=="record-created")throw new IOException("Portrait rollback fixture");});}catch(IOException){rollback=true;}
                Check(rollback&&bytes.SequenceEqual(SnapshotCodec.Capture(em,root)),"Failed restore retains original portrait records");
                var invalidSave=SnapshotCodec.Decode(em,root,bytes);invalidSave.Records.First(r=>r.Identity.Id==id).Portrait.Parts[0]=int.MaxValue;
                bool rejected=false;try{SnapshotCodec.Restore(em,root,invalidSave);}catch(InvalidDataException){rejected=true;}
                Check(rejected&&bytes.SequenceEqual(SnapshotCodec.Capture(em,root)),"Invalid portrait save is rejected without mutation");
                Soldiers(em,root);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Soldiers(EntityManager em,Entity root)
        {
            int definition=Sim.FirstDefinition(em,root,ContentKind.Soldier);var unit=Sim.Spawn(em,root,definition,float3.zero,true);
            Sim.Set(em,unit,new Soldier{Experience=39});MilitaryOps.ConfigureCombatant(em,root,unit,0,false,false,0,float3.zero);MilitaryOps.InitializePerson(em,root,unit);
            ulong id=em.GetComponentData<Identity>(unit).Id;var state=em.GetComponentData<Session>(root);state.Phase=Phase.Day;state.Paused=0;em.SetComponentData(root,state);
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.RenameSoldier,Target=id,Text="老兵见山"})==ResultCode.Success&&em.GetComponentData<SoldierPerson>(unit).SpecialAttention==0,"Renaming never implicitly enables special attention");
            int beforeMemorial=em.GetBuffer<HistoryEntry>(root).Length;MilitaryOps.RememberSoldier(em,root,unit,false);
            Check(em.GetBuffer<HistoryEntry>(root).Length==beforeMemorial&&em.GetComponentData<SoldierPerson>(unit).DeathNotified==0,"Renamed but unwatched soldier has no memorial");
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.SetSoldierAttention,Target=id,Argument=1})==ResultCode.Success,"Explicit special attention is authoritative");
            var attentionBytes=SnapshotCodec.Capture(em,root);var attentionSnapshot=SnapshotCodec.Decode(em,root,attentionBytes);
            Check(attentionSnapshot.Records.Any(r=>r.Identity.Id==id&&r.SoldierPerson.SpecialAttention==1),"Special attention survives snapshot serialization");
            var legacy=SnapshotCodec.CaptureLegacyV23ForVerification(em,root);using(var stream=new System.IO.MemoryStream(legacy,true)){using var reader=new System.IO.BinaryReader(stream,System.Text.Encoding.UTF8,true);reader.ReadString();using var writer=new System.IO.BinaryWriter(stream,System.Text.Encoding.UTF8,true);writer.Write(22);}
            Check(SnapshotCodec.Decode(em,root,legacy).Records.Single(r=>r.Identity.Id==id).SoldierPerson.SpecialAttention==0,"Version 22 name marker never becomes attention during migration");
            GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.SetSoldierAttention,Target=id,Argument=0});MilitaryOps.RememberSoldier(em,root,unit,false);
            Check(em.GetBuffer<HistoryEntry>(root).Length==beforeMemorial,"Unchecking attention suppresses memorial again");
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.SetSoldierAttention,Target=id,Argument=2})==ResultCode.InvalidContent,"Invalid attention toggle value rejected");
            GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.SetSoldierAttention,Target=id,Argument=1});
            var oldDNA=em.GetComponentData<PortraitDNA>(unit);var military=em.GetComponentData<Soldier>(unit);var stats=em.GetComponentData<Combatant>(unit);var health=em.GetComponentData<Health>(unit);
            var person=em.GetComponentData<SoldierPerson>(unit);person.Age=person.Lifespan-1;person.LastAgeTurn=state.Turn-1;em.SetComponentData(unit,person);
            MilitaryOps.AgeSoldiers(em,root);person=em.GetComponentData<SoldierPerson>(unit);
            Check(person.Incarnation==1&&person.Age>=18&&person.Age<=30&&person.SpecialAttention==0&&em.GetComponentData<Identity>(unit).Name.ToString()!="老兵见山"&&em.GetComponentData<PortraitDNA>(unit).Seed!=oldDNA.Seed,"Natural death refreshes age name portrait and resets personal mark");
            Check(military.Equals(em.GetComponentData<Soldier>(unit))&&stats.Equals(em.GetComponentData<Combatant>(unit))&&health.Equals(em.GetComponentData<Health>(unit)),"Natural replacement leaves military attributes untouched");
            Check(em.GetBuffer<HistoryEntry>(root).AsNativeArray().ToArray().Any(h=>h.SourceName.ToString()=="老兵见山"&&h.Category==HistoryCategory.Important&&h.Text.ToString().Contains("遗言")),"Natural memorial freezes original full name in history");
            MilitaryOps.AgeSoldiers(em,root);Check(person.Equals(em.GetComponentData<SoldierPerson>(unit)),"Dawn age settlement is idempotent");
            var saved=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,saved));unit=Sim.Find(em,id);
            Check(saved.SequenceEqual(SnapshotCodec.Capture(em,root)),"Soldier incarnation and appearance persist");
            GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.SetSoldierAttention,Target=id,Argument=1});
            health=em.GetComponentData<Health>(unit);health.Current=0;em.SetComponentData(unit,health);CombatOps.Death(em,root,unit,Entity.Null);int events=em.GetBuffer<HistoryEntry>(root).Length;CombatOps.Death(em,root,unit,Entity.Null);
            Check(em.GetComponentData<SoldierPerson>(unit).DeathNotified==1&&events==em.GetBuffer<HistoryEntry>(root).Length,"Watched, never-renamed replacement emits battle memorial once");
            MilitaryOps.AgeSoldiers(em,root);Check(!Sim.Alive(em,unit)&&em.GetComponentData<SoldierPerson>(unit).Incarnation==1,"Battle death never triggers natural replacement");
            MilitaryOps.Dawn(em,root);Check(Sim.Find(em,id)==Entity.Null,"Battle casualty still removed by existing dawn rules");
        }
    }
}
#endif
