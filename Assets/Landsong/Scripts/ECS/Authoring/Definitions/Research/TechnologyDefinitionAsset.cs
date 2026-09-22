using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Research/Technology")]
    public sealed class TechnologyDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("研究点费用")]
        public int ResearchPointCost;
        [LabelText("可重复完成")]
        public bool Repeatable;
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Prerequisites = new DefinitionPrerequisitesSource();
        [LabelText("完成奖励")]
        public DefinitionRewardsSource Rewards = new DefinitionRewardsSource();
        [LabelText("属性效果")]
        public DefinitionEffectsSource Effects = new DefinitionEffectsSource();
        [LabelText("指定科技树位置")]
        public bool HasTreePosition;
        [LabelText("科技树位置"), ShowIf(nameof(HasTreePosition))]
        public Vector2 TreePosition;
    }
}
