using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public GameObject MarriageWindow {get;private set;}
        public Button MarriageEventButton {get;private set;}
        public Button MarriageApproveButton {get;private set;}
        public Button MarriageRefuseButton {get;private set;}
        public Button MarriageCloseButton {get;private set;}
        public bool MarriageOpen=>MarriageWindow!=null&&MarriageWindow.activeSelf;
        ulong marriagePerson,marriageMate,marriageKing;int marriageTurn;float nextMarriageRefresh;
        bool marriageArranging;string marriageCandidatesSignature;
        void RefreshMarriageEvents()
        {
            if(MarriageOpen)
            {
                var person=Sim.Find(em,marriagePerson);
                if(marriageArranging)RefreshMarriagePicker(person);
                else if(!RoyalFamilyOps.RequestValid(em,root,person))CloseMarriage();
                else
                {
                    var p=em.GetComponentData<Royal>(person);
                    if(p.RequestedSpouse!=marriageMate||p.MarriageRequestTurn!=marriageTurn||p.MarriageRequestMonarch!=marriageKing)CloseMarriage();
                }
                if(MarriageOpen){bool enabled=CourtDay&&!intel&&em.GetComponentData<Session>(root).Paused==0;MarriageApproveButton.interactable=enabled&&(!marriageArranging||marriageMate!=0);MarriageRefuseButton.interactable=marriageArranging||enabled;}
            }
            if(Time.unscaledTime<nextMarriageRefresh)return;nextMarriageRefresh=Time.unscaledTime+.25f;
            ulong first=0;int count=0;
            using(var all=Sim.OrderedEntities<Royal>(em))foreach(var e in all)if(RoyalFamilyOps.RequestValid(em,root,e)){if(first==0)first=em.GetComponentData<Identity>(e).Id;count++;}
            if(MarriageEventButton==null&&count==0)return;
            if(MarriageEventButton==null)
            {
                MarriageEventButton=InterfaceWidgets.Button("",GetComponentInParent<Canvas>().transform,Status.font,null);
                MarriageEventButton.name="Marriage requests";var rect=(RectTransform)MarriageEventButton.transform;
                rect.anchorMin=new Vector2(.77f,.16f);rect.anchorMax=new Vector2(.99f,.225f);rect.offsetMin=rect.offsetMax=Vector2.zero;
                MarriageEventButton.GetComponentInChildren<TMP_Text>().fontSize=15;
                MarriageEventButton.image.color=new Color(.28f,.22f,.11f,.98f);
            }
            MarriageEventButton.gameObject.SetActive(count>0&&!intel&&(PauseMenu==null||!PauseMenu.IsOpen));
            if(count==0)return;
            var pfirst=em.GetComponentData<Royal>(Sim.Find(em,first));
            MarriageEventButton.GetComponentInChildren<TMP_Text>().text=PersonName(first)+"希望能和"+PersonName(pfirst.RequestedSpouse)+"结婚"+(count>1?"（待办 "+count+"）":"");
            MarriageEventButton.onClick.RemoveAllListeners();MarriageEventButton.onClick.AddListener(()=>ShowMarriage(first));MarriageEventButton.interactable=!MarriageOpen;
        }
        public void CloseMarriage()
        {if(MarriageWindow!=null)MarriageWindow.SetActive(false);nextMarriageRefresh=0;nextRefresh=0;}
        public void ShowMarriage(ulong id)
        {
            if(SoldierDetailsOpen||MarriageOpen||PersonRequestsOpen||PortraitOpen||intel||PauseMenu!=null&&PauseMenu.IsOpen||BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf)return;
            var person=Sim.Find(em,id);if(!RoyalFamilyOps.RequestValid(em,root,person))return;
            marriageArranging=false;
            var p=em.GetComponentData<Royal>(person);marriagePerson=id;marriageMate=p.RequestedSpouse;marriageKing=p.MarriageRequestMonarch;marriageTurn=p.MarriageRequestTurn;
            if(MarriageWindow!=null){MarriageWindow.SetActive(false);Destroy(MarriageWindow);}
            EndBuildingPlacement();EndInventoryDrag();cameraDragging=false;
            MarriageWindow=InterfaceWidgets.Modal("Royal marriage request",GetComponentInParent<Canvas>().transform,450,out var card);
            var title=InterfaceWidgets.Text(PersonName(id)+"希望能和"+PersonName(marriageMate)+"结婚",InterfaceWidgets.Rect("Title",card,new Vector2(.02f,.89f),new Vector2(.98f,1)),Status.font,24);title.alignment=TextAlignmentOptions.Center;
            MarriageDetails(person,InterfaceWidgets.Scroll(card,"Requesting child",new Vector2(.025f,.2f),new Vector2(.49f,.88f)));
            MarriageDetails(Sim.Find(em,marriageMate),InterfaceWidgets.Scroll(card,"Proposed spouse",new Vector2(.51f,.2f),new Vector2(.975f,.88f)));
            InterfaceWidgets.Text("拒绝可能让请求人记恨，增加弑君或夺位风险。",InterfaceWidgets.Rect("Decision hint",card,new Vector2(.025f,.125f),new Vector2(.975f,.2f)),Status.font,16);
            var actions=InterfaceWidgets.Rect("Decisions",card,new Vector2(.06f,.035f),new Vector2(.94f,.12f));var group=actions.gameObject.AddComponent<HorizontalLayoutGroup>();group.spacing=12;group.childForceExpandWidth=true;
            MarriageApproveButton=InterfaceWidgets.Button("同意赐婚",actions,Status.font,()=>DecideMarriage(1));
            MarriageRefuseButton=InterfaceWidgets.Button("不同意",actions,Status.font,()=>DecideMarriage(0));
            MarriageCloseButton=InterfaceWidgets.Button("稍后处理",actions,Status.font,CloseMarriage);
        }
        void DecideMarriage(int option)
        {
            var id=marriagePerson;var mate=marriageMate;var turn=marriageTurn;
            CloseMarriage();Send(CommandKind.ResolveMarriage,id,mate,definition:turn,argument:option);
        }
        void MarriageDetails(Entity person,RectTransform rows)
        {
            var id=em.GetComponentData<Identity>(person);var p=em.GetComponentData<Royal>(person);
            var portrait=InterfaceWidgets.Rect("Portrait placeholder",rows,Vector2.zero,Vector2.one);portrait.gameObject.AddComponent<LayoutElement>().preferredHeight=160;PortraitImageBinding.Bind(portrait.gameObject.AddComponent<Image>(),em,root,id.Id);
            void Line(string value,int size=18)
            {var r=InterfaceWidgets.Rect("Person detail",rows,Vector2.zero,Vector2.one);var t=InterfaceWidgets.Text(value,r,Status.font,size);r.gameObject.AddComponent<LayoutElement>().preferredHeight=Mathf.Max(38,t.GetPreferredValues(value,420,0).y+12);}
            Line(id.Name.ToString(),24);Line(GenderName(p.Gender)+" · "+p.Age+" 岁 · "+(p.Role==0?"国王":p.Role==4?"交际人物":"王室成员"));
            Line("影响力 "+p.Influence.ToString("0.0")+" / 100 · 成长性 "+p.Growth.ToString("0.00"));
            Line("父亲："+PersonName(RoyalFamilyOps.ParentOfGender(em,person,PersonGender.Male)));Line("母亲："+PersonName(RoyalFamilyOps.ParentOfGender(em,person,PersonGender.Female)));Line("配偶："+PersonName(p.Spouse));
            if(em.HasComponent<Talent>(person))
            {var t=em.GetComponentData<Talent>(person);Line("人才等级 "+t.Level+" · 经验 "+t.Experience);Line(t.Slot>=0?"岗位："+Name(t.Slot)+" · "+(t.Paid!=0?"已付薪":"未付薪"):"当前未任职");Line("与国王好感 "+p.Affection+" / 100");}
            bool traits=false;foreach(var trait in em.GetBuffer<TraitEntry>(person))if(trait.Revealed!=0){traits=true;Line("特性："+Name(trait.Definition)+(trait.Active!=0?"（已激活）":"（尚未激活）"));}
            if(!traits)Line("暂无已知特性");
            if(p.FateUntil>0)Line("知天命：自然寿限还剩 "+System.Math.Max(0,p.FateUntil-em.GetComponentData<Session>(root).Turn)+" 回合");
        }
    }
}
