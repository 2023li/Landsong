using System;
using UnityEngine;
using Sirenix.OdinInspector;

namespace Landsong.ECS.Presentation
{
    [CreateAssetMenu(menuName = "Landsong/Presentation/Audio Catalog")]
    public sealed class AudioCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Cue
        {
            [LabelText("提示类型")]
            public PresentationCue Id;
            [LabelText("音频片段")]
            public AudioClip Clip;
            [LabelText("音频通道")]
            public AudioBus Bus = AudioBus.Effects;
            [LabelText("音量"), Range(0, 1)]
            public float Volume = .65f;
            [LabelText("冷却秒数"), Min(0)]
            public float Cooldown = .08f;
            [LabelText("同时播放上限"), Range(1, 16)]
            public int Concurrency = 3;
        }

        [LabelText("主菜单音乐")]
        public AudioClip MenuMusic;
        [LabelText("白天音乐")]
        public AudioClip DayMusic;
        [LabelText("夜晚音乐")]
        public AudioClip NightMusic;
        [LabelText("战斗音乐")]
        public AudioClip CombatMusic;
        [LabelText("白天环境音")]
        public AudioClip DayAmbient;
        [LabelText("夜晚环境音")]
        public AudioClip NightAmbient;
        [LabelText("淡入淡出秒数"), Range(.1f, 5)]
        public float FadeSeconds = .6f;
        [LabelText("提示音")]
        public Cue[] Cues = Array.Empty<Cue>();
        public Cue Find(PresentationCue id) => Array.Find(Cues, cue => cue != null && cue.Id == id);
    }
}
