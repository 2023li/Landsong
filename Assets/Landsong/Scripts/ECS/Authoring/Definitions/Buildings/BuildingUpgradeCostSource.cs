using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingUpgradeCostSource
    {
        [LabelText("升级目标等级（0 = 所有升级）")]
        [MinValue(0)]
        public int TargetLevel = 1;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        [MinValue(0)]
        public int Quantity = 1;
    }
}
