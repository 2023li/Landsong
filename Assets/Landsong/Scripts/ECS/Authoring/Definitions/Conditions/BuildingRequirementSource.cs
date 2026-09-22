using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BuildingRequirementSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("所需等级或次数")]
        public int Required = 1;
    }
}
