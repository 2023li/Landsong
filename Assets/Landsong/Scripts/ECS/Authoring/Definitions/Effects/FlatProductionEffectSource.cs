using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class FlatProductionEffectSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("适用等级")]
        public int Level;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("数量")]
        public int Quantity;
    }
}
