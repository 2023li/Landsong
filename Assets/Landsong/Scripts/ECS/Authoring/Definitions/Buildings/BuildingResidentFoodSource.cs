using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingResidentFoodSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("食物组")]
        public ItemGroupDefinitionAsset FoodGroup;
        [LabelText("所需品种")]
        [MinValue(0)]
        public int Varieties;
        [LabelText("每人每品种消耗")]
        [MinValue(0)]
        public int AmountPerResident;
    }
}
