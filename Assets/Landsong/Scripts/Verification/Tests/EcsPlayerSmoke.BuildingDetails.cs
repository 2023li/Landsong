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
        static IEnumerator BuildingDetailsUi(UI_GamePanel view,EntityManager em,Entity root)
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
                view.OpenPanel(GamePanelId.Building);view.Buildings.SelectBuildingDetails(key);
                yield return WaitFor(()=>view.Buildings.BuildingDetailsPanel.activeSelf&&view.Buildings.BuildingCard.BuildingId==key,"Selecting building automatically opens redesigned detail card");
                var card=view.Buildings.BuildingCard;
                var baseOutput=card.Block<UI_GamePanel_BuildingDetails_Block_基础产出>();
                var workforce=card.Block<UI_GamePanel_BuildingDetails_Block_岗位>();
                var planting=card.Block<UI_GamePanel_BuildingDetails_Block_种植>();
                Require(view.Buildings.BuildingRangesVisible&&card.Footer.text.Contains("移动力"),"Selection defaults to range overlays and fixed coordinates/action power: "+view.Buildings.BuildingRangesVisible+" / "+card.Footer.text);
                Require(view.Buildings.BuildingToolbar.parent==card.transform,"Building actions belong to the fixed detail footer");
                Require(card.Name.transform.IsChildOf(card.transform)&&!view.GetComponentsInChildren<UnityEngine.UI.Button>(true).Any(b=>b.name=="Rename"),"Editable building name is inside detail header without old rename toolbar");
                card.Name.text="春耕园";card.Name.onEndEdit.Invoke(card.Name.text);
                yield return WaitFor(()=>em.GetComponentData<Identity>(farm).Name.ToString()=="春耕园","Name field commits player rename through ECS");
                ExecuteEvents.Execute(card.Warning.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Require(card.Tooltip.activeSelf&&card.TooltipText.text.Contains("没有工人"),"Warning hover explains actual building abnormalities");
                ExecuteEvents.Execute(card.Warning.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);Require(!card.Tooltip.activeSelf,"Warning leaves without lingering overlay");
                int gold=WorkforceOps.Quote(em,root,farm).Gold;int stock=InventoryOps.Count(em,root,gold);workforce.Increase.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Building>(farm).SubsidyBudget==1,"Left arrow increases subsidy budget by one");
                Require(InventoryOps.Count(em,root,gold)==stock&&em.GetComponentData<Building>(farm).PaidSubsidy==0,"Budget setting neither pays immediately nor adds effective attraction");yield return new WaitForSecondsRealtime(.3f);
                var q=WorkforceOps.Quote(em,root,farm);Require(Mathf.Approximately(workforce.SubsidyFill.rectTransform.anchorMax.x,q.Planned/100)&&workforce.SubsidyFill.rectTransform.anchorMax.x>workforce.SubsidyFill.rectTransform.anchorMin.x&&workforce.SubsidyFill.color.g>.75f,"Yellow subsidy preview immediately reflects the configured unpaid budget");
                Require(!baseOutput.gameObject.activeSelf,"Farm without base output removes the entire output block from layout");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/subsidy-preview.png");yield return null;}
                state=em.GetComponentData<Building>(farm);state.PaidSubsidy=1;state.PaidSubsidyTurn=session.Turn;state.Workers=1;em.SetComponentData(farm,state);
                view.Refresh(); // Direct fixture projection; production budget commands below still refresh through events.
                yield return new WaitForSecondsRealtime(.3f);
                Require(workforce.SubsidyFill.rectTransform.anchorMax.x>workforce.SubsidyFill.rectTransform.anchorMin.x&&workforce.JobTicks.Count(t=>t.gameObject.activeSelf)==Mathf.Min(10,q.Capacity),"Paid attraction and at most ten proportional job markers rendered");
                workforce.Decrease.onClick.Invoke();yield return WaitFor(()=>em.GetComponentData<Building>(farm).SubsidyBudget==0,"Right arrow reduces future subsidy without erasing paid benefit");
                yield return new WaitForSecondsRealtime(.3f);
                Require(Mathf.Approximately(workforce.SubsidyFill.rectTransform.anchorMin.x,workforce.SubsidyFill.rectTransform.anchorMax.x)&&em.GetComponentData<Building>(farm).PaidSubsidy==1&&workforce.Attraction.text.Contains("当前实际"),"Removing future budget clears yellow preview while preserving and explaining paid attraction");
                card.Upgrade.onClick.Invoke();Require(!view.Buildings.BuildingConfirmPanel.activeSelf&&!string.IsNullOrEmpty(view.Hud.Message.text),"Gray upgrade remains clickable and reports missing requirements");
                state=em.GetComponentData<Building>(farm);state.Experience=100000;state.Workers=em.GetComponentData<BuildingStats>(farm).JobCapacity;state.Maintained=1;em.SetComponentData(farm,state);
                var d=Sim.Definition(em,root,definition);BlueprintOps.Grant(em,root,definition,d.Level);
                foreach(var cost in BuildingOps.CheckUpgrade(em,root,farm).Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                view.Refresh(); // XP, blueprint and inventory were populated directly by this fixture.
                yield return new WaitForSecondsRealtime(.3f);
                if(d.Level>1){Require(BuildingOps.CheckUpgrade(em,root,farm).Allowed,"Upgrade fixture satisfies actual requirements");card.Upgrade.onClick.Invoke();Require(view.Buildings.BuildingConfirmPanel.activeSelf&&view.Buildings.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("费用")),"Upgrade opens material confirmation");view.Buildings.CancelBuildingInteraction();}
                if(card.Style.interactable){card.Style.onClick.Invoke();Require(view.Buildings.BuildingConfirmPanel.activeSelf&&view.Buildings.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("选择建筑皮肤")),"Style opens separate skin choices");view.Buildings.CancelBuildingInteraction();}
                var cropRule=Sim.Rule(em,root,definition,RuleKind.Crop);int crop=cropRule.Target;Require(crop>=0,"Farm fixture exposes a crop");
                foreach(var cost in BuildingCostOps.Rules(em,root,crop,RuleKind.PlacementCost,1))InventoryOps.Add(em,root,cost.Item,cost.Amount);
                planting.Select.onClick.Invoke();Require(view.Buildings.BuildingConfirmPanel.activeSelf,"Circular crop button opens crop choices");
                string cropName=Sim.Definition(em,root,crop).Name.ToString();view.Buildings.BuildingConfirmRows.GetComponentsInChildren<UnityEngine.UI.Button>().First(button=>button.interactable&&button.GetComponentInChildren<TMP_Text>().text.StartsWith(cropName+" · ")).onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Building>(farm).Crop==crop,"Crop choice dispatches planting command");yield return new WaitForSecondsRealtime(.3f);
                Require(planting.Label.text.Contains(cropName)&&planting.Icon.transform.IsChildOf(planting.Select.transform)&&planting.Icon.sprite!=null,"Chosen crop is shown on circle child icon and maturity label");
                var beforeHover=SnapshotCodec.Capture(em,root);
                ExecuteEvents.Execute(planting.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Require(card.Sidebar.activeSelf&&card.SidebarText.text.Contains("1人口")&&card.SidebarText.text.Contains(cropName)&&card.SidebarText.text.Contains("全生长期")&&card.SidebarText.text.Contains("生长暂停"),"Planting hover displays worker tiers, growth threshold and whole-cycle bonus");
                Require(beforeHover.SequenceEqual(SnapshotCodec.Capture(em,root)),"Worker tooltip does not mutate simulation or consume crop RNG");
                ExecuteEvents.Execute(planting.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                ExecuteEvents.Execute(card.Sidebar,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.25f);Require(card.Sidebar.activeSelf,"Building sidebar remains open when pointer enters it for scrolling");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/worker-tiers.png");yield return null;}
                ExecuteEvents.Execute(card.Sidebar,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
                yield return new WaitForSecondsRealtime(.25f);Require(!card.Sidebar.activeSelf,"Building sidebar closes after leaving module and sidebar");
                state=em.GetComponentData<Building>(farm);state.CropProgress=1;em.SetComponentData(farm,state);view.Refresh();yield return new WaitForSecondsRealtime(.3f);
                Require(planting.Fill.rectTransform.anchorMax.x>0,"Crop bar reads authoritative maturity progress");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-details.png");yield return null;}
                planting.Clear.onClick.Invoke();Require(view.Buildings.BuildingConfirmPanel.activeSelf,"Crop X requires removal confirmation");ClickIn(view.Buildings.BuildingConfirmRows,"确认");
                yield return WaitFor(()=>em.GetComponentData<Building>(farm).Crop<0,"Confirmed crop removal clears actual crop");
                state=em.GetComponentData<Building>(farm);state.Level=d.Level;state.Experience=100000;em.SetComponentData(farm,state);BuildingOps.ApplyLevel(em,root,farm,false);view.Refresh();yield return new WaitForSecondsRealtime(.3f);
                Require(card.Experience.text=="MAX"&&!card.Upgrade.gameObject.activeSelf,"Full XP at final level displays MAX without upgrade action");
                int warehouseDef=Def("b仓库");Entity warehouse=Entity.Null;
                for(int i=0;i<grid.Value.Value.Cells.Length;i++){var cell=grid.Value.Value.Min+new int2(i%grid.Value.Value.Size.x,i/grid.Value.Value.Size.x);if(GridOps.CanPlace(em,root,warehouseDef,cell,0)){warehouse=BuildingOps.Create(em,root,warehouseDef,cell,0,1,true);break;}}
                Require(warehouse!=Entity.Null,"Upgrade fixture creates multilevel warehouse");ulong warehouseId=em.GetComponentData<Identity>(warehouse).Id;
                var wb=em.GetComponentData<Building>(warehouse);wb.Experience=100000;wb.Workers=em.GetComponentData<BuildingStats>(warehouse).JobCapacity;wb.Maintained=1;em.SetComponentData(warehouse,wb);
                var grants=em.GetBuffer<Entitlement>(root);for(int i=grants.Length-1;i>=0;i--)if(grants[i].Definition==warehouseDef)grants.RemoveAt(i);BlueprintOps.Grant(em,root,warehouseDef,1);
                view.Buildings.SelectBuildingDetails(warehouseId);yield return new WaitForSecondsRealtime(.3f);card.Upgrade.onClick.Invoke();Require(!view.Buildings.BuildingConfirmPanel.activeSelf&&view.Hud.Message.text.Contains("下一等级蓝图"),"Full XP without higher blueprint stays gray and explains missing blueprint");
                Require(baseOutput.gameObject.activeSelf,"Selecting warehouse restores populated base output block");
                ExecuteEvents.Execute(baseOutput.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                Require(card.Sidebar.activeSelf&&card.SidebarText.text.Contains("库存槽")&&!card.SidebarText.text.Contains(cropName),"Warehouse hover shows its own worker effects without stale crop data");
                card.HideSidebar();
                BlueprintOps.Grant(em,root,warehouseDef,2);foreach(var cost in BuildingOps.CheckUpgrade(em,root,warehouse).Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                view.Refresh(); // Show the newly authored fixture license and material availability.
                Require(BuildingOps.CheckUpgrade(em,root,warehouse).Allowed,"Warehouse upgrade fixture meets authoritative requirements");yield return new WaitForSecondsRealtime(.3f);card.Upgrade.onClick.Invoke();
                Require(view.Buildings.BuildingConfirmPanel.activeSelf&&view.Buildings.BuildingConfirmRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("费用")),"Usable upgrade opens quoted material confirmation");
                var beforeUpgrade=SnapshotCodec.Capture(em,root);view.Buildings.CancelBuildingInteraction();Require(beforeUpgrade.SequenceEqual(SnapshotCodec.Capture(em,root)),"Cancel upgrade does not spend materials or XP");
                card.Upgrade.onClick.Invoke();ClickIn(view.Buildings.BuildingConfirmRows,"确认");yield return WaitFor(()=>em.GetComponentData<Building>(warehouse).Level==2,"Upgrade confirmation changes real building level");
                using(var reach=BuildingRangeOps.Reach(em,root,farm,Allocator.Temp)){var provider=ResourceNetworkOps.Provider(em,root,farm);if(provider!=Entity.Null){var path=BuildingRangeOps.ProviderPath(em,root,provider,reach);Require(path.Count>0&&path.Skip(1).Select((cell,i)=>math.csum(math.abs(cell-path[i]))).All(gap=>gap==1),"Resource overlay uses contiguous path to actual selected provider");}}
                ExecuteEvents.Execute(workforce.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.Quests.QuestTracking.gameObject.activeSelf,"Building details suppress quest tracking before local close");
                card.Close.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);Require(!view.Buildings.BuildingDetailsPanel.activeSelf&&!card.Sidebar.activeSelf,"Building detail X remains closed across refresh and clears its sidebar");
                Require(view.Quests.QuestTracking.gameObject.activeInHierarchy,"Closing building details restores quest HUD during idle day without another command");
                view.Buildings.SelectBuildingDetails(warehouseId);yield return new WaitForSecondsRealtime(.3f);
                Require(view.Buildings.BuildingDetailsPanel.activeSelf&&!view.Quests.QuestTracking.gameObject.activeSelf,"Reopened building details hide quest tracking again");
                for(int i=0;i<4&&view.Buildings.CancelBuildingInteraction();i++) { }
                yield return new WaitForSecondsRealtime(.3f);
                Require(!view.Buildings.BuildingDetailsPanel.activeSelf&&view.Quests.QuestTracking.gameObject.activeInHierarchy,"Cancelling building details restores quest HUD without relying on periodic idle rebuilds");
            }
            finally{view.Buildings.BuildingConfirmPanel.SetActive(false);view.Buildings.BuildingDetailsClose.onClick.Invoke();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
