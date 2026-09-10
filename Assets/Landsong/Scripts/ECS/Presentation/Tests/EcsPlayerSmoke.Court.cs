#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;
using Text = TMPro.TextMeshProUGUI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator CourtUi(EcsGameView view,EntityManager em,Entity root,string map)
        {
            var original=SnapshotCodec.Capture(em,root); var gold=em.GetComponentData<GameSettings>(root).Gold;
            InventoryOps.Add(em,root,gold,100); var wood=Sim.FindDefinition(em,root,new FixedString128Bytes("原木")); InventoryOps.Add(em,root,wood,30);
            Entity person=Entity.Null; using(var all=Sim.OrderedEntities<Talent>(em)) foreach(var e in all) if(em.GetComponentData<Identity>(e).Definition==Sim.FindDefinition(em,root,new FixedString128Bytes("talent.placeholder1"))) person=e;
            Require(person!=Entity.Null,"Placeholder contacts exist on new map"); var pid=em.GetComponentData<Identity>(person).Id;
            var pick=(Entity)typeof(EcsGameView).GetMethod("Hit",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(view,new object[]{Vector3.zero,false});
            Require(pick==Entity.Null || !em.HasComponent<Royal>(pick),"Nonvisual people never intercept world building clicks");
            view.OpenPanel("人才"); yield return WaitFor(()=>HasRow(view.PrimaryRows,"打开交际页面"),"Native talent panel opens"); ClickRow(view,"打开交际页面");
            yield return WaitFor(()=>view.PrimaryRows.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("人才1 · 军事顾问")),"Contact list displayed");
            ClickContains(view.PrimaryRows,"人才1 · 军事顾问"); yield return WaitFor(()=>view.PrimaryRows.GetComponentsInChildren<Button>().Any(b=>b.interactable && b.GetComponentInChildren<Text>()?.text.StartsWith("个人委托：提交")==true),"Social task action");
            ClickContains(view.PrimaryRows,"个人委托：提交"); yield return WaitFor(()=>em.GetComponentData<Royal>(person).TaskClaimed==1,"Real UGUI social task raises affection once");
            yield return WaitFor(()=>HasRow(view.PrimaryRows,"招募人才（好感需 30）"),"Affection unlocks recruitment"); ClickRow(view,"招募人才（好感需 30）");
            yield return WaitFor(()=>em.GetComponentData<Talent>(person).Recruited==1,"Same contact recruited");
            yield return WaitFor(()=>HasRow(view.PrimaryRows,"任职：军事顾问（立即付一次工资）"),"Profession appointment action"); ClickRow(view,"任职：军事顾问（立即付一次工资）");
            yield return WaitFor(()=>em.GetComponentData<Talent>(person).Slot>=0,"Native UGUI appointment command processed");
            Require(System.Math.Abs(Sim.Modifier(em,root,RuleKind.SoldierAttackBonus,-1)-.1f)<.0001,"UI appointment pays exact soldier bonus");
            if(Application.isEditor) { yield return new WaitForSecondsRealtime(.4f); ScreenCapture.CaptureScreenshot("Library/LandsongEcs/court-social-"+map+".png"); yield return new WaitForEndOfFrame(); }
            var king=CourtOps.Monarch(em); var heir=DynastyOps.CreateRoyal(em,root,"UI 幼年继承人",2,8,em.GetComponentData<Identity>(king).Id); var hid=em.GetComponentData<Identity>(heir).Id;
            view.OpenPanel("王室"); yield return WaitFor(()=>view.PrimaryRows.GetComponentsInChildren<Button>().Any(b=>b.interactable && b.GetComponentInChildren<Text>()?.text.Contains("UI 幼年继承人 ·")==true),"Underage royal visible"); ClickContains(view.PrimaryRows,"UI 幼年继承人 ·");
            yield return WaitFor(()=>HasRow(view.PrimaryRows,"立为储君：UI 幼年继承人"),"Underage designation enabled"); var before=SnapshotCodec.Capture(em,root);
            ClickRow(view,"立为储君：UI 幼年继承人"); ClickIn(view.BuildingConfirmRows,"取消"); Require(before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Cancelling designation preserves exact day");
            ClickRow(view,"立为储君：UI 幼年继承人"); ClickIn(view.BuildingConfirmRows,"确认"); yield return WaitFor(()=>CourtOps.State(em,root).Crown==hid,"Real UI crown command");
            yield return WaitFor(()=>HasRow(view.PrimaryRows,"赐死：UI 幼年继承人"),"Execution button available for deliberate choice"); before=SnapshotCodec.Capture(em,root); ClickRow(view,"赐死：UI 幼年继承人");
            Require(view.BuildingConfirmRows.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("合法继承人")),"Execution confirmation previews surviving heirs"); ClickIn(view.BuildingConfirmRows,"取消"); Require(before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Cancelled execution cannot kill or penalize crown");
            if(Application.isEditor) { yield return new WaitForSecondsRealtime(.4f); ScreenCapture.CaptureScreenshot("Library/LandsongEcs/court-royal-"+map+".png"); yield return new WaitForEndOfFrame(); }
            var s=em.GetComponentData<Session>(root); s.PublicOpinion=50; em.SetComponentData(root,s); view.OpenPanel("政策");
            yield return WaitFor(()=>HasRow(view.PrimaryRows,"采用：宫廷宿卫"),"Native policy selection"); ClickRow(view,"采用：宫廷宿卫"); yield return WaitFor(()=>em.GetBuffer<PolicyChoice>(root).Length==1,"UI policy chosen");
            view.OpenPanel("情报"); yield return WaitFor(()=>view.PrimaryRows.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("宫廷情报")),"Court intelligence appears in dedicated intelligence panel");
            Require(!view.PrimaryRows.GetComponentsInChildren<Text>().Any(t=>t.text.Contains("精确概率") || t.text.Contains("尝试次数")),"Court UI does not expose retry metadata");
            view.OpenPanel("建筑"); SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original)); Require(original.SequenceEqual(SnapshotCodec.Capture(em,root)),"Wave-nine UI fixtures restore original day without player save IO");
        }
    }
}
#endif
