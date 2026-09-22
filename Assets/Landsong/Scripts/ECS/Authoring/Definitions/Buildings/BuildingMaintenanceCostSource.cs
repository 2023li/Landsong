using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingMaintenanceCostSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        [MinValue(0)]
        public int Quantity = 1;
    }
}
