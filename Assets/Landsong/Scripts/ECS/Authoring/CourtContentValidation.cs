using System;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Landsong.ECS.Authoring
{
    public static class CourtContentValidation
    {
        public static void Validate(GameCatalogAsset c, ContentCompilation compiled=null)
        {
            compiled??=new ContentCompilation(c);
            var q=c.Court;
            if(q.ExpeditionRequestCooldown<1||!math.isfinite(q.ExpeditionRequestChance)||q.ExpeditionRequestChance<0||q.ExpeditionRequestChance>1)throw new InvalidOperationException("Court: invalid expedition request settings");
            foreach(var person in c.RoyalFamily)if((byte)person.Gender>2)throw new InvalidOperationException("Court: invalid initial gender");
            var monarchGender=PersonGender.Male;
            foreach(var person in c.RoyalFamily)if(person.Role==0&&person.Gender!=PersonGender.Unspecified)monarchGender=person.Gender;
            foreach(var person in c.RoyalFamily)if(person.Role==1&&person.Gender!=PersonGender.Unspecified&&person.Gender==monarchGender)throw new InvalidOperationException("Court: initial spouses must have opposite genders");
            if(q.MarriageRequestCooldown<1) throw new InvalidOperationException("Court: invalid marriage request cooldown");
            foreach(var value in new[]{q.MarriageRequestChance,q.MarriageRefusalGrievanceChance,q.MarriageRefusalGrievance}) if(!math.isfinite(value)||value<0||value>1) throw new InvalidOperationException("Court: invalid marriage request probability/grievance");
            if(q.InitialOpinion<0 || q.InitialOpinion>100 || q.OpinionRecovery<0 || q.DisorderOpinionCost<0) throw new InvalidOperationException("Court: invalid public opinion settings");
            if(q.MarriageAge<18 || q.CaptainAge<1 || q.GiftCost<0 || q.GiftAffection<1 || q.RecruitAffection<0 || q.MarriageAffection<q.RecruitAffection || q.MarriageAffection>100 || q.PrinceGrowth<1 || q.MinimumReign<1 || q.VisitInterval<1 || q.VisitDuration<1 || q.VisitCost<0 || q.TemporaryTurns<1 || q.ElectionTurns<1 || q.DisorderTurns<1) throw new InvalidOperationException("Court: invalid ages, interaction or duration settings");
            foreach(var chance in new[]{q.DeathYoung,q.DeathAdult,q.DeathMature,q.DeathOld,q.DeathAncient,q.PrinceRisk,q.RegicideChance,q.UsurpChance}) if(!math.isfinite(chance) || chance<0 || chance>1) throw new InvalidOperationException("Court: probability must be 0..1");
            foreach(var d in c.Content)
            {
                if(d.Kind!=ContentKind.Talent && d.Kind!=ContentKind.TalentSlot && d.Kind!=ContentKind.RoyalTrait && d.Kind!=ContentKind.Policy) continue;
                if(d.Kind==ContentKind.Talent && (d.Level<1 || d.Capacity<d.Level || d.Cost<1 || d.Duration<0)) throw new InvalidOperationException(d.Id+": invalid talent growth");
                if(d.Kind==ContentKind.RoyalTrait && (d.Level<0 || d.Duration<d.Level || !math.isfinite(d.Chance) || d.Chance<0 || d.Chance>1)) throw new InvalidOperationException(d.Id+": invalid reveal/activation/inheritance");
                var traits=new HashSet<string>();
                foreach(var r in compiled.For(d))
                {
                    if(!math.isfinite(r.Value) || !math.isfinite(r.Extra)) throw new InvalidOperationException(d.Id+": nonfinite rule");
                    if(r.Kind==RuleKind.Wage && (r.Amount<0 || r.B<0)) throw new InvalidOperationException(d.Id+": negative wage");
                    if(r.Kind==RuleKind.Trait || r.Kind==RuleKind.GeneConflict || r.Kind==RuleKind.GeneRequired)
                    { var at=r.Target; if(at<0 || c.Content[at].Kind!=ContentKind.RoyalTrait || compiled.Id(r.Target)==d.Id || r.Kind==RuleKind.Trait && !traits.Add(compiled.Id(r.Target))) throw new InvalidOperationException(d.Id+": invalid gene reference"); }
                    if(r.Kind==RuleKind.SocialTask) { var at=r.Target; if(at<0 || c.Content[at].Kind!=ContentKind.Item || r.Amount<1 || r.B<1) throw new InvalidOperationException(d.Id+": invalid social task"); }
                }
            }
        }
    }
}
