using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingRareProductionSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("数量")]
        [MinValue(0)]
        public int Quantity = 1;
        [LabelText("所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
        [LabelText("概率")]
        [Range(0, 1)]
        public float Probability;
    }
}
