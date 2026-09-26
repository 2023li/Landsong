using System;
using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;
using Landsong.ECS.Authoring.Definitions;

namespace Landsong.ECS.Authoring
{
    [Serializable]
    public abstract class NightWaveGeneratorSource
    {
        public abstract NightWaveGeneratorSettings Compile();
    }

    [Serializable]
    public sealed class BudgetNightWaveGeneratorSource : NightWaveGeneratorSource
    {
        [LabelText("数量随机倍率下限"), MinValue(0.01)]
        public float MinimumCountScale = .65f;
        [LabelText("数量随机倍率上限"), MinValue(0.01)]
        public float MaximumCountScale = 1.35f;

        public override NightWaveGeneratorSettings Compile() => new NightWaveGeneratorSettings
        {
            Kind = NightWaveGeneratorKind.Budget,
            MinimumCountScale = MinimumCountScale,
            MaximumCountScale = MaximumCountScale
        };
    }

    [Serializable]
    public sealed class FixedCountNightWaveGeneratorSource : NightWaveGeneratorSource
    {
        [LabelText("每波普通敌人数量"), Range(1, 256)]
        public int Count = 3;

        public override NightWaveGeneratorSettings Compile() => new NightWaveGeneratorSettings
        {
            Kind = NightWaveGeneratorKind.FixedCount,
            FixedCount = Count
        };
    }

    [Serializable]
    public sealed class NightEnemySource
    {
        [LabelText("敌军定义"), Required]
        public EnemyDefinitionAsset Enemy;
        [LabelText("抽取权重"), MinValue(0.001)]
        public float Weight = 1;
    }

    [Serializable]
    public sealed class NightBuildingConditionSource
    {
        [LabelText("建筑"), Required]
        public BuildingDefinitionAsset Building;
        [LabelText("运营建筑数量"), MinValue(0)]
        public int Count = 1;
        [LabelText("最低等级"), MinValue(0)]
        public int MinimumLevel;
    }

    [Serializable]
    public sealed class NightItemConditionSource
    {
        [LabelText("物品"), Required]
        public ItemDefinitionAsset Item;
        [LabelText("数量"), MinValue(0)]
        public int Quantity = 1;
    }

    [Serializable]
    public sealed class NightTechnologyConditionSource
    {
        [LabelText("科技"), Required]
        public TechnologyDefinitionAsset Technology;
        [LabelText("研究完成次数"), MinValue(0)]
        public int Count = 1;
    }

    [Serializable]
    public sealed class NightEventConditionsSource
    {
        [LabelText("最低回合"), MinValue(0)]
        public int MinimumTurn;
        [LabelText("运营建筑条件")]
        public NightBuildingConditionSource[] Buildings = Array.Empty<NightBuildingConditionSource>();
        [LabelText("物品条件")]
        public NightItemConditionSource[] Items = Array.Empty<NightItemConditionSource>();
        [LabelText("科技条件")]
        public NightTechnologyConditionSource[] Technologies = Array.Empty<NightTechnologyConditionSource>();
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Completions = new DefinitionPrerequisitesSource();
    }

    [Serializable]
    public sealed class NightEventSource
    {
        [LabelText("事件标识"), Required]
        public string Id;
        [LabelText("后续事件标识")]
        public string FollowUp;
        [LabelText("夜晚类型")]
        public NightKind Kind;
        [LabelText("优先级")]
        public int Priority;
        [LabelText("最早回合"), MinValue(1)]
        public int MinTurn = 1;
        [LabelText("最晚回合（0 = 不限）"), MinValue(0)]
        public int MaxTurn;
        [LabelText("间隔回合"), MinValue(0)]
        public int Interval;
        [LabelText("冷却回合"), MinValue(0)]
        public int Cooldown;
        [LabelText("波次数量"), Range(1, 32)]
        public int WaveCount = 3;
        [LabelText("返回延迟回合"), MinValue(1)]
        public int ReturnDelay = 5;
        [LabelText("事件抽取权重"), MinValue(0.001)]
        public float Weight = 1;
        [LabelText("预算倍率"), MinValue(0.001)]
        public float BudgetScale = 1;
        [LabelText("仅触发一次")]
        public bool Once;
        [LabelText("仅作为返回事件")]
        public bool ReturnOnly;
        [LabelText("强制事件")]
        public bool Forced;
        [LabelText("波次预警时间比例（0~1）")]
        public float[] WaveTimes = Array.Empty<float>();
        [LabelText("敌军抽取池")]
        public NightEnemySource[] Enemies = Array.Empty<NightEnemySource>();
        [LabelText("触发条件")]
        public NightEventConditionsSource Conditions = new NightEventConditionsSource();
    }

    [CreateAssetMenu(menuName = "Landsong/ECS/Night/Event Catalog")]
    public sealed class NightEventCatalogAsset : ScriptableObject
    {
        [LabelText("夜晚定义")]
        public NightDefinitionAsset[] Nights = Array.Empty<NightDefinitionAsset>();
        [HideInInspector]
        [SerializeReference, LabelText("波次生成器")]
        public NightWaveGeneratorSource WaveGenerator = new BudgetNightWaveGeneratorSource();
        [HideInInspector]
        public NightEventSource[] Events = Array.Empty<NightEventSource>();
    }
}
