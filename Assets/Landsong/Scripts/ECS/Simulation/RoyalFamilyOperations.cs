using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Stable person IDs keep requests, families and succession history in the simulation snapshot.
    public static class RoyalFamilyOps
    {
        public static ulong ParentOfGender(EntityManager em,Entity child,PersonGender gender)
        {
            var p=em.GetComponentData<Royal>(child);
            foreach(var id in new[]{p.Parent,p.SecondParent}){var e=Sim.Find(em,id);if(e!=Entity.Null&&em.HasComponent<Royal>(e)&&em.GetComponentData<Royal>(e).Gender==gender)return id;}
            return 0;
        }
        public static bool CanArrange(EntityManager em,Entity root,Entity person)
            =>CanReproduce(em,root,person)&&!CourtOps.Alive(em,Sim.Find(em,em.GetComponentData<Royal>(person).Spouse));
        public static List<Entity> Candidates(EntityManager em,Entity root,Entity person)
        {
            var list=new List<Entity>();if(!CanArrange(em,root,person))return list;
            using var all=Sim.OrderedEntities<Royal>(em);
            foreach(var e in all)
            {
                if(em.GetComponentData<Royal>(e).Role!=4||!CanMarry(em,root,person,e))continue;
                var id=em.GetComponentData<Identity>(e).Id;bool reserved=false;
                foreach(var other in all)if(other!=person&&em.GetComponentData<Royal>(other).RequestedSpouse==id&&RequestValid(em,root,other))reserved=true;
                if(!reserved)list.Add(e);
            }
            return list;
        }
        public static ResultCode Prepare(EntityManager em,Entity root,Entity person)
        {
            if(!CanArrange(em,root,person))return ResultCode.Unavailable;
            int count=Candidates(em,root,person).Count;
            for(int i=count;i<3;i++)NewCandidate(em,root,person);
            return ResultCode.Success;
        }
        public static ResultCode Arrange(EntityManager em,Entity root,Entity person,Entity mate)
        {
            if(!CanArrange(em,root,person)||!Candidates(em,root,person).Contains(mate))return ResultCode.Unavailable;
            bool king=person==CourtOps.Monarch(em);Marry(em,person,mate,king);ClearInvalidRequests(em,root);
            CourtOps.Log(em,root,"君王主动赐婚，二人结为配偶",em.GetComponentData<Identity>(person).Id);BuildingOps.Changed(em,root);return ResultCode.Success;
        }
        public static bool DirectChild(EntityManager em, Entity king, Entity person)
        {
            if(!CourtOps.Alive(em,king)||!CourtOps.Alive(em,person)||king==person) return false;
            var id=em.GetComponentData<Identity>(king).Id; var p=em.GetComponentData<Royal>(person);
            return p.Retired==0 && (p.Parent==id || p.SecondParent==id);
        }
        public static bool CanReproduce(EntityManager em, Entity root, Entity person)
        {
            var king=CourtOps.Monarch(em);
            return CourtOps.Alive(em,person) && em.GetComponentData<Royal>(person).Age>=CourtOps.Rules(em,root).MarriageAge &&
                (king==person || DirectChild(em,king,person));
        }
        static HashSet<ulong> Ancestors(EntityManager em, ulong person)
        {
            var result=new HashSet<ulong>(); var pending=new Stack<ulong>(); pending.Push(person);
            while(pending.Count>0)
            {
                var id=pending.Pop(); if(id==0||!result.Add(id)) continue;
                var e=Sim.Find(em,id); if(e==Entity.Null||!em.HasComponent<Royal>(e))continue;
                var p=em.GetComponentData<Royal>(e); pending.Push(p.Parent); pending.Push(p.SecondParent);
            }
            return result;
        }
        public static bool CanMarry(EntityManager em, Entity root, Entity first, Entity second)
        {
            if(first==second || !CourtOps.Alive(em,first) || !CourtOps.Alive(em,second)) return false;
            var a=em.GetComponentData<Royal>(first); var b=em.GetComponentData<Royal>(second); int age=CourtOps.Rules(em,root).MarriageAge;
            if(a.Gender==b.Gender||a.Gender==PersonGender.Unspecified||b.Gender==PersonGender.Unspecified)return false;
            if(a.Retired!=0||b.Retired!=0||a.Age<age||b.Age<age||CourtOps.Alive(em,Sim.Find(em,a.Spouse))||CourtOps.Alive(em,Sim.Find(em,b.Spouse)))return false;
            var ancestors=Ancestors(em,em.GetComponentData<Identity>(first).Id);
            return !ancestors.Overlaps(Ancestors(em,em.GetComponentData<Identity>(second).Id));
        }
        public static bool RequestValid(EntityManager em, Entity root, Entity person)
        {
            if(!CourtOps.Alive(em,person))return false;
            var p=em.GetComponentData<Royal>(person);var king=CourtOps.Monarch(em);
            return king!=Entity.Null && p.RequestedSpouse!=0 && p.MarriageRequestMonarch==em.GetComponentData<Identity>(king).Id &&
                DirectChild(em,king,person) && CanMarry(em,root,person,Sim.Find(em,p.RequestedSpouse));
        }
        public static void ClearInvalidRequests(EntityManager em, Entity root)
        {
            using var people=Sim.OrderedEntities<Royal>(em);
            foreach(var e in people)
            {
                var p=em.GetComponentData<Royal>(e);if(p.RequestedSpouse==0||RequestValid(em,root,e))continue;
                p.RequestedSpouse=0;p.MarriageRequestMonarch=0;p.MarriageRequestTurn=0;em.SetComponentData(e,p);
            }
        }
        public static bool Request(EntityManager em, Entity root, Entity person, Entity mate)
        {
            var king=CourtOps.Monarch(em);if(!DirectChild(em,king,person)||!CanMarry(em,root,person,mate))return false;
            var p=em.GetComponentData<Royal>(person);int turn=em.GetComponentData<Session>(root).Turn;
            if(p.RequestedSpouse!=0||turn<p.MarriageCooldownUntil)return false;
            ulong id=em.GetComponentData<Identity>(mate).Id;
            using(var all=Sim.OrderedEntities<Royal>(em))foreach(var e in all)if(em.GetComponentData<Royal>(e).RequestedSpouse==id&&RequestValid(em,root,e))return false;
            p.RequestedSpouse=id;p.MarriageRequestMonarch=em.GetComponentData<Identity>(king).Id;p.MarriageRequestTurn=turn;
            p.MarriageCooldownUntil=turn+CourtOps.Rules(em,root).MarriageRequestCooldown;em.SetComponentData(person,p);
            CourtOps.Log(em,root,"请求赐婚，等待君王裁决",em.GetComponentData<Identity>(person).Id);return true;
        }
        public static void Marry(EntityManager em, Entity first, Entity second, bool monarch)
        {
            var a=em.GetComponentData<Royal>(first);var b=em.GetComponentData<Royal>(second);
            foreach(var id in new[]{a.Spouse,b.Spouse})
            {var old=Sim.Find(em,id);if(old==Entity.Null||!em.HasComponent<Royal>(old))continue;var p=em.GetComponentData<Royal>(old);p.Spouse=0;em.SetComponentData(old,p);}
            a.Spouse=em.GetComponentData<Identity>(second).Id;b.Spouse=em.GetComponentData<Identity>(first).Id;
            b.Role=(byte)(monarch?1:2);b.Generation=math.max(b.Generation,a.Generation);
            em.SetComponentData(first,a);em.SetComponentData(second,b);
            if(monarch)CourtOps.Vacate(em,second);
        }
        public static ResultCode Resolve(EntityManager em, Entity root, Entity person, int option, ulong expectedMate, int expectedTurn)
        {
            if(option!=0&&option!=1)return ResultCode.InvalidContent;
            if(!RequestValid(em,root,person))return ResultCode.Unavailable;
            var p=em.GetComponentData<Royal>(person);
            if(p.RequestedSpouse!=expectedMate||p.MarriageRequestTurn!=expectedTurn)return ResultCode.Unavailable;
            if(option==1)Marry(em,person,Sim.Find(em,p.RequestedSpouse),false);
            p=em.GetComponentData<Royal>(person);p.RequestedSpouse=0;p.MarriageRequestMonarch=0;p.MarriageRequestTurn=0;
            p.MarriageCooldownUntil=em.GetComponentData<Session>(root).Turn+CourtOps.Rules(em,root).MarriageRequestCooldown;
            bool resent=option==0&&CourtOps.Roll(em,root,CourtOps.Rules(em,root).MarriageRefusalGrievanceChance);
            if(resent)p.Grievance=math.min(1,p.Grievance+CourtOps.Rules(em,root).MarriageRefusalGrievance);
            em.SetComponentData(person,p);ClearInvalidRequests(em,root);
            CourtOps.Log(em,root,option==1?"君王同意赐婚，二人结为配偶":resent?"赐婚请求被拒，继承人心生记恨":"君王拒绝赐婚请求",em.GetComponentData<Identity>(person).Id);
            BuildingOps.Changed(em,root);return ResultCode.Success;
        }
        static Entity NewCandidate(EntityManager em, Entity root,Entity person)
        {
            string[] surnames={"沈","陆","苏","顾","林","谢","宋","江"};string[] names={"知远","清和","云舒","怀瑾","景明","如月","望舒","安宁","星澜","若初"};
            var name=surnames[Sim.NextRandom(em,root)%(uint)surnames.Length]+names[Sim.NextRandom(em,root)%(uint)names.Length];
            var candidate=DynastyOps.CreateRoyal(em,root,new FixedString128Bytes(name),4,CourtOps.Rules(em,root).MarriageAge+(int)(Sim.NextRandom(em,root)%12));
            var p=em.GetComponentData<Royal>(candidate);p.Gender=em.GetComponentData<Royal>(person).Gender==PersonGender.Male?PersonGender.Female:PersonGender.Male;em.SetComponentData(candidate,p);PortraitOps.Ensure(em,root,candidate);return candidate;
        }
        public static void Settle(EntityManager em, Entity root)
        {
            ClearInvalidRequests(em,root); var king=CourtOps.Monarch(em);if(king==Entity.Null)return;
            using var people=Sim.OrderedEntities<Royal>(em); var couples=new HashSet<ulong>();
            var settings=em.GetComponentData<DynastySettings>(root);var rules=CourtOps.Rules(em,root);bool offered=false;
            foreach(var e in people)
            {
                if(!CanReproduce(em,root,e))continue;
                var p=em.GetComponentData<Royal>(e);var id=em.GetComponentData<Identity>(e).Id;var spouse=Sim.Find(em,p.Spouse);
                if(CourtOps.Alive(em,spouse))
                {
                    if(couples.Contains(p.Spouse)||em.GetComponentData<Royal>(spouse).Age<rules.MarriageAge||em.GetComponentData<Royal>(spouse).Gender==p.Gender)continue;
                    couples.Add(id);int children=0;
                    using(var all=Sim.OrderedEntities<Royal>(em))foreach(var child in all){var r=em.GetComponentData<Royal>(child);if(r.Parent==id||r.SecondParent==id)children++;}
                    if(children>=settings.MaxChildren||!CourtOps.Roll(em,root,settings.BirthChance))continue;
                    var newborn=DynastyOps.CreateRoyal(em,root,new FixedString128Bytes("王室后代 "+(children+1)),2,0,id);
                    var born=em.GetComponentData<Royal>(newborn);born.SecondParent=p.Spouse;born.Generation=math.max(p.Generation,em.GetComponentData<Royal>(spouse).Generation)+1;
                    em.SetComponentData(newborn,born);CourtOps.Inherit(em,root,newborn,e,spouse);PortraitOps.Inherit(em,root,newborn,e,spouse);CourtOps.Log(em,root,"王室诞下子嗣",em.GetComponentData<Identity>(newborn).Id);
                }
                else if(e!=king&&!offered&&p.RequestedSpouse==0&&em.GetComponentData<Session>(root).Turn>=p.MarriageCooldownUntil&&CourtOps.Roll(em,root,rules.MarriageRequestChance))
                {
                    var candidates=new List<Entity>();foreach(var candidate in people)
                    {
                        if(em.GetComponentData<Royal>(candidate).Role!=4||!CanMarry(em,root,e,candidate))continue;
                        var mateId=em.GetComponentData<Identity>(candidate).Id;bool reserved=false;
                        foreach(var other in people)if(em.GetComponentData<Royal>(other).RequestedSpouse==mateId)reserved=true;
                        if(!reserved)candidates.Add(candidate);
                    }
                    var mate=candidates.Count>0&&CourtOps.Roll(em,root,.5f)?candidates[(int)(Sim.NextRandom(em,root)%(uint)candidates.Count)]:NewCandidate(em,root,e);
                    offered=Request(em,root,e,mate);
                }
            }
        }
    }
}
