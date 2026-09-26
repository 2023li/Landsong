using System;
using Landsong.ECS;
using Landsong.ECS.Authoring.Definitions;
using Sirenix.OdinInspector;
using UnityEngine;

#if UNITY_EDITOR
using System.Text;
using UnityEditor;
#endif

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

#if UNITY_EDITOR
        [Button("打印本夜波次", ButtonSizes.Large)]
        public void PrintWavePreview(int turn = 1, int playerPower = 1)
        {
            if (turn < 1 || playerPower < 0)
            {
                Debug.LogError("回合数至少为 1，玩家战斗力不能为负数。", this);
                return;
            }

            GameContentSetAsset content = null;
            foreach (var guid in AssetDatabase.FindAssets("t:GameContentSetAsset"))
            {
                var candidate = AssetDatabase.LoadAssetAtPath<GameContentSetAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (candidate?.NightEvents?.Nights == null || Array.IndexOf(candidate.NightEvents.Nights, this) < 0)
                    continue;
                content = candidate;
                break;
            }

            if (content == null)
            {
                Debug.LogError("未找到引用此夜晚定义的 GameContentSet，无法读取难度系数配置。", this);
                return;
            }

            var difficulty = NightTemplatePlanning.Difficulty(content.Night, turn, playerPower);
            var result = new StringBuilder();
            result.Append("[夜晚波次预览] ").Append(Id)
                .Append(" | 回合 ").Append(turn)
                .Append(" | 玩家战斗力 ").Append(playerPower)
                .Append(" | 难度系数 ").Append(difficulty.ToString("0.###"))
                .AppendLine();
            if (turn < MinTurn || MaxTurn > 0 && turn > MaxTurn || Interval > 0 && (turn - MinTurn) % Interval != 0)
                result.AppendLine("当前回合不满足此夜晚的出现条件；以下仍按输入战力预览模板。");

            int waveCount = 0;
            if (Waves != null)
                for (int i = 0; i < Waves.Length; i++)
                {
                    var wave = Waves[i];
                    if (wave?.Enemies == null) continue;
                    var enemies = new StringBuilder();
                    foreach (var row in wave.Enemies)
                    {
                        if (row?.Enemy == null) continue;
                        int count = NightTemplatePlanning.Count(row.Weight, difficulty, row.Fixed, row.FixedCount);
                        if (count <= 0) continue;
                        var name = string.IsNullOrWhiteSpace(row.Enemy.Metadata.Name) ? row.Enemy.name : row.Enemy.Metadata.Name;
                        enemies.Append("  ").Append(name).Append(" × ").Append(count).AppendLine();
                    }

                    if (enemies.Length == 0) continue;
                    waveCount++;
                    result.Append("第").Append(waveCount).Append("波 入夜")
                        .Append(wave.AtSeconds.ToString("0.##")).Append("秒");
                    if (wave.JitterSeconds > 0)
                        result.Append("（实际时间范围 ")
                            .Append((wave.AtSeconds - wave.JitterSeconds).ToString("0.##"))
                            .Append("～")
                            .Append((wave.AtSeconds + wave.JitterSeconds).ToString("0.##"))
                            .Append("秒）");
                    result.AppendLine().Append(enemies);
                }

            result.Insert(0, "本夜会生成 " + waveCount + " 波敌人\n");
            if (waveCount == 0)
                result.AppendLine("没有会生成的敌人。");
            result.Append("普通敌人数量按权重乘难度系数后向下取整；固定数量不缩放。实际出兵时间在规划夜晚时锁定。");
            Debug.Log(result.ToString(), this);
        }
#endif

    }
}
