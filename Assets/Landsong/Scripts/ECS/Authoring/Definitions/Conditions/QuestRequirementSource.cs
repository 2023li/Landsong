using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestRequirementSource
    {
        [LabelText("执行顺序")]
        public int Order;
        [LabelText("任务")]
        public QuestDefinitionAsset Quest;
        [LabelText("所需等级或次数")]
        public int Required = 1;
    }
}
