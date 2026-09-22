using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingGatheringLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("每次采集物品（空 = 最后一次发放奖励）")]
        public ItemDefinitionAsset Item;
        [LabelText("可采集次数")]
        [MinValue(0)]
        public int Uses;
        [LabelText("每次采集数量")]
        [MinValue(0)]
        public int AmountPerUse;
    }
}
