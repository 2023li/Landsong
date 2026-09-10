using System;
using System.Text;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public RoyalPersonDetailsView RoyalDetails {get;private set;}
        bool royalOverview,royalPrimaryMoved;
        Transform royalPrimaryParent;Vector2 royalPrimaryMin,royalPrimaryMax,royalPrimaryOffsetMin,royalPrimaryOffsetMax;
        void SelectRoyalPerson(ulong id){courtPerson=id;royalOverview=false;nextRefresh=0;}
        void RestoreRoyalPrimary()
        {
            if(!royalPrimaryMoved)return;
            var rect=(RectTransform)PrimaryRows.parent.parent;rect.SetParent(royalPrimaryParent,false);rect.anchorMin=royalPrimaryMin;rect.anchorMax=royalPrimaryMax;rect.offsetMin=royalPrimaryOffsetMin;rect.offsetMax=royalPrimaryOffsetMax;royalPrimaryMoved=false;
        }
        void RefreshRoyalDetails(bool visible)
        {
            if(RoyalDetails!=null)RoyalDetails.gameObject.SetActive(visible);
            if(!visible){RestoreRoyalPrimary();return;}
            if(RoyalDetails==null)RoyalDetails=RoyalPersonDetailsView.Create(courtGraph.transform,Status.font,value=>{royalOverview=value;nextRefresh=0;});
            var primary=(RectTransform)PrimaryRows.parent.parent;
            if(!royalPrimaryMoved)
            {
                royalPrimaryParent=primary.parent;royalPrimaryMin=primary.anchorMin;royalPrimaryMax=primary.anchorMax;royalPrimaryOffsetMin=primary.offsetMin;royalPrimaryOffsetMax=primary.offsetMax;
                primary.SetParent(RoyalDetails.OverviewHost,false);primary.anchorMin=Vector2.zero;primary.anchorMax=Vector2.one;primary.offsetMin=primary.offsetMax=Vector2.zero;royalPrimaryMoved=true;
            }
            primary.gameObject.SetActive(royalOverview);
            var person=Sim.Find(em,courtPerson);
            if(person==Entity.Null||!em.HasComponent<Royal>(person)||em.GetComponentData<Royal>(person).Role==4)
            {PortraitImageBinding.Bind(RoyalDetails.Portrait,em,root,0);RoyalDetails.Show(0,"点击肖像查看人物","选择家谱中的人物，查看亲属、声望与特性。",null,null,null,royalOverview);return;}
            var p=em.GetComponentData<Royal>(person);var id=em.GetComponentData<Identity>(person);var king=CourtOps.Monarch(em);var court=CourtOps.State(em,root);
            PortraitImageBinding.Bind(RoyalDetails.Portrait,em,root,id.Id);
            var body=new StringBuilder();
            body.AppendLine("父亲："+PersonName(RoyalFamilyOps.ParentOfGender(em,person,PersonGender.Male)));
            body.AppendLine("母亲："+PersonName(RoyalFamilyOps.ParentOfGender(em,person,PersonGender.Female)));
            body.AppendLine("配偶："+PersonName(p.Spouse));body.AppendLine();
            body.AppendLine("国中声望："+p.Influence.ToString("0.0")+" / 100");body.AppendLine("成长性："+p.Growth.ToString("0.00"));
            body.AppendLine("野心："+PublicRoyalAmbition(person));body.AppendLine();body.AppendLine("特性：");
            bool any=false;foreach(var trait in em.GetBuffer<TraitEntry>(person))if(trait.Revealed!=0){any=true;body.AppendLine(Name(trait.Definition)+(trait.Active!=0?"（已激活）":"（尚未激活）"));}
            if(!any)body.AppendLine("暂无已知特性");
            if(p.Alive==0)body.AppendLine("已逝，无法执行人物操作。");
            if(p.FateUntil>0&&p.Alive!=0)body.AppendLine("知天命：还剩 "+Math.Max(0,p.FateUntil-em.GetComponentData<Session>(root).Turn)+" 回合");
            bool can=CourtDay&&em.GetComponentData<Session>(root).Paused==0&&p.Alive!=0;bool eligible=CourtOps.Eligible(em,king,person);ulong key=id.Id;
            RoyalDetails.Show(key,id.Name+" · "+GenderName(p.Gender)+" · "+p.Age+" 岁"+(p.Alive==0?" · 已逝":p.Role==0?" · 国王":court.Crown==key?" · 储君":""),body.ToString(),
                can&&eligible&&court.Crown!=key?()=>ConfirmRoyalDesignation(key):null,
                can&&eligible?()=>ConfirmRoyalExecution(key):null,
                can&&RoyalFamilyOps.CanArrange(em,root,person)?()=>OpenMarriagePicker(key):null,royalOverview,p.Alive!=0?()=>ShowPersonRequests(key):null);
        }
        string PublicRoyalAmbition(Entity person)
        {
            var p=em.GetComponentData<Royal>(person);if(p.Evidence!=0)return "已查获弑君阴谋";
            int tier=IntelOps.Tier(em.GetComponentData<GameSettings>(root),IntelOps.Known(em,root));
            if(tier>=2&&CourtOps.PlotChance(em,root,CourtOps.Monarch(em),person)>0)return tier>=3?"有夺权动机，暂无确凿证据":"势力值得关注";
            return "未表现出野心";
        }
        static string GenderName(PersonGender gender)=>gender==PersonGender.Male?"男":gender==PersonGender.Female?"女":"未知";
        void ConfirmRoyalDesignation(ulong id)
        {ShowBuildingConfirmation("立储："+PersonName(id),new[]{CourtOps.State(em,root).Crown==0?"立储后正向影响力增长提高；仍可能存在政治风险。":"更换储君产生朝局动荡，原储君保留影响力与不满。"},()=>Send(CommandKind.DesignateHeir,id));}
        void ConfirmRoyalExecution(ulong id)
        {
            var person=Sim.Find(em,id);if(!CourtOps.Alive(em,person))return;var p=em.GetComponentData<Royal>(person);
            ShowBuildingConfirmation("确认赐死："+PersonName(id),new[]{p.Evidence!=0?"已有确凿证据，政治代价较低。":"无确凿证据，朝局动荡较重。",CourtOps.State(em,root).Crown==id?"储君死亡还会损害继承秩序。":"此行为不可撤销。"},()=>Send(CommandKind.ExecuteHeir,id,argument:1));
        }
    }
}
