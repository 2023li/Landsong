using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class ExpeditionRequirementSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("远征")]
        public ExpeditionDefinitionAsset Expedition;
        [LabelText("所需等级或次数")]
        public int Required = 1;
    }
}
