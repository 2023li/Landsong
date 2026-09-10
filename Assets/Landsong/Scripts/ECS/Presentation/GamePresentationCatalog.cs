using System;
using UnityEngine;

namespace Landsong.ECS.Presentation
{
    public enum AudioBus { Music, Ambient, Effects, UI }
    public enum PresentationCue { Click, Back, Denied, Build, Complete, Repair, Harvest, Hit, Death, Ruin, Bell, HeroWake, Visitor, Capture, Loot, Celebration, Warning, Report }
    [CreateAssetMenu(menuName="Landsong/ECS/Presentation Catalog")]
    public sealed class GamePresentationCatalog : ScriptableObject
    {
        [Serializable] public sealed class Cue
        {
            public PresentationCue Id; public AudioClip Clip; public AudioBus Bus=AudioBus.Effects;
            [Range(0,1)] public float Volume=.65f; [Min(0)] public float Cooldown=.08f; [Range(1,16)] public int Concurrency=3;
            public GameObject Effect; [Range(.1f,10)] public float Lifetime=1;
        }
        [Serializable] public sealed class Model
        {
            [Tooltip("Stable content ID, not display name.")] public string Definition;
            public LifeStage Stage=LifeStage.Operational; [Min(1)] public int Level=1; public string Skin="";
            [Tooltip("Presentation-only prefab. No gameplay, AudioListener, Camera or ECS authoring components.")] public GameObject Prefab;
            public Vector3 Offset; public Vector3 Scale=Vector3.one;
        }
        [Serializable] public sealed class Portrait
        { public string Definition; [Tooltip("0 = definition/default portrait. Nonzero overrides this stable person ID.")] public ulong Person; public Sprite Image; }
        [Serializable] public sealed class Translation { public string Table,Key; [TextArea] public string Zh,En; }
        public AudioClip MenuMusic,DayMusic,NightMusic,CombatMusic,DayAmbient,NightAmbient;
        [Range(.1f,5)] public float FadeSeconds=.6f;
        public Cue[] Cues=Array.Empty<Cue>(); public Model[] Models=Array.Empty<Model>(); public Portrait[] Portraits=Array.Empty<Portrait>();
        public Sprite DefaultPortrait; public Translation[] Text=Array.Empty<Translation>();
        public Cue Find(PresentationCue id)=>Array.Find(Cues,c=>c!=null&&c.Id==id);
        public static bool PurePrefab(GameObject prefab)
        {
            if(prefab==null)return false;
            foreach(var component in prefab.GetComponentsInChildren<Component>(true))
                if(component==null||!(component is Transform||component is MeshFilter||component is Renderer||component is Animator||component is ParticleSystem||component is Light||component is LODGroup||component is PresentationActor))return false;
            return true;
        }
        public Model Select(string definition,LifeStage stage,int level,string skin)
        {
            Model best=null;int score=-1;foreach(var model in Models)
            {
                if(model==null||model.Prefab==null||model.Definition!=definition||model.Level>level||(model.Skin??"")!=(skin??""))continue;
                int value=model.Stage==stage?10000:model.Stage==LifeStage.Operational?0:-1;if(value<0)continue;value+=model.Level;
                if(value>score){score=value;best=model;}
            }return best;
        }
        public Sprite Face(string definition,ulong person)
        {var exact=Array.Find(Portraits,p=>p!=null&&p.Person==person&&person!=0&&p.Image!=null);if(exact!=null)return exact.Image;return Array.Find(Portraits,p=>p!=null&&p.Person==0&&p.Definition==definition&&p.Image!=null)?.Image??DefaultPortrait;}
    }
}
