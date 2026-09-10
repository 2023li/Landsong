using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS
{
    public readonly struct PersonRequestInfo
    {
        public readonly PersonRequestKind Kind;public readonly int Turn;public readonly ulong Target;public readonly bool Travelling;
        public PersonRequestInfo(PersonRequestKind kind,int turn,ulong target=0,bool travelling=false){Kind=kind;Turn=turn;Target=target;Travelling=travelling;}
    }
    public static class PersonRequestOps
    {
        static bool Eligible(EntityManager em,Entity root,Entity person)=>CourtOps.Alive(em,person)&&CourtOps.Eligible(em,CourtOps.Monarch(em),person)&&
            em.GetComponentData<Royal>(person).Role==2&&em.GetComponentData<Royal>(person).Age>=CourtOps.Rules(em,root).CaptainAge;
        public static List<PersonRequestInfo> Pending(EntityManager em,Entity root,Entity person)
        {
            var result=new List<PersonRequestInfo>();if(!CourtOps.Alive(em,person))return result;
            var p=em.GetComponentData<Royal>(person);
            if(PortraitOps.CanCustomize(em,root,person))result.Add(new PersonRequestInfo(PersonRequestKind.Portrait,0));
            if(RoyalFamilyOps.RequestValid(em,root,person))result.Add(new PersonRequestInfo(PersonRequestKind.Marriage,p.MarriageRequestTurn,p.RequestedSpouse));
            if(em.HasBuffer<PersonRequestEntry>(person))foreach(var r in em.GetBuffer<PersonRequestEntry>(person))
            {
                if(r.Status==PersonRequestStatus.Pending&&Eligible(em,root,person))result.Add(new PersonRequestInfo(r.Kind,r.CreatedTurn));
                else if(r.Status==PersonRequestStatus.Travelling)result.Add(new PersonRequestInfo(r.Kind,r.CreatedTurn,r.Journey,true));
            }
            if(em.HasComponent<Talent>(person)&&p.TaskClaimed==0&&Sim.Rule(em,root,em.GetComponentData<Identity>(person).Definition,RuleKind.SocialTask).Level>=0)
                result.Add(new PersonRequestInfo(PersonRequestKind.SocialTask,0));
            return result;
        }
        public static bool OfferExpedition(EntityManager em,Entity root,Entity person)
        {
            if(!Eligible(em,root,person)||!CourtOps.AvailableCaptain(em,root,person)||!FeatureOps.Unlocked(em,root,"Expedition"))return false;
            int turn=em.GetComponentData<Session>(root).Turn;
            if(em.HasBuffer<PersonRequestEntry>(person))foreach(var r in em.GetBuffer<PersonRequestEntry>(person))if(r.Kind==PersonRequestKind.Expedition&&
                (r.Status==PersonRequestStatus.Pending||r.Status==PersonRequestStatus.Travelling||turn-System.Math.Max(r.CreatedTurn,r.ResolvedTurn)<CourtOps.Rules(em,root).ExpeditionRequestCooldown))return false;
            Sim.Buffer<PersonRequestEntry>(em,person);var buffer=em.GetBuffer<PersonRequestEntry>(person);if(buffer.Length>=16)buffer.RemoveAt(0);
            buffer.Add(new PersonRequestEntry{Kind=PersonRequestKind.Expedition,CreatedTurn=turn});
            CourtOps.Log(em,root,"渴望一次远征，等待君王安排",em.GetComponentData<Identity>(person).Id);return true;
        }
        public static void Settle(EntityManager em,Entity root)
        {
            CancelInvalid(em,root);if(!FeatureOps.Unlocked(em,root,"Expedition"))return;
            using var people=Sim.OrderedEntities<Royal>(em);
            foreach(var person in people)if(Eligible(em,root,person)&&CourtOps.AvailableCaptain(em,root,person)&&CourtOps.Roll(em,root,CourtOps.Rules(em,root).ExpeditionRequestChance)&&OfferExpedition(em,root,person))break;
        }
        public static void CancelInvalid(EntityManager em,Entity root)
        {
            using var people=Sim.OrderedEntities<Royal>(em);int turn=em.GetComponentData<Session>(root).Turn;
            foreach(var person in people)
            {
                if(!em.HasBuffer<PersonRequestEntry>(person))continue;var buffer=em.GetBuffer<PersonRequestEntry>(person);
                for(int i=0;i<buffer.Length;i++)
                {
                    var r=buffer[i];if(r.Status!=PersonRequestStatus.Pending&&r.Status!=PersonRequestStatus.Travelling)continue;
                    if(CourtOps.Alive(em,person)&&(r.Status==PersonRequestStatus.Travelling||Eligible(em,root,person)))continue;
                    r.Status=PersonRequestStatus.Cancelled;r.ResolvedTurn=turn;r.Journey=0;buffer[i]=r;
                }
            }
        }
        public static ResultCode Refuse(EntityManager em,Entity root,Entity person,int expectedTurn)
        {
            if(!Eligible(em,root,person)||!em.HasBuffer<PersonRequestEntry>(person))return ResultCode.Unavailable;
            var buffer=em.GetBuffer<PersonRequestEntry>(person);
            for(int i=0;i<buffer.Length;i++)
            {
                var r=buffer[i];if(r.Kind!=PersonRequestKind.Expedition||r.Status!=PersonRequestStatus.Pending||r.CreatedTurn!=expectedTurn)continue;
                r.Status=PersonRequestStatus.Refused;r.ResolvedTurn=em.GetComponentData<Session>(root).Turn;buffer[i]=r;
                CourtOps.Log(em,root,"君王拒绝了远征请求",em.GetComponentData<Identity>(person).Id);return ResultCode.Success;
            }
            return ResultCode.Unavailable;
        }
        public static void Departed(EntityManager em,Entity root,Entity journey)
        {
            var trip=em.GetComponentData<Expedition>(journey);var person=Sim.Find(em,trip.Captain);
            if(!CourtOps.Alive(em,person)||!em.HasBuffer<PersonRequestEntry>(person))return;
            var buffer=em.GetBuffer<PersonRequestEntry>(person);
            for(int i=0;i<buffer.Length;i++){var r=buffer[i];if(r.Kind!=PersonRequestKind.Expedition||r.Status!=PersonRequestStatus.Pending)continue;r.Status=PersonRequestStatus.Travelling;r.Journey=em.GetComponentData<Identity>(journey).Id;buffer[i]=r;}
        }
        public static void Returned(EntityManager em,Entity root,Entity journey,bool completed)
        {
            var trip=em.GetComponentData<Expedition>(journey);var person=Sim.Find(em,trip.Captain);
            if(person==Entity.Null||!em.HasBuffer<PersonRequestEntry>(person))return;
            var id=em.GetComponentData<Identity>(journey).Id;var buffer=em.GetBuffer<PersonRequestEntry>(person);bool fulfilled=false;
            for(int i=0;i<buffer.Length;i++)
            {
                var r=buffer[i];if(r.Status!=PersonRequestStatus.Travelling||r.Journey!=id)continue;
                r.Journey=0;r.Status=!CourtOps.Alive(em,person)?PersonRequestStatus.Cancelled:completed?PersonRequestStatus.Completed:Eligible(em,root,person)?PersonRequestStatus.Pending:PersonRequestStatus.Cancelled;
                r.ResolvedTurn=r.Status==PersonRequestStatus.Pending?0:em.GetComponentData<Session>(root).Turn;buffer[i]=r;fulfilled|=r.Status==PersonRequestStatus.Completed;
            }
            if(fulfilled)CourtOps.Log(em,root,"远征归来，完成了渴望一次远征的请求",trip.Captain);
        }
    }
}
