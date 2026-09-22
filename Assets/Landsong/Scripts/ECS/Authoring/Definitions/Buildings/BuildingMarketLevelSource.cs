using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingMarketLevelSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("收入货币")]
        public ItemDefinitionAsset Currency;
        [LabelText("每市场点所需流转价值")]
        [MinValue(0)]
        public int ValuePerMarketPoint;
        [LabelText("收入比例")]
        [Range(0, 1)]
        public float IncomeRatio;
    }
}
