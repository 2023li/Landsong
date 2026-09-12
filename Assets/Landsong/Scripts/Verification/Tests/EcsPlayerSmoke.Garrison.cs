#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        static IEnumerator GarrisonUi(UI_GamePanel view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);
            try
            {
                Entity palace=Entity.Null;using(var sites=Sim.OrderedEntities<Building>(em))foreach(var site in sites)if(em.GetComponentData<BuildingStats>(site).IsCore!=0){palace=site;break;}
                Require(palace!=Entity.Null,"New game has a palace");ulong palaceId=em.GetComponentData<Identity>(palace).Id;
                Require(MilitaryOps.GarrisonCount(em,palaceId)==2&&MilitaryOps.AtSlot(em,palaceId,1)!=Entity.Null&&MilitaryOps.AtSlot(em,palaceId,2)!=Entity.Null&&MilitaryOps.AtSlot(em,palaceId,3)==Entity.Null,"New game grants exactly two palace soldiers in slots one and two");
                var initialSoldier=MilitaryOps.AtSlot(em,palaceId,1);Require(em.HasComponent<SoldierPerson>(initialSoldier)&&PortraitOps.Age(em,initialSoldier)>=18&&em.GetComponentData<Building>(palace).SoldiersRecruited==0,"Initial soldiers have persistent person data and consume no recruitment quota");
                var initialId=em.GetComponentData<Identity>(initialSoldier).Id;SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));
                Require(Sim.Find(em,initialId)!=Entity.Null&&MilitaryOps.GarrisonCount(em,palaceId)==2,"Restoring starting snapshot preserves identity without duplicating garrison");
                var session=em.GetComponentData<Session>(root);session.Phase=Phase.Day;session.Paused=0;session.CheckpointPending=0;session.BasePopulation+=100;em.SetComponentData(root,session);
                using(var units=Sim.OrderedEntities<Soldier>(em))foreach(var unit in units)em.DestroyEntity(unit);
                Entity home=Entity.Null;using(var sites=Sim.OrderedEntities<Building>(em))foreach(var site in sites)if(Sim.Operational(em,site)&&em.GetComponentData<BuildingStats>(site).Garrison>0){home=site;break;}
                Require(home!=Entity.Null,"Garrison fixture has an operational home");ulong homeId=em.GetComponentData<Identity>(home).Id;
                var b=em.GetComponentData<Building>(home);b.SoldiersRecruited=0;em.SetComponentData(home,b);
                int definition=Sim.FirstDefinition(em,root,ContentKind.Soldier);var quote=MilitaryOps.RecruitQuote(em,root,homeId,definition,1,true);foreach(var cost in quote.Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                view.Buildings.SelectBuildingDetails(homeId);yield return WaitFor(()=>view.Buildings.BuildingCard.Block<UI_GamePanel_BuildingDetails_Block_驻军>().gameObject.activeSelf&&view.Buildings.BuildingCard.BuildingId==homeId,"Building displays standalone garrison module");
                var card=view.Buildings.BuildingCard;
                var baseOutput=card.Block<UI_GamePanel_BuildingDetails_Block_基础产出>();
                var garrison=card.Block<UI_GamePanel_BuildingDetails_Block_驻军>();
                Require(baseOutput.transform.parent==garrison.transform.parent&&baseOutput.Label.text.Contains("士兵槽"),"Base output is in main container and includes soldier slots");
                garrison.Slots.GetComponentInChildren<Button>().onClick.Invoke();yield return WaitFor(()=>view.IsPanelOpen&&view.Panel==GamePanelId.Garrison,"Empty slot opens garrison management");
                Button Recruit()=>view.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text.StartsWith("募兵 "));
                yield return WaitFor(()=>Recruit()!=null,"Garrison panel provides affordable recruitment");Recruit().onClick.Invoke();
                yield return WaitFor(()=>view.Buildings.BuildingConfirmPanel.activeSelf,"Recruitment opens cost confirmation");
                var confirm=view.Buildings.BuildingConfirmPanel.GetComponentsInChildren<Button>().First(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text=="确认");confirm.onClick.Invoke();
                Entity Pending(){using var units=Sim.OrderedEntities<Soldier>(em);foreach(var unit in units)if(em.GetComponentData<Soldier>(unit).Garrison==0)return unit;return Entity.Null;}
                yield return WaitFor(()=>Pending()!=Entity.Null,"Recruitment places soldier in pending pool");ulong id=em.GetComponentData<Identity>(Pending()).Id;
                // Exercise the actual list button then a specific destination slot.
                UI_GamePanel_SoldierItem PoolCard()=>view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(x=>x.PersonId==id);
                Button Pool()=>PoolCard()?.Select;
                yield return WaitFor(()=>Pool()!=null,"Pending soldier is selectable");Pool().onClick.Invoke();Require(!view.soldierController.SoldierDetailsOpen,"Selecting a pending card never opens details");
                PoolCard().Attention.isOn=true;yield return WaitFor(()=>em.GetComponentData<SoldierPerson>(Sim.Find(em,id)).SpecialAttention==1,"Attention toggle changes persistent soldier state");
                PoolCard().Details.onClick.Invoke();Require(view.soldierController.SoldierDetailsOpen,"Only explicit card detail button opens details");
                var soldierDetails=view.soldierController.SoldierDetailsPanel;
                Require(soldierDetails.PortraitBinding.Target==soldierDetails.Portrait,"Soldier details uses its explicitly configured portrait image");
                yield return WaitFor(()=>soldierDetails.PortraitBinding.BoundPersonId==id&&soldierDetails.Portrait.sprite!=null&&PoolCard()!=null&&soldierDetails.Portrait.sprite==PoolCard().Portrait.sprite,"Pending soldier detail binds the selected person and shares its actual roster portrait");
                view.soldierController.CloseSoldierDetails();
                Require(soldierDetails.PortraitBinding.BoundPersonId==0&&soldierDetails.Portrait.sprite==null,"Closing soldier details clears its person binding and displayed portrait");
                Button Slot()=>view.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text=="槽 1 · 空位");
                yield return WaitFor(()=>Slot()!=null,"Selecting pending soldier enables empty slot");Slot().onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison!=0,"Clicking destination assigns selected soldier");
                ulong assigned=em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison;
                yield return WaitFor(()=>PoolCard()==null,"Assigned soldier disappears from pending panel");
                var group=view.PrimaryRows.GetComponentsInChildren<UI_GamePanel_GarrisonGroup>().Single(g=>g.BuildingId==assigned);
                UI_GamePanel_SoldierItem AssignedCard()=>group.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(x=>x.PersonId==id);
                Require(AssignedCard()!=null&&AssignedCard().Attention.isOn,"Building group owns its soldier slots and attention follows soldier");
                AssignedCard().Select.onClick.Invoke();Require(!view.soldierController.SoldierDetailsOpen,"Occupied card body does not open details");
                AssignedCard().Remove.onClick.Invoke();yield return WaitFor(()=>PoolCard()!=null&&em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison==0,"Remove returns soldier to pending exactly once");
                Pool().onClick.Invoke();yield return WaitFor(()=>Slot()!=null,"Removed soldier can be selected again");Slot().onClick.Invoke();yield return WaitFor(()=>em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison==assigned,"Removed soldier reassigns to selected slot");
                var extraQuote=MilitaryOps.RecruitQuote(em,root,homeId,definition,2,true);foreach(var cost in extraQuote.Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                Require(GameLoopSystem.Execute(em,root,new Command{Kind=CommandKind.RecruitSoldier,Target=homeId,Definition=definition,Amount=2,Argument=1})==ResultCode.Success,"Sorting fixture recruits two distinct pending soldiers");
                ulong[] sortingIds;using(var all=Sim.OrderedEntities<Soldier>(em))sortingIds=all.ToArray().Where(e=>em.GetComponentData<Soldier>(e).Garrison==0).Select(e=>em.GetComponentData<Identity>(e).Id).ToArray();
                for(int i=0;i<sortingIds.Length;i++){var soldierEntity=Sim.Find(em,sortingIds[i]);var soldier=em.GetComponentData<Soldier>(soldierEntity);soldier.Experience=MilitaryOps.LevelThreshold(Sim.Definition(em,root,definition).SoldierGrowth,2)+i;em.SetComponentData(soldierEntity,soldier);}
                view.Refresh(); // Direct Execute/XP fixture writes do not run the command system's event publication.
                yield return WaitFor(()=>view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().Length==2,"Pending panel contains only two unassigned soldiers");
                Require(view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().Select(c=>c.PersonId).SequenceEqual(sortingIds),"Equal levels sort by stable ID instead of raw experience");
                view.SecondaryRows.GetComponentsInChildren<Button>().First(x=>x.GetComponentInChildren<TMP_Text>()?.text.StartsWith("排序：")==true).onClick.Invoke();
                yield return WaitFor(()=>view.SecondaryRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("按总属性值")),"Total attribute order is selectable");
                var wounded=Sim.Find(em,sortingIds[0]);float total=UI_GamePanel_Garrison.SoldierAttributeTotal(em,root,wounded);var woundedHealth=em.GetComponentData<Health>(wounded);woundedHealth.Current=1;em.SetComponentData(wounded,woundedHealth);
                Require(UI_GamePanel_Garrison.SoldierAttributeTotal(em,root,wounded)==total,"Injury does not lower maximum-attribute sorting value");
                var stronger=Sim.Find(em,sortingIds[1]);var strongerHealth=em.GetComponentData<Health>(stronger);strongerHealth.Maximum+=40;em.SetComponentData(stronger,strongerHealth);
                view.Refresh(); // Project the fixture's changed maximum into the visible sorted cards.
                yield return WaitFor(()=>view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().First().PersonId==sortingIds[1],"Total-attribute sorting uses the actual displayed health maximum, independent of equal levels");
                yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
                var title=AssignedCard().Title;title.ForceMeshUpdate();Require(title.textInfo.characterInfo.Take(title.textInfo.characterCount).Any(c=>c.isVisible),"Soldier name actually renders within its reserved line");
                Require(!view.Buildings.BuildingToolbar.gameObject.activeSelf,"Building action toolbar cannot cover garrison cards");
                ScreenCapture.CaptureScreenshot("Library/LandsongEcs/garrison-cards.png");yield return new WaitForEndOfFrame();
                view.Buildings.SelectBuildingDetails(assigned);
                yield return WaitFor(()=>card.BuildingId==assigned&&garrison.Slots.GetComponentsInChildren<TMP_Text>().Any(x=>x.text==em.GetComponentData<Identity>(Sim.Find(em,id)).Name.ToString()),"Occupied slot displays soldier name");
                UI_GamePanel_GarrisonSlot BuildingSlot()=>garrison.Slots.GetComponentsInChildren<UI_GamePanel_GarrisonSlot>().FirstOrDefault(slot=>slot.NameLabel.gameObject.activeSelf&&slot.NameLabel.text==em.GetComponentData<Identity>(Sim.Find(em,id)).Name.ToString());
                Image BuildingPortrait()=>BuildingSlot()?.Portrait;
                yield return WaitFor(()=>BuildingPortrait()!=null&&BuildingPortrait().sprite!=null&&!BuildingPortrait().canvasRenderer.cull,"Building garrison renders a real visible soldier portrait");
                Require(BuildingPortrait().preserveAspect&&!BuildingPortrait().raycastTarget,"Portrait preserves aspect and does not block slot clicks");
                var visiblePortrait=BuildingPortrait();
                garrison.gameObject.SetActive(false);yield return null;garrison.gameObject.SetActive(true);
                yield return WaitFor(()=>BuildingPortrait()==visiblePortrait&&visiblePortrait.sprite!=null,"Reopening unchanged garrison rebinds the cached portrait");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-garrison-portraits.png");yield return null;
                var expectedDetailPortrait=visiblePortrait.sprite;
                BuildingSlot().Select.onClick.Invoke();
                Require(view.soldierController.SoldierDetailsOpen,"Occupied portrait opens soldier detail");
                yield return WaitFor(()=>soldierDetails.PortraitBinding.BoundPersonId==id&&soldierDetails.Portrait.sprite!=null&&soldierDetails.Portrait.sprite==expectedDetailPortrait&&!soldierDetails.Portrait.canvasRenderer.cull,"Occupied soldier detail rebinds the selected person and renders its actual visible portrait");
                Require(view.soldierController.SoldierDetailsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("敏捷：")&&t.text.Contains("血量：")),"Soldier basic attributes include agility and actual health");
                view.OpenPanel(GamePanelId.Economy);Require(view.soldierController.SoldierDetailsOpen&&!view.IsPanelOpen,"Soldier detail prevents underlying panel navigation");
                view.soldierController.SoldierDetailsName.onEndEdit.Invoke("守城新兵");yield return WaitFor(()=>em.GetComponentData<Identity>(Sim.Find(em,id)).Name.ToString()=="守城新兵","Soldier detail renames authoritative identity");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldier-details.png");yield return null;
                view.soldierController.CloseSoldierDetails();Require(!view.soldierController.SoldierDetailsOpen,"Soldier detail can close without losing underlying building");
                Require(soldierDetails.PortraitBinding.BoundPersonId==0&&soldierDetails.Portrait.sprite==null,"Reopened soldier details releases its portrait again on close");
                view.OpenPanel(GamePanelId.Garrison);yield return WaitFor(()=>view.PrimaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().Any(c=>c.PersonId==id),"Return to grouped soldier cards");
                var dismissalUnit=Sim.Find(em,id);var unitState=em.GetComponentData<Soldier>(dismissalUnit);int employed=Sim.Employed(em);
                view.PrimaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().Single(c=>c.PersonId==id).Dismiss.onClick.Invoke();
                yield return WaitFor(()=>Sim.Find(em,id)==Entity.Null,"Immediate dismissal removes only selected soldier");
                Require(MilitaryOps.AtSlot(em,assigned,1)==Entity.Null&&Sim.Employed(em)==employed-unitState.PopulationCost,"Immediate dismissal frees actual slot and population");
                var sort=view.SecondaryRows.GetComponentsInChildren<Button>().First(x=>x.GetComponentInChildren<TMP_Text>()?.text.StartsWith("排序：")==true);sort.onClick.Invoke();
                yield return WaitFor(()=>view.SecondaryRows.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("按等级")),"Pending pool can return to level sorting");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-garrison.png");yield return null;
            }
            finally{view.soldierController.CloseSoldierDetails();if(view.Buildings.BuildingConfirmPanel!=null)view.Buildings.BuildingConfirmPanel.SetActive(false);view.Buildings.BuildingDetailsPanel.SetActive(false);view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
