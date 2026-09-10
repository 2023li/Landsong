using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public GameObject SoldierDetailsWindow {get;private set;}
        public bool SoldierDetailsOpen=>SoldierDetailsWindow!=null&&SoldierDetailsWindow.activeSelf;
        public TMP_InputField SoldierDetailsName {get;private set;}
        TMP_Text soldierDetailsAge,soldierDetailsStats,soldierDetailsAbilities;
        ulong detailsSoldier;string garrisonSignature;
        void RefreshBuildingGarrison(Entity site)
        {
            var card=BuildingCard;int capacity=em.GetComponentData<BuildingStats>(site).Garrison;ulong home=em.GetComponentData<Identity>(site).Id;
            card.GarrisonBlock.SetActive(capacity>0);if(capacity<=0)return;
            card.GarrisonLabel.text=$"驻军：{MilitaryOps.GarrisonCount(em,home)}/{capacity}";
            BuildingDetailsView.Bind(card.AdjustGarrison,()=>OpenPanel("驻军"));
            string signature=home+":"+capacity;
            for(int i=1;i<=capacity;i++){var e=MilitaryOps.AtSlot(em,home,i);signature+=e==Entity.Null?"/0":"/"+em.GetComponentData<Identity>(e).Id+":"+em.GetComponentData<Identity>(e).Name+":"+Sim.Alive(em,e);}
            if(signature==garrisonSignature)return;garrisonSignature=signature;
            foreach(Transform child in card.GarrisonSlots){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            card.GarrisonSlots.sizeDelta=new Vector2(capacity*90+8,0);
            for(int i=1;i<=capacity;i++)
            {
                var unit=MilitaryOps.AtSlot(em,home,i);ulong id=unit==Entity.Null?0:em.GetComponentData<Identity>(unit).Id;
                var area=InterfaceWidgets.Rect("Slot "+i,card.GarrisonSlots,new Vector2(0,0),new Vector2(0,1));area.pivot=new Vector2(0,.5f);area.sizeDelta=new Vector2(82,-8);area.anchoredPosition=new Vector2(4+(i-1)*90,0);
                var button=InterfaceWidgets.Button(id==0?"空":"",area,Status.font,()=>{if(id==0)OpenPanel("驻军");else OpenSoldierDetails(id);});button.image.color=Color.white;
                if(id==0){button.GetComponentInChildren<TMP_Text>().color=Color.black;continue;}
                var art=InterfaceWidgets.Rect("Portrait placeholder",button.transform,new Vector2(.12f,.3f),new Vector2(.88f,.92f));art.gameObject.AddComponent<Image>().color=Sim.Alive(em,unit)?new Color(.87f,.24f,.24f):Color.gray;
                var caption=InterfaceWidgets.Text(em.GetComponentData<Identity>(unit).Name.ToString(),InterfaceWidgets.Rect("Soldier name",button.transform,Vector2.zero,new Vector2(1,.3f)),Status.font,14);caption.color=Color.black;caption.alignment=TextAlignmentOptions.Center;caption.margin=Vector4.zero;
            }
        }
        public void OpenSoldierDetails(ulong id)
        {
            if(MarriageOpen||PersonRequestsOpen||PortraitOpen||intel||PauseMenu!=null&&PauseMenu.IsOpen||BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf)return;
            var unit=Sim.Find(em,id);if(unit==Entity.Null||!em.HasComponent<Soldier>(unit))return;
            if(SoldierDetailsWindow==null)
            {
                SoldierDetailsWindow=InterfaceWidgets.Modal("Soldier details",transform,230,out var card);
                var portrait=InterfaceWidgets.Rect("Portrait placeholder",card,new Vector2(.035f,.52f),new Vector2(.31f,.94f));portrait.gameObject.AddComponent<Image>().color=Color.white;
                SoldierDetailsName=InterfaceWidgets.Input("士兵姓名",InterfaceWidgets.Rect("Name",card,new Vector2(.36f,.83f),new Vector2(.91f,.94f)),Status.font,30);
                SoldierDetailsName.onEndEdit.AddListener(value=>Send(CommandKind.RenameSoldier,detailsSoldier,text:value));
                soldierDetailsAge=InterfaceWidgets.Text("",InterfaceWidgets.Rect("Age",card,new Vector2(.36f,.74f),new Vector2(.91f,.83f)),Status.font,21);
                soldierDetailsStats=InterfaceWidgets.Text("",InterfaceWidgets.Rect("Attributes",card,new Vector2(.36f,.6f),new Vector2(.96f,.72f)),Status.font,22);
                var abilities=InterfaceWidgets.Scroll(card,"Soldier abilities",new Vector2(.39f,.06f),new Vector2(.96f,.57f));
                soldierDetailsAbilities=InterfaceWidgets.Text("",abilities,Status.font,19);soldierDetailsAbilities.gameObject.AddComponent<LayoutElement>().preferredHeight=280;
                void Equipment(string name,Vector2 min,Vector2 max){InterfaceWidgets.Button(name+"\n未开放",InterfaceWidgets.Rect(name,card,min,max),Status.font,null);}
                Equipment("头盔",new Vector2(.19f,.32f),new Vector2(.31f,.48f));Equipment("武器",new Vector2(.045f,.11f),new Vector2(.165f,.27f));Equipment("铠甲",new Vector2(.20f,.11f),new Vector2(.32f,.27f));
                InterfaceWidgets.Button("X",InterfaceWidgets.Rect("Close",card,new Vector2(.93f,.94f),Vector2.one),Status.font,CloseSoldierDetails,32);
            }
            detailsSoldier=id;SoldierDetailsName.SetTextWithoutNotify(em.GetComponentData<Identity>(unit).Name.ToString());SoldierDetailsWindow.SetActive(true);RefreshSoldierDetails();
        }
        public void CloseSoldierDetails(){if(SoldierDetailsWindow!=null)SoldierDetailsWindow.SetActive(false);nextRefresh=0;}
        void RefreshSoldierDetails()
        {
            if(!SoldierDetailsOpen)return;var unit=Sim.Find(em,detailsSoldier);if(unit==Entity.Null||!em.HasComponent<Soldier>(unit)){CloseSoldierDetails();return;}
            var s=em.GetComponentData<Soldier>(unit);var stats=MilitaryOps.SoldierStats(em,root,unit);var d=Sim.Definition(em,root,em.GetComponentData<Identity>(unit).Definition);var session=em.GetComponentData<Session>(root);
            SoldierDetailsName.interactable=session.Phase==Phase.Day&&session.Paused==0&&session.CheckpointPending==0&&Sim.Alive(em,unit);
            if(!SoldierDetailsName.isFocused)SoldierDetailsName.SetTextWithoutNotify(em.GetComponentData<Identity>(unit).Name.ToString());
            soldierDetailsAge.text="年龄："+(em.HasComponent<SoldierPerson>(unit)?PortraitOps.Age(em,unit)+" 岁":"暂无记录");
            float health=em.HasComponent<Health>(unit)?em.GetComponentData<Health>(unit).Current:stats.Health;
            float maximum=em.HasComponent<Health>(unit)?em.GetComponentData<Health>(unit).Maximum:stats.Health;
            soldierDetailsStats.text=$"力量：—    知识：—\n敏捷：{stats.Speed:0.#}    血量：{health:0.#}/{maximum:0.#}";
            soldierDetailsAbilities.text=$"士兵能力\n{d.Name} · Lv.{MilitaryOps.Level(d.SoldierGrowth,s.Experience)}\n攻击：{stats.Damage:0.#}\n经验：{s.Experience}\n自动巡逻与攻击\n\n驻地：{EntityName(s.Garrison)}\n槽位：{s.Slot}";
        }
    }
}
