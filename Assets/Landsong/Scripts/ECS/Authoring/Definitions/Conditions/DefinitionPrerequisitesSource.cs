using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class DefinitionPrerequisitesSource
    {
        [LabelText("建筑条件")]
        public BuildingRequirementSource[] BuildingRequirements = Array.Empty<BuildingRequirementSource>();
        [LabelText("科技条件")]
        public TechnologyRequirementSource[] TechnologyRequirements = Array.Empty<TechnologyRequirementSource>();
        [LabelText("增益条件")]
        public BuffRequirementSource[] BuffRequirements = Array.Empty<BuffRequirementSource>();
        [LabelText("功能许可条件")]
        public FeatureRequirementSource[] FeatureRequirements = Array.Empty<FeatureRequirementSource>();
        [LabelText("任务条件")]
        public QuestRequirementSource[] QuestRequirements = Array.Empty<QuestRequirementSource>();
        [LabelText("远征条件")]
        public ExpeditionRequirementSource[] ExpeditionRequirements = Array.Empty<ExpeditionRequirementSource>();
    }
}
