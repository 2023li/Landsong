using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public void OpenMarriagePicker(ulong id)
        {
            if(SoldierDetailsOpen||MarriageOpen||PersonRequestsOpen||PortraitOpen||intel||PauseMenu!=null&&PauseMenu.IsOpen||BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf)return;
            var person=Sim.Find(em,id);if(!CourtDay||em.GetComponentData<Session>(root).Paused!=0||!RoyalFamilyOps.CanArrange(em,root,person))return;
            Send(CommandKind.PrepareMarriage,id);
            marriageArranging=true;marriagePerson=id;marriageMate=0;marriageKing=em.GetComponentData<Identity>(CourtOps.Monarch(em)).Id;marriageCandidatesSignature=null;
            BuildMarriagePicker();
        }
        void RefreshMarriagePicker(Entity person)
        {
            var king=CourtOps.Monarch(em);
            if(!RoyalFamilyOps.CanArrange(em,root,person)||king==Entity.Null||em.GetComponentData<Identity>(king).Id!=marriageKing){CloseMarriage();return;}
            var candidates=RoyalFamilyOps.Candidates(em,root,person);var signature=string.Join(",",candidates.Select(e=>em.GetComponentData<Identity>(e).Id));
            if(marriageMate!=0&&!candidates.Contains(Sim.Find(em,marriageMate))){marriageMate=0;BuildMarriagePicker();}
            if(marriageMate==0&&signature!=marriageCandidatesSignature){marriageCandidatesSignature=signature;BuildMarriagePicker();}
        }
        void BuildMarriagePicker()
        {
            if(MarriageWindow!=null){MarriageWindow.SetActive(false);Destroy(MarriageWindow);}
            EndBuildingPlacement();EndInventoryDrag();cameraDragging=false;
            MarriageWindow=InterfaceWidgets.Modal("Arrange royal marriage",GetComponentInParent<Canvas>().transform,450,out var card);
            var title=InterfaceWidgets.Text("为 "+PersonName(marriagePerson)+" 选择配偶",InterfaceWidgets.Rect("Title",card,new Vector2(.02f,.89f),new Vector2(.98f,1)),Status.font,24);title.alignment=TMPro.TextAlignmentOptions.Center;
            var person=Sim.Find(em,marriagePerson);MarriageDetails(person,InterfaceWidgets.Scroll(card,"Royal person",new Vector2(.025f,.2f),new Vector2(.49f,.88f)));
            var right=InterfaceWidgets.Scroll(card,"Spouse candidates",new Vector2(.51f,.2f),new Vector2(.975f,.88f));
            if(marriageMate!=0)MarriageDetails(Sim.Find(em,marriageMate),right);
            else
            {
                var candidates=RoyalFamilyOps.Candidates(em,root,person);
                foreach(var e in candidates)
                {
                    var identity=em.GetComponentData<Identity>(e);var p=em.GetComponentData<Royal>(e);ulong key=identity.Id;
                    InterfaceWidgets.Button(identity.Name+" · "+GenderName(p.Gender)+" · "+p.Age+" 岁"+(em.HasComponent<Talent>(e)?" · 人才":""),right,Status.font,()=>{marriageMate=key;BuildMarriagePicker();},52);
                }
                if(candidates.Count==0)InterfaceWidgets.Button("正在准备可选配偶…",right,Status.font,null,48);
            }
            InterfaceWidgets.Text("选中配偶可查看详情；确认赐婚后，原有请求随婚姻结束。",InterfaceWidgets.Rect("Hint",card,new Vector2(.025f,.125f),new Vector2(.975f,.2f)),Status.font,16);
            var actions=InterfaceWidgets.Rect("Actions",card,new Vector2(.06f,.035f),new Vector2(.94f,.12f));actions.gameObject.AddComponent<HorizontalLayoutGroup>().spacing=12;
            MarriageApproveButton=InterfaceWidgets.Button("确认赐婚",actions,Status.font,()=>{ulong id=marriagePerson,mate=marriageMate;CloseMarriage();Send(CommandKind.ArrangeMarriage,id,mate);});MarriageApproveButton.interactable=marriageMate!=0;
            MarriageRefuseButton=InterfaceWidgets.Button("重新选择",actions,Status.font,()=>{marriageMate=0;BuildMarriagePicker();});
            MarriageCloseButton=InterfaceWidgets.Button("取消",actions,Status.font,CloseMarriage);
        }
    }
}
