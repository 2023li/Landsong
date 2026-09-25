using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Buildings/Building")]
    public sealed class BuildingDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("建筑阵营")]
        public BuildingFaction Faction = BuildingFaction.Settlement;
        [LabelText("数量限制分组")]
        public BuildingLimitGroupDefinitionAsset LimitGroup;
        [LabelText("最高等级")]
        public int MaximumLevel = 1;
        [LabelText("施工回合")]
        public int ConstructionTurns = 1;
        [LabelText("资源连接行动力")]
        public int ResourceConnectionActionPower;
        [LabelText("数量上限")]
        public int MaximumCount;
        [LabelText("占地尺寸")]
        public Vector2Int Footprint = new Vector2Int(1, 1);
        [LabelText("耐久上限")]
        public float MaximumDurability = 100;
        [LabelText("通行消耗")]
        public float MovementCost = 2;
        [LabelText("防御属性")]
        public UnitCombatStatsSource DefenseStats = new UnitCombatStatsSource();
        [LabelText("占位与外观")]
        public BuildingPlacementAndVisualsSource PlacementAndVisuals = new BuildingPlacementAndVisualsSource();
        [LabelText("建筑功能")]
        public BuildingCapabilitiesSource Capabilities = new BuildingCapabilitiesSource();
        [LabelText("实体预制体"), Required]
        public GameObject Prefab;
    }
}
