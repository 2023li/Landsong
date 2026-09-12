#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Linq;
using Landsong.ECS.Persistence;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.ECS.Presentation
{
    public sealed partial class EcsPlayerSmoke
    {
        IEnumerator PresentationUi(UI_GamePanel view,EntityManager em,Entity root)
        {
            var original=SnapshotCodec.Capture(em,root);var preferences=InterfaceSettings.Current.Copy();var priorPersistence=InterfaceSettings.PersistenceOverride;string saved=null;InterfaceSettings.PersistenceOverride=json=>saved=json;
            var runtime=PresentationRuntime.Instance;Require(runtime!=null&&runtime.Catalog!=null,"15 application presentation runtime and resource catalog loaded");var catalog=runtime.Catalog;
            var models=catalog.Models;var portraits=catalog.Portraits;var dayMusic=catalog.DayMusic;var click=catalog.Find(PresentationCue.Click);var hit=catalog.Find(PresentationCue.Hit);var clickClip=click.Clip;var hitClip=hit.Clip;float cooldown=click.Cooldown;int concurrent=click.Concurrency;
            var ownedClip=AudioClip.Create("Owned 15 audio probe",44100,1,44100,false);var ownedPixels=new Texture2D(2,2);ownedPixels.SetPixels(new[]{Color.cyan,Color.cyan,Color.cyan,Color.cyan});ownedPixels.Apply();var ownedPortrait=Sprite.Create(ownedPixels,new Rect(0,0,2,2),new Vector2(.5f,.5f));
            var bridge=view.WorldInteraction.WorldPresentation;
            Require(bridge != null && bridge.IsBound, "15 world presentation explicitly bound to current game session");
            try
            {
                InterfaceSettings.Apply(new InterfacePreferences(),false,false);runtime.StopEffects();view.OpenPanel(GamePanelId.Building);yield return null;
                Require(FindObjectsByType<PresentationRuntime>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1&&runtime.SourceCount==18,"15 exactly one runtime/listener and bounded 18 audio sources");
                click.Clip=hit.Clip=ownedClip;click.Cooldown=1;click.Concurrency=2;Require(runtime.Play(PresentationCue.Click)&&!runtime.Play(PresentationCue.Click),"15 cue cooldown rejects duplicate dispatch");runtime.StopEffects();click.Cooldown=0;
                Require(runtime.Play(PresentationCue.Click)&&runtime.Play(PresentationCue.Click)&&!runtime.Play(PresentationCue.Click)&&runtime.VoiceCount==2,"15 per-cue concurrency cap enforced");runtime.StopEffects();
                Require(runtime.Play(PresentationCue.Hit),"15 effects bus playback");var s=em.GetComponentData<Session>(root);s.Paused=1;em.SetComponentData(root,s);yield return null;yield return null;
                Require(runtime.VoiceCount==1&&!runtime.Play(PresentationCue.Hit)&&runtime.Play(PresentationCue.Click),"15 pause freezes world audio while UI stays usable");runtime.StopEffects();s.Paused=0;em.SetComponentData(root,s);
                var p=InterfaceSettings.Current.Copy();p.Muted=true;p.Music=.23f;p.Effects=.34f;p.Ambient=.45f;InterfaceSettings.Apply(p,false,false);yield return null;
                Require(AudioListener.volume==0&&Mathf.Approximately(runtime.Gain(AudioBus.Music),.23f)&&Mathf.Approximately(runtime.Gain(AudioBus.Effects),.34f)&&Mathf.Approximately(runtime.Gain(AudioBus.Ambient),.45f),"15 mute and separate buses consume shared preferences once");
                catalog.DayMusic=ownedClip;yield return new WaitForSecondsRealtime(1.6f);Require(runtime.Mode=="Day"&&runtime.GetComponentsInChildren<AudioSource>().Count(a=>a.loop&&a.clip==ownedClip&&a.isPlaying)==1,"15 BGM state selects and loops exactly one source");
                p.Muted=false;InterfaceSettings.Apply(p,false,false);
                view.PauseMenu.Open();yield return null;view.PauseMenu.SettingsButton.onClick.Invoke();yield return null;
                yield return WaitFor(() => Shared<UI_SettingPanel>() != null, "15 opens shared settings");
                var settingsPanel = Shared<UI_SettingPanel>();
                Require(bridge.IsBound, "15 shared settings retain game world presentation binding");
                settingsPanel.Language.onClick.Invoke();settingsPanel.ApplyButton.onClick.Invoke();yield return new WaitForSecondsRealtime(.4f);
                Require(saved!=null&&InterfaceSettings.Decode(saved).Language=="en"&&PresentationText.Language=="en","15 language selected through actual settings and owned persistence sink");
                var translated=settingsPanel.ApplyButton.GetComponentInChildren<TMP_Text>();translated.ForceMeshUpdate(false,true);
                Require(translated.text=="应用"&&translated.GetParsedText().Contains("Apply"),"15 rendered TMP text translates without mutating raw routing text");
                Require(view.Buildings.NameInput.textComponent.GetComponent<UI_Common_TextBinding>()==null,"15 editable player names are not language preprocessed");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/presentation-language.png");yield return new WaitForEndOfFrame();}
                yield return CloseShared<UI_SettingPanel>();
                view.PauseMenu.Close();yield return null;InterfaceSettings.Apply(new InterfacePreferences(),false,false);
                var king=CourtOps.Monarch(em);var kid=DynastyOps.CreateRoyal(em,root,"表现验收继承人",2,8,em.GetComponentData<Identity>(king).Id);var kidId=em.GetComponentData<Identity>(kid).Id;
                PortraitOps.Ensure(em,root,kid);Require(em.HasComponent<PortraitDNA>(kid),"15 manually created fixture receives the normal portrait initialization");
                catalog.Portraits=portraits.Concat(new[]{new GamePresentationCatalog.Portrait{Person=kidId,Image=ownedPortrait}}).ToArray();view.OpenPanel(GamePanelId.Royal);
                var graph=view.Court.CourtGraph;
                yield return WaitFor(()=>graph.gameObject.activeInHierarchy&&graph.Node(kidId)!=null,"15 actual family graph node creation");
                Require(graph.EdgeCount>0&&graph.NodeView(kidId)?.PortraitBinding!=null,"15 parent relation and persistent portrait view binding");graph.Node(kidId).onClick.Invoke();
                yield return WaitFor(()=>view.Court.RoyalDetails!=null&&view.Court.RoyalDetails.PersonId==kidId&&view.Court.RoyalDetails.Designate.interactable,"15 graph selection routes to existing lawful royal actions");
                yield return WaitFor(()=>view.Court.RoyalDetails.Portrait.sprite==ownedPortrait,"15 stable person ID resolves its configured portrait in selected details");
                int node=graph.Node(kidId).GetInstanceID();graph.Scroll.horizontalNormalizedPosition=.4f;yield return new WaitForSecondsRealtime(.5f);Require(graph.Node(kidId).GetInstanceID()==node,"15 family cards remain stable across refreshes");
                if(Application.isEditor){ScreenCapture.CaptureScreenshot("Library/LandsongEcs/presentation-family.png");yield return new WaitForEndOfFrame();}
                view.OpenPanel(GamePanelId.Policy);yield return new WaitForSecondsRealtime(.4f);var policyGraph=view.Policies.CourtGraph;
                Require(policyGraph!=graph&&policyGraph.gameObject.activeInHierarchy&&policyGraph.NodeCount>0&&policyGraph.Header.text.StartsWith("政策"),"15 policies render selectable tiered cards in their own configured graph");
                Require(!graph.gameObject.activeSelf&&!view.Talents.CourtGraph.gameObject.activeSelf&&view.FeaturePanels.All(panel=>panel.gameObject.activeSelf==(panel.PanelId==GamePanelId.Policy)),"15 policy navigation hides royal/talent graphs and every other feature root");
                view.OpenPanel(GamePanelId.Building);Require(!graph.gameObject.activeSelf&&!policyGraph.gameObject.activeSelf&&!view.Talents.CourtGraph.gameObject.activeSelf,"15 all court graphs release input ownership immediately on panel switch");
                Entity building;using(var all=Sim.OrderedEntities<Building>(em))building=all[0];var id=em.GetComponentData<Identity>(building);var b=em.GetComponentData<Building>(building);
                catalog.Models=models.Concat(new[]{new GamePresentationCatalog.Model{Definition=Sim.Definition(em,root,id.Definition).Id.ToString(),Stage=b.Stage,Level=b.Level,Skin=b.Skin.ToString(),ActorPrefab=models[0].ActorPrefab,Scale=Vector3.one*2}}).ToArray();
                yield return WaitFor(()=>em.HasComponent<ExternalVisual>(building)&&em.GetComponentData<ExternalVisual>(building).Active==1,"15 configured dynamic model acquires render ownership");
                var actor=FindObjectsByType<PresentationActor>(FindObjectsSortMode.None).First(a=>a.gameObject.name.StartsWith("View · "+id.Id+" ·"));
                Require(actor.transform.parent.parent==null&&actor.transform.lossyScale==Vector3.one*2&&actor.Animator!=null,"15 model world scale independent of Canvas and Animator bound");
                Require(actor.gameObject.scene == view.WorldInteraction.Camera.gameObject.scene, "15 world presentation belongs to explicit game scene rather than persistent UI scene");
                var authority=SnapshotCodec.Capture(em,root);bridge.Emit(PresentationCue.Hit,actor.transform.position,true);yield return null;Require(bridge.EffectCount>0,"15 spatial cue spawns bounded visual effect");
                s=em.GetComponentData<Session>(root);s.Paused=1;em.SetComponentData(root,s);yield return null;int effects=bridge.EffectCount;yield return new WaitForSecondsRealtime(1);Require(bridge.EffectCount==effects&&actor.Animator.speed==0,"15 paused particles and Animator do not expire or advance");
                s.Paused=0;em.SetComponentData(root,s);yield return new WaitForSecondsRealtime(1);Require(bridge.EffectCount==0,"15 completed effect released by owner");
                Require(authority.SequenceEqual(SnapshotCodec.Capture(em,root)),"15 models/audio/FX do not alter authority or rewards");
                catalog.Models=models;yield return null;yield return null;Require(em.GetComponentData<ExternalVisual>(building).Active==0,"15 removing optional model returns ECS render ownership");
                for(int i=0;i<50;i++)bridge.Emit(PresentationCue.Hit,Vector3.zero,true);Require(bridge.EffectCount==32,"15 visual effect concurrency bounded at 32");bridge.ClearViews();Require(bridge.EffectCount==0&&bridge.ModelCount==0,"15 explicit owner cleanup releases all models/effects");
            }
            finally
            {
                catalog.Models=models;catalog.Portraits=portraits;catalog.DayMusic=dayMusic;click.Clip=clickClip;hit.Clip=hitClip;click.Cooldown=cooldown;click.Concurrency=concurrent;runtime.StopEffects();
                // Detach the owned test music before destroying its clip; production assets were never saved.
                foreach(var source in runtime.GetComponentsInChildren<AudioSource>())if(source.clip==ownedClip){source.Stop();source.clip=null;}
                Destroy(ownedClip);Destroy(ownedPortrait);Destroy(ownedPixels);bridge.ClearViews();if(view.PauseMenu.IsOpen)view.PauseMenu.Close();
                InterfaceSettings.Apply(preferences,false,false);InterfaceSettings.PersistenceOverride=priorPersistence;SnapshotCodec.Restore(em,root,SnapshotCodec.Decode(em,root,original));view.OpenPanel(GamePanelId.Building);
            }
        }
        void RequirePresentationReleased()
        {
            Require(FindObjectsByType<PresentationRuntime>(FindObjectsSortMode.None).Length==1&&FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length==1,"15 scene transition retains exactly one runtime/listener");
            Require(FindObjectsByType<WorldPresentationView>(FindObjectsSortMode.None).Length==0&&FindObjectsByType<PresentationActor>(FindObjectsSortMode.None).Length==0&&GameObject.Find("ECS World Presentation")==null,"15 scene unload destroys owned world models/effects/root");
        }
    }
}
#endif
