#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        static IEnumerator BuildingDetailsUi(EcsGameView view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);
            try
            {
                int Def(string name)=>Sim.FindDefinition(em,root,new FixedString128Bytes(name));
                var session=em.GetComponentData<Session>(root);session.Phase=Phase.Day;session.Paused=0;session.CheckpointPending=0;session.BasePopulation=100;em.SetComponentData(root,session);
                int definition=Def("b农田");var grid=em.GetComponentData<GridData>(root);Entity farm=Entity.Null;
                for(int i=0;i<grid.Value.Value.Cells.Length;i++){var cell=grid.Value.Value.Min+new int2(i%grid.Value.Value.Size.x,i/grid.Value.Value.Size.x);if(GridOps.CanPlace(em,root,definition,cell,0)){farm=BuildingOps.Create(em,root,definition,cell,0,1,true);break;}}
                Require(farm!=Entity.Null,"Building detail fixture has real farm");ulong key=em.GetComponentData<Identity>(farm).Id;
                var state=em.GetComponentData<Building>(farm);state.SubsidyBudget=state.PaidSubsidy=0;state.Workers=0;em.SetComponentData(farm,state);
                view.OpenPanel("建筑");view.SelectBuildingDetails(key);
                yield return WaitFor(()=>view.BuildingDetailsPanel.activeSelf&&view.BuildingCard.BuildingId==key,"Selecting building automatically opens redesigned detail card");
                var card=view.BuildingCard;Require(view.BuildingRangesVisible&&card.Footer.text.Contains("移动力"),"Selection defaults to range overlays and fixed coordinates/action power: "+view.BuildingRangesVisible+" / "+card.Footer.text);
                Require(card.Name.transform.IsChildOf(card.transform)&&!view.Rename.gameObject.activeSelf,"Editable building name is inside detail header without old rename toolbar");
                card.Name.text="春耕园";card.Name.onEndEdit.Invoke(card.Name.text);
                yield return WaitFor(()=>em.GetComponentData<Identity>(farm).Name.ToString()=="春耕园","Name field commits player rename through ECS");
                ExecuteEvents.Execute(card.Warning.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Require(card.Tooltip.activeSelf&&card.TooltipText.text.Contains("没有工人"),"Warning hover explains actual building abnormalities");
                ExecuteEvents.Execute(card.Warning.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);Require(!card.Tooltip.activeSelf,"Warning leaves without lingering overlay");
                int gold=WorkforceOps.Quote(em,root,farm).Gold;int stock=InventoryOps.Count(em,root,gold);card.Increase.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Building>(farm).SubsidyBudget==1,"Left arrow increases subsidy budget by one");
                Require(InventoryOps.Count(em,root,gold)==stock&&em.GetComponentData<Building>(farm).PaidSubsidy==0,"Budget setting neither pays immediately nor adds effective attraction");yield return new WaitForSecondsRealtime(.3f);
                var q=WorkforceOps.Quote(em,root,farm);Require(Mathf.Approximately(card.SubsidyFill.rectTransform.anchorMin.x,card.SubsidyFill.rectTransform.anchorMax.x),"Orange bar reflects paid subsidy rather than unpaid budget");
                state=em.GetComponentData<Building>(farm);state.PaidSubsidy=1;state.PaidSubsidyTurn=session.Turn;state.Workers=1;em.SetComponentData(farm,state);yield return new WaitForSecondsRealtime(.3f);
                Require(card.SubsidyFill.rectTransform.anchorMax.x>card.SubsidyFill.rectTransform.anchorMin.x&&card.JobTicks.Count(t=>t.gameObject.activeSelf)==Mathf.Min(10,q.Capacity),"Paid attraction and at most ten proportional job markers rendered");
                card.Decrease.onClick.Invoke();yield return WaitFor(()=>em.GetComponentData<Building>(farm).SubsidyBudget==0,"Right arrow reduces future subsidy without erasing paid benefit");
                card.Upgrade.onClick.Invoke();Require(!view.BuildingConfirmPanel.activeSelf&&!string.IsNullOrEmpty(view.Message.text),"Gray upgrade remains clickable and reports missing requirements");
                state=em.GetComponentData<Building>(farm);state.Experience=100000;state.Workers=em.GetComponentData<BuildingStats>(farm).JobCapacity;state.Maintained=1;em.SetComponentData(farm,state);
                var d=Sim.Definition(em,root,definition);Sim.Grant(em,root,definition,d.Level);
                foreach(var cost in BuildingOps.CheckUpgrade(em,root,farm).Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                yield return new WaitForSecondsRealtime(.3f);
                if(d.Level>1){Require(BuildingOps.CheckUpgrade(em,root,farm).Allowed,"Upgrade fixture satisfies actual requirements");card.Upgrade.onClick.Invoke();Require(view.BuildingConfirmPanel.activeSelf&&view.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("费用")),"Upgrade opens material confirmation");view.CancelBuildingInteraction();}
                if(card.Style.interactable){card.Style.onClick.Invoke();Require(view.BuildingConfirmPanel.activeSelf&&view.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("选择建筑皮肤")),"Style opens separate skin choices");view.CancelBuildingInteraction();}
                var cropRule=Sim.Rule(em,root,definition,RuleKind.Crop);int crop=cropRule.Target;Require(crop>=0,"Farm fixture exposes a crop");
                foreach(var cost in BuildingCostOps.Rules(em,root,crop,RuleKind.PlacementCost,1))InventoryOps.Add(em,root,cost.Item,cost.Amount);
                card.Crop.onClick.Invoke();Require(view.BuildingConfirmPanel.activeSelf,"Circular crop button opens crop choices");
                string cropName=Sim.Definition(em,root,crop).Name.ToString();view.BuildingConfirmRows.GetComponentsInChildren<UnityEngine.UI.Button>().First(button=>button.interactable&&button.GetComponentInChildren<TMP_Text>().text.StartsWith(cropName+" · ")).onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Building>(farm).Crop==crop,"Crop choice dispatches planting command");yield return new WaitForSecondsRealtime(.3f);
                Require(card.CropLabel.text.Contains(cropName)&&card.CropIcon.transform.IsChildOf(card.Crop.transform)&&card.CropIcon.sprite!=null,"Chosen crop is shown on circle child icon and maturity label");
                state=em.GetComponentData<Building>(farm);state.CropProgress=1;em.SetComponentData(farm,state);yield return new WaitForSecondsRealtime(.3f);
                Require(card.CropFill.rectTransform.anchorMax.x>0,"Crop bar reads authoritative maturity progress");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-details.png");yield return null;}
                card.ClearCrop.onClick.Invoke();Require(view.BuildingConfirmPanel.activeSelf,"Crop X requires removal confirmation");ClickIn(view.BuildingConfirmRows,"确认");
                yield return WaitFor(()=>em.GetComponentData<Building>(farm).Crop<0,"Confirmed crop removal clears actual crop");
                state=em.GetComponentData<Building>(farm);state.Level=d.Level;state.Experience=100000;em.SetComponentData(farm,state);BuildingOps.ApplyLevel(em,root,farm,false);yield return new WaitForSecondsRealtime(.3f);
                Require(card.Experience.text=="MAX"&&!card.Upgrade.gameObject.activeSelf,"Full XP at final level displays MAX without upgrade action");
                int warehouseDef=Def("b仓库");Entity warehouse=Entity.Null;
                for(int i=0;i<grid.Value.Value.Cells.Length;i++){var cell=grid.Value.Value.Min+new int2(i%grid.Value.Value.Size.x,i/grid.Value.Value.Size.x);if(GridOps.CanPlace(em,root,warehouseDef,cell,0)){warehouse=BuildingOps.Create(em,root,warehouseDef,cell,0,1,true);break;}}
                Require(warehouse!=Entity.Null,"Upgrade fixture creates multilevel warehouse");ulong warehouseId=em.GetComponentData<Identity>(warehouse).Id;
                var wb=em.GetComponentData<Building>(warehouse);wb.Experience=100000;wb.Workers=em.GetComponentData<BuildingStats>(warehouse).JobCapacity;wb.Maintained=1;em.SetComponentData(warehouse,wb);
                var grants=em.GetBuffer<Entitlement>(root);for(int i=grants.Length-1;i>=0;i--)if(grants[i].Definition==warehouseDef)grants.RemoveAt(i);Sim.Grant(em,root,warehouseDef,1);
                view.SelectBuildingDetails(warehouseId);yield return new WaitForSecondsRealtime(.3f);card.Upgrade.onClick.Invoke();Require(!view.BuildingConfirmPanel.activeSelf&&view.Message.text.Contains("下一等级蓝图"),"Full XP without higher blueprint stays gray and explains missing blueprint");
                Sim.Grant(em,root,warehouseDef,2);foreach(var cost in BuildingOps.CheckUpgrade(em,root,warehouse).Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                Require(BuildingOps.CheckUpgrade(em,root,warehouse).Allowed,"Warehouse upgrade fixture meets authoritative requirements");yield return new WaitForSecondsRealtime(.3f);card.Upgrade.onClick.Invoke();
                Require(view.BuildingConfirmPanel.activeSelf&&view.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("费用")),"Usable upgrade opens quoted material confirmation");
                var beforeUpgrade=SnapshotCodec.Capture(em,root);view.CancelBuildingInteraction();Require(beforeUpgrade.SequenceEqual(SnapshotCodec.Capture(em,root)),"Cancel upgrade does not spend materials or XP");
                card.Upgrade.onClick.Invoke();ClickIn(view.BuildingConfirmRows,"确认");yield return WaitFor(()=>em.GetComponentData<Building>(warehouse).Level==2,"Upgrade confirmation changes real building level");
                using(var reach=BuildingRangeOps.Reach(em,root,farm,Allocator.Temp)){var provider=ResourceNetworkOps.Provider(em,root,farm);if(provider!=Entity.Null){var path=BuildingRangeOps.ProviderPath(em,root,provider,reach);Require(path.Count>0&&path.Skip(1).Select((cell,i)=>math.csum(math.abs(cell-path[i]))).All(gap=>gap==1),"Resource overlay uses contiguous path to actual selected provider");}}
                card.Close.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);Require(!view.BuildingDetailsPanel.activeSelf,"Building detail X remains closed across refresh");
            }
            finally{view.BuildingConfirmPanel.SetActive(false);view.BuildingDetailsClose.onClick.Invoke();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
