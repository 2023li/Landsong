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
        static IEnumerator GarrisonUi(EcsGameView view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);
            try
            {
                var session=em.GetComponentData<Session>(root);session.Phase=Phase.Day;session.Paused=0;session.CheckpointPending=0;session.BasePopulation+=100;em.SetComponentData(root,session);
                using(var units=Sim.OrderedEntities<Soldier>(em))foreach(var unit in units)em.DestroyEntity(unit);
                Entity home=Entity.Null;using(var sites=Sim.OrderedEntities<Building>(em))foreach(var site in sites)if(Sim.Operational(em,site)&&em.GetComponentData<BuildingStats>(site).Garrison>0){home=site;break;}
                Require(home!=Entity.Null,"Garrison fixture has an operational home");ulong homeId=em.GetComponentData<Identity>(home).Id;
                var b=em.GetComponentData<Building>(home);b.SoldiersRecruited=0;em.SetComponentData(home,b);
                int definition=Sim.FirstDefinition(em,root,ContentKind.Soldier);var quote=MilitaryOps.RecruitQuote(em,root,homeId,definition,1,true);foreach(var cost in quote.Costs)InventoryOps.Add(em,root,cost.Item,cost.Amount);
                view.SelectBuildingDetails(homeId);yield return WaitFor(()=>view.BuildingCard.GarrisonBlock.activeSelf&&view.BuildingCard.BuildingId==homeId,"Building displays standalone garrison module");
                var card=view.BuildingCard;Require(card.BaseOutput.transform.parent.parent==card.GarrisonBlock.transform.parent&&card.BaseOutput.text.Contains("士兵槽"),"Base output is in main container and includes soldier slots");
                card.GarrisonSlots.GetComponentInChildren<Button>().onClick.Invoke();yield return WaitFor(()=>view.IsPanelOpen&&view.Panel=="驻军","Empty slot opens garrison management");
                Button Recruit()=>view.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text.StartsWith("募兵 "));
                yield return WaitFor(()=>Recruit()!=null,"Garrison panel provides affordable recruitment");Recruit().onClick.Invoke();
                yield return WaitFor(()=>view.BuildingConfirmPanel.activeSelf,"Recruitment opens cost confirmation");
                var confirm=view.BuildingConfirmPanel.GetComponentsInChildren<Button>().First(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text=="确认");confirm.onClick.Invoke();
                Entity Pending(){using var units=Sim.OrderedEntities<Soldier>(em);foreach(var unit in units)if(em.GetComponentData<Soldier>(unit).Garrison==0)return unit;return Entity.Null;}
                yield return WaitFor(()=>Pending()!=Entity.Null,"Recruitment places soldier in pending pool");ulong id=em.GetComponentData<Identity>(Pending()).Id;
                // Exercise the actual list button then a specific destination slot.
                Button Pool()=>view.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text.Contains("#"+id+" ·"));
                yield return WaitFor(()=>Pool()!=null,"Pending soldier is selectable");Pool().onClick.Invoke();
                Button Slot()=>view.GetComponentsInChildren<Button>().FirstOrDefault(x=>x.interactable&&x.GetComponentInChildren<TMP_Text>()!=null&&x.GetComponentInChildren<TMP_Text>().text=="槽 1 · 空位");
                yield return WaitFor(()=>Slot()!=null,"Selecting pending soldier enables empty slot");Slot().onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison!=0,"Clicking destination assigns selected soldier");
                ulong assigned=em.GetComponentData<Soldier>(Sim.Find(em,id)).Garrison;view.SelectBuildingDetails(assigned);
                yield return WaitFor(()=>card.BuildingId==assigned&&card.GarrisonSlots.GetComponentsInChildren<TMP_Text>().Any(x=>x.text==em.GetComponentData<Identity>(Sim.Find(em,id)).Name.ToString()),"Occupied slot displays soldier name");
                card.GarrisonSlots.GetComponentsInChildren<Button>().First(x=>x.GetComponentsInChildren<TMP_Text>().Any(t=>t.text==em.GetComponentData<Identity>(Sim.Find(em,id)).Name.ToString())).onClick.Invoke();
                Require(view.SoldierDetailsOpen,"Occupied portrait opens soldier detail");view.SoldierDetailsName.onEndEdit.Invoke("守城新兵");yield return WaitFor(()=>em.GetComponentData<Identity>(Sim.Find(em,id)).Name.ToString()=="守城新兵","Soldier detail renames authoritative identity");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldier-details.png");yield return null;
                view.CloseSoldierDetails();Require(!view.SoldierDetailsOpen,"Soldier detail can close without losing underlying building");
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Library/LandsongEcs/building-garrison.png");yield return null;
            }
            finally{view.CloseSoldierDetails();if(view.BuildingConfirmPanel!=null)view.BuildingConfirmPanel.SetActive(false);view.BuildingDetailsPanel.SetActive(false);view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
