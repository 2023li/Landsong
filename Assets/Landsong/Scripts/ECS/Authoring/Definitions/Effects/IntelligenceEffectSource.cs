using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class IntelligenceEffectSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("适用等级")]
        public int Level;
        [LabelText("所需科技")]
        public TechnologyDefinitionAsset RequiredTechnology;
        [LabelText("情报点数")]
        public int Points;
    }
}
