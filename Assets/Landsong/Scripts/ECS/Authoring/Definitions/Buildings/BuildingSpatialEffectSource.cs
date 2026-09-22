using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingSpatialEffectSource
    {
        [LabelText("适用等级（0 = 全部）")]
        [MinValue(0)]
        public int Level = 1;
        [LabelText("指定建筑（空 = 所有建筑）")]
        public BuildingDefinitionAsset Building;
        [LabelText("效果数值")]
        [MinValue(0)]
        public int Magnitude;
        [LabelText("效果种类")]
        public BuildingEnvironmentKind Type;
        [LabelText("所需工人")]
        [MinValue(0)]
        public int RequiredWorkers;
        [LabelText("曼哈顿半径")]
        [MinValue(0)]
        public float Radius;
        [LabelText("叠加方式")]
        public BuildingEffectStacking Stacking;
        [LabelText("效果分组")]
        public string Group = "";
    }
}
