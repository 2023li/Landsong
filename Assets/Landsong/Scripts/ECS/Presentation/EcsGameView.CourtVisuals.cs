using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        CourtPresentationView courtGraph;int policyFocus=-1;
        public Sprite RoyalCrownIcon;
        void RefreshCourtPresentation()
        {
            bool active=IsPanelOpen&&!intel&&(Panel=="王室"||Panel=="人才"||Panel=="政策");if(courtGraph!=null)courtGraph.gameObject.SetActive(active);if(!active){RefreshRoyalDetails(false);return;}
            if(courtGraph==null)courtGraph=CourtPresentationView.Create(GetComponentInParent<Canvas>().transform,Status.font,ClosePanel);
            courtGraph.CrownIcon=RoyalCrownIcon;
            RefreshRoyalDetails(Panel=="王室");var cards=new List<CourtCard>();var rows=new Dictionary<int,int>();var catalog=PresentationRuntime.Instance?.Catalog;
            if(Panel=="政策")
            {
                ForDefinitions(ContentKind.Policy,(i,d)=>
                {
                    bool chosen=false;foreach(var choice in em.GetBuffer<PolicyChoice>(root))if(choice.Definition==i)chosen=true;int col=Mathf.Max(0,d.Level-1);rows.TryGetValue(col,out int row);rows[col]=row+1;var source=BuildingSource(i);
                    var card=new CourtCard {Id=(ulong)i+1,Column=col,Row=row,Title=d.Name.ToString(),Detail="民意需 "+d.Cost+"\n"+(chosen?(CourtOps.PolicyActive(em,root,i)?"已生效":"条件不足，暂停"):"未采用")+"\n"+(!ProgressionOps.Prerequisites(em,root,i)?"缺少前置":"点击定位操作"),Portrait=source?.Icon,Selected=policyFocus==i,Click=()=>{policyFocus=i;nextRefresh=0;PrimaryRows.GetComponentInParent<UnityEngine.UI.ScrollRect>().verticalNormalizedPosition=1;}};
                    for(int n=0;n<d.RuleCount;n++){var rule=Sim.GetRule(em,root,d.RuleStart+n);if(rule.Kind==RuleKind.Prerequisite&&Sim.ValidDefinition(em,root,rule.Target)&&Sim.Definition(em,root,rule.Target).Kind==ContentKind.Policy){if(card.Parent==0)card.Parent=(ulong)rule.Target+1;else card.SecondParent=(ulong)rule.Target+1;}}
                    cards.Add(card);
                });courtGraph.Show("政策 · 分层展示；左侧操作沿用原规则",cards);return;
            }
            using(var all=Sim.OrderedEntities<Royal>(em))foreach(var entity in all)
            {
                var person=em.GetComponentData<Royal>(entity);if(Panel=="王室"&&person.Role==4||Panel=="人才"&&!em.HasComponent<Talent>(entity))continue;var id=em.GetComponentData<Identity>(entity);int col=Panel=="人才"?0:Mathf.Max(0,person.Generation);rows.TryGetValue(col,out int row);rows[col]=row+1;
                string definition=Sim.ValidDefinition(em,root,id.Definition)?Sim.Definition(em,root,id.Definition).Id.ToString():"";var portrait=catalog?.Face(definition,id.Id)??BuildingSource(id.Definition)?.Icon;var key=id.Id;
                cards.Add(new CourtCard {Id=key,Parent=person.Parent,SecondParent=person.SecondParent,Spouse=person.Spouse,Column=col,Row=row,Title=id.Name.ToString(),Detail=person.Age+" 岁 · "+(person.Alive==0?"已逝":person.Role==0?"君王":person.Role==1?"配偶":person.Role==3?"前朝成员":person.Role==4?"交际人物":"王室成员")+(Panel=="人才"?"\n影响力 "+person.Influence.ToString("0.0"):"")+(CourtOps.State(em,root).Crown==key?" · 储君":""),BindPortrait=image=>PortraitImageBinding.Bind(image,em,root,key),RequestCount=PersonRequestOps.Pending(em,root,entity).Count,Influence=person.Influence,Monarch=person.Alive!=0&&person.Role==0,EverMonarch=person.EverMonarch!=0,Portrait=portrait,Dead=person.Alive==0,Selected=courtPerson==key,Click=()=>{if(Panel=="王室")SelectRoyalPerson(key);else{courtPerson=key;nextRefresh=0;}}});
            }
            courtGraph.Show(Panel=="人才"?"人物画像 · 点击查看交际 / 任职":"王室家谱 · 金线标记曾登基的子代",cards,Panel=="王室");
        }
    }
}
