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
        IEnumerator RoyalFamilyUi(EcsGameView view,EntityManager em,Entity root)
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
                yield return WaitFor(()=>view.MarriageEventButton!=null&&view.MarriageEventButton.gameObject.activeSelf,"Pending marriage appears as clickable HUD event");
                var pending=SnapshotCodec.Capture(em,root);view.MarriageEventButton.onClick.Invoke();
                yield return WaitFor(()=>view.MarriageOpen,"Clicking marriage event opens two-person decision modal");
                var labels=view.MarriageWindow.GetComponentsInChildren<TMPro.TMP_Text>();
                Require(labels.Any(t=>t.text=="沈知远")&&labels.Any(t=>t.text=="苏清和")&&labels.Any(t=>t.text.Contains("成长性")),"Both people have named detailed panels");
                view.MarriageCloseButton.onClick.Invoke();
                Require(!view.MarriageOpen&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Later closes without accepting, refusing, or rolling RNG");
                SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,pending));
                king=Sim.Find(em,oldId);child=Sim.Find(em,childId);mate=Sim.Find(em,mateId);
                yield return PersonRequestsUi(view,em,root,childId);
                king=Sim.Find(em,oldId);child=Sim.Find(em,childId);mate=Sim.Find(em,mateId);
                Require(RoyalFamilyOps.RequestValid(em,root,child),"Restored pending request can still be opened and adjudicated");
                view.ShowMarriage(childId);yield return new WaitForSecondsRealtime(.3f);
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/marriage-request.png");yield return null;}
                view.MarriageApproveButton.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Royal>(Sim.Find(em,childId)).Spouse==mateId,"Approve button dispatches ECS marriage decision");
                Require(!view.MarriageOpen&&em.GetComponentData<Royal>(Sim.Find(em,mateId)).Spouse==childId,"Marriage modal closes and spouses become reciprocal");
                var grandson=DynastyOps.CreateRoyal(em,root,"沈景明",2,3,childId);var g=em.GetComponentData<Royal>(grandson);g.SecondParent=mateId;em.SetComponentData(grandson,g);
                CourtOps.Succeed(em,root,king,child,true);CourtOps.Die(em,root,king,0);
                view.OpenPanel("王室");
                yield return WaitFor(()=>Object.FindFirstObjectByType<CourtPresentationView>()?.GenerationCount>=3,"Family view groups all historical generations into horizontal bands");
                var graph=Object.FindFirstObjectByType<CourtPresentationView>();
                Require(graph.FamilyCount>=2&&graph.SuccessionEdgeCount>=1,"Family groups and permanent succession links are drawn");
                Require(graph.Node(childId).transform.Find("Crown").gameObject.activeSelf&&!graph.Node(oldId).transform.Find("Crown").gameObject.activeSelf,"Only current monarch carries crown marker");
                Require(graph.Node(oldId).image.color.r<graph.Node(childId).image.color.r,"Dead monarch is rendered gray");
                Require(((RectTransform)graph.Node(childId).transform).anchoredPosition.y<((RectTransform)graph.Node(oldId).transform).anchoredPosition.y,"Child generation lies below parents");
                Require(Mathf.Approximately(((RectTransform)graph.Node(childId).transform).anchoredPosition.y,((RectTransform)graph.Node(mateId).transform).anchoredPosition.y),"Spouses share one generation and family row");
                graph.Node(childId).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(view.RoyalDetails.PersonId==childId&&view.RoyalDetails.Identity.text.Contains("沈知远")&&view.RoyalDetails.Identity.text.Contains("男"),"Clicking portrait opens selected identity and gender in right details");
                Require(!view.PrimaryRows.parent.parent.gameObject.activeSelf&&view.RoyalDetails.gameObject.activeInHierarchy,"Royal details replace the old left scrolling person list");
                graph.Node(Id(grandson)).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(view.RoyalDetails.Description.text.Contains("父亲：沈知远")&&view.RoyalDetails.Description.text.Contains("母亲：苏清和"),"Right details map real father and mother");
                Require(view.RoyalDetails.Designate.interactable&&!view.RoyalDetails.Marriage.interactable,"Underage heir can inherit but cannot marry");
                view.RoyalDetails.Designate.onClick.Invoke();Require(view.BuildingConfirmPanel.activeSelf,"Detail designation opens existing confirmation");view.CancelBuildingInteraction();
                graph.Node(oldId).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(!view.RoyalDetails.Designate.interactable&&!view.RoyalDetails.Execute.interactable&&!view.RoyalDetails.Marriage.interactable,"Dead portrait remains inspectable with all actions disabled");
                view.RoyalDetails.OverviewTab.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(view.PrimaryRows.parent.parent.gameObject.activeInHierarchy,"Court affairs preserve global decisions in right tab");
                graph.Node(childId).onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/royal-family.png");yield return null;}
                graph.CloseButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Require(!view.IsPanelOpen&&!graph.gameObject.activeSelf,"Family close dismisses entire royal panel");
                var request=DynastyOps.CreateRoyal(em,root,"沈云舒",2,20,childId);var proposed=DynastyOps.CreateRoyal(em,root,"陆怀瑾",4,22);
                Gender(request,PersonGender.Female);Gender(proposed,PersonGender.Male);
                Require(RoyalFamilyOps.Request(em,root,request,proposed),"Second marriage request belongs to new monarch's child");view.ShowMarriage(Id(request));
                yield return WaitFor(()=>view.MarriageOpen,"New dynasty generation can open marriage request");view.MarriageRefuseButton.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<Royal>(request).RequestedSpouse==0,"Refuse button resolves pending request through ECS");
                Require(em.GetComponentData<Royal>(request).Spouse==0,"Refused requester remains unmarried");
                view.OpenPanel("王室");yield return WaitFor(()=>graph.Node(Id(request))!=null,"New child has a portrait");graph.Node(Id(request)).onClick.Invoke();
                yield return WaitFor(()=>view.RoyalDetails.PersonId==Id(request)&&view.RoyalDetails.Marriage.interactable,"Adult child can initiate arranged marriage without pending request");
                view.RoyalDetails.Marriage.onClick.Invoke();yield return WaitFor(()=>view.MarriageOpen&&RoyalFamilyOps.Candidates(em,root,request).Count>=3,"Arranged marriage prepares persistent candidates");
                yield return new WaitForSecondsRealtime(.3f);
                var candidate=RoyalFamilyOps.Candidates(em,root,request)[0];var candidateId=Id(candidate);var candidateName=em.GetComponentData<Identity>(candidate).Name.ToString();
                var candidateButton=view.MarriageWindow.GetComponentsInChildren<UnityEngine.UI.Button>().First(b=>b.GetComponentInChildren<TMPro.TMP_Text>().text.StartsWith(candidateName+" · "));candidateButton.onClick.Invoke();
                yield return new WaitForSecondsRealtime(.3f);Require(view.MarriageApproveButton.interactable,"Chosen spouse details allow final confirmation");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/arrange-marriage.png");yield return null;}
                view.MarriageApproveButton.onClick.Invoke();yield return WaitFor(()=>em.GetComponentData<Royal>(request).Spouse==candidateId,"Active marriage confirmation commits chosen spouse");
            }
            finally{view.CloseMarriage();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
