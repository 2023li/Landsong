using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.UI;


namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public void OpenInvitations(ulong source = 0) { questTypeMask = 15; questSourceFilter = source; OpenPanel("任务"); }
        void InvitationPool(bool day)
        {
            var entries=new List<(Entity Source,int Slot,QuestOfferQuote Quote)>();
            using var buildings=Sim.OrderedEntities<Building>(em);
            foreach(var e in buildings)
            {
                var id=em.GetComponentData<Identity>(e);if(questSourceFilter!=0 && id.Id!=questSourceFilter)continue;
                var slots=em.GetBuffer<QuestOfferSlot>(e);
                for(var i=0;i<slots.Length;i++)
                {
                    var slot=slots[i];if((questTypeMask&(1<<slot.Type))==0)continue;
                    var rule=QuestOfferOps.SourceRule(em,root,e,slot.Type);if(rule.Level<0 || slot.Index>=rule.Amount)continue;
                    entries.Add((e,i,QuestOfferOps.Quote(em,root,e,i)));
                }
            }
            entries.Sort((a,b)=>
            {
                if((a.Quote.Offer!=0)!=(b.Quote.Offer!=0))return a.Quote.Offer!=0?-1:1;
                if(a.Quote.Offer!=0)return QuestOps.CompareValue(em,root,Sim.Find(em,a.Quote.Offer),Sim.Find(em,b.Quote.Offer));
                var order=em.GetComponentData<Identity>(a.Source).Id.CompareTo(em.GetComponentData<Identity>(b.Source).Id);
                return order!=0?order:a.Slot.CompareTo(b.Slot);
            });
            foreach(var entry in entries)
            {
                var e=entry.Source;var at=entry.Slot;var q=entry.Quote;var id=em.GetComponentData<Identity>(e);var slot=em.GetBuffer<QuestOfferSlot>(e)[at];
                var source=QuestBuildingSource(e)+" 提供 · "+QuestOfferOps.TypeName(slot.Type)+" "+(slot.Index+1);
                var key="offer:"+id.Id+":"+at;
                if(q.Offer!=0)
                {
                    var quest=Sim.Find(em,q.Offer);source+=" · 价值 "+QuestOps.RewardValue(em,root,em.GetComponentData<Identity>(quest).Definition);
                    ShowQuestCard(key,QuestPoolRows,source,quest,day);continue;
                }
                var card=QuestCard(key,QuestPoolRows);var available=QuestOfferOps.Available(em,root,e,out var reason);
                var wait=!available?"刷新暂停："+reason:q.Code==ResultCode.Success?q.Wait>0?q.Wait+" 回合后刷新":"下次结算刷新":q.Reason;
                card.Show(0,source,wait,"",true,false,null,"",null);Clear(card.Body);
                Row("立即邀约："+CostText(q.Costs),day && q.Code==ResultCode.Success && BuildingCostOps.CanPay(em,root,q.Costs)?()=>ConfirmQuestRecruit(id,at):null,parent:card.Body);
            }
        }
        void BindInvitationMessage(GameEvent message)
        {
            var button = Message.GetComponent<Button>(); if (button == null) button = Message.gameObject.AddComponent<Button>();
            Message.raycastTarget = true; button.targetGraphic = Message;
            button.onClick.RemoveAllListeners();
            if (message.Kind != EventKind.Message || !message.Message.ToString().StartsWith("新邀约：", StringComparison.Ordinal)) return;
            var source = message.Target;
            button.onClick.AddListener(() => { FocusBuilding(source); OpenInvitations(source); });
        }
    }
}
