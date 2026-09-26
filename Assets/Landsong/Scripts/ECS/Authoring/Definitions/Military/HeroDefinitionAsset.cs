using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Military/Hero")]
    public sealed class HeroDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("战斗属性")]
        public UnitCombatStatsSource CombatStats = new UnitCombatStatsSource();
        [LabelText("威胁值")]
        public int ThreatValue;
        [LabelText("夜晚基础战力"), MinValue(0)]
        public int NightPower = 5;
        [LabelText("目标选择模式")]
        public byte TargetMode;
        [LabelText("占用人口")]
        public int PopulationCost = 1;
        [LabelText("默认唤醒金币")]
        public int FallbackWakeGold;
        [LabelText("复活冷却回合")]
        public int RevivalCooldownTurns = 1;
        [LabelText("成长配置")]
        public HeroGrowth Growth = HeroGrowth.Default;
        [LabelText("唤醒费用")]
        public LeveledItemAmountSource[] AwakeningCosts = Array.Empty<LeveledItemAmountSource>();
        [LabelText("供奉费用")]
        public LeveledItemAmountSource[] OfferingCosts = Array.Empty<LeveledItemAmountSource>();
        [LabelText("实体预制体"), Required]
        public GameObject Prefab;
    }
}
