#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator PortraitsUi(UI_GamePanel view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);
            try
            {
                var king=CourtOps.Monarch(em);ulong id=em.GetComponentData<Identity>(king).Id;
                var royal=em.GetComponentData<Royal>(king);royal.Age=16;em.SetComponentData(king,royal);
                int beauty=Sim.FindDefinition(em,root,new FixedString128Bytes("gene.beauty"));var traits=em.GetBuffer<TraitEntry>(king);
                for(int i=traits.Length-1;i>=0;i--)if(traits[i].Definition==beauty)traits.RemoveAt(i);
                traits.Add(new TraitEntry{Definition=beauty,Revealed=1,Active=1});PortraitOps.Ensure(em,root,king);
                var dna=em.GetComponentData<PortraitDNA>(king);dna.Customized=0;dna.InvitationAnnounced=0;em.SetComponentData(king,dna);PortraitOps.Announce(em,root);
                yield return WaitFor(()=>view.portraitController.BeautyEventButton!=null&&view.portraitController.BeautyEventButton.gameObject.activeSelf,"Youth beauty exposes a postponable HUD event");
                view.OpenPanel(GamePanelId.Royal);
                var graph=view.Court.CourtGraph;
                yield return WaitFor(()=>graph.gameObject.activeInHierarchy&&graph.Node(id)!=null,"Portrait family fixture is visible");
                graph.Node(id).onClick.Invoke();
                yield return WaitFor(()=>view.Court.RoyalDetails.Portrait.sprite!=null,"Burst composition publishes royal detail sprite");
                Require(!view.portraitController.BeautyEventButton.gameObject.activeSelf,"Beauty HUD never covers family detail panel");
                var face=graph.NodeView(id).Portrait;
                yield return WaitFor(()=>face.sprite!=null&&face.sprite==view.Court.RoyalDetails.Portrait.sprite,"Family and detail share one cached sprite");
                Require(face.sprite.texture.width==64&&face.sprite.texture.filterMode==FilterMode.Point,"Project resolution and pixel sampling reach actual UI");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/portraits-family.png");yield return null;}
                view.ClosePanel();view.OpenPanel(GamePanelId.Royal);yield return WaitFor(()=>view.Court.RoyalDetails.Portrait.sprite!=null,"Reopening a panel rebinds its cached portrait");
                var before=SnapshotCodec.Capture(em,root);view.portraitController.OpenPortrait(id);
                var customization=view.portraitController.PortraitPanel;
                yield return WaitFor(()=>view.portraitController.PortraitOpen&&customization.Preview.gameObject.activeInHierarchy&&customization.Preview.sprite!=null,"Customization preview composes through same renderer");
                Require(customization.PartLabels[0].text.StartsWith("脸型："),"Configured first customization control edits face shape");
                customization.PartButtons[0].onClick.Invoke();
                customization.ColorSliders[0].value=177;
                yield return new WaitForSecondsRealtime(.4f);
                Require(before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Preview slider edits never mutate authoritative DNA");
                view.portraitController.PortraitCloseButton.onClick.Invoke();Require(!view.portraitController.PortraitOpen&&PortraitOps.CanCustomize(em,root,king),"Later preserves the one-use opportunity");
                view.portraitController.OpenPortrait(id);customization.ColorSliders[0].value=177;
                customization.Scroll.verticalNormalizedPosition=0;
                yield return new WaitForSecondsRealtime(.5f);
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/beauty-customization.png");yield return null;}
                view.portraitController.PortraitConfirmButton.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<PortraitDNA>(king).Customized==1,"Confirm dispatches one authoritative customization command");
                Require(em.GetComponentData<PortraitDNA>(king).Skin.r==177,"Runtime RGB choice persists exactly");
                view.portraitController.OpenPortrait(id);Require(!view.portraitController.PortraitOpen,"Completed beauty cannot be customized twice");
                var saved=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,saved));king=Sim.Find(em,id);
                view.ClosePanel();view.OpenPanel(GamePanelId.Royal);
                yield return WaitFor(()=>view.Court.RoyalDetails.Portrait.sprite!=null,"Restore rebinds portrait after ECS entity replacement");
                Require(!PortraitOps.CanCustomize(em,root,king),"Save restore preserves consumed customization");
                var adultSprite=view.Court.RoyalDetails.Portrait.sprite;royal=em.GetComponentData<Royal>(king);royal.Age=75;em.SetComponentData(king,royal);
                view.Refresh(); // Fixture age changes bypass day settlement and its presentation events.
                yield return WaitFor(()=>view.Court.RoyalDetails.Portrait.sprite!=null&&view.Court.RoyalDetails.Portrait.sprite!=adultSprite,"Customized portrait still ages procedurally");
                royal=em.GetComponentData<Royal>(king);royal.Age=15;em.SetComponentData(king,royal);var aged=view.Court.RoyalDetails.Portrait.sprite;
                view.Refresh();
                yield return WaitFor(()=>view.Court.RoyalDetails.Portrait.sprite!=null&&view.Court.RoyalDetails.Portrait.sprite!=aged,"Under-youth portrait uses colored swaddle renderer");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/portrait-swaddle.png");yield return null;}
                view.ClosePanel();int definition=Sim.FirstDefinition(em,root,ContentKind.Soldier);var soldier=Sim.Spawn(em,root,definition,Unity.Mathematics.float3.zero,true);
                Sim.Set(em,soldier,new Soldier{Experience=39});MilitaryOps.ConfigureCombatant(em,root,soldier,0,false,false,0,Unity.Mathematics.float3.zero);MilitaryOps.InitializePerson(em,root,soldier);
                ulong soldierId=em.GetComponentData<Identity>(soldier).Id;
                UI_GamePanel_SoldierItem SoldierCard()=>view.SecondaryRows.GetComponentsInChildren<UI_GamePanel_SoldierItem>().FirstOrDefault(card=>card.PersonId==soldierId);
                view.OpenPanel(GamePanelId.Garrison);
                yield return WaitFor(()=>SoldierCard()!=null&&SoldierCard().Portrait.sprite!=null,"Soldier roster renders persistent individual portrait for the fixture soldier");
                Canvas.ForceUpdateCanvases();var soldierImage=SoldierCard().Portrait;var corners=new Vector3[4];soldierImage.rectTransform.GetWorldCorners(corners);
                Require(((RectTransform)soldierImage.transform.parent).InverseTransformPoint(corners[0]).x>=((RectTransform)soldierImage.transform.parent).rect.xMin-.1f,"Soldier portrait stays inside row without left clipping");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldier-portraits.png");yield return null;}
            }
            finally{view.portraitController.ClosePortrait();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
