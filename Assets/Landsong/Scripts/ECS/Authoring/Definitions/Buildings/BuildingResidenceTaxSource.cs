using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingResidenceTaxSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("物品")]
        public ItemDefinitionAsset Item;
        [LabelText("每居民税收")]
        [MinValue(0)]
        public int PerResident;
        [LabelText("税收间隔")]
        [MinValue(0)]
        public int Interval = 1;
    }
}
