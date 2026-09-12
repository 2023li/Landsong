using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace Landsong.ECS
{
    // Turn-based political simulation. All references are stable person IDs, never UI objects.
    public static class CourtOps
    {
        public static CourtSettings Rules(EntityManager em, Entity root) => em.GetComponentData<ContentCatalog>(root).Value.Value.Court;
        public static CourtState State(EntityManager em, Entity root) => em.HasComponent<CourtState>(root) ? em.GetComponentData<CourtState>(root) : default;
        public static Entity Monarch(EntityManager em)
        { using var all=Sim.OrderedEntities<Royal>(em); foreach(var e in all) { var p=em.GetComponentData<Royal>(e); if(p.Alive!=0 && p.Role==0) return e; } return Entity.Null; }
        public static bool Alive(EntityManager em, Entity e) => e!=Entity.Null && em.Exists(e) && em.HasComponent<Royal>(e) && em.GetComponentData<Royal>(e).Alive!=0;
        public static bool JobEligible(EntityManager em, Entity e)
        { if(!em.HasComponent<Royal>(e)) return true; var p=em.GetComponentData<Royal>(e); return p.Alive!=0 && p.Role!=0 && p.Role!=1 && p.Role!=3 && p.Retired==0; }
        public static void Vacate(EntityManager em, Entity e)
        { if(!em.HasComponent<Talent>(e)) return; var t=em.GetComponentData<Talent>(e); t.Slot=-1; t.Paid=0; em.SetComponentData(e,t); }
        public static void Log(EntityManager em, Entity root, string message, ulong person=0, HistoryCategory category=HistoryCategory.General)
        {
            Sim.Buffer<CourtLogEntry>(em,root); var logs=em.GetBuffer<CourtLogEntry>(root);
            if(logs.Length>=64) logs.RemoveAt(0);
            logs.Add(new CourtLogEntry { Turn=em.GetComponentData<Session>(root).Turn,Person=person,Message=new FixedString128Bytes(message) });
            Sim.Emit(em,root,EventKind.Message,message,person,category:category);
        }
        public static Royal NewPerson(EntityManager em, Entity root, byte role, int age, ulong parent=0)
        {
            var r=new Royal { Role=role,Alive=1,Age=age,Parent=parent,EverMonarch=(byte)(role==0?1:0),Influence=role==0?50:10,
                Growth=.5f+(Sim.NextRandom(em,root)%151)/100f,Ambition=(Sim.NextRandom(em,root)%101)/100f,
                ReignSince=em.GetComponentData<Session>(root).Turn, InfluenceSource="初始影响力" };
            r.Gender=role==0?PersonGender.Male:role==1?PersonGender.Female:(Sim.NextRandom(em,root)%2==0?PersonGender.Male:PersonGender.Female);
            if(role==1){var monarch=Monarch(em);if(monarch!=Entity.Null)r.Gender=em.GetComponentData<Royal>(monarch).Gender==PersonGender.Male?PersonGender.Female:PersonGender.Male;}
            var pe=Sim.Find(em,parent); if(Alive(em,pe)) r.Generation=em.GetComponentData<Royal>(pe).Generation+1;
            return r;
        }
        public static void Initialize(EntityManager em, Entity root)
        {
            Sim.Set(em,root,new CourtState { VisitOfferTurn=1 }); Sim.Buffer<CourtLogEntry>(em,root);
            var initialSession=em.GetComponentData<Session>(root); initialSession.PublicOpinion=Rules(em,root).InitialOpinion; em.SetComponentData(root,initialSession);
            var king=Monarch(em); if(king==Entity.Null) return;
            var id=em.GetComponentData<Identity>(king).Id; var k=em.GetComponentData<Royal>(king);
            using var all=Sim.OrderedEntities<Royal>(em);
            foreach(var e in all)
            {
                var p=em.GetComponentData<Royal>(e);
                if(p.Role==1 && k.Spouse==0) { p.Spouse=id; k.Spouse=em.GetComponentData<Identity>(e).Id; }
                if(p.Role==2 && p.Parent==0) { p.Parent=id; p.SecondParent=k.Spouse; p.Generation=k.Generation+1; }
                em.SetComponentData(e,p); RefreshTraits(em,root,e);
            }
            em.SetComponentData(king,k);
        }
        public static bool Descendant(EntityManager em, ulong person, ulong ancestor, int depth=0)
        {
            if(person==0 || ancestor==0 || person==ancestor || depth>=128) return false;
            var e=Sim.Find(em,person); if(e==Entity.Null || !em.HasComponent<Royal>(e)) return false;
            var p=em.GetComponentData<Royal>(e);
            return p.Parent==ancestor || p.SecondParent==ancestor || Descendant(em,p.Parent,ancestor,depth+1) || Descendant(em,p.SecondParent,ancestor,depth+1);
        }
        public static bool Eligible(EntityManager em, Entity monarch, Entity person)
        {
            if(monarch==Entity.Null || !Alive(em,person) || monarch==person) return false;
            var p=em.GetComponentData<Royal>(person);
            return p.Role!=0 && p.Role!=3 && p.Retired==0 && Descendant(em,em.GetComponentData<Identity>(person).Id,em.GetComponentData<Identity>(monarch).Id);
        }
        public static int CandidateCount(EntityManager em, Entity excluded=default)
        { var k=Monarch(em); int n=0; using var all=Sim.OrderedEntities<Royal>(em); foreach(var e in all) if(e!=excluded && Eligible(em,k,e)) n++; return n; }
        public static void Influence(EntityManager em, Entity root, Entity e, float amount, string source)
        {
            if(!Alive(em,e)) return; var p=em.GetComponentData<Royal>(e);
            if(amount>0 && State(em,root).Crown==em.GetComponentData<Identity>(e).Id) amount*=Rules(em,root).PrinceGrowth;
            p.Influence=math.clamp(p.Influence+amount,0,100); p.InfluenceSource=new FixedString128Bytes(source+" "+amount.ToString("+0.##;-0.##;0")); em.SetComponentData(e,p);
        }
        public static void Disorder(EntityManager em, Entity root, int stacks)
        {
            var c=State(em,root); var q=Rules(em,root); var turn=em.GetComponentData<Session>(root).Turn;
            c.Disorder=math.min(q.DisorderCap,(turn<=c.DisorderUntil?c.Disorder:0)+q.DisorderPerStack*stacks);
            c.DisorderUntil=math.min(turn+q.DisorderTurns*2,math.max(turn,c.DisorderUntil)+q.DisorderTurns);
            var session=em.GetComponentData<Session>(root); session.PublicOpinion=math.max(0,session.PublicOpinion-q.DisorderOpinionCost*stacks); em.SetComponentData(root,session);
            Sim.Set(em,root,c);
        }
        public static ResultCode Designate(EntityManager em, Entity root, Entity person)
        {
            var k=Monarch(em); if(person!=Entity.Null && !Eligible(em,k,person)) return ResultCode.Unavailable;
            var c=State(em,root); var id=person==Entity.Null?0:em.GetComponentData<Identity>(person).Id;
            if(c.Crown==id) return ResultCode.Unavailable;
            if(c.Crown!=0)
            { var old=Sim.Find(em,c.Crown); if(Alive(em,old)) { var p=em.GetComponentData<Royal>(old); p.Grievance=math.min(1,p.Grievance+.3f); em.SetComponentData(old,p); } Disorder(em,root,1); c=State(em,root); }
            c.Crown=id; c.CrownSince=em.GetComponentData<Session>(root).Turn; Sim.Set(em,root,c);
            Log(em,root,id==0?"储位空缺，废储引发朝局动荡":"已立储，正向影响力增长提高",id); return ResultCode.Success;
        }
        // Death cause: 0 natural, 1 execution, 2 regicide, 3 explicit expedition accident.
        public static void Die(EntityManager em, Entity root, Entity e, byte cause)
        {
            if(!Alive(em,e)) return; var p=em.GetComponentData<Royal>(e); var id=em.GetComponentData<Identity>(e).Id;
            p.Alive=0; p.VisitUntil=0; em.SetComponentData(e,p); Vacate(em,e);
            if(em.HasComponent<Talent>(e)) { var talent=em.GetComponentData<Talent>(e); talent.Recruited=0; em.SetComponentData(e,talent); }
            var c=State(em,root); if(c.Crown==id) { c.Crown=0; Sim.Set(em,root,c); Disorder(em,root,1); Log(em,root,"储君去世，继承秩序受损",id); }
            Log(em,root,cause==0?"王室成员自然逝世":cause==1?"王室成员被赐死":cause==2?"君王遭弑杀":"王室成员在高风险出访中遇难",id, category: HistoryCategory.Important);
            if(p.Role==0 && cause!=2) Succeed(em,root,e,Entity.Null,false);
            RoyalFamilyOps.ClearInvalidRequests(em,root);
            PersonRequestOps.CancelInvalid(em,root);
        }
        public static ResultCode Execute(EntityManager em, Entity root, Entity e, bool confirmed)
        {
            if(!Eligible(em,Monarch(em),e)) return ResultCode.Unavailable;
            if(!confirmed) return ResultCode.ConfirmationRequired;
            var p=em.GetComponentData<Royal>(e); Disorder(em,root,p.Evidence!=0?1:3);
            Die(em,root,e,1); return ResultCode.Success;
        }
        static void Legacy(EntityManager em, Entity root, Entity king, bool regicide)
        {
            var c=State(em,root); var q=Rules(em,root); var p=em.GetComponentData<Royal>(king);
            c.LegacyProduction=math.min(c.LegacyProduction,regicide?q.RegicideProduction:q.UsurpProduction);
            c.LegacyAttack=math.min(c.LegacyAttack,regicide?q.RegicideAttack:q.UsurpAttack);
            c.LegacyFounder=em.GetComponentData<Identity>(king).Id; c.LegacyGeneration=p.Generation; c.LegacySeverity=(byte)(regicide?2:1);
            c.TemporaryProduction=0; c.TemporaryAttack=0; c.TemporaryUntil=0; Sim.Set(em,root,c);
        }
        public static void Succeed(EntityManager em, Entity root, Entity former, Entity forced, bool abdication, bool regicide=false)
        {
            var c=State(em,root); var q=Rules(em,root); var turn=em.GetComponentData<Session>(root).Turn;
            var crown=Sim.Find(em,c.Crown); var chosen=forced; var usurp=false;
            using var all=Sim.OrderedEntities<Royal>(em);
            if(chosen==Entity.Null && Eligible(em,former,crown))
            {
                chosen=crown; Entity rival=Entity.Null; var influence=em.GetComponentData<Royal>(crown).Influence+q.UsurpGap;
                foreach(var e in all) if(Eligible(em,former,e) && em.GetComponentData<Royal>(e).Influence>influence) { rival=e; influence=em.GetComponentData<Royal>(e).Influence; }
                if(rival!=Entity.Null && Roll(em,root,UsurpChance(em,root,rival))) { chosen=rival; usurp=true; }
            }
            if(chosen==Entity.Null)
            {
                float total=0; foreach(var e in all) if(Eligible(em,former,e)) total+=math.max(1,em.GetComponentData<Royal>(e).Influence);
                if(total>0) { var pick=(Sim.NextRandom(em,root)%1000000)/1000000f*total; foreach(var e in all) if(Eligible(em,former,e)) { chosen=e; pick-=math.max(1,em.GetComponentData<Royal>(e).Influence); if(pick<0) break; } }
            }
            if(chosen==Entity.Null)
            { c.Extinction=1; Sim.Set(em,root,c); var s=em.GetComponentData<Session>(root); s.Phase=Phase.GameOver; s.Paused=0; em.SetComponentData(root,s); Log(em,root,"王朝绝嗣：没有在世直系后代，王朝终结", category: HistoryCategory.Important); return; }
            var old=em.GetComponentData<Royal>(former); old.Role=3; old.Retired=1; em.SetComponentData(former,old); Vacate(em,former);
            var spouse=Sim.Find(em,old.Spouse); if(Alive(em,spouse)) { var p=em.GetComponentData<Royal>(spouse); p.Role=3; p.Retired=1; em.SetComponentData(spouse,p); Vacate(em,spouse); }
            var next=em.GetComponentData<Royal>(chosen); next.Role=0; next.EverMonarch=1; next.ReignSince=turn; em.SetComponentData(chosen,next); Vacate(em,chosen);
            spouse=Sim.Find(em,next.Spouse); if(Alive(em,spouse)) { var p=em.GetComponentData<Royal>(spouse); p.Role=1; em.SetComponentData(spouse,p); Vacate(em,spouse); }
            // Generation depth only advances after an actual full reign. Same-generation turnover never launders legacy.
            if(c.LegacySeverity!=0 && next.Generation>c.LegacyGeneration && (!abdication || turn-old.ReignSince>=q.MinimumReign))
            {
                var delta=next.Generation-c.LegacyGeneration; var factor=delta>=3?0:delta==2?.25f:.5f;
                c.LegacyProduction*=factor; c.LegacyAttack*=factor; c.LegacyGeneration=next.Generation;
                var founder=Sim.Find(em,c.LegacyFounder);
                if(founder!=Entity.Null && next.Generation-em.GetComponentData<Royal>(founder).Generation>=3) { c.LegacyProduction=0; c.LegacyAttack=0; c.LegacySeverity=0; }
            }
            if(!usurp && !regicide)
            {
                var designated=chosen==crown; var stable=designated && next.Influence>=q.StrongInfluence && turn-c.CrownSince>=q.StableDesignationTurns;
                c.TemporaryProduction=stable?q.StableProduction:designated?q.WeakProduction:q.ElectionProduction;
                c.TemporaryAttack=stable?q.StableAttack:designated?q.WeakAttack:q.ElectionAttack;
                c.TemporaryUntil=turn+(designated?q.TemporaryTurns:q.ElectionTurns)-1;
            }
            c.Crown=0; c.CrownSince=0; Sim.Set(em,root,c);
            if(usurp || regicide) Legacy(em,root,chosen,regicide);
            RefreshTraits(em,root,chosen);
            RoyalFamilyOps.ClearInvalidRequests(em,root);
            PersonRequestOps.CancelInvalid(em,root);
            Log(em,root,regicide?"弑君者登基，王朝承受跨代政治创伤":usurp?"强势继承人夺位，政治创伤将影响后代":"新君即位，继承结果已结算",em.GetComponentData<Identity>(chosen).Id);
        }
        public static bool Roll(EntityManager em, Entity root, float chance) => Sim.NextRandom(em,root)%1000000<math.clamp(chance,0,1)*1000000;
        public static float UsurpChance(EntityManager em, Entity root, Entity person) => math.clamp(Rules(em,root).UsurpChance*(1+em.GetComponentData<Royal>(person).Grievance),0,1);
        public static float NaturalChance(EntityManager em, Entity root, Entity person)
        {
            var p=em.GetComponentData<Royal>(person); var q=Rules(em,root);
            var chance=p.Age<18?q.DeathYoung:p.Age<50?q.DeathAdult:p.Age<65?q.DeathMature:p.Age<80?q.DeathOld:q.DeathAncient;
            return math.clamp(chance*(1+EffectOps.PersonalRisk(em,root,person)),0,1);
        }
        public static int FateGrace(int age) => math.clamp(15-(age-30)/5,5,15);
        public static bool NaturalDeath(EntityManager em, Entity root, Entity e)
        {
            var p=em.GetComponentData<Royal>(e); var turn=em.GetComponentData<Session>(root).Turn;
            if(p.FateUntil>0) { if(turn<p.FateUntil) return false; Die(em,root,e,0); return true; }
            if(p.Age>=30 && p.FateUsed==0 && HasTrait(em,root,e,"gene.fate"))
            { p.FateUsed=1; p.FateUntil=turn+FateGrace(p.Age); em.SetComponentData(e,p); Log(em,root,"知天命：自然寿命将尽，尚余 "+FateGrace(p.Age)+" 回合",em.GetComponentData<Identity>(e).Id); return false; }
            Die(em,root,e,0); return true;
        }
        public static bool HasTrait(EntityManager em, Entity root, Entity e, string key)
        { if(!em.HasBuffer<TraitEntry>(e)) return false; var d=Sim.FindDefinition(em,root,new FixedString128Bytes(key)); foreach(var t in em.GetBuffer<TraitEntry>(e)) if(t.Definition==d && t.Active!=0) return true; return false; }


        public static bool PolicyActive(EntityManager em, Entity root, int definition)
        { var d=Sim.Definition(em,root,definition); return em.GetComponentData<Session>(root).PublicOpinion>=d.Cost && ConditionOps.Prerequisites(em,root,definition); }
        public static float Modifier(EntityManager em, Entity root, RuleKind kind, int target)
            => EffectOps.Value(em, root, new EffectQuery(kind, target, domain: EffectDomain.Court));
        public static void RefreshTraits(EntityManager em, Entity root, Entity person)
        {
            if(!em.HasBuffer<TraitEntry>(person)) return; var p=em.GetComponentData<Royal>(person); var traits=em.GetBuffer<TraitEntry>(person);
            for(int i=0;i<traits.Length;i++)
            {
                var t=traits[i]; var d=Sim.Definition(em,root,t.Definition); if(d.Kind!=ContentKind.RoyalTrait) continue;
                if(p.Age>=d.Level) t.Revealed=1;
                bool valid=p.Alive!=0 && t.Revealed!=0 && p.Age>=d.Duration && ConditionOps.Prerequisites(em,root,t.Definition);
                for(int n=0;n<d.RuleCount;n++) { var r=Sim.GetRule(em,root,d.RuleStart+n); if(r.Kind!=RuleKind.GeneRequired) continue; bool found=false; foreach(var other in traits) if(other.Definition==r.Target) found=true; if(!found) valid=false; }
                t.Active=(byte)(valid?1:0); traits[i]=t;
            }
        }
        public static void Inherit(EntityManager em, Entity root, Entity child, Entity first, Entity second)
        {
            var blob=em.GetComponentData<ContentCatalog>(root).Value; var traits=em.GetBuffer<TraitEntry>(child);
            for(int d=0;d<blob.Value.Definitions.Length;d++)
            {
                var def=blob.Value.Definitions[d]; if(def.Kind!=ContentKind.RoyalTrait || (def.Flags&1)==0) continue;
                int parents=0;
                foreach(var parent in new[]{first,second}) if(parent!=Entity.Null && em.HasBuffer<TraitEntry>(parent)) foreach(var t in em.GetBuffer<TraitEntry>(parent)) if(t.Definition==d) parents++;
                var chance=parents==0?em.GetComponentData<DynastySettings>(root).MutationChance:def.Chance*(parents==2?1.5f:1);
                if(!Roll(em,root,chance)) continue; bool conflict=false;
                for(int n=0;n<def.RuleCount;n++) { var r=blob.Value.Rules[def.RuleStart+n]; if(r.Kind==RuleKind.GeneConflict) foreach(var t in traits) if(t.Definition==r.Target) conflict=true; }
                foreach(var t in traits) { var td=blob.Value.Definitions[t.Definition]; for(int n=0;n<td.RuleCount;n++) if(blob.Value.Rules[td.RuleStart+n].Kind==RuleKind.GeneConflict && blob.Value.Rules[td.RuleStart+n].Target==d) conflict=true; }
                if(!conflict) traits.Add(new TraitEntry { Definition=d });
            }
            RefreshTraits(em,root,child);
        }
        public static bool AvailableCaptain(EntityManager em, Entity root, Entity e)
        {
            if(!Alive(em,e)) return false; var p=em.GetComponentData<Royal>(e); if(p.Role!=2 || p.Age<Rules(em,root).CaptainAge || p.VisitUntil>0) return false;
            var id=em.GetComponentData<Identity>(e).Id; using var trips=Sim.Entities<Expedition>(em); foreach(var t in trips) { var j=em.GetComponentData<Expedition>(t); if(j.Captain==id && j.Status==ExpeditionStatus.Travelling) return false; } return true;
        }
        public static float PlotChance(EntityManager em, Entity root, Entity king, Entity e)
        {
            if(!Eligible(em,king,e)) return 0; var q=Rules(em,root); var p=em.GetComponentData<Royal>(e); var k=em.GetComponentData<Royal>(king);
            if(p.Influence<k.Influence-q.RegicideGap) return 0;
            var chance=q.RegicideChance*p.Ambition*(1+p.Grievance)*math.max(0,1+EffectOps.Modifier(em,root,RuleKind.PlotRisk,-1));
            if(State(em,root).Crown==em.GetComponentData<Identity>(e).Id) chance*=q.PrinceRisk;
            return math.clamp(chance,0,.5f);
        }
        public static ResultCode Visit(EntityManager em, Entity root, Entity e, int option)
        {
            var c=State(em,root); var q=Rules(em,root); var turn=em.GetComponentData<Session>(root).Turn;
            if(c.VisitOfferTurn==0 || c.VisitResolved!=0 || option<0 || option>2) return ResultCode.Unavailable;
            if(option!=0 && !AvailableCaptain(em,root,e)) return ResultCode.Unavailable;
            if(option==1 && !InventoryOps.Remove(em,root,em.GetComponentData<GameSettings>(root).Gold,q.VisitCost)) return ResultCode.InsufficientResources;
            c.VisitResolved=1; Sim.Set(em,root,c);
            if(option==0) { Log(em,root,"婉拒远方大国的邀请"); return ResultCode.Success; }
            if(option==2 && Roll(em,root,.1f)) { Die(em,root,e,3); return ResultCode.Success; }
            var p=em.GetComponentData<Royal>(e); p.VisitUntil=turn+q.VisitDuration; em.SetComponentData(e,p);
            Log(em,root,option==1?"继承人出访，护卫已随行":"继承人选择无护卫的高风险出访",em.GetComponentData<Identity>(e).Id); return ResultCode.Success;
        }
        public static void Settle(EntityManager em, Entity root)
        {
            if(EconomyJournalOps.Forecast(em,root)) return;
            var turn=em.GetComponentData<Session>(root).Turn; var c=State(em,root); if(c.LastSettledTurn==turn || c.Extinction!=0) return;
            c.LastSettledTurn=turn; if(c.VisitOfferTurn==0 || turn-c.VisitOfferTurn>=Rules(em,root).VisitInterval) { c.VisitOfferTurn=turn; c.VisitResolved=0; } Sim.Set(em,root,c);
            var reigning=Monarch(em); var research=(int)math.floor(EffectOps.Modifier(em,root,RuleKind.ResearchOutput,-1));
            var opinion=em.GetComponentData<Session>(root); var rules=Rules(em,root);
            var recovery=opinion.PublicOpinion<rules.InitialOpinion?math.min(rules.OpinionRecovery,rules.InitialOpinion-opinion.PublicOpinion):0;
            opinion.PublicOpinion=math.clamp(opinion.PublicOpinion+recovery+(int)math.floor(EffectOps.Modifier(em,root,RuleKind.PublicOpinion,-1)),0,100); em.SetComponentData(root,opinion);
            if(research!=0) { var s=em.GetComponentData<Session>(root); s.ResearchPoints=math.max(0,s.ResearchPoints+research); em.SetComponentData(root,s); }
            using(var all=Sim.OrderedEntities<Royal>(em)) foreach(var e in all)
            {
                var p=em.GetComponentData<Royal>(e); if(p.Alive==0) continue; p.Age++; em.SetComponentData(e,p); RefreshTraits(em,root,e);
                if(p.VisitUntil>0 && turn+1>=p.VisitUntil) { p.VisitUntil=0; em.SetComponentData(e,p); Influence(em,root,e,8,"完成外交出访"); Log(em,root,"出访归来，影响力提高",em.GetComponentData<Identity>(e).Id); }
                Influence(em,root,e,p.Growth,"每回合自然成长");
                if(p.FateUntil>0 || Roll(em,root,NaturalChance(em,root,e))) NaturalDeath(em,root,e);
                if(State(em,root).Extinction!=0) return;
            }
            // At most one plot resolves per settlement; never kill the newly succeeded ruler in the same turn.
            var king=Monarch(em);
            if(king!=Entity.Null && king==reigning)
            {
                using var all=Sim.OrderedEntities<Royal>(em);
                foreach(var e in all) if(PlotChance(em,root,king,e)>0 && Roll(em,root,PlotChance(em,root,king,e)))
                { if(Roll(em,root,.5f)) { Die(em,root,king,2); Succeed(em,root,king,e,false,true); } else { var p=em.GetComponentData<Royal>(e); p.Evidence=1; em.SetComponentData(e,p); Log(em,root,"弑君阴谋失败，已查获确凿证据",em.GetComponentData<Identity>(e).Id, category: HistoryCategory.Important); } break; }
            }
            RoyalFamilyOps.Settle(em,root);
            PersonRequestOps.Settle(em,root);
            PortraitOps.EnsurePeople(em,root);PortraitOps.Announce(em,root);
        }
    }
}
