using System;
using System.Collections.Generic;
using System.Linq;
using Landsong.ECS.Authoring;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsGameView
    {
        public BuildingDetailsView BuildingCard {get;private set;}
        public bool BuildingRangesVisible=>showBuildingRange;
        public int ResourcePathCellCount {get;private set;}
        void InitializeBuildingCard()
        {
            BuildingCard=BuildingDetailsView.Create(BuildingDetailsPanel,Status.font,NameInput,BuildingDetailsClose);
            BuildingDetailsRows=BuildingCard.Rows;Rename.gameObject.SetActive(false);
            NameInput.onEndEdit.AddListener(value=>{if(BuildingCard.BuildingId!=0)Send(CommandKind.Rename,BuildingCard.BuildingId,text:value);});
        }
        public void SelectBuildingDetails(ulong id)
        {
            if(SoldierDetailsOpen||MarriageOpen||PersonRequestsOpen||BuildingConfirmPanel!=null&&BuildingConfirmPanel.activeSelf)return;
            var entity=Sim.Find(em,id);if(entity==Entity.Null||!em.HasComponent<Building>(entity))return;
            ClosePanel();selected=id;showBuildingDetails=true;showBuildingRange=true;rangeRevision=-1;nextRefresh=0;
        }
        void RefreshBuildingCard(Entity entity)
        {
            var card=BuildingCard;var id=em.GetComponentData<Identity>(entity);var b=em.GetComponentData<Building>(entity);var stats=em.GetComponentData<BuildingStats>(entity);var d=Sim.Definition(em,root,id.Definition);var session=em.GetComponentData<Session>(root);
            bool can=session.Phase==Phase.Day&&session.Paused==0&&session.CheckpointPending==0;bool normal=Sim.Operational(em,entity);
            card.Select(id.Id,id.Name.ToString());card.Name.interactable=can;card.Icon.sprite=BuildingSource(id.Definition)?.Icon;card.Icon.color=Color.white;
            card.Level.text="LV"+b.Level;
            var xp=Sim.Rule(em,root,id.Definition,RuleKind.Experience,b.Level);int required=Mathf.Max(0,xp.B);bool full=required==0||b.Experience>=required;bool maximum=b.Level>=d.Level;
            BuildingDetailsView.Span(card.ExperienceFill,0,required>0?(float)b.Experience/required:maximum?1:0);
            card.Experience.text=maximum&&full?"MAX":b.Experience+" / "+required;
            card.Upgrade.gameObject.SetActive(!(maximum&&full));var upgrade=BuildingOps.CheckUpgrade(em,root,entity);
            BuildingDetailsView.Bind(card.Upgrade,()=>{if(!can){Message.text="请在未暂停的白天升级";return;}ConfirmBuildingCommand(CommandKind.Upgrade);});
            card.Upgrade.image.color=can&&upgrade.Allowed?new Color(.2f,.4f,.66f):new Color(.34f,.36f,.36f);
            var outputs=new List<string>();if(stats.MaxPopulation+stats.BasePopulation>0)outputs.Add("人口 +"+(stats.MaxPopulation+stats.BasePopulation));int research=0;
            for(int i=0;i<d.RuleCount;i++){var r=Sim.GetRule(em,root,d.RuleStart+i);if(EconomyOps.Matches(r,RuleKind.ResearchOutput,b.Level))research+=r.Amount;}
            if(research>0)outputs.Add("科研值 +"+research);if(stats.Garrison>0)outputs.Add("士兵槽 "+stats.Garrison);
            for(int i=0;i<d.RuleCount;i++){var r=Sim.GetRule(em,root,d.RuleStart+i);if(EconomyOps.Matches(r,RuleKind.Warehouse,b.Level))outputs.Add("库存 "+Name(r.Target)+" ×"+r.Amount+"（工人≥"+r.B+"）");}
            int invitations=em.HasBuffer<QuestOfferSlot>(entity)?em.GetBuffer<QuestOfferSlot>(entity).Length:0;
            if(invitations>0)outputs.Add("邀约槽 "+invitations);if(stats.QuestCapacity>0)outputs.Add("任务槽位 "+stats.QuestCapacity);
            card.BaseOutput.text="基础产出\n"+(outputs.Count>0?string.Join(" · ",outputs):"暂无基础产出");
            card.BaseOutput.transform.parent.GetComponent<LayoutElement>().preferredHeight=Mathf.Max(94,38+Mathf.Ceil(outputs.Sum(s=>s.Length)/22f)*24);
            RefreshBuildingGarrison(entity);
            var pos=Sim.Position(em,entity);card.Footer.text=$"(x: {pos.x:0.#}, y: {pos.y:0.#}, z: {pos.z:0.#})  移动力: {BuildingRangeOps.ActionPower(em,root,entity)}";
            card.SetWarnings(BuildingWarnings(entity));
            bool skins=em.HasBuffer<BuildingVisualSlot>(entity)&&em.GetBuffer<BuildingVisualSlot>(entity).Length>0;BuildingDetailsView.Bind(card.Style,skins?()=>BuildingSkins(id.Id):null);
            var workforce=WorkforceOps.Quote(em,root,entity);card.Workforce(workforce,Name(workforce.Gold),can&&normal,value=>Send(CommandKind.WorkforceBudget,id.Id,amount:value-workforce.SubsidyCost,argument:1));
            bool crops=false;for(int i=0;i<d.RuleCount;i++)if(Sim.GetRule(em,root,d.RuleStart+i).Kind==RuleKind.Crop)crops=true;card.CropBlock.SetActive(crops);
            if(crops)
            {
                bool planted=b.Crop>=0;int duration=planted?Mathf.Max(1,Sim.Definition(em,root,b.Crop).Duration):1;
                card.CropLabel.text=planted?"种植 · "+Name(b.Crop)+" "+b.CropProgress+"/"+duration:"种植 · 点击圆钮选择作物";
                BuildingDetailsView.Span(card.CropFill,0,planted?(float)b.CropProgress/duration:0);card.CropIcon.sprite=planted?CropPortrait(b.Crop):null;card.CropIcon.enabled=card.CropIcon.sprite!=null;
                BuildingDetailsView.Bind(card.Crop,()=>BuildingCrops(id.Id));
                BuildingDetailsView.Bind(card.ClearCrop,can&&normal&&planted?()=>ShowBuildingConfirmation("铲除 "+Name(b.Crop),new[]{"失去当前作物与进度，不返种植费用。"},()=>Send(CommandKind.ClearCrop,id.Id)):null);
            }
        }
        string BuildingWarnings(Entity e)
        {
            var b=em.GetComponentData<Building>(e);var id=em.GetComponentData<Identity>(e);var stats=em.GetComponentData<BuildingStats>(e);var warnings=new List<string>();
            if(b.Stage!=LifeStage.Operational)warnings.Add(BuildingStageName(b.Stage));
            if(stats.IsCore==0){var h=em.GetComponentData<Health>(e);if(h.Current<h.Maximum)warnings.Add($"耐久受损：{h.Current:0}/{h.Maximum:0}");}
            if(b.FoodFailures>0)warnings.Add("居民连续缺粮 "+b.FoodFailures+" 回合");
            if(EconomyOps.WorkforceLocked(em,id.Id))warnings.Add("远征在途，岗位、移动与升级锁定");
            var maintenance=BuildingCostOps.Rules(em,root,id.Definition,RuleKind.Maintenance,b.Level);if(maintenance.Count>0&&b.Maintained==0)warnings.Add("维护未满足："+CostText(maintenance));
            void Shortage(string label,IEnumerable<BuildingCost> costs){foreach(var cost in costs){int missing=cost.Amount-InventoryOps.Count(em,root,cost.Item);if(missing>0)warnings.Add(label+"："+Name(cost.Item)+" 缺 "+missing);}}
            if(b.Stage==LifeStage.Operational){Shortage("下次维护材料不足",maintenance);Shortage("生产原料不足",BuildingCostOps.Rules(em,root,id.Definition,RuleKind.Input,b.Level));}
            if(b.Stage==LifeStage.Construction)Shortage("下期施工材料不足",BuildingCostOps.Rules(em,root,id.Definition,RuleKind.ConstructionCost,b.Progress+1));
            if(b.Crop>=0&&b.Workers<Sim.Definition(em,root,b.Crop).Population)warnings.Add("作物停止生长：工人不足");
            var q=WorkforceOps.Quote(em,root,e);if(q.Capacity>0){if(b.Workers==0)warnings.Add("没有工人入驻");if(q.SubsidyCost>q.Stock)warnings.Add("下次补贴资金不足");if(q.Workers>q.CurrentStable)warnings.Add("工人数超过当前可稳定人数，可能离职");}
            var definition=Sim.Definition(em,root,id.Definition);bool needsNetwork=maintenance.Count>0||BuildingCostOps.Rules(em,root,id.Definition,RuleKind.Input,b.Level).Count>0;
            if(b.Stage==LifeStage.Construction)needsNetwork|=BuildingCostOps.Rules(em,root,id.Definition,RuleKind.ConstructionCost,b.Progress+1).Count>0;
            if(b.Stage==LifeStage.Repairing){var repair=BuildingCostOps.QuoteRepair(em,root,e);if(repair.Payments.Any(p=>p.Missing>0))warnings.Add("修复材料不足");needsNetwork|=repair.NeedsNetwork;}
            if(needsNetwork&&ResourceNetworkOps.Provider(em,root,e)==Entity.Null)warnings.Add("无法连接资源提供点，需要普通库存的生产/维护/施工会暂停");
            for(int i=0;i<definition.RuleCount;i++)
            {
                var r=Sim.GetRule(em,root,definition.RuleStart+i);if(r.Level!=0&&r.Level!=b.Level)continue;
                if(r.Kind==RuleKind.Production&&b.Workers<r.B)warnings.Add("生产缺少工人：需要 "+r.B);
                if(r.Kind==RuleKind.Environment&&EconomyOps.SpatialValue(em,root,e,r.B)<r.Amount)warnings.Add(EnvironmentName(r.B)+"不足：需要 "+r.Amount);
            }
            return string.Join("\n",warnings.Distinct().Select(w=>"• "+w));
        }
        Sprite CropPortrait(int definition)
        {
            var icon=BuildingSource(definition)?.Icon;if(icon!=null)return icon;
            var harvest=Sim.Rule(em,root,definition,RuleKind.RewardItem);return harvest.Target>=0?BuildingSource(harvest.Target)?.Icon:null;
        }
        void BuildingChoice(string title)
        {ShowBuildingConfirmation(title,Array.Empty<string>(),()=>{});Clear(BuildingConfirmRows);Row(title,parent:BuildingConfirmRows);}
        void BuildingSkins(ulong key)
        {
            var e=Sim.Find(em,key);if(e==Entity.Null||!em.HasBuffer<BuildingVisualSlot>(e))return;BuildingChoice("选择建筑皮肤");var skins=new HashSet<string>();var b=em.GetComponentData<Building>(e);bool can=CourtDay&&em.GetComponentData<Session>(root).Paused==0&&Sim.Operational(em,e);
            foreach(var slot in em.GetBuffer<BuildingVisualSlot>(e))if(slot.Purpose==BuildingVisualPurpose.Operational&&skins.Add(slot.Skin.ToString())){var skin=slot.Skin.ToString();Row((skin==b.Skin.ToString()?"✓ ":"")+(skin.Length==0?"默认":skin),can?()=>{BuildingConfirmPanel.SetActive(false);Send(CommandKind.ChangeBuildingSkin,key,text:skin);}:null,parent:BuildingConfirmRows);}
            Row("关闭",()=>BuildingConfirmPanel.SetActive(false),parent:BuildingConfirmRows);
        }
        void BuildingCrops(ulong key)
        {
            var e=Sim.Find(em,key);if(e==Entity.Null)return;var b=em.GetComponentData<Building>(e);var d=Sim.Definition(em,root,em.GetComponentData<Identity>(e).Definition);BuildingChoice("选择作物");
            if(b.Crop>=0)Row("已种植 "+Name(b.Crop)+"，更换前请先使用 X 铲除。",parent:BuildingConfirmRows);
            var seen=new HashSet<int>();for(int i=0;i<d.RuleCount;i++){var r=Sim.GetRule(em,root,d.RuleStart+i);if(r.Kind!=RuleKind.Crop||!seen.Add(r.Target))continue;int crop=r.Target;var costs=BuildingCostOps.Rules(em,root,crop,RuleKind.PlacementCost,1);bool can=CourtDay&&em.GetComponentData<Session>(root).Paused==0&&Sim.Operational(em,e)&&b.Crop<0&&BuildingCostOps.CanPay(em,root,costs);Row(Name(crop)+" · "+CostText(costs)+" · "+Sim.Definition(em,root,crop).Duration+" 回合",can?()=>{BuildingConfirmPanel.SetActive(false);Send(CommandKind.Plant,key,definition:crop);}:null,parent:BuildingConfirmRows);}
            Row("关闭",()=>BuildingConfirmPanel.SetActive(false),parent:BuildingConfirmRows);
        }
        void CompactWorkforceActions(Entity entity,bool editable)
        {
            var q=WorkforceOps.Quote(em,root,entity);ulong key=em.GetComponentData<Identity>(entity).Id;
            Row("招募 1 名工人 · "+q.RecruitCost+" "+Name(q.Gold),editable&&WorkforceOps.CanChange(q,1)==ResultCode.Success?()=>Send(CommandKind.Workers,key,amount:1,argument:q.RecruitCost,text:"workforce-quote"):null,parent:BuildingDetailsRows);
            Row("释放 1 名工人",editable&&WorkforceOps.CanChange(q,-1)==ResultCode.Success?()=>Send(CommandKind.Workers,key,amount:-1):null,parent:BuildingDetailsRows);
        }
    }
}
