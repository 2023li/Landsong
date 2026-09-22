using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingConstructionCostSource
    {
        [LabelText("施工阶段（0 = 每期）")]
        [MinValue(0)]
        public int Stage = 1;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        [MinValue(0)]
        public int Quantity = 1;
    }
}
