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
        IEnumerator PersonRequestsUi(EcsGameView view,EntityManager em,Entity root,ulong personId)
        {
            var original=SnapshotCodec.Capture(em,root);
            try
            {
                Sim.Grant(em,root,Sim.FindDefinition(em,root,new FixedString128Bytes("feature.Expedition")));
                Require(PersonRequestOps.OfferExpedition(em,root,Sim.Find(em,personId)),"Personal request UI fixture adds expedition wish");
                view.OpenPanel("王室");yield return new WaitForSecondsRealtime(.4f);
                var graph=Object.FindFirstObjectByType<CourtPresentationView>();graph.Node(personId).onClick.Invoke();
                yield return WaitFor(()=>view.RoyalDetails.PersonId==personId&&view.RoyalDetails.Requests.interactable,"Selected living person has Handle requests action");
                Require(graph.Node(personId).transform.Find("Pending requests").GetComponentInChildren<TMP_Text>().text=="! 2","Family badge counts simultaneous pending requests");
                var pending=SnapshotCodec.Capture(em,root);view.RoyalDetails.Requests.onClick.Invoke();
                yield return WaitFor(()=>view.PersonRequestsOpen&&view.PersonRequestsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("渴望一次远征")),"Request window lists expedition wish");
                Require(view.PersonRequestsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text.Contains("赐婚请求")),"Snoozed marriage remains in selected person's request list");
                view.Send(CommandKind.Advance);view.OpenPanel("建筑");Require(view.Panel=="王室"&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Request modal blocks underlying gameplay commands and panel switching");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/person-requests.png");yield return null;}
                view.PersonRequestsCloseButton.onClick.Invoke();Require(!view.PersonRequestsOpen&&pending.SequenceEqual(SnapshotCodec.Capture(em,root)),"Closing request list defers all decisions without state change");
                view.RoyalDetails.Requests.onClick.Invoke();yield return new WaitForSecondsRealtime(.3f);
                Button Action(string label)=>view.PersonRequestsWindow.GetComponentsInChildren<Button>().Single(b=>b.GetComponentInChildren<TMP_Text>().text==label);
                Action("查看赐婚请求").onClick.Invoke();Require(!view.PersonRequestsOpen&&view.MarriageOpen,"Marriage request opens existing two-person decision flow");
                view.MarriageCloseButton.onClick.Invoke();view.ShowPersonRequests(personId);yield return new WaitForSecondsRealtime(.3f);
                Action("安排远征").onClick.Invoke();Require(view.Panel=="远征"&&!view.PersonRequestsOpen&&PersonRequestOps.Pending(em,root,Sim.Find(em,personId)).Count==2,"Arranging expedition navigates without prematurely fulfilling wish");
                view.OpenPanel("王室");yield return new WaitForSecondsRealtime(.3f);view.ShowPersonRequests(personId);yield return new WaitForSecondsRealtime(.3f);
                Action("拒绝远征请求").onClick.Invoke();
                yield return WaitFor(()=>PersonRequestOps.Pending(em,root,Sim.Find(em,personId)).Count==1,"Refusal dispatches only selected expedition request command");
                yield return new WaitForSecondsRealtime(.3f);
                Require(graph.Node(personId).transform.Find("Pending requests").GetComponentInChildren<TMP_Text>().text=="! 1","Family badge refreshes after individual request resolution");
                view.ShowMarriage(personId);view.MarriageApproveButton.onClick.Invoke();
                yield return WaitFor(()=>PersonRequestOps.Pending(em,root,Sim.Find(em,personId)).Count==0,"Marriage approval clears last pending request");yield return new WaitForSecondsRealtime(.3f);
                Require(!graph.Node(personId).transform.Find("Pending requests").gameObject.activeSelf,"Family badge disappears when all requests resolved");
                view.ShowPersonRequests(personId);yield return new WaitForSecondsRealtime(.3f);
                Require(view.PersonRequestsWindow.GetComponentsInChildren<TMP_Text>().Any(t=>t.text=="暂无待处理请求。"),"Empty request list has explicit feedback and remains closeable");view.BackPanel();Require(!view.PersonRequestsOpen&&view.Panel=="王室","Back closes request window before family panel");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/royal-requests-details.png");yield return null;}
            }
            finally{view.ClosePersonRequests();view.CloseMarriage();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
