#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Entities;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator RoyalFamilyUi(UI_GamePanel view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);
            ulong Id(Entity e)=>em.GetComponentData<Identity>(e).Id;
            void Gender(Entity e,PersonGender gender){var p=em.GetComponentData<Royal>(e);p.Gender=gender;em.SetComponentData(e,p);}
            try
            {
                var king=CourtOps.Monarch(em);var oldId=Id(king);
                var child=DynastyOps.CreateRoyal(em,root,"沈知远",2,24,oldId);
                var mate=DynastyOps.CreateRoyal(em,root,"苏清和",4,23);var childId=Id(child);var mateId=Id(mate);
                Gender(child,PersonGender.Male);Gender(mate,PersonGender.Female);
                Require(RoyalFamilyOps.Request(em,root,child,mate),"Marriage UI fixture creates authoritative pending request");
                yield return WaitFor(()=>view.marriageController.MarriageEventButton!=null&&view.marriageController.MarriageEventButton.gameObject.activeSelf,"Pending marriage appears as clickable HUD event");
                var pending=SnapshotCodec.Capture(em,root);view.marriageController.MarriageEventButton.onClick.Invoke();
                yield return WaitFor(()=>view.marriageController.MarriageOpen,"Clicking marriage event opens two-person decision modal");
                var personDetails=view.marriageController.MarriagePanel.Person.Details.text;
                var mateDetails=view.marriageController.MarriagePanel.Mate.Details.text;
                Require(personDetails.StartsWith("沈知远\n")&&mateDetails.StartsWith("苏清和\n")&&personDetails.Contains("成长性")&&mateDetails.Contains("成长性"),"Both people have named detailed panels");
                view.marriageController.MarriageCloseButton.onClick.Invoke();
                Require(!view.marriageController.MarriageOpen&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Later closes without accepting, refusing, or rolling RNG");
                SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,pending));
                king=Sim.Find(em,oldId);child=Sim.Find(em,childId);mate=Sim.Find(em,mateId);
                yield return PersonRequestsUi(view,em,root,childId);
                king=Sim.Find(em,oldId);child=Sim.Find(em,childId);mate=Sim.Find(em,mateId);
                Require(RoyalFamilyOps.RequestValid(em,root,child),"Restored pending request can still be opened and adjudicated");
                view.marriageController.ShowMarriage(childId);yield return new WaitForSecondsRealtime(.3f);
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/marriage-request.png");yield return null;}
                view.marriageController.MarriageApproveButton.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Royal>(Sim.Find(em,childId)).Spouse==mateId,"Approve button dispatches ECS marriage decision");
                Require(!view.marriageController.MarriageOpen&&em.GetComponentData<Royal>(Sim.Find(em,mateId)).Spouse==childId,"Marriage modal closes and spouses become reciprocal");
                var grandson=DynastyOps.CreateRoyal(em,root,"沈景明",2,3,childId);var g=em.GetComponentData<Royal>(grandson);g.SecondParent=mateId;em.SetComponentData(grandson,g);
                CourtOps.Succeed(em,root,king,child,true);CourtOps.Die(em,root,king,0);
                view.OpenPanel(GamePanelId.Royal);
                var graph=view.Court.CourtGraph;
                yield return WaitFor(()=>graph.gameObject.activeInHierarchy&&graph.GenerationCount>=3,"Family view groups all historical generations into horizontal bands");
                Require(graph.FamilyCount>=2&&graph.SuccessionEdgeCount>=1,"Family groups and permanent succession links are drawn");
                Require(graph.NodeView(childId).CrownRoot.activeSelf&&!graph.NodeView(oldId).CrownRoot.activeSelf,"Only current monarch carries crown marker");
                Require(graph.Node(oldId).image.color.r<graph.Node(childId).image.color.r,"Dead monarch is rendered gray");
                Require(((RectTransform)graph.Node(childId).transform).anchoredPosition.y<((RectTransform)graph.Node(oldId).transform).anchoredPosition.y,"Child generation lies below parents");
                Require(Mathf.Approximately(((RectTransform)graph.Node(childId).transform).anchoredPosition.y,((RectTransform)graph.Node(mateId).transform).anchoredPosition.y),"Spouses share one generation and family row");
                graph.Node(childId).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(view.Court.RoyalDetails.PersonId==childId&&view.Court.RoyalDetails.Identity.text.Contains("沈知远")&&view.Court.RoyalDetails.Identity.text.Contains("男"),"Clicking portrait opens selected identity and gender in right details");
                Require(!view.Court.RoyalOverviewRoot.gameObject.activeSelf&&view.Court.RoyalDetails.gameObject.activeInHierarchy,"Royal details replace the old left scrolling person list");
                graph.Node(Id(grandson)).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(view.Court.RoyalDetails.Description.text.Contains("父亲：沈知远")&&view.Court.RoyalDetails.Description.text.Contains("母亲：苏清和"),"Right details map real father and mother");
                Require(view.Court.RoyalDetails.Designate.interactable&&!view.Court.RoyalDetails.Marriage.interactable,"Underage heir can inherit but cannot marry");
                view.Court.RoyalDetails.Designate.onClick.Invoke();Require(view.Buildings.BuildingConfirmPanel.activeSelf,"Detail designation opens existing confirmation");view.Buildings.CancelBuildingInteraction();
                graph.Node(oldId).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(!view.Court.RoyalDetails.Designate.interactable&&!view.Court.RoyalDetails.Execute.interactable&&!view.Court.RoyalDetails.Marriage.interactable,"Dead portrait remains inspectable with all actions disabled");
                view.Court.RoyalDetails.OverviewTab.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(view.Court.RoyalOverviewRoot.gameObject.activeInHierarchy,"Court affairs preserve global decisions in right tab");
                graph.Node(childId).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/royal-family.png");yield return null;}
                graph.CloseButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(!view.IsPanelOpen&&!graph.gameObject.activeSelf,"Family close dismisses entire royal panel");
                var request=DynastyOps.CreateRoyal(em,root,"沈云舒",2,20,childId);var proposed=DynastyOps.CreateRoyal(em,root,"陆怀瑾",4,22);
                Gender(request,PersonGender.Female);Gender(proposed,PersonGender.Male);
                Require(RoyalFamilyOps.Request(em,root,request,proposed),"Second marriage request belongs to new monarch's child");view.marriageController.ShowMarriage(Id(request));
                yield return WaitFor(()=>view.marriageController.MarriageOpen,"New dynasty generation can open marriage request");view.marriageController.MarriageRefuseButton.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Royal>(request).RequestedSpouse==0,"Refuse button resolves pending request through ECS");
                Require(em.GetComponentData<Royal>(request).Spouse==0,"Refused requester remains unmarried");
                view.OpenPanel(GamePanelId.Royal);yield return WaitFor(()=>graph.Node(Id(request))!=null,"New child has a portrait");graph.Node(Id(request)).onClick.Invoke();
                yield return WaitFor(()=>view.Court.RoyalDetails.PersonId==Id(request)&&view.Court.RoyalDetails.Marriage.interactable,"Adult child can initiate arranged marriage without pending request");
                view.Court.RoyalDetails.Marriage.onClick.Invoke();yield return WaitFor(()=>view.marriageController.MarriageOpen&&RoyalFamilyOps.Candidates(em,root,request).Count>=3,"Arranged marriage prepares persistent candidates");
                yield return new WaitForSecondsRealtime(.3f);
                var candidate=RoyalFamilyOps.Candidates(em,root,request)[0];var candidateId=Id(candidate);var candidateName=em.GetComponentData<Identity>(candidate).Name.ToString();
                var candidateButton=view.marriageController.MarriageWindow.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith(candidateName+" · "));candidateButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);Require(view.marriageController.MarriageApproveButton.interactable,"Chosen spouse details allow final confirmation");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/arrange-marriage.png");yield return null;}
                view.marriageController.MarriageApproveButton.onClick.Invoke();yield return WaitFor(()=>em.GetComponentData<Royal>(request).Spouse==candidateId,"Active marriage confirmation commits chosen spouse");
            }
            finally{view.marriageController.CloseMarriage();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
