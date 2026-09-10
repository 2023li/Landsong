using System;
using System.Linq;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public GameObject PersonRequestsWindow {get;private set;}
        public Button PersonRequestsCloseButton {get;private set;}
        public bool PersonRequestsOpen=>PersonRequestsWindow!=null&&PersonRequestsWindow.activeSelf;
        ulong requestsPerson;string requestsSignature;RectTransform requestsRows;TMP_Text requestsTitle;
        public void ShowPersonRequests(ulong id)
        {
            if(SoldierDetailsOpen||PersonRequestsOpen||MarriageOpen||PortraitOpen||intel||PauseMenu!=null&&PauseMenu.IsOpen||BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf)return;
            if(!CourtOps.Alive(em,Sim.Find(em,id)))return;
            if(PersonRequestsWindow==null)
            {
                PersonRequestsWindow=InterfaceWidgets.Modal("Person requests",GetComponentInParent<Canvas>().transform,460,out var card);
                requestsTitle=InterfaceWidgets.Text("",InterfaceWidgets.Rect("Title",card,new Vector2(.03f,.89f),new Vector2(.97f,.99f)),Status.font,24);
                requestsTitle.alignment=TextAlignmentOptions.Center;
                requestsRows=InterfaceWidgets.Scroll(card,"Requests",new Vector2(.04f,.18f),new Vector2(.96f,.88f));
                PersonRequestsCloseButton=InterfaceWidgets.Button("关闭（稍后处理）",InterfaceWidgets.Rect("Close",card,new Vector2(.3f,.035f),new Vector2(.7f,.13f)),Status.font,ClosePersonRequests);
            }
            EndBuildingPlacement();EndInventoryDrag();cameraDragging=false;
            requestsPerson=id;requestsSignature=null;PersonRequestsWindow.SetActive(true);
            requestsRows.GetComponentInParent<ScrollRect>().verticalNormalizedPosition=1;RefreshPersonRequests();
        }
        public void ClosePersonRequests()
        {if(PersonRequestsWindow!=null)PersonRequestsWindow.SetActive(false);nextRefresh=0;}
        void RefreshPersonRequests()
        {
            if(!PersonRequestsOpen)return;
            var person=Sim.Find(em,requestsPerson);if(!CourtOps.Alive(em,person)){ClosePersonRequests();return;}
            var list=PersonRequestOps.Pending(em,root,person);bool can=CourtDay&&em.GetComponentData<Session>(root).Paused==0;
            int definition=em.GetComponentData<Identity>(person).Definition;
            var task=Sim.ValidDefinition(em,root,definition)?Sim.Rule(em,root,definition,RuleKind.SocialTask):new Rule{Level=-1};
            bool supplies=task.Level>=0&&InventoryOps.Count(em,root,task.Target)>=task.Amount;
            bool expedition=FeatureOps.Unlocked(em,root,"Expedition")&&CourtOps.AvailableCaptain(em,root,person);
            string next=requestsPerson+"/"+can+"/"+supplies+"/"+expedition+"/"+string.Join("|",list.Select(r=>$"{r.Kind}:{r.Turn}:{r.Target}:{r.Travelling}"));
            if(next==requestsSignature)return;
            if(Mouse.current!=null&&(Mouse.current.leftButton.isPressed||Mouse.current.leftButton.wasReleasedThisFrame))return;
            requestsSignature=next;requestsTitle.text=PersonName(requestsPerson)+"的请求（"+list.Count+"）";
            foreach(Transform child in requestsRows){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            void Line(string value)
            {var line=InterfaceWidgets.Text(value,requestsRows,Status.font,18);line.gameObject.AddComponent<LayoutElement>().preferredHeight=Mathf.Max(48,line.GetPreferredValues(value,Mathf.Max(300,requestsRows.rect.width-24),0).y+16);}
            void ActionButton(string label,Action action)=>InterfaceWidgets.Button(label,requestsRows,Status.font,action,42);
            if(list.Count==0)Line("暂无待处理请求。");
            foreach(var entry in list)
            {
                ulong id=requestsPerson;
                if(entry.Kind==PersonRequestKind.Portrait){Line("丽质初成：可以塑造一次容貌。");ActionButton("塑造容貌",can?()=>{ClosePersonRequests();OpenPortrait(id);}:null);}
                else if(entry.Kind==PersonRequestKind.Marriage)
                {
                    Line("赐婚请求 · 第 "+entry.Turn+" 回合\n希望能和"+PersonName(entry.Target)+"结婚");
                    ActionButton("查看赐婚请求",()=>{ClosePersonRequests();ShowMarriage(id);});
                }
                else if(entry.Kind==PersonRequestKind.Expedition)
                {
                    Line("渴望一次远征 · 第 "+entry.Turn+" 回合\n"+(entry.Travelling?"远征中，存活归来后完成。":"等待安排：担任远征队长，结束远征并存活归来后完成。"));
                    if(!entry.Travelling)
                    {
                        ActionButton(expedition?"安排远征":"暂时无法担任远征队长",can&&expedition?()=>{ClosePersonRequests();expeditionCaptain=id;OpenPanel("远征");}:null);
                        int turn=entry.Turn;
                        ActionButton("拒绝远征请求",can?()=>{ClosePersonRequests();Send(CommandKind.RefusePersonRequest,id,definition:turn);}:null);
                    }
                }
                else if(entry.Kind==PersonRequestKind.SocialTask)
                {
                    Line("个人委托\n提交 "+Name(task.Target)+" × "+task.Amount+"，好感 +"+task.B);
                    ActionButton(supplies?"提交委托物品":"委托物品不足",can&&supplies?()=>{ClosePersonRequests();Send(CommandKind.CompleteSocialTask,id);}:null);
                }
            }
        }
    }
}
