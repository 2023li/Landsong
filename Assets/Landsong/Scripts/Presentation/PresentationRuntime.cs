using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Entities;
using UnityEngine;
using UnityEngine.SceneManagement;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    // Application-owned presentation only. Never creates a listener or owns simulation state.
    [DefaultExecutionOrder(-8000)]
    public sealed class PresentationRuntime : MonoBehaviour
    {
        public static PresentationRuntime Instance {get;private set;}
        [SerializeField, LabelText("表现资源目录"), Required] GamePresentationCatalog catalog;
        [SerializeField, LabelText("音乐声源"), Required] AudioSource music;
        [SerializeField, LabelText("环境声源"), Required] AudioSource ambient;
        [SerializeField, LabelText("音效声源池"), Required] AudioSource[] effectSources;
        public GamePresentationCatalog Catalog => catalog;
        sealed class Voice {public AudioSource Source;public PresentationCue Cue;public AudioBus Bus;public float Gain;public bool Frozen;}
        readonly List<Voice> voices=new List<Voice>();readonly Dictionary<PresentationCue,float> last=new Dictionary<PresentationCue,float>();
        AudioClip nextMusic,nextAmbient;int preferences=-1;bool paused,game;
        EntityManager sessionManager; World sessionWorld; Entity sessionRoot;
        public void BindSession(EntityManager manager, Entity root)
        {
            if (manager.World == null || !manager.World.IsCreated || root == Entity.Null || !manager.Exists(root)) throw new InvalidOperationException("视听服务会话绑定无效。");
            UnbindSession(); sessionManager = manager; sessionWorld = manager.World; sessionRoot = root;
        }
        public void UnbindSession() { StopEffects(); SetPaused(false); sessionRoot = Entity.Null; sessionWorld = null; sessionManager = default; }
        public int VoiceCount=>voices.Count(v=>v.Source.isPlaying||v.Frozen);
        public int SourceCount=>voices.Count+2;
        public string Mode {get;private set;}
        public static string LanguageDirectory=>Path.Combine(Application.persistentDataPath,"ExternalLanguagePacks");
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetRegistry() { Instance = null; }
        public void Configure(GamePresentationCatalog content, AudioSource musicSource, AudioSource ambientSource, AudioSource[] effects)
        { catalog = content; music = musicSource; ambient = ambientSource; effectSources = effects; }
        void Awake()
        {
            if(Instance!=null&&Instance!=this)throw new InvalidOperationException("表现服务重复配置。");
            if(catalog==null||music==null||ambient==null||effectSources==null||effectSources.Length==0||effectSources.Any(s=>s==null)||effectSources.Distinct().Count()!=effectSources.Length)
                throw new InvalidOperationException("表现服务必须显式配置资源目录、音乐、环境声和独立音效声源池。");
            Instance=this;music.loop=ambient.loop=true;
            foreach(var source in effectSources)voices.Add(new Voice{Source=source});
            PresentationText.Initialize(Catalog);PresentationText.Discover(LanguageDirectory);ApplyPreferences();SceneManager.activeSceneChanged+=SceneChanged;
        }
        void SceneChanged(Scene from,Scene to)=>StopEffects();
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
            game=EcsSceneFlow.GameReady&&sessionWorld!=null&&sessionWorld.IsCreated&&sessionRoot!=Entity.Null&&sessionManager.Exists(sessionRoot);Session state=default;
            if(game)state=sessionManager.GetComponentData<Session>(sessionRoot);
            SetPaused(game&&state.Paused!=0);Mode=!game?"Menu":state.Phase==Phase.Day?"Day":state.NightKind!=NightKind.Peaceful?"Combat":"Night";
            if(Catalog!=null){nextMusic=Mode=="Menu"?Catalog.MenuMusic:Mode=="Day"?Catalog.DayMusic:Mode=="Combat"?Catalog.CombatMusic:Catalog.NightMusic;nextAmbient=Mode=="Night"||Mode=="Combat"?Catalog.NightAmbient:Catalog.DayAmbient;}
            Loop(music,nextMusic,Gain(AudioBus.Music));Loop(ambient,nextAmbient,Gain(AudioBus.Ambient));
            foreach(var voice in voices)voice.Source.volume=Mathf.Clamp01(voice.Gain)*Gain(voice.Bus);
        }
        void Loop(AudioSource source,AudioClip desired,float volume)
        {
            float step=Time.unscaledDeltaTime/Mathf.Max(.1f,Catalog==null?.6f:Catalog.FadeSeconds);
            if(source.clip!=desired){source.volume=Mathf.MoveTowards(source.volume,0,step);if(source.volume<=.001f){source.Stop();source.clip=desired;if(desired!=null)source.Play();}}
            else {source.volume=Mathf.MoveTowards(source.volume,volume,step);if(desired!=null&&!source.isPlaying)source.Play();}
        }
    }
}
