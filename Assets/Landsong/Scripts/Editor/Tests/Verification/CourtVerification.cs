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
    public static partial class CourtVerification
    {
        static StringBuilder log; static int checks;
        static void Check(bool value,string label) { if(!value) throw new InvalidOperationException("FAIL "+label); checks++; log.AppendLine("PASS "+label); }
        [MenuItem("Landsong/ECS/Verification/Court")]
        public static string Run()
        {
            log=new StringBuilder(); checks=0;
            try { Configuration(); Map(); log.AppendLine("Assertions: "+checks); return log.ToString(); }
            catch(Exception e) { log.AppendLine(e.ToString()); throw; }
            finally { Directory.CreateDirectory("Library/LandsongEcs"); File.WriteAllText("Library/LandsongEcs/court-verification.txt",log.ToString()); }
        }
        static void Configuration()
        {
            var c=AssetDatabase.LoadAssetAtPath<GameCatalogAsset>("Assets/Landsong/ECSContent/GameCatalog.asset"); CourtContentValidation.Validate(c);
            Check(c.Content.Count(d=>d.Kind==ContentKind.Talent)>=3,"Three authored placeholder talents");
            foreach(var id in new[]{"gene.fate","gene.long_life","gene.military","gene.agriculture","gene.scholar","talent.slot1","talent.slot2","talent.slot3","policy.palace_guard","policy.court_dialogue"}) Check(c.Find(id)>=0,"Runnable authored definition "+id);
            var copy=UnityEngine.Object.Instantiate(c);
            try { var q=copy.Court; q.DeathAdult=float.NaN; copy.Court=q; bool rejected=false; try{CourtContentValidation.Validate(copy);}catch(InvalidOperationException){rejected=true;} Check(rejected,"NaN mortality rejected"); }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }
        static void Map()
        {
            var scene=EditorSceneManager.OpenPreviewScene("Assets/Landsong/Scenes/EntityMaps/Map_Test2_Entities.unity"); using var store=new BlobAssetStore(128); using var world=new World("Wave nine verification",WorldFlags.Game);
            try
            {
                EcsVerification.Bake(world,scene.GetRootGameObjects(),store); var em=world.EntityManager; var root=Sim.Root(em); GameLoopSystem.Initialize(em,root);
                int Def(string id)=>Sim.FindDefinition(em,root,new FixedString128Bytes(id));
                ulong Id(Entity e)=>em.GetComponentData<Identity>(e).Id;
                Entity Person(string id) { using var all=Sim.OrderedEntities<Talent>(em); foreach(var e in all) if(em.GetComponentData<Identity>(e).Definition==Def(id)) return e; throw new InvalidOperationException(id); }
                void Turn(int turn) { var s=em.GetComponentData<Session>(root); s.Turn=turn; s.Phase=Phase.Day; s.CheckpointPending=0; em.SetComponentData(root,s); }
                void Stock(int d,int n) { InventoryOps.Remove(em,root,d,InventoryOps.Count(em,root,d)); Check(InventoryOps.Add(em,root,d,n)==n,"Fixture stock "+n); }
                ResultCode Cmd(CommandKind k,ulong id=0,int definition=-1,int argument=0)=>GameLoopSystem.Execute(em,root,new Command { Kind=k,Target=id,Definition=definition,Argument=argument });
                void State(Entity e,ActionRef<Royal> change) { var r=em.GetComponentData<Royal>(e); change(ref r); em.SetComponentData(e,r); }
                var gold=em.GetComponentData<GameSettings>(root).Gold; Stock(gold,100); Stock(Def("原木"),50);
                var original=SnapshotCodec.Capture(em,root);
                void Reset() { SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original)); }
                FamilyRules(em,root); Reset();
                PersonRequests(em,root); Reset();
                var person=Person("talent.placeholder1"); var pid=Id(person);
                Check(Cmd(CommandKind.RecruitTalent,pid)==ResultCode.Unavailable,"Recruitment gated by affection");
                Check(Cmd(CommandKind.GiftPerson,pid)==ResultCode.Success,"Gift consumes resources");
                var afterGift=InventoryOps.Count(em,root,gold); Check(Cmd(CommandKind.GiftPerson,pid)==ResultCode.Unavailable && InventoryOps.Count(em,root,gold)==afterGift,"One gift per turn; retry does not spend");
                Check(Cmd(CommandKind.CompleteSocialTask,pid)==ResultCode.Success,"Personal task raises affection");
                Check(Cmd(CommandKind.CompleteSocialTask,pid)==ResultCode.Unavailable,"Personal task cannot repeat");
                Check(Cmd(CommandKind.RecruitTalent,pid)==ResultCode.Success && Id(person)==pid,"Recruitment keeps same person ID");
                Check(EffectOps.Modifier(em,root,RuleKind.SoldierAttackBonus,-1)==0,"Unassigned recruitment grants no bonus");
                Check(Cmd(CommandKind.AssignTalent,pid,Def("talent.slot2"))==ResultCode.Unavailable,"Wrong profession rejected");
                Check(Cmd(CommandKind.AssignTalent,pid,Def("talent.slot1"))==ResultCode.Success,"Paid profession appointment");
                Check(math.abs(EffectOps.Modifier(em,root,RuleKind.SoldierAttackBonus,-1)-.1f)<.0001f,"Talent one exactly ten percent; personal genes do not double stack");
                Check(EffectOps.Modifier(em,root,RuleKind.AttackBonus,-1)==0,"Talent soldier effect does not leak to hero generic damage");
                var wageBefore=InventoryOps.Count(em,root,gold); SocialOps.PayWages(em,root); Check(InventoryOps.Count(em,root,gold)==wageBefore,"Entry wage not charged again in same turn");
                Turn(2); Stock(gold,0); SocialOps.PayWages(em,root); Check(EffectOps.Modifier(em,root,RuleKind.SoldierAttackBonus,-1)==0,"Unpaid bonus disabled before production");
                Stock(gold,100); Turn(3); SocialOps.PayWages(em,root); Check(em.GetComponentData<Talent>(person).Paid==1,"Salary resumes next settlement");
                var snap=SnapshotCodec.Capture(em,root); SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,snap)); person=Sim.Find(em,pid);
                Check(snap.SequenceEqual(SnapshotCodec.Capture(em,root)),"v9 roundtrip keeps wages affection task and traits");
                Check(Cmd(CommandKind.DismissTalent,pid)==ResultCode.Success && em.Exists(person) && em.GetComponentData<Royal>(person).Affection==40,"Dismiss retains persistent contact and affection");
                Reset(); var king=CourtOps.Monarch(em); var kingId=Id(king);
                var child=DynastyOps.CreateRoyal(em,root,"幼年继承人",2,5,kingId); var childId=Id(child);
                Check(CourtOps.Eligible(em,king,child),"Underage direct descendant eligible without an age threshold");
                var grand=DynastyOps.CreateRoyal(em,root,"孙辈",2,0,childId); Check(CourtOps.Eligible(em,king,grand),"Grandchild eligible");
                var outsider=Person("talent.placeholder2"); Check(!CourtOps.Eligible(em,king,outsider),"Unrelated talent cannot inherit");
                Check(Cmd(CommandKind.DesignateHeir,childId)==ResultCode.Success,"Designate underage heir");
                var before=em.GetComponentData<Royal>(child).Influence; CourtOps.Influence(em,root,child,10,"测试成长"); Check(em.GetComponentData<Royal>(child).Influence==before+15,"Crown positive gains multiplied");
                CourtOps.Influence(em,root,child,-10,"测试损失"); Check(em.GetComponentData<Royal>(child).Influence==before+5,"Crown losses not discounted");
                Check(Cmd(CommandKind.DesignateHeir,Id(grand))==ResultCode.Success && CourtOps.State(em,root).Disorder>0 && em.GetComponentData<Royal>(child).Grievance>0,"Replacement retains influence and creates grievance and disorder");
                Check(Cmd(CommandKind.ExecuteHeir,Id(grand))==ResultCode.ConfirmationRequired,"Execution requires explicit confirmation");
                var disorder=CourtOps.State(em,root).Disorder; Check(Cmd(CommandKind.ExecuteHeir,Id(grand),argument:1)==ResultCode.Success && CourtOps.State(em,root).Crown==0 && em.GetComponentData<Royal>(grand).Alive==0,"Executed crown dies and leaves vacancy");
                Check(CourtOps.State(em,root).Disorder<=CourtOps.Rules(em,root).DisorderCap,"Repeated political disorder capped");
                Reset(); king=CourtOps.Monarch(em); kingId=Id(king); child=DynastyOps.CreateRoyal(em,root,"储君",2,8,kingId); childId=Id(child);
                Cmd(CommandKind.DesignateHeir,childId); State(child,(ref Royal r)=>r.Influence=80); Turn(5);
                Check(Cmd(CommandKind.Abdicate,childId)==ResultCode.Success && Id(CourtOps.Monarch(em))==childId,"Abdication shares underage succession");
                Check(CourtOps.State(em,root).TemporaryProduction==.05f,"Long-established strong designation gets stability bonus");
                Check(!CourtOps.Eligible(em,child,king) && !CourtOps.JobEligible(em,king),"Retired king cannot be recycled as heir or talent");
                Reset(); king=CourtOps.Monarch(em); child=DynastyOps.CreateRoyal(em,root,"弱储君",2,3,Id(king)); Cmd(CommandKind.DesignateHeir,Id(child)); CourtOps.Die(em,root,king,0);
                Check(CourtOps.State(em,root).TemporaryProduction==-.1f && em.GetComponentData<Royal>(child).Role==0,"Weak newly designated heir gets weak succession outcome");
                Reset(); king=CourtOps.Monarch(em); child=DynastyOps.CreateRoyal(em,root,"未立储后代",2,2,Id(king)); CourtOps.Die(em,root,king,0);
                Check(CourtOps.Monarch(em)==child && CourtOps.State(em,root).TemporaryProduction==-.15f,"No crown uses weighted heirs and election disorder");
                Reset(); king=CourtOps.Monarch(em); child=DynastyOps.CreateRoyal(em,root,"有野心的继承人",2,20,Id(king)); State(child,(ref Royal r)=>{r.Influence=90;r.Ambition=1;});
                var risk=CourtOps.PlotChance(em,root,king,child); Check(risk>0,"Threatening influence permits regicide risk"); Cmd(CommandKind.DesignateHeir,Id(child)); Check(CourtOps.PlotChance(em,root,king,child)>0 && CourtOps.PlotChance(em,root,king,child)<risk,"Designation reduces but does not eliminate regicide risk");
                var ss=em.GetComponentData<Session>(root); ss.PublicOpinion=50; em.SetComponentData(root,ss); var beforePolicy=CourtOps.PlotChance(em,root,king,child);
                Check(Cmd(CommandKind.SelectPolicy,definition:Def("policy.palace_guard"))==ResultCode.Success && CourtOps.PlotChance(em,root,king,child)<beforePolicy,"Policy reduces plot risk without spending opinion");
                ss=em.GetComponentData<Session>(root); Check(ss.PublicOpinion==50,"Policy opinion not consumed"); ss.PublicOpinion=0; em.SetComponentData(root,ss); Check(CourtOps.Modifier(em,root,RuleKind.PlotRisk,-1)==0,"Chosen policy sleeps below threshold"); ss.PublicOpinion=50; em.SetComponentData(root,ss); Check(CourtOps.Modifier(em,root,RuleKind.PlotRisk,-1)<0,"Chosen policy automatically reactivates");
                Cmd(CommandKind.SelectPolicy,definition:Def("policy.court_dialogue")); Check(em.GetBuffer<PolicyChoice>(root).Length==1 && CourtOps.Modifier(em,root,RuleKind.PlotRisk,-1)==-.3f,"Same layer policies exclusive");
                Cmd(CommandKind.CancelPolicy,definition:Def("policy.court_dialogue")); Check(CourtOps.Modifier(em,root,RuleKind.PlotRisk,-1)==0,"Policy cancellation removes effect");
                CourtOps.Die(em,root,king,2); CourtOps.Succeed(em,root,king,child,false,true);
                Check(CourtOps.State(em,root).LegacyAttack==-.3f && CourtOps.Monarch(em)==child,"Regicide swaps player ruler and applies heavy legacy");
                var family=DynastyOps.CreateRoyal(em,root,"下一代",2,1,Id(child)); var legacy=CourtOps.State(em,root).LegacyAttack;
                CourtOps.Die(em,root,child,0); Check(math.abs(CourtOps.State(em,root).LegacyAttack-legacy*.5f)<.0001f,"Child generation halves inherited political wound");
                Reset(); king=CourtOps.Monarch(em); State(king,(ref Royal r)=>r.Age=30); CourtOps.RefreshTraits(em,root,king);
                Check(!CourtOps.NaturalDeath(em,root,king) && em.GetComponentData<Royal>(king).FateUsed==1,"Fate intercepts first natural death before succession");
                var until=em.GetComponentData<Royal>(king).FateUntil; Check(until==16 && CourtOps.FateGrace(90)==5,"Fate grace varies 5 to 15 by age");
                snap=SnapshotCodec.Capture(em,root); SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,snap)); king=CourtOps.Monarch(em);
                Check(em.GetComponentData<Royal>(king).FateUntil==until && !CourtOps.NaturalDeath(em,root,king),"Save/load cannot renew or shorten fate grace");
                Turn(until); Check(CourtOps.NaturalDeath(em,root,king) && CourtOps.State(em,root).Extinction==1,"Grace expiry causes natural death and extinction when childless");
                Check(Cmd(CommandKind.RetryDay)==ResultCode.Unavailable && Cmd(CommandKind.RetryDusk)==ResultCode.Unavailable,"Extinction cannot use battle retries");
                snap=SnapshotCodec.Capture(em,root); SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,snap)); Check(CourtOps.State(em,root).Extinction==1,"Terminal extinction snapshot roundtrip");
                Reset(); king=CourtOps.Monarch(em); State(king,(ref Royal r)=>r.Age=60); CourtOps.RefreshTraits(em,root,king); var death=CourtOps.NaturalChance(em,root,king);
                em.GetBuffer<TraitEntry>(king).Add(new TraitEntry {Definition=Def("gene.long_life"),Revealed=1,Active=1}); Check(CourtOps.NaturalChance(em,root,king)==death*.5f,"Long life scales probability, not fixed lifespan");
                person=Person("talent.placeholder1"); pid=Id(person); var spouse=Sim.Find(em,em.GetComponentData<Royal>(king).Spouse); CourtOps.Die(em,root,spouse,0);
                State(person,(ref Royal r)=>r.Affection=100); Cmd(CommandKind.RecruitTalent,pid); Cmd(CommandKind.AssignTalent,pid,Def("talent.slot1"));
                State(person,(ref Royal r)=>r.Gender=em.GetComponentData<Royal>(king).Gender==PersonGender.Male?PersonGender.Female:PersonGender.Male); Check(Cmd(CommandKind.ProposeMarriage,pid)==ResultCode.Success && em.GetComponentData<Royal>(person).Role==1 && em.GetComponentData<Talent>(person).Slot==-1 && em.GetComponentData<Talent>(person).Paid==0,"Working talent becomes same spouse and vacates paid position");
                Check(Cmd(CommandKind.AssignTalent,pid,Def("talent.slot1"))==ResultCode.Unavailable,"Consort cannot be reassigned");
                snap=SnapshotCodec.Capture(em,root); SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,snap)); Check(snap.SequenceEqual(SnapshotCodec.Capture(em,root)),"Remarriage reciprocal ancestry survives snapshot");
                Check(EconomyForecastOps.Create(em,root)==ResultCode.Success && snap.SequenceEqual(SnapshotCodec.Capture(em,root)),"Forecast consumes no social progress, mortality or political RNG");
                bool rejected=false; var bad=SnapshotCodec.Decode(em,root,snap); bad.Records.Single(x=>x.Identity.Id==pid).Royal.Affection=101; try{SnapshotCodec.Restore(em,root,bad);}catch(InvalidDataException){rejected=true;}
                Check(rejected && snap.SequenceEqual(SnapshotCodec.Capture(em,root)),"Invalid person save rejected atomically");
                var originalPerson=Sim.Find(em,pid); bool failed=false; try{SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original),probe:stage=>{if(stage=="root-published")throw new IOException("Court rollback probe");});}catch(IOException){failed=true;}
                Check(failed && em.Exists(originalPerson) && snap.SequenceEqual(SnapshotCodec.Capture(em,root)),"Publication failure retains original people and court log");
                Reset(); king=CourtOps.Monarch(em); child=DynastyOps.CreateRoyal(em,root,"出访者",2,16,Id(king));
                Check(CourtOps.AvailableCaptain(em,root,child),"Sixteen-year-old captain ready"); Check(Cmd(CommandKind.RoyalVisit,Id(child),argument:1)==ResultCode.Success && !CourtOps.AvailableCaptain(em,root,child),"Visit reserves person independently of workers");
                Check(Cmd(CommandKind.RoyalVisit,Id(child),argument:1)==ResultCode.Unavailable,"Visit event can resolve only once");
                Check(Cmd(CommandKind.DesignateHeir,Id(child))==ResultCode.Success,"Away heir can be designated");
                var age=em.GetComponentData<Royal>(child).Age; CourtOps.Settle(em,root); var settled=SnapshotCodec.Capture(em,root); CourtOps.Settle(em,root);
                Check(em.GetComponentData<Royal>(child).Age==age+1 && settled.SequenceEqual(SnapshotCodec.Capture(em,root)),"Age/political settlement runs exactly once per turn");
                Reset(); Check(CourtOps.State(em,root).Extinction==0 && em.GetComponentData<Session>(root).Phase==Phase.Day,"Living childless king is not an immediate game over");
                foreach(var entry in new[]{("talent.placeholder2","talent.slot2",RuleKind.ActionPowerBonus,2f),("talent.placeholder3","talent.slot3",RuleKind.CropHarvestBonus,.2f)})
                { person=Person(entry.Item1); State(person,(ref Royal r)=>r.Affection=100); Cmd(CommandKind.RecruitTalent,Id(person)); Check(Cmd(CommandKind.AssignTalent,Id(person),Def(entry.Item2))==ResultCode.Success,"Appoint "+entry.Item1); Check(math.abs(EffectOps.Modifier(em,root,entry.Item3,-1)-entry.Item4)<.0001,"Specific modifier "+entry.Item3); }
                var core=Entity.Null; using(var all=Sim.OrderedEntities<Building>(em)) foreach(var e in all) if(em.GetComponentData<BuildingStats>(e).IsCore!=0) core=e;
                Check(BuildingRangeOps.ActionPower(em,root,core)==em.GetComponentData<BuildingStats>(core).ActionPower+2,"Building connection calculation includes talent AP");
                Check(EffectOps.Modifier(em,root,RuleKind.ProductionBonus,Def("原木"))==0,"Crop talent does not affect ordinary production");
                Reset(); king=CourtOps.Monarch(em); var traits=em.GetBuffer<TraitEntry>(king); traits.Add(new TraitEntry {Definition=Def("gene.military"),Revealed=1,Active=1});
                Check(CourtOps.Modifier(em,root,RuleKind.SoldierAttackBonus,-1)==.1f && CourtOps.Modifier(em,root,RuleKind.SoldierSpeedBonus,-1)==.1f,"Reigning military genius drives soldier attack and speed");
                traits.Add(new TraitEntry {Definition=Def("gene.scholar"),Revealed=1,Active=1}); var research=em.GetComponentData<Session>(root).ResearchPoints; CourtOps.Settle(em,root); Check(em.GetComponentData<Session>(root).ResearchPoints==research+1,"Scholar gene pays one research point");
                Reset(); king=CourtOps.Monarch(em); State(king,(ref Royal r)=>r.Age=29); CourtOps.RefreshTraits(em,root,king); Check(em.GetBuffer<TraitEntry>(king)[0].Revealed==0,"Fate hidden before thirty");
                State(king,(ref Royal r)=>r.Age=30); CourtOps.RefreshTraits(em,root,king); Check(em.GetBuffer<TraitEntry>(king)[0].Active==1,"Fate activated at thirty");
                child=DynastyOps.CreateRoyal(em,root,"命运试验后代",2,30,Id(king)); em.GetBuffer<TraitEntry>(child).Add(new TraitEntry {Definition=Def("gene.fate"),Revealed=1,Active=1}); Cmd(CommandKind.DesignateHeir,Id(child)); CourtOps.Die(em,root,child,1);
                Check(em.GetComponentData<Royal>(child).Alive==0 && em.GetComponentData<Royal>(child).FateUsed==0,"Fate does not prevent execution");
                Reset(); king=CourtOps.Monarch(em); var inherited=0;
                for(int i=0;i<24;i++) { child=DynastyOps.CreateRoyal(em,root,new FixedString128Bytes("遗传试验 "+i),2,0,Id(king)); CourtOps.Inherit(em,root,child,king,Sim.Find(em,em.GetComponentData<Royal>(king).Spouse)); foreach(var trait in em.GetBuffer<TraitEntry>(child)) if(trait.Definition==Def("gene.fate")) inherited++; }
                Check(inherited>0 && inherited<24,"Seeded parent inheritance produces variable children");
                Reset(); person=Person("talent.placeholder1"); pid=Id(person); State(person,(ref Royal r)=>r.Affection=100); Cmd(CommandKind.RecruitTalent,pid); Cmd(CommandKind.AssignTalent,pid,Def("talent.slot1")); CourtOps.Die(em,root,person,0);
                Check(em.GetComponentData<Talent>(person).Recruited==0 && em.GetComponentData<Talent>(person).Slot<0,"Dead talent releases roster and slot"); SocialOps.EnsureContacts(em,root); bool replacement=false;
                using(var contacts=Sim.OrderedEntities<Talent>(em)) foreach(var e in contacts) if(em.GetComponentData<Identity>(e).Definition==Def("talent.placeholder1") && Id(e)!=pid && CourtOps.Alive(em,e)) replacement=true;
                Check(replacement && Sim.Find(em,pid)==person,"New contact receives new ID while deceased history remains");
                Reset(); var checkpoints=world.GetOrCreateSystemManaged<CheckpointSystem>(); checkpoints.Update(); king=CourtOps.Monarch(em); State(king,(ref Royal r)=>{r.FateUsed=1;r.FateUntil=0;}); CourtOps.Die(em,root,king,0); checkpoints.Update();
                var archive=RunArchiveCodec.Decode(RunArchiveCodec.Encode(checkpoints.Export(root))); Check(archive.Recovery.Extinction==1 && archive.Recovery.AwaitingDecision==1 && archive.Dusk==null,"Extinction durable without any previous combat/dusk");
                checkpoints.Import(root,archive,false); Check(em.GetComponentData<Session>(root).Phase==Phase.GameOver && CourtOps.State(em,root).Extinction==1,"Archive import preserves extinction decision");
                bool blocked=false; try{checkpoints.Retry(root,false);}catch(InvalidDataException){blocked=true;} Check(blocked,"Persistence API also refuses extinction retry");
                var storage=Path.Combine(Path.GetTempPath(),"LandsongCourt-"+Guid.NewGuid().ToString("N"));
                if(Directory.Exists(storage)) throw new InvalidOperationException("Expected a new isolated court fixture directory.");
                try
                {
                    var disk=new RunArchiveStore(storage); disk.Write(archive); disk.End(archive.RunId,"测试王朝",1,"王朝绝嗣");
                    Check(File.ReadAllText(Path.Combine(storage,"history",archive.RunId+".txt")).Contains("王朝绝嗣") && !File.Exists(disk.RunPath(archive.RunId)),"Explicit end removes owned test archive and keeps correct extinction reason");
                }
                finally { if(Directory.Exists(storage)) Directory.Delete(storage,true); }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        delegate void ActionRef<T>(ref T value);
    }
}
#endif
