using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Landsong.ECS.Presentation
{
    // Application-owned presentation only. Never creates a listener or owns simulation state.
    public sealed class PresentationRuntime : MonoBehaviour
    {
        public static PresentationRuntime Instance {get;private set;}
        public GamePresentationCatalog Catalog {get;private set;}
        sealed class Voice {public AudioSource Source;public PresentationCue Cue;public AudioBus Bus;public float Gain;public bool Frozen;}
        readonly List<Voice> voices=new List<Voice>();readonly Dictionary<PresentationCue,float> last=new Dictionary<PresentationCue,float>();
        AudioSource music,ambient;AudioClip nextMusic,nextAmbient;int preferences=-1;float nextBind;bool paused,game;
        public int VoiceCount=>voices.Count(v=>v.Source.isPlaying||v.Frozen);
        public int SourceCount=>voices.Count+2;
        public string Mode {get;private set;}
        public static string LanguageDirectory=>Path.Combine(Application.persistentDataPath,"ExternalLanguagePacks");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Begin(){if(Instance==null)new GameObject("ECS Presentation Runtime").AddComponent<PresentationRuntime>();}
        void Awake()
        {
            if(Instance!=null&&Instance!=this){Destroy(gameObject);return;}Instance=this;DontDestroyOnLoad(gameObject);
            Catalog=Resources.Load<GamePresentationCatalog>("LandsongPresentation");music=Source("Music");ambient=Source("Ambient");music.loop=ambient.loop=true;
            for(int i=0;i<16;i++)voices.Add(new Voice{Source=Source("Cue "+i)});
            PresentationText.Initialize(Catalog);PresentationText.Discover(LanguageDirectory);ApplyPreferences();SceneManager.activeSceneChanged+=SceneChanged;
        }
        AudioSource Source(string name){var child=new GameObject(name);child.transform.SetParent(transform,false);var source=child.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;return source;}
        void SceneChanged(Scene from,Scene to){nextBind=0;StopEffects();}
        void OnDestroy(){SceneManager.activeSceneChanged-=SceneChanged;StopEffects();if(Instance==this)Instance=null;}
        public void StopEffects(){foreach(var voice in voices){if(voice.Source==null)continue;voice.Source.Stop();voice.Source.clip=null;voice.Frozen=false;}last.Clear();}
        void ApplyPreferences()
        {
            preferences=InterfaceSettings.Revision;var p=InterfaceSettings.Current;PresentationText.SetLanguage(p.Language);AudioListener.volume=p.Muted?0:p.Master;
        }
        public float Gain(AudioBus bus)=>bus==AudioBus.Music?InterfaceSettings.Current.Music:bus==AudioBus.Ambient?InterfaceSettings.Current.Ambient:InterfaceSettings.Current.Effects;
        public bool Play(PresentationCue cue)
        {
            var data=Catalog?.Find(cue);if(data==null||data.Clip==null||paused&&data.Bus!=AudioBus.UI)return false;
            if(last.TryGetValue(cue,out float at)&&Time.unscaledTime-at<data.Cooldown)return false;
            if(voices.Count(v=>(v.Source.isPlaying||v.Frozen)&&v.Cue==cue)>=Mathf.Clamp(data.Concurrency,1,16))return false;
            var voice=voices.FirstOrDefault(v=>!v.Source.isPlaying&&!v.Frozen);if(voice==null)return false;
            last[cue]=Time.unscaledTime;voice.Cue=cue;voice.Bus=data.Bus;voice.Gain=data.Volume;voice.Source.clip=data.Clip;voice.Source.volume=Mathf.Clamp01(data.Volume)*Gain(data.Bus);voice.Source.Play();return true;
        }
        public void SetPaused(bool value)
        {
            if(paused==value)return;paused=value;foreach(var voice in voices){if(voice.Bus==AudioBus.UI)continue;if(value&&voice.Source.isPlaying){voice.Source.Pause();voice.Frozen=true;}else if(!value&&voice.Frozen){voice.Source.UnPause();voice.Frozen=false;}}
        }
        void Update()
        {
            if(preferences!=InterfaceSettings.Revision)ApplyPreferences();
            var path=SceneManager.GetActiveScene().path;game=EcsSceneFlow.GameReady&&path==EcsSceneFlow.Game;Session state=default;
            if(game){var world=World.DefaultGameObjectInjectionWorld;if(world!=null&&world.IsCreated){var root=Sim.Root(world.EntityManager);if(root!=Entity.Null)state=world.EntityManager.GetComponentData<Session>(root);}}
            SetPaused(game&&state.Paused!=0);Mode=!game?"Menu":state.Phase==Phase.Day?"Day":state.NightKind!=NightKind.Peaceful?"Combat":"Night";
            if(Catalog!=null){nextMusic=Mode=="Menu"?Catalog.MenuMusic:Mode=="Day"?Catalog.DayMusic:Mode=="Combat"?Catalog.CombatMusic:Catalog.NightMusic;nextAmbient=Mode=="Night"||Mode=="Combat"?Catalog.NightAmbient:Catalog.DayAmbient;}
            Loop(music,nextMusic,Gain(AudioBus.Music));Loop(ambient,nextAmbient,Gain(AudioBus.Ambient));
            foreach(var voice in voices)voice.Source.volume=Mathf.Clamp01(voice.Gain)*Gain(voice.Bus);
            if(Time.unscaledTime>=nextBind){nextBind=Time.unscaledTime+.3f;BindUi();}
        }
        void Loop(AudioSource source,AudioClip desired,float volume)
        {
            float step=Time.unscaledDeltaTime/Mathf.Max(.1f,Catalog==null?.6f:Catalog.FadeSeconds);
            if(source.clip!=desired){source.volume=Mathf.MoveTowards(source.volume,0,step);if(source.volume<=.001f){source.Stop();source.clip=desired;if(desired!=null)source.Play();}}
            else {source.volume=Mathf.MoveTowards(source.volume,volume,step);if(desired!=null&&!source.isPlaying)source.Play();}
        }
        public void BindUi()
        {
            foreach(var text in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            {
                if(!text.gameObject.scene.IsValid()||!text.gameObject.scene.isLoaded||!EcsSceneFlow.IsAppScene(text.gameObject.scene.path))continue;
                var binding=text.GetComponent<PresentationTextBinding>();var input=text.GetComponentInParent<TMP_InputField>(true);
                if(input!=null&&input.textComponent==text){if(binding!=null)Destroy(binding);continue;}if(binding==null)text.gameObject.AddComponent<PresentationTextBinding>();
            }
            foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(button.gameObject.scene.IsValid()&&button.gameObject.scene.isLoaded&&EcsSceneFlow.IsAppScene(button.gameObject.scene.path)&&button.GetComponent<PresentationClick>()==null)button.gameObject.AddComponent<PresentationClick>();
        }
    }
    public sealed class PresentationClick : MonoBehaviour,IPointerClickHandler,ISubmitHandler
    {
        Button button;void Awake(){button=GetComponent<Button>();}void Click(){if(button!=null&&button.IsInteractable())PresentationRuntime.Instance?.Play(PresentationCue.Click);}
        public void OnPointerClick(PointerEventData data){if(data.button==PointerEventData.InputButton.Left)Click();}
        public void OnSubmit(BaseEventData data){Click();}
    }
}
