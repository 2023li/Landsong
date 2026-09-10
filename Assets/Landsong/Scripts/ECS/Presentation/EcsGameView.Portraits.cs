using System;
using System.Collections.Generic;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public GameObject PortraitWindow {get;private set;}
        public Button PortraitConfirmButton {get;private set;}
        public Button PortraitCloseButton {get;private set;}
        public Button BeautyEventButton {get;private set;}
        public bool PortraitOpen=>PortraitWindow!=null&&PortraitWindow.activeSelf;
        PortraitDNA portraitDraft;ulong portraitPerson;Image portraitPreview;float nextPortraitRefresh;
        static readonly string[] PortraitPartNames={"脸型","耳朵","眼睛","眉毛","鼻子","嘴型","发型","胡须","身体","服装","头饰","面饰","饰品"};
        public void ClosePortrait(){if(PortraitWindow!=null)PortraitWindow.SetActive(false);nextRefresh=nextPortraitRefresh=0;}
        void RefreshPortraitCustomization()
        {
            if(BeautyEventButton!=null&&(IsPanelOpen||PortraitOpen||MarriageOpen||PersonRequestsOpen||intel||PauseMenu!=null&&PauseMenu.IsOpen))BeautyEventButton.gameObject.SetActive(false);
            if(PortraitOpen&&!PortraitOps.CanCustomize(em,root,Sim.Find(em,portraitPerson)))ClosePortrait();
            if(PortraitOpen)PortraitConfirmButton.interactable=CourtDay&&em.GetComponentData<Session>(root).Paused==0;
            if(Time.unscaledTime<nextPortraitRefresh)return;nextPortraitRefresh=Time.unscaledTime+.25f;
            ulong first=0;int count=0;using(var people=Sim.OrderedEntities<Royal>(em))foreach(var person in people)if(PortraitOps.CanCustomize(em,root,person)){if(first==0)first=em.GetComponentData<Identity>(person).Id;count++;}
            if(BeautyEventButton==null&&count==0)return;
            if(BeautyEventButton==null)
            {BeautyEventButton=InterfaceWidgets.Button("",GetComponentInParent<Canvas>().transform,Status.font,null);var r=(RectTransform)BeautyEventButton.transform;r.anchorMin=new Vector2(.77f,.24f);r.anchorMax=new Vector2(.99f,.30f);r.offsetMin=r.offsetMax=Vector2.zero;}
            BeautyEventButton.gameObject.SetActive(count>0&&!intel&&!IsPanelOpen&&!PortraitOpen&&!MarriageOpen&&!PersonRequestsOpen&&(PauseMenu==null||!PauseMenu.IsOpen));BeautyEventButton.interactable=true;
            BeautyEventButton.GetComponentInChildren<TMP_Text>().text="丽质初成："+PersonName(first)+"（待塑容 "+count+"）";
            BeautyEventButton.onClick.RemoveAllListeners();BeautyEventButton.onClick.AddListener(()=>OpenPortrait(first));
        }
        public void OpenPortrait(ulong id)
        {
            if(SoldierDetailsOpen||PortraitOpen||MarriageOpen||PersonRequestsOpen||intel||PauseMenu!=null&&PauseMenu.IsOpen||BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf)return;
            var person=Sim.Find(em,id);if(!PortraitOps.CanCustomize(em,root,person))return;
            portraitPerson=id;portraitDraft=em.GetComponentData<PortraitDNA>(person);
            if(PortraitWindow!=null){PortraitWindow.SetActive(false);Destroy(PortraitWindow);}
            EndBuildingPlacement();EndInventoryDrag();cameraDragging=false;
            PortraitWindow=InterfaceWidgets.Modal("Beauty customization",GetComponentInParent<Canvas>().transform,470,out var card);
            InterfaceWidgets.Text("丽质 · 为"+PersonName(id)+"塑造容貌",InterfaceWidgets.Rect("Title",card,new Vector2(.03f,.89f),new Vector2(.97f,.99f)),Status.font,24);
            var preview=InterfaceWidgets.Rect("Portrait preview",card,new Vector2(.04f,.34f),new Vector2(.39f,.83f));portraitPreview=preview.gameObject.AddComponent<Image>();portraitPreview.preserveAspect=true;
            InterfaceWidgets.Text("仅有一次机会。确认后基础容貌固定，仍会自然衰老。修改后的颜色可由之后出生的子女继承。",InterfaceWidgets.Rect("Rules",card,new Vector2(.04f,.16f),new Vector2(.4f,.32f)),Status.font,17);
            var rows=InterfaceWidgets.Scroll(card,"Appearance choices",new Vector2(.43f,.18f),new Vector2(.97f,.87f));
            var library=em.GetComponentData<PortraitLibrary>(root).Value;var gender=PortraitOps.Gender(em,person);
            for(int slot=0;slot<PortraitOps.Slots;slot++)
            {
                int at=slot;var choices=new List<int>();if(PortraitOps.Compatible(ref library.Value,0,(PortraitPartType)at,gender))choices.Add(0);
                for(int i=0;i<library.Value.Parts.Length;i++){ref var part=ref library.Value.Parts[i];if(part.Type==(PortraitPartType)at&&PortraitOps.Compatible(ref library.Value,part.Id,(PortraitPartType)at,gender))choices.Add(part.Id);}
                Button button=null;string Label()=>PortraitPartNames[at]+"："+(portraitDraft.Parts[at]==0?"无":"款式 "+(choices.IndexOf(portraitDraft.Parts[at])+1))+"（点击切换）";
                button=InterfaceWidgets.Button(Label(),rows,Status.font,choices.Count>1?()=>{int index=choices.IndexOf(portraitDraft.Parts[at]);portraitDraft.Parts[at]=choices[(index+1)%choices.Count];button.GetComponentInChildren<TMP_Text>().text=Label();UpdatePortraitPreview();}:null,42);
            }
            for(int channel=0;channel<3;channel++)for(int component=0;component<3;component++)
            {
                int ch=channel,part=component;var color=ch==0?portraitDraft.Skin:ch==1?portraitDraft.Hair:portraitDraft.Eyes;int value=part==0?color.r:part==1?color.g:color.b;
                var slider=InterfaceWidgets.Slider((ch==0?"肤色":ch==1?"发色":"眼睛颜色")+" · "+(part==0?"红":part==1?"绿":"蓝"),rows,Status.font,0,255,value,v=>
                {var c=ch==0?portraitDraft.Skin:ch==1?portraitDraft.Hair:portraitDraft.Eyes;if(part==0)c.r=(byte)v;else if(part==1)c.g=(byte)v;else c.b=(byte)v;if(ch==0)portraitDraft.Skin=c;else if(ch==1)portraitDraft.Hair=c;else portraitDraft.Eyes=c;UpdatePortraitPreview();});slider.wholeNumbers=true;
            }
            var actions=InterfaceWidgets.Rect("Actions",card,new Vector2(.10f,.035f),new Vector2(.90f,.12f));actions.gameObject.AddComponent<HorizontalLayoutGroup>().spacing=12;
            PortraitConfirmButton=InterfaceWidgets.Button("确定容貌（仅一次）",actions,Status.font,()=>{var dna=portraitDraft;ulong target=portraitPerson;ClosePortrait();Send(CommandKind.CustomizePortrait,target,dna.Seed,text:PortraitOps.Payload(dna));});
            PortraitCloseButton=InterfaceWidgets.Button("稍后再定",actions,Status.font,ClosePortrait);UpdatePortraitPreview();
        }
        void UpdatePortraitPreview()=>PortraitImageBinding.Bind(portraitPreview,em,root,portraitPerson,portraitDraft);
        GameObject SoldierPortraitRow(Entity unit,string label,Action click=null,bool right=false)
        {
            var row=Row(label,click,right);var child=row.transform.Find("Person portrait");Image portrait;
            if(child==null){var rect=InterfaceWidgets.Rect("Person portrait",row.transform,new Vector2(0,0),new Vector2(0,1));rect.pivot=new Vector2(0,.5f);rect.sizeDelta=new Vector2(60,0);portrait=rect.gameObject.AddComponent<Image>();}else portrait=child.GetComponent<Image>();
            portrait.gameObject.SetActive(true);row.GetComponentInChildren<TMP_Text>().margin=new Vector4(68,4,8,4);row.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(68,row.GetComponent<LayoutElement>().preferredHeight);
            PortraitImageBinding.Bind(portrait,em,root,em.GetComponentData<Identity>(unit).Id);return row;
        }
    }
}
