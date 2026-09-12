using System;
using UnityEngine;
using UnityEngine.Serialization;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    public enum AudioBus {
        [LabelText("背景音乐")] Music,
        [LabelText("环境声音")] Ambient,
        [LabelText("世界音效")] Effects,
        [LabelText("界面音效")] UI
    }
    public enum PresentationCue {
        [LabelText("点击")] Click,
        [LabelText("返回")] Back,
        [LabelText("操作拒绝")] Denied,
        [LabelText("建造")] Build,
        [LabelText("完工")] Complete,
        [LabelText("修复")] Repair,
        [LabelText("采集")] Harvest,
        [LabelText("受击")] Hit,
        [LabelText("死亡")] Death,
        [LabelText("荒废")] Ruin,
        [LabelText("敲钟")] Bell,
        [LabelText("英雄唤醒")] HeroWake,
        [LabelText("访客到达")] Visitor,
        [LabelText("捕获")] Capture,
        [LabelText("获得掉落")] Loot,
        [LabelText("庆祝")] Celebration,
        [LabelText("预警")] Warning,
        [LabelText("战报")] Report
    }
    [CreateAssetMenu(menuName="Landsong/ECS/Presentation Catalog")]
    public sealed class GamePresentationCatalog : ScriptableObject
    {
        [Serializable] public sealed class Cue
        {
            [LabelText("提示类型")] public PresentationCue Id;
            [LabelText("音频片段")] public AudioClip Clip;
            [LabelText("音频通道")] public AudioBus Bus = AudioBus.Effects;
            [LabelText("音量"), Range(0, 1)] public float Volume = .65f;
            [LabelText("冷却秒数"), Min(0)] public float Cooldown = .08f;
            [LabelText("同时播放上限"), Range(1, 16)] public int Concurrency = 3;
            [LabelText("空间特效模板")] public PresentationEffect EffectPrefab;
            [SerializeField, HideInInspector, FormerlySerializedAs("Effect"), LabelText("旧特效迁移引用")] GameObject legacyEffect;
            [LabelText("特效持续秒数"), Range(.1f, 10)] public float Lifetime = 1;
#if UNITY_EDITOR
            public GameObject LegacyEffectForMigration => legacyEffect;
            public void CompleteEffectMigration(PresentationEffect value) { EffectPrefab = value; legacyEffect = null; }
#endif
        }
        [Serializable] public sealed class Model
        {
            [LabelText("内容稳定编号"), Tooltip("填写内容 ID，不使用显示名称。")] public string Definition;
            [LabelText("建筑阶段")] public LifeStage Stage = LifeStage.Operational;
            [LabelText("最低等级"), Min(1)] public int Level = 1;
            [LabelText("皮肤编号")] public string Skin = "";
            [LabelText("表现对象模板"), Required] public PresentationActor ActorPrefab;
            [SerializeField, HideInInspector, FormerlySerializedAs("Prefab"), LabelText("旧模型迁移引用")] GameObject legacyPrefab;
            [LabelText("位置偏移")] public Vector3 Offset;
            [LabelText("模型缩放")] public Vector3 Scale = Vector3.one;
#if UNITY_EDITOR
            public GameObject LegacyPrefabForMigration => legacyPrefab;
            public void CompleteModelMigration(PresentationActor value) { ActorPrefab = value; legacyPrefab = null; }
#endif
        }
        [Serializable] public sealed class Portrait
        {
            [LabelText("内容稳定编号")] public string Definition;
            [LabelText("人物稳定编号"), Tooltip("0 表示默认画像，其他值覆盖指定人物的画像。")] public ulong Person;
            [LabelText("画像图片")] public Sprite Image;
        }
        [Serializable] public sealed class Translation
        {
            [LabelText("语言表")] public string Table;
            [LabelText("文本键")] public string Key;
            [LabelText("中文内容"), TextArea] public string Zh;
            [LabelText("英文内容"), TextArea] public string En;
        }
        [LabelText("主菜单音乐")] public AudioClip MenuMusic;
        [LabelText("白天音乐")] public AudioClip DayMusic;
        [LabelText("夜晚音乐")] public AudioClip NightMusic;
        [LabelText("战斗音乐")] public AudioClip CombatMusic;
        [LabelText("白天环境音")] public AudioClip DayAmbient;
        [LabelText("夜晚环境音")] public AudioClip NightAmbient;
        [LabelText("淡入淡出秒数"), Range(.1f, 5)] public float FadeSeconds = .6f;
        [LabelText("提示音与特效")] public Cue[] Cues = Array.Empty<Cue>();
        [LabelText("世界模型")] public Model[] Models = Array.Empty<Model>();
        [LabelText("人物画像")] public Portrait[] Portraits = Array.Empty<Portrait>();
        [LabelText("默认画像")] public Sprite DefaultPortrait;
        [LabelText("语言条目")] public Translation[] Text = Array.Empty<Translation>();
        public Cue Find(PresentationCue id)=>Array.Find(Cues,c=>c!=null&&c.Id==id);
        public Model Select(string definition,LifeStage stage,int level,string skin)
        {
            Model best=null;int score=-1;foreach(var model in Models)
            {
                if(model==null||model.ActorPrefab==null||model.Definition!=definition||model.Level>level||(model.Skin??"")!=(skin??""))continue;
                int value=model.Stage==stage?10000:model.Stage==LifeStage.Operational?0:-1;if(value<0)continue;value+=model.Level;
                if(value>score){score=value;best=model;}
            }return best;
        }
        public Sprite Face(string definition,ulong person)
        {var exact=Array.Find(Portraits,p=>p!=null&&p.Person==person&&person!=0&&p.Image!=null);if(exact!=null)return exact.Image;return Array.Find(Portraits,p=>p!=null&&p.Person==0&&p.Definition==definition&&p.Image!=null)?.Image??DefaultPortrait;}
    }
}
