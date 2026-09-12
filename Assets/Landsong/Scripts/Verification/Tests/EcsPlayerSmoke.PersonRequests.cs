#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator PersonRequestsUi(UI_GamePanel view,EntityManager em,Entity root,ulong personId)
        {
            var original=SnapshotCodec.Capture(em,root);
            try
            {
                FeatureOps.Unlock(em,root,Sim.FindDefinition(em,root,new FixedString128Bytes("feature.Expedition")));
                Require(PersonRequestOps.OfferExpedition(em,root,Sim.Find(em,personId)),"Personal request UI fixture adds expedition wish");
                view.OpenPanel(GamePanelId.Royal);yield return new WaitForSecondsRealtime(.4f);
                var graph=view.Court.CourtGraph;graph.Node(personId).onClick.Invoke();
                yield return WaitFor(()=>view.Court.RoyalDetails.PersonId==personId&&view.Court.RoyalDetails.Requests.interactable,"Selected living person has Handle requests action");
                Require(graph.NodeView(personId).RequestLabel.text=="! 2","Family badge counts simultaneous pending requests");
                var pending=SnapshotCodec.Capture(em,root);view.Court.RoyalDetails.Requests.onClick.Invoke();
                yield return WaitFor(()=>view.requestsController.PersonRequestsOpen&&view.requestsController.PersonRequestsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("渴望一次远征")),"Request window lists expedition wish");
                Require(view.requestsController.PersonRequestsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("赐婚请求")),"Snoozed marriage remains in selected person's request list");
                view.Commands.Send(CommandKind.Advance);view.OpenPanel(GamePanelId.Building);Require(view.Panel==GamePanelId.Royal&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Request modal blocks underlying gameplay commands and panel switching");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/person-requests.png");yield return null;}
                view.requestsController.PersonRequestsCloseButton.onClick.Invoke();Require(!view.requestsController.PersonRequestsOpen&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Closing request list defers all decisions without state change");
                view.Court.RoyalDetails.Requests.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Button Action(string label)=>view.requestsController.PersonRequestsWindow.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text==label);
                Action("查看赐婚请求").onClick.Invoke();Require(!view.requestsController.PersonRequestsOpen&&view.marriageController.MarriageOpen,"Marriage request opens existing two-person decision flow");
                view.marriageController.MarriageCloseButton.onClick.Invoke();view.requestsController.ShowPersonRequests(personId);yield return new WaitForSecondsRealtime(.3f);
                Action("安排远征").onClick.Invoke();Require(view.Panel==GamePanelId.Expedition&&!view.requestsController.PersonRequestsOpen&&PersonRequestOps.Pending(em,root,Sim.Find(em,personId)).Count==2,"Arranging expedition navigates without prematurely fulfilling wish");
                view.OpenPanel(GamePanelId.Royal);yield return new WaitForSecondsRealtime(.3f);view.requestsController.ShowPersonRequests(personId);yield return new WaitForSecondsRealtime(.3f);
                Action("拒绝远征请求").onClick.Invoke();
                yield return WaitFor(()=>PersonRequestOps.Pending(em,root,Sim.Find(em,personId)).Count==1,"Refusal dispatches only selected expedition request command");
                yield return new WaitForSecondsRealtime(.3f);
                Require(graph.NodeView(personId).RequestLabel.text=="! 1","Family badge refreshes after individual request resolution");
                view.marriageController.ShowMarriage(personId);view.marriageController.MarriageApproveButton.onClick.Invoke();
                yield return WaitFor(()=>PersonRequestOps.Pending(em,root,Sim.Find(em,personId)).Count==0,"Marriage approval clears last pending request");yield return new WaitForSecondsRealtime(.3f);
                Require(!graph.NodeView(personId).RequestRoot.activeSelf,"Family badge disappears when all requests resolved");
                view.requestsController.ShowPersonRequests(personId);yield return new WaitForSecondsRealtime(.3f);
                Require(view.requestsController.PersonRequestsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="暂无待处理请求。"),"Empty request list has explicit feedback and remains closeable");view.BackPanel();Require(!view.requestsController.PersonRequestsOpen&&view.Panel==GamePanelId.Royal,"Back closes request window before family panel");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/royal-requests-details.png");yield return null;}
            }
            finally{view.requestsController.ClosePersonRequests();view.marriageController.CloseMarriage();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
