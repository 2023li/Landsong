using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    public static class SocialOps
    {
        public static void EnsureContacts(EntityManager em, Entity root)
        {
            var blob=em.GetComponentData<ContentCatalog>(root).Value;
            for(int d=0;d<blob.Value.Definitions.Length;d++)
            {
                if(blob.Value.Definitions[d].Kind!=ContentKind.Talent || !ConditionOps.Prerequisites(em,root,d)) continue;
                bool found=false; using(var all=Sim.OrderedEntities<Talent>(em)) foreach(var e in all) if(em.GetComponentData<Identity>(e).Definition==d && CourtOps.Alive(em,e)) found=true;
                if(found) continue;
                var person=Sim.Spawn(em,root,d,default,true); Sim.Set(em,person,new Talent { Slot=-1,Level=math.max(1,blob.Value.Definitions[d].Level) });
                Sim.Set(em,person,CourtOps.NewPerson(em,root,4,22)); Sim.Buffer<TraitEntry>(em,person);PortraitOps.Ensure(em,root,person);
                var def=blob.Value.Definitions[d]; for(int n=0;n<def.RuleCount;n++) { var r=blob.Value.Rules[def.RuleStart+n]; if(r.Kind==RuleKind.Trait) em.GetBuffer<TraitEntry>(person).Add(new TraitEntry { Definition=r.Target }); }
                CourtOps.RefreshTraits(em,root,person);
            }
        }
        public static int Wage(EntityManager em, Entity root, Entity person)
        { var t=em.GetComponentData<Talent>(person); var r=Sim.Rule(em,root,em.GetComponentData<Identity>(person).Definition,RuleKind.Wage); return math.max(0,r.Amount+(t.Level-1)*r.B); }
        public static bool Accepts(EntityManager em, Entity root, Entity person, int slot)
        {
            if(!Sim.ValidDefinition(em,root,slot) || !CourtOps.JobEligible(em,person)) return false;
            var s=Sim.Definition(em,root,slot); var d=Sim.Definition(em,root,em.GetComponentData<Identity>(person).Definition);
            if(s.Kind!=ContentKind.TalentSlot || !ConditionOps.Prerequisites(em,root,slot) || s.Value!=0 && s.Value!=d.Value) return false;
            for(int i=0;i<s.RuleCount;i++) { var r=Sim.GetRule(em,root,s.RuleStart+i); if(r.Kind!=RuleKind.GeneRequired) continue; bool found=false; foreach(var t in em.GetBuffer<TraitEntry>(person)) if(t.Definition==r.Target && t.Revealed!=0) found=true; if(!found) return false; }
            return true;
        }
        public static ResultCode TalentCommand(EntityManager em, Entity root, Command c)
        {
            if(c.Kind==CommandKind.RefreshTalents) { EnsureContacts(em,root); return ResultCode.Success; }
            var e=Sim.Find(em,c.Target); if(e==Entity.Null || !em.HasComponent<Talent>(e)) return ResultCode.InvalidTarget;
            var t=em.GetComponentData<Talent>(e); var settings=em.GetComponentData<DynastySettings>(root);
            if(c.Kind==CommandKind.RecruitTalent)
            {
                if(t.Recruited!=0 || !CourtOps.JobEligible(em,e)) return ResultCode.Unavailable;
                if(em.HasComponent<Royal>(e) && em.GetComponentData<Royal>(e).Affection<CourtOps.Rules(em,root).RecruitAffection) return ResultCode.Unavailable;
                int count=0; using(var all=Sim.OrderedEntities<Talent>(em)) foreach(var person in all) if(em.GetComponentData<Talent>(person).Recruited!=0 && CourtOps.JobEligible(em,person)) count++;
                if(count>=settings.TalentCapacity) return ResultCode.NoCapacity;
                if(!InventoryOps.Remove(em,root,em.GetComponentData<GameSettings>(root).Gold,settings.TalentRecruitCost)) return ResultCode.InsufficientResources;
                t.Recruited=1; t.Paid=0;
            }
            else if(c.Kind==CommandKind.DismissTalent) { t.Slot=-1; t.Recruited=0; t.Paid=0; }
            else if(c.Kind==CommandKind.AssignTalent)
            {
                if(t.Recruited==0 || !CourtOps.JobEligible(em,e)) return ResultCode.Unavailable;
                if(c.Definition>=0 && !Accepts(em,root,e,c.Definition)) return ResultCode.Unavailable;
                if(t.Slot==c.Definition) return ResultCode.Unavailable;
                if(c.Definition>=0 && !InventoryOps.Remove(em,root,em.GetComponentData<GameSettings>(root).Gold,Wage(em,root,e))) return ResultCode.InsufficientResources;
                if(c.Definition>=0) using(var all=Sim.OrderedEntities<Talent>(em)) foreach(var other in all) if(other!=e) { var old=em.GetComponentData<Talent>(other); if(old.Slot==c.Definition) { old.Slot=-1; old.Paid=0; em.SetComponentData(other,old); } }
                t.Slot=c.Definition; t.Paid=(byte)(c.Definition>=0?1:0); t.WageTurn=em.GetComponentData<Session>(root).Turn;
            }
            else return ResultCode.InvalidContent;
            em.SetComponentData(e,t); BuildingOps.Changed(em,root); return ResultCode.Success;
        }
        public static ResultCode Command(EntityManager em, Entity root, Command c)
        {
            var e=Sim.Find(em,c.Target); if(!CourtOps.Alive(em,e) || !em.HasComponent<Talent>(e)) return ResultCode.InvalidTarget;
            var p=em.GetComponentData<Royal>(e); var q=CourtOps.Rules(em,root); var turn=em.GetComponentData<Session>(root).Turn;
            if(c.Kind==CommandKind.GiftPerson)
            {
                if(p.LastGiftTurn==turn || p.Affection>=100) return ResultCode.Unavailable;
                if(!InventoryOps.Remove(em,root,em.GetComponentData<GameSettings>(root).Gold,q.GiftCost)) return ResultCode.InsufficientResources;
                p.LastGiftTurn=turn; p.Affection=math.min(100,p.Affection+q.GiftAffection);
            }
            else if(c.Kind==CommandKind.CompleteSocialTask)
            {
                if(p.TaskClaimed!=0) return ResultCode.Unavailable;
                var r=Sim.Rule(em,root,em.GetComponentData<Identity>(e).Definition,RuleKind.SocialTask);
                if(r.Level<0 || r.Target<0) return ResultCode.Unavailable;
                if(!InventoryOps.Remove(em,root,r.Target,r.Amount)) return ResultCode.InsufficientResources;
                p.TaskClaimed=1; p.Affection=math.min(100,p.Affection+r.B);
            }
            else if(c.Kind==CommandKind.ProposeMarriage)
            {
                var king=CourtOps.Monarch(em); if(king==Entity.Null || king==e || p.Affection<q.MarriageAffection || p.Age<q.MarriageAge || p.Retired!=0) return ResultCode.Unavailable;
                var k=em.GetComponentData<Royal>(king); var kid=em.GetComponentData<Identity>(king).Id; var id=em.GetComponentData<Identity>(e).Id;
                if(!RoyalFamilyOps.CanMarry(em,root,king,e)) return ResultCode.Unavailable;
                RoyalFamilyOps.Marry(em,king,e,true);p=em.GetComponentData<Royal>(e);RoyalFamilyOps.ClearInvalidRequests(em,root);
                CourtOps.Log(em,root,"结为配偶，原人才岗位已腾空并停薪",id);
            }
            else return ResultCode.InvalidContent;
            em.SetComponentData(e,p); BuildingOps.Changed(em,root); return ResultCode.Success;
        }
        // Pay before buildings consume effects: an unpaid worker cannot lend last turn's production bonus.
        public static void PayWages(EntityManager em, Entity root)
        {
            using var all=Sim.OrderedEntities<Talent>(em);
            foreach(var e in all)
            {
                var t=em.GetComponentData<Talent>(e);
                if(t.Recruited==0 || t.Slot<0 || !CourtOps.JobEligible(em,e) || !Accepts(em,root,e,t.Slot)) { t.Paid=0; em.SetComponentData(e,t); continue; }
                using var scope=EconomyJournalOps.For(em,root,e,EconomyReason.TalentWage);
                var turn=em.GetComponentData<Session>(root).Turn; if(t.WageTurn==turn) continue; t.WageTurn=turn;
                t.Paid=(byte)(InventoryOps.Remove(em,root,em.GetComponentData<GameSettings>(root).Gold,Wage(em,root,e))?1:0);
                if(t.Paid==0) EconomyJournalOps.Note(em,root,"工资不足，本期人才收益停用"); em.SetComponentData(e,t);
            }
        }
    }
}
