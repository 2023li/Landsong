using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Military/Soldier")]
    public sealed class SoldierDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("战斗属性")]
        public UnitCombatStatsSource CombatStats = new UnitCombatStatsSource();
        [LabelText("威胁值")]
        public int ThreatValue;
        [LabelText("目标选择模式")]
        public byte TargetMode;
        [LabelText("占用人口")]
        public int PopulationCost = 1;
        [LabelText("默认募兵金币")]
        public int FallbackRecruitGold;
        [LabelText("成长配置")]
        public SoldierGrowth Growth = SoldierGrowth.Default;
        [LabelText("募兵费用")]
        public LeveledItemAmountSource[] RecruitmentCosts = Array.Empty<LeveledItemAmountSource>();
        [LabelText("实体预制体"), Required]
        public GameObject Prefab;
    }
}
