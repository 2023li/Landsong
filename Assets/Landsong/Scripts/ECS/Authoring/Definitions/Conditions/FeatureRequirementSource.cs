using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class FeatureRequirementSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("功能许可")]
        public FeatureDefinitionAsset Feature;
        [LabelText("所需等级或次数")]
        public int Required = 1;
    }
}
