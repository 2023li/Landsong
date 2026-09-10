#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS.Editor
{
    public static partial class CourtVerification
    {
        static void PersonRequests(EntityManager em,Entity root)
        {
            int Def(string name)=>Sim.FindDefinition(em,root,new FixedString128Bytes(name));
            ulong Id(Entity e)=>em.GetComponentData<Identity>(e).Id;
            void Turn(int turn){var s=em.GetComponentData<Session>(root);s.Turn=turn;s.Phase=Phase.Day;s.Paused=0;s.CheckpointPending=0;s.BasePopulation=100;em.SetComponentData(root,s);}
            Turn(2);var child=DynastyOps.CreateRoyal(em,root,"请求测试",2,24,Id(CourtOps.Monarch(em)));ulong childId=Id(child);
            Check(!PersonRequestOps.OfferExpedition(em,root,child),"Expedition wish waits for feature unlock");
            InvitationExpeditionVerification.FixturePermissions(em,root);
            var def=Def("b陆上远征所");var grid=em.GetComponentData<GridData>(root);Entity post=Entity.Null;
            for(int i=0;i<grid.Value.Value.Cells.Length;i++){var cell=grid.Value.Value.Min+new int2(i%grid.Value.Value.Size.x,i/grid.Value.Value.Size.x);if(!GridOps.CanPlace(em,root,def,cell,0))continue;post=BuildingOps.Create(em,root,def,cell,0,2,true);break;}
            Check(post!=Entity.Null,"Request fixture has expedition site");var building=em.GetComponentData<Building>(post);building.Workers=building.StableWorkers=15;em.SetComponentData(post,building);ulong postId=Id(post);
            InventoryOps.Add(em,root,em.GetComponentData<GameSettings>(root).Gold,100);
            var mate=DynastyOps.CreateRoyal(em,root,"请求配偶",4,23);var p=em.GetComponentData<Royal>(mate);p.Gender=em.GetComponentData<Royal>(child).Gender==PersonGender.Male?PersonGender.Female:PersonGender.Male;em.SetComponentData(mate,p);
            Check(RoyalFamilyOps.Request(em,root,child,mate)&&PersonRequestOps.OfferExpedition(em,root,child),"One person can hold marriage and expedition requests together");
            var before=SnapshotCodec.Capture(em,root);Check(PersonRequestOps.Pending(em,root,child).Count==2&&before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Request projection lists all pending kinds without mutation");
            Check(!PersonRequestOps.OfferExpedition(em,root,child),"No duplicate expedition wish");
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.RefusePersonRequest,Target=childId,Definition=1})==ResultCode.Unavailable,"Stale refusal cannot resolve a later request");
            SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,before));child=Sim.Find(em,childId);post=Sim.Find(em,postId);
            Check(before.SequenceEqual(SnapshotCodec.Capture(em,root))&&PersonRequestOps.Pending(em,root,child).Count==2,"Pending personal requests survive snapshot roundtrip");
            bool rolledBack=false;try{SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,before),probe:step=>{if(step=="record-created")throw new IOException("Request rollback fixture");});}catch(IOException){rolledBack=true;}
            Check(rolledBack&&before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Failed restore preserves original request buffer");
            void Invalid(Action<SnapshotCodec.Snapshot> change,string label){var data=SnapshotCodec.Decode(em,root,before);change(data);bool rejected=false;try{SnapshotCodec.Restore(em,root,data);}catch(InvalidDataException){rejected=true;}Check(rejected&&before.SequenceEqual(SnapshotCodec.Capture(em,root)),label);}
            Invalid(s=>s.Records.Single(r=>r.Identity.Id==childId).PersonRequests[0].Status=(PersonRequestStatus)99,"Unknown request status rejected atomically");
            Invalid(s=>s.Records.Single(r=>r.Identity.Id==childId).PersonRequests[0].Journey=childId,"Pending request cannot contain a journey link");
            Invalid(s=>{var r=s.Records.Single(x=>x.Identity.Id==childId);r.PersonRequests=new[]{r.PersonRequests[0],r.PersonRequests[0]};},"Duplicate request token rejected atomically");
            int destination=Def("expedition.nearby_recon");
            Entity Depart(){post=Sim.Find(em,postId);var quote=ExpeditionOps.Quote(em,root,post,destination,10);Check(quote.Code==ResultCode.Success,"Request expedition quote available");Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.StartExpedition,Target=postId,Other=childId,Definition=destination,Amount=10,Text=ExpeditionOps.Payload(quote)})==ResultCode.Success,"Requested captain departs through command");using var all=Sim.OrderedEntities<Expedition>(em);return all[all.Length-1];}
            PersonRequestEntry Request()=>em.GetBuffer<PersonRequestEntry>(Sim.Find(em,childId))[0];
            var journey=Depart();ulong journeyId=Id(journey);
            Check(Request().Status==PersonRequestStatus.Travelling&&Request().Journey==journeyId&&PersonRequestOps.Pending(em,root,Sim.Find(em,childId)).Count==2,"Departure keeps wish pending with matched journey");
            var travelling=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,travelling));journey=Sim.Find(em,journeyId);
            Check(travelling.SequenceEqual(SnapshotCodec.Capture(em,root)),"Travelling request survives save and load");
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.AbandonExpedition,Target=journeyId})==ResultCode.Success&&Request().Status==PersonRequestStatus.Pending,"Abandon returns wish to pending without completing");
            journey=Depart();ExpeditionOps.WithdrawFromSite(em,root,postId);
            Check(Request().Status==PersonRequestStatus.Pending&&Request().Journey==0,"Site withdrawal restores pending wish");
            journey=Depart();var state=em.GetComponentData<Expedition>(journey);state.SuccessChance=1;em.SetComponentData(journey,state);Turn(state.Arrival-1);ExpeditionOps.Settle(em,root);
            Check(Request().Status==PersonRequestStatus.Completed&&PersonRequestOps.Pending(em,root,Sim.Find(em,childId)).Count==1,"Return completes wish before claiming rewards and retains marriage request");
            var returned=SnapshotCodec.Capture(em,root);ExpeditionOps.Settle(em,root);Check(returned.SequenceEqual(SnapshotCodec.Capture(em,root)),"Return request completion is one-shot");
            SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,travelling));journey=Sim.Find(em,journeyId);state=em.GetComponentData<Expedition>(journey);state.SuccessChance=0;em.SetComponentData(journey,state);Turn(state.Arrival-1);ExpeditionOps.Settle(em,root);
            Check(Request().Status==PersonRequestStatus.Completed,"Failed expedition still fulfils wish when captain returns alive");
            SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,travelling));CourtOps.Die(em,root,Sim.Find(em,childId),0);
            Check(Request().Status==PersonRequestStatus.Cancelled&&PersonRequestOps.Pending(em,root,Sim.Find(em,childId)).Count==0,"Dead requester has no outstanding request or badge");
            SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,before));child=Sim.Find(em,childId);
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.RefusePersonRequest,Target=childId,Definition=2})==ResultCode.Success&&Request().Status==PersonRequestStatus.Refused,"Current expedition request can be refused through command");
            Check(!PersonRequestOps.OfferExpedition(em,root,child),"Refused wish respects configured cooldown");
            Turn(2+CourtOps.Rules(em,root).ExpeditionRequestCooldown);Check(PersonRequestOps.OfferExpedition(em,root,child),"New wish allowed after cooldown");
        }
    }
}
#endif
