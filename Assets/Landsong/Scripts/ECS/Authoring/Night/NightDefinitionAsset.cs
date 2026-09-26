using System;
using Landsong.ECS.Authoring.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.ECS.Authoring
{
    [Flags]
    public enum NightWeatherMask
    {
        [LabelText("晴天")] Sunny = 1 << 0,
        [LabelText("下雨")] Rain = 1 << 1,
        [LabelText("下雪")] Snow = 1 << 2,
        [LabelText("小雨")] LightRain = 1 << 3,
        [LabelText("大雨")] HeavyRain = 1 << 4,
        [LabelText("所有天气")] Any = Sunny | Rain | Snow | LightRain | HeavyRain,
        [LabelText("所有雨天")] AnyRain = Rain | LightRain | HeavyRain
    }

    [Serializable]
    public sealed class NightWaveEnemySource
    {
        [Required, LabelText("敌人")]
        public EnemyDefinitionAsset Enemy;
        [LabelText("固定数量")]
        public bool Fixed;
        [LabelText("数量权重"), MinValue(0)]
        public float Weight = 1;
        [LabelText("固定只数"), MinValue(1)]
        public int FixedCount = 1;
    }

    [Serializable]
    public sealed class NightWaveTemplateSource
    {
        [LabelText("实际出兵时间（入夜后秒）"), MinValue(0)]
        public float AtSeconds;
        [LabelText("时间波动（正负秒，入夜时锁定）"), MinValue(0)]
        public float JitterSeconds;
        [LabelText("敌人组成")]
        public NightWaveEnemySource[] Enemies = Array.Empty<NightWaveEnemySource>();
    }

    [CreateAssetMenu(menuName = "Landsong/ECS/Night/Night Definition")]
    public sealed class NightDefinitionAsset : ScriptableObject
    {
        [Required, LabelText("稳定标识")]
        public string Id;
        [LabelText("夜晚类型")]
        public NightKind Kind;
        [LabelText("最早回合"), MinValue(1)]
        public int MinTurn = 1;
        [LabelText("最晚回合（0 = 不限）"), MinValue(0)]
        public int MaxTurn;
        [LabelText("出现间隔（0 = 不限）"), MinValue(0)]
        public int Interval;
        [LabelText("冷却回合"), MinValue(0)]
        public int Cooldown;
        [LabelText("抽取权重"), MinValue(.001)]
        public float Weight = 1;
        [LabelText("必定出现（符合条件时）")]
        public bool Guaranteed;
        [LabelText("必出优先级")]
        public int Priority;
        [LabelText("仅出现一次")]
        public bool Once;
        [LabelText("允许天气")]
        public NightWeatherMask AllowedWeather = NightWeatherMask.Any;
        [LabelText("其他出现条件")]
        public NightEventConditionsSource Conditions = new NightEventConditionsSource();
        [LabelText("开场字幕"), TextArea]
        public string OpeningCaption;
        [LabelText("特殊登场字幕"), TextArea]
        public string SpecialCaption;
        [LabelText("胜利字幕"), TextArea]
        public string VictoryCaption;
        [LabelText("波次模板")]
        public NightWaveTemplateSource[] Waves = Array.Empty<NightWaveTemplateSource>();
    }
}
