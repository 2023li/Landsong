using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class BlueprintRewardSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("建筑")]
        public BuildingDefinitionAsset Building;
        [LabelText("许可等级")]
        public int GrantedLevel = 1;
    }
}
