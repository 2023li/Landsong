using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class FeatureRewardSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("功能许可")]
        public FeatureDefinitionAsset Feature;
        [LabelText("许可等级")]
        public int GrantedLevel = 1;
    }
}
