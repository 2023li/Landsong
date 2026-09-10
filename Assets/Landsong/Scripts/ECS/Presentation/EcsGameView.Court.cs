using System.Collections.Generic;
using Unity.Entities;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        ulong courtPerson;
        bool courtSocial;
        bool CourtDay => em.GetComponentData<Session>(root).Phase==Phase.Day;
        string PersonName(ulong id) => id==0?"无":EntityName(id);
        void TalentRows()
        {
            Row(courtSocial?"交际 · 人物身份与人才岗位共用":"人才 · 任职且已付薪才产生加成");
            Row(courtSocial?"返回人才岗位":"打开交际页面",()=> { courtSocial=!courtSocial; courtPerson=0; nextRefresh=0; });
            Row("查看新增交际对象",CourtDay?()=>Send(CommandKind.RefreshTalents):null);
            var q=CourtOps.Rules(em,root);
            using(var all=Sim.OrderedEntities<Talent>(em)) foreach(var e in all)
            {
                var id=em.GetComponentData<Identity>(e); var t=em.GetComponentData<Talent>(e); var p=em.GetComponentData<Royal>(e);
                Row((courtPerson==id.Id?"▶ ":"")+id.Name+" · "+(p.Alive==0?"已逝":courtSocial?"好感 "+p.Affection:t.Recruited==0?"未招募":t.Slot<0?"未任职":Name(t.Slot)+(t.Paid!=0?"（生效）":"（欠薪停用）")),()=> { courtPerson=id.Id; nextRefresh=0; });
            }
            var person=Sim.Find(em,courtPerson); if(person==Entity.Null || !em.HasComponent<Talent>(person)) { Row("选择人物查看详情与操作"); return; }
            var identity=em.GetComponentData<Identity>(person); var talent=em.GetComponentData<Talent>(person); var royal=em.GetComponentData<Royal>(person); var can=CourtDay && royal.Alive!=0;
            Row(identity.Name+" · "+royal.Age+" 岁 · 好感 "+royal.Affection+"/100");
            Row("等级 "+talent.Level+" · 经验 "+talent.Experience+" · 任职 "+talent.AssignedTurns+" 回合 · 工资 "+SocialOps.Wage(em,root,person)+" 金币/次结算");
            var d=Sim.Definition(em,root,identity.Definition);
            for(int i=0;i<d.RuleCount;i++) { var r=Sim.GetRule(em,root,d.RuleStart+i); if(r.Kind==RuleKind.SoldierAttackBonus) Row("岗位效果：所有士兵攻击 +"+r.Value.ToString("P0")+"（不含英雄）"); if(r.Kind==RuleKind.ActionPowerBonus) Row("岗位效果：所有建筑连接行动力 +"+r.Value); if(r.Kind==RuleKind.CropHarvestBonus) Row("岗位效果：农田收获 +"+r.Value.ToString("P0")); }
            if(courtSocial)
            {
                Row("赠礼："+q.GiftCost+" 金币，好感 +"+q.GiftAffection+"（每回合一次）",can && royal.LastGiftTurn!=em.GetComponentData<Session>(root).Turn?()=>Send(CommandKind.GiftPerson,identity.Id):null);
                var task=Sim.Rule(em,root,identity.Definition,RuleKind.SocialTask);
                if(task.Level>=0) Row(royal.TaskClaimed!=0?"个人委托：已完成":"个人委托：提交 "+Name(task.Target)+" × "+task.Amount+"，好感 +"+task.B,can && royal.TaskClaimed==0?()=>Send(CommandKind.CompleteSocialTask,identity.Id):null);
                Row("求婚（好感需 "+q.MarriageAffection+"；双方成年且无在世配偶）",can && royal.Affection>=q.MarriageAffection?()=>ShowBuildingConfirmation("向 "+identity.Name+" 求婚",new[]{"成为君王配偶后会自动离开人才岗位并停薪，不再产生岗位加成。", "身份、经验和基因保留。君王及配偶不可兼任其他职位。"},()=>Send(CommandKind.ProposeMarriage,identity.Id)):null);
            }
            if(talent.Recruited==0) Row("招募人才（好感需 "+q.RecruitAffection+"）",can && royal.Affection>=q.RecruitAffection && CourtOps.JobEligible(em,person)?()=>Send(CommandKind.RecruitTalent,identity.Id):null);
            else
            {
                if(!CourtOps.JobEligible(em,person)) Row("当前身份不可任职：君王、配偶及退位者不兼任人才岗位。");
                ForDefinitions(ContentKind.TalentSlot,(i,slot)=>Row("任职："+slot.Name+"（立即付一次工资）",can && SocialOps.Accepts(em,root,person,i) && talent.Slot!=i?()=>Send(CommandKind.AssignTalent,identity.Id,definition:i):null));
                Row("离开岗位",can && talent.Slot>=0?()=>Send(CommandKind.AssignTalent,identity.Id,definition:-1):null);
                Row("解雇人才（保留人物与好感）",can?()=>ShowBuildingConfirmation("解雇 "+identity.Name,new[]{"停止工资及岗位效果；人物不会被删除，可以再次交际。"},()=>Send(CommandKind.DismissTalent,identity.Id)):null);
            }
            TraitRows(person);
        }
        void TraitRows(Entity e)
        {
            foreach(var t in em.GetBuffer<TraitEntry>(e)) if(t.Revealed!=0) Row("特性："+Name(t.Definition)+(t.Active!=0?"（已激活）":"（尚未激活）"));
            var p=em.GetComponentData<Royal>(e); if(p.FateUntil>0 && p.Alive!=0) Row("知天命：自然寿限还剩 "+System.Math.Max(0,p.FateUntil-em.GetComponentData<Session>(root).Turn)+" 回合；不抵挡非自然死亡。");
        }
        void RoyalRows()
        {
            var c=CourtOps.State(em,root); var q=CourtOps.Rules(em,root); var king=CourtOps.Monarch(em);
            Row("王位传承 · 储君："+PersonName(c.Crown));
            Row("生育范围：现任国王及其成年子女；孙辈须等父母登基。历史家谱永久保留。");
            using(var requests=Sim.OrderedEntities<Royal>(em))foreach(var e in requests)if(RoyalFamilyOps.RequestValid(em,root,e))
            {var id=em.GetComponentData<Identity>(e).Id;var p=em.GetComponentData<Royal>(e);Row(PersonName(id)+"希望能和"+PersonName(p.RequestedSpouse)+"结婚",()=>ShowMarriage(id));}
            Row("只由当前君王在世直系后代继承，不限性别和年龄。影响力上限 100；储君正增长 ×"+q.PrinceGrowth+"。");
            Row("产能修正 "+CourtOps.Modifier(em,root,RuleKind.ProductionBonus,-1).ToString("P0")+" · 士兵攻击修正 "+CourtOps.Modifier(em,root,RuleKind.SoldierAttackBonus,-1).ToString("P0"));
            if(c.Disorder>0 && em.GetComponentData<Session>(root).Turn<=c.DisorderUntil) Row("朝局动荡：−"+c.Disorder.ToString("P0")+"，至回合 "+c.DisorderUntil);
            if(c.LegacySeverity!=0) Row("跨代政治遗留：产能 "+c.LegacyProduction.ToString("P0")+" / 士兵攻击 "+c.LegacyAttack.ToString("P0")+"；同辈换君和短期退位不会减轻。");
            var person=Sim.Find(em,courtPerson);
            Row("废除储君",CourtDay && c.Crown!=0?()=>ShowBuildingConfirmation("废除储君",new[]{"产生朝局动荡；原储君不会失去影响力。"},()=>Send(CommandKind.DesignateHeir)):null);
            Row("主动退位",CourtDay && c.Crown!=0?()=>ShowBuildingConfirmation("主动退位",new[]{"仍按正常继承规则判定夺位与政治效果，不保证储君继位。","退位君王及配偶不可重新任职或再次继位。"},()=>Send(CommandKind.Abdicate,c.Crown)):null);
            if(c.VisitOfferTurn>0 && c.VisitResolved==0)
            {
                Row("外交邀请：远方大国邀请一位适龄继承人访问（队长需 "+q.CaptainAge+" 岁）。");
                Row("派所选继承人出访：护卫费 "+q.VisitCost+" 金币",CourtDay && CourtOps.AvailableCaptain(em,root,person)?()=>Send(CommandKind.RoyalVisit,courtPerson,argument:1):null);
                Row("无护卫出访：10% 遇难风险",CourtDay && CourtOps.AvailableCaptain(em,root,person)?()=>ShowBuildingConfirmation("高风险出访",new[]{"10% 概率遇难，知天命不能抵挡。储君遇难也会产生继承动荡。","存活后出访 "+q.VisitDuration+" 回合，归来提高影响力。"},()=>Send(CommandKind.RoyalVisit,courtPerson,argument:2)):null);
                Row("婉拒邀请",CourtDay?()=>Send(CommandKind.RoyalVisit,argument:0):null);
            }
            Row("宫廷纪事"); if(em.HasBuffer<CourtLogEntry>(root)) foreach(var log in em.GetBuffer<CourtLogEntry>(root)) Row("回合 "+log.Turn+" · "+(log.Person==0?"":PersonName(log.Person)+"：")+log.Message);
        }
        void PolicyRows()
        {
            Row("当前民意："+em.GetComponentData<Session>(root).PublicOpinion+" / 100");
            Row("同组同层只能选一项。民意是门槛，不扣除；不足时停用，恢复后自动生效。");
            ForDefinitions(ContentKind.Policy,(i,d)=>
            {
                bool chosen=false; foreach(var p in em.GetBuffer<PolicyChoice>(root)) if(p.Definition==i) chosen=true;
                Row((policyFocus==i?"▶ ":"")+d.Name+" · 民意需 "+d.Cost+" · "+(chosen?(CourtOps.PolicyActive(em,root,i)?"已生效":"已选，条件不足暂停"):"未选"));
                for(int n=0;n<d.RuleCount;n++) { var r=Sim.GetRule(em,root,d.RuleStart+n); if(r.Kind==RuleKind.PlotRisk) Row("弑君风险修正 "+r.Value.ToString("P0")); if(r.Kind==RuleKind.ProductionBonus) Row("生产修正 "+r.Value.ToString("P0")); }
                Row("采用："+d.Name,CourtDay && !chosen?()=>Send(CommandKind.SelectPolicy,definition:i):null);
                Row("取消："+d.Name,CourtDay && chosen?()=>Send(CommandKind.CancelPolicy,definition:i):null);
            });
        }
        void CourtIntelRows(int value)
        {
            foreach (var line in IntelOps.CourtLines(em, root, value)) Row(line);
        }
    }
}
