using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Military/Enemy")]
    public sealed class EnemyDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("战斗属性")]
        public UnitCombatStatsSource CombatStats = new UnitCombatStatsSource();
        [LabelText("威胁值")]
        public int ThreatValue;
        [LabelText("行为标记")]
        public EnemyBehaviorFlags Behavior;
        [LabelText("优先目标建筑分类")]
        public BuildingCategory PreferredTargetCategory;
        [LabelText("击杀奖励")]
        public DefinitionRewardsSource KillRewards = new DefinitionRewardsSource();
        [LabelText("特殊掉落")]
        public SpecialItemDropSource[] SpecialDrops = Array.Empty<SpecialItemDropSource>();
        [LabelText("实体预制体"), Required]
        public GameObject Prefab;
    }
}
