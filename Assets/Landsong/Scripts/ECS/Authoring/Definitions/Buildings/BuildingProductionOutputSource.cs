using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingProductionOutputSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        [MinValue(0)]
        public int Quantity = 1;
        [LabelText("最少工人")]
        [MinValue(0)]
        public int MinimumWorkers;
        [LabelText("最多工人（0 = 不限）")]
        [MinValue(0)]
        public int MaximumWorkers;
    }
}
