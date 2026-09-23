using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class QuestTechnologyObjectiveSource : QuestObjectiveSource
    {
        [LabelText("科技")]
        public TechnologyDefinitionAsset Technology;
        [LabelText("数量")]
        public int Count;
    }
}
