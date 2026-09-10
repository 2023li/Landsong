#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;

namespace Landsong.ECS.Editor
{
    public static partial class CourtVerification
    {
        static void FamilyRules(EntityManager em,Entity root)
        {
            ulong Id(Entity e)=>em.GetComponentData<Identity>(e).Id;
            void Gender(Entity e,PersonGender gender){var r=em.GetComponentData<Royal>(e);r.Gender=gender;em.SetComponentData(e,r);}
            var king=CourtOps.Monarch(em);var child=DynastyOps.CreateRoyal(em,root,"赐婚测试子女",2,24,Id(king));
            var mate=DynastyOps.CreateRoyal(em,root,"赐婚测试对象",4,23);
            var grandchild=DynastyOps.CreateRoyal(em,root,"赐婚测试孙辈",2,18,Id(child));
            var grandMate=DynastyOps.CreateRoyal(em,root,"孙辈配偶",4,20);
            Gender(child,PersonGender.Female);Gender(mate,PersonGender.Male);Gender(grandchild,PersonGender.Male);Gender(grandMate,PersonGender.Female);
            RoyalFamilyOps.Marry(em,grandchild,grandMate,false);
            Check(RoyalFamilyOps.CanReproduce(em,root,child)&&!RoyalFamilyOps.CanReproduce(em,root,grandchild),"Only monarch and adult direct children may reproduce");
            Check(!RoyalFamilyOps.CanMarry(em,root,child,grandchild),"Blood relatives cannot marry");
            Check(RoyalFamilyOps.Request(em,root,child,mate),"Adult direct child creates a persistent marriage request");
            var other=DynastyOps.CreateRoyal(em,root,"另一子女",2,24,Id(king));
            Gender(other,PersonGender.Female);
            Check(!RoyalFamilyOps.Request(em,root,other,mate),"Pending spouse cannot be promised twice");
            var p=em.GetComponentData<Royal>(child);var pending=SnapshotCodec.Capture(em,root);
            var decoded=SnapshotCodec.Decode(em,root,pending);
            Check(decoded.Records.Single(r=>r.Identity.Id==Id(child)).Royal.RequestedSpouse==Id(mate),"Pending request survives codec round trip");
            var bad=SnapshotCodec.Decode(em,root,pending);var index=Array.FindIndex(bad.Records,r=>r.Identity.Id==Id(child));bad.Records[index].Royal.RequestedSpouse=ulong.MaxValue;
            bool rejected=false;try{SnapshotCodec.Restore(em,root,bad);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Invalid request reference rejected without mutating live world");
            ResultCode Decide(int option,ulong mateId,int turn)=>GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.ResolveMarriage,Target=Id(child),Other=mateId,Definition=turn,Argument=option});
            Check(Decide(1,Id(mate),p.MarriageRequestTurn+1)==ResultCode.Unavailable,"Stale dialog token cannot resolve a new request");
            var session=em.GetComponentData<Session>(root);session.Phase=Phase.Deployment;em.SetComponentData(root,session);
            Check(Decide(1,Id(mate),p.MarriageRequestTurn)==ResultCode.WrongPhase,"Marriage decision obeys day command gate");
            session.Phase=Phase.Day;em.SetComponentData(root,session);
            Check(Decide(1,Id(mate),p.MarriageRequestTurn)==ResultCode.Success,"Approve command establishes marriage");
            Check(em.GetComponentData<Royal>(child).Spouse==Id(mate)&&em.GetComponentData<Royal>(mate).Spouse==Id(child),"Approved spouses retain reciprocal stable identities");
            Check(Decide(1,Id(mate),p.MarriageRequestTurn)==ResultCode.Unavailable,"Repeated approval cannot execute twice");
            var settings=em.GetComponentData<DynastySettings>(root);var forced=settings;forced.BirthChance=1;em.SetComponentData(root,forced);
            int Children(ulong id){using var all=Sim.OrderedEntities<Royal>(em);return all.Count(e=>{var r=em.GetComponentData<Royal>(e);return r.Parent==id||r.SecondParent==id;});}
            try
            {
                int before=Children(Id(child));RoyalFamilyOps.Settle(em,root);
                Check(Children(Id(child))==before+1&&Children(Id(grandchild))==0,"Child family gives birth; adult grandchild family does not");
                CourtOps.Succeed(em,root,king,child,true);
                Check(em.GetComponentData<Royal>(king).Alive!=0&&em.GetComponentData<Royal>(child).EverMonarch==1,"Abdication retains living ancestor and permanent accession marker");
                RoyalFamilyOps.Settle(em,root);
                Check(Children(Id(grandchild))==1&&!RoyalFamilyOps.CanReproduce(em,root,other)&&!RoyalFamilyOps.CanReproduce(em,root,king),"Succession enables new monarch's children and stops old collateral/retired branches without four-generation cap");
            }
            finally{em.SetComponentData(root,settings);}
            var requester=DynastyOps.CreateRoyal(em,root,"拒婚测试",2,22,Id(child));var target=DynastyOps.CreateRoyal(em,root,"拒婚对象",4,22);
            Gender(requester,PersonGender.Female);Gender(target,PersonGender.Male);
            var rp=em.GetComponentData<Royal>(requester);rp.Influence=100;rp.Ambition=1;em.SetComponentData(requester,rp);
            float plot=CourtOps.PlotChance(em,root,child,requester),usurp=CourtOps.UsurpChance(em,root,requester);bool resented=false;
            for(int attempt=0;attempt<40&&!resented;attempt++)
            {
                rp=em.GetComponentData<Royal>(requester);rp.MarriageCooldownUntil=0;em.SetComponentData(requester,rp);
                Check(RoyalFamilyOps.Request(em,root,requester,target),"Rejected request can recur after cooldown");rp=em.GetComponentData<Royal>(requester);
                Check(RoyalFamilyOps.Resolve(em,root,requester,0,Id(target),rp.MarriageRequestTurn)==ResultCode.Success,"Refusal resolves once");resented=em.GetComponentData<Royal>(requester).Grievance>0;
            }
            Check(resented&&CourtOps.PlotChance(em,root,child,requester)>plot&&CourtOps.UsurpChance(em,root,requester)>usurp,"Refusal grievance increases both regicide and usurp probability");
            Check(!RoyalFamilyOps.Request(em,root,requester,target),"Refusal cooldown prevents immediate request spam");
            rp=em.GetComponentData<Royal>(requester);rp.MarriageCooldownUntil=0;em.SetComponentData(requester,rp);RoyalFamilyOps.Request(em,root,requester,target);
            CourtOps.Die(em,root,target,1);Check(!RoyalFamilyOps.RequestValid(em,root,requester)&&em.GetComponentData<Royal>(requester).RequestedSpouse==0,"Participant death cancels pending request without refusal grievance");
            Check(em.GetComponentData<Royal>(CourtOps.Monarch(em)).Gender==PersonGender.Female,"Female child inherits without gender restriction");
            Check(RoyalFamilyOps.Prepare(em,root,requester)==ResultCode.Success&&RoyalFamilyOps.Candidates(em,root,requester).Count>=3,"Active arrangement supplies eligible candidates without a random request");
            var prepared=SnapshotCodec.Capture(em,root);RoyalFamilyOps.Prepare(em,root,requester);
            Check(prepared.SequenceEqual(SnapshotCodec.Capture(em,root)),"Reopening candidate list does not reroll or spawn more candidates");
            var arranged=RoyalFamilyOps.Candidates(em,root,requester)[0];
            Check(em.GetComponentData<Royal>(arranged).Gender==PersonGender.Male,"Candidates respect opposite gender");
            Check(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.ArrangeMarriage,Target=Id(requester),Other=Id(arranged)})==ResultCode.Success,"Player can proactively arrange an adult child's marriage through ECS");
            Check(RoyalFamilyOps.Arrange(em,root,requester,arranged)==ResultCode.Unavailable,"Repeated arranged marriage is unavailable");
            var baby=DynastyOps.CreateRoyal(em,root,"父母映射",2,0,Id(requester));var bp=em.GetComponentData<Royal>(baby);bp.SecondParent=Id(arranged);em.SetComponentData(baby,bp);
            Check(RoyalFamilyOps.ParentOfGender(em,baby,PersonGender.Female)==Id(requester)&&RoyalFamilyOps.ParentOfGender(em,baby,PersonGender.Male)==Id(arranged),"Father/mother derive from gender, not primary-parent order");
            var final=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,final));
            Check(final.SequenceEqual(SnapshotCodec.Capture(em,root)),"Family history and marriage decisions restore byte-for-byte");
        }
    }
}
#endif
