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
        IEnumerator PortraitsUi(EcsGameView view,EntityManager em,Entity root)
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
                yield return WaitFor(()=>view.BeautyEventButton!=null&&view.BeautyEventButton.gameObject.activeSelf,"Youth beauty exposes a postponable HUD event");
                view.OpenPanel("王室");
                yield return WaitFor(()=>Object.FindFirstObjectByType<CourtPresentationView>()?.Node(id)!=null,"Portrait family fixture is visible");
                var graph=Object.FindFirstObjectByType<CourtPresentationView>();graph.Node(id).onClick.Invoke();
                yield return WaitFor(()=>view.RoyalDetails.Portrait.sprite!=null,"Burst composition publishes royal detail sprite");
                Require(!view.BeautyEventButton.gameObject.activeSelf,"Beauty HUD never covers family detail panel");
                var face=graph.Node(id).transform.Find("Portrait").GetComponent<Image>();
                yield return WaitFor(()=>face.sprite!=null&&face.sprite==view.RoyalDetails.Portrait.sprite,"Family and detail share one cached sprite");
                Require(face.sprite.texture.width==64&&face.sprite.texture.filterMode==FilterMode.Point,"Project resolution and pixel sampling reach actual UI");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/portraits-family.png");yield return null;}
                view.ClosePanel();view.OpenPanel("王室");yield return WaitFor(()=>view.RoyalDetails.Portrait.sprite!=null,"Reopening a panel rebinds its cached portrait");
                var before=SnapshotCodec.Capture(em,root);view.OpenPortrait(id);
                yield return WaitFor(()=>view.PortraitOpen&&view.PortraitWindow.GetComponentsInChildren<Image>().Any(i=>i.name=="Portrait preview"&&i.sprite!=null),"Customization preview composes through same renderer");
                view.PortraitWindow.GetComponentsInChildren<Button>(true).First(b=>b.GetComponentInChildren<TMPro.TMP_Text>()?.text.StartsWith("脸型：")==true).onClick.Invoke();
                view.PortraitWindow.GetComponentsInChildren<Slider>(true)[0].value=177;
                yield return new WaitForSecondsRealtime(.4f);
                Require(before.SequenceEqual(SnapshotCodec.Capture(em,root)),"Preview slider edits never mutate authoritative DNA");
                view.PortraitCloseButton.onClick.Invoke();Require(!view.PortraitOpen&&PortraitOps.CanCustomize(em,root,king),"Later preserves the one-use opportunity");
                view.OpenPortrait(id);view.PortraitWindow.GetComponentsInChildren<Slider>(true)[0].value=177;
                view.PortraitWindow.GetComponentsInChildren<ScrollRect>()[0].verticalNormalizedPosition=0;
                yield return new WaitForSecondsRealtime(.5f);
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/beauty-customization.png");yield return null;}
                view.PortraitConfirmButton.onClick.Invoke();
                yield return WaitFor(()=>em.GetComponentData<PortraitDNA>(king).Customized==1,"Confirm dispatches one authoritative customization command");
                Require(em.GetComponentData<PortraitDNA>(king).Skin.r==177,"Runtime RGB choice persists exactly");
                view.OpenPortrait(id);Require(!view.PortraitOpen,"Completed beauty cannot be customized twice");
                var saved=SnapshotCodec.Capture(em,root);SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,saved));king=Sim.Find(em,id);
                view.ClosePanel();view.OpenPanel("王室");
                yield return WaitFor(()=>view.RoyalDetails.Portrait.sprite!=null,"Restore rebinds portrait after ECS entity replacement");
                Require(!PortraitOps.CanCustomize(em,root,king),"Save restore preserves consumed customization");
                var adultSprite=view.RoyalDetails.Portrait.sprite;royal=em.GetComponentData<Royal>(king);royal.Age=75;em.SetComponentData(king,royal);
                yield return WaitFor(()=>view.RoyalDetails.Portrait.sprite!=null&&view.RoyalDetails.Portrait.sprite!=adultSprite,"Customized portrait still ages procedurally");
                royal=em.GetComponentData<Royal>(king);royal.Age=15;em.SetComponentData(king,royal);var aged=view.RoyalDetails.Portrait.sprite;
                yield return WaitFor(()=>view.RoyalDetails.Portrait.sprite!=null&&view.RoyalDetails.Portrait.sprite!=aged,"Under-youth portrait uses colored swaddle renderer");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/portrait-swaddle.png");yield return null;}
                view.ClosePanel();int definition=Sim.FirstDefinition(em,root,ContentKind.Soldier);var soldier=Sim.Spawn(em,root,definition,Unity.Mathematics.float3.zero,true);
                Sim.Set(em,soldier,new Soldier{Experience=39});MilitaryOps.ConfigureCombatant(em,root,soldier,0,false,false,0,Unity.Mathematics.float3.zero);MilitaryOps.InitializePerson(em,root,soldier);
                view.OpenPanel("驻军");
                yield return WaitFor(()=>view.PrimaryRows.GetComponentsInChildren<Image>().Concat(view.SecondaryRows.GetComponentsInChildren<Image>()).Any(i=>i.name=="Person portrait"&&i.sprite!=null),"Soldier roster renders persistent individual portrait");
                Canvas.ForceUpdateCanvases();var soldierImage=view.SecondaryRows.GetComponentsInChildren<Image>().First(i=>i.name=="Person portrait");var corners=new Vector3[4];soldierImage.rectTransform.GetWorldCorners(corners);
                Require(((RectTransform)soldierImage.transform.parent).InverseTransformPoint(corners[0]).x>=((RectTransform)soldierImage.transform.parent).rect.xMin-.1f,"Soldier portrait stays inside row without left clipping");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/soldier-portraits.png");yield return null;}
            }
            finally{view.ClosePortrait();view.ClosePanel();SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));}
        }
    }
}
#endif
