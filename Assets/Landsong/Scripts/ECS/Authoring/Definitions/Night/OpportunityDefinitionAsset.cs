using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Night/Opportunity")]
    public sealed class OpportunityDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("访客配置")]
        public OpportunityProfile VisitorProfile = OpportunityProfile.Default;
        [LabelText("完成奖励")]
        public DefinitionRewardsSource Rewards = new DefinitionRewardsSource();
        [LabelText("实体预制体")]
        public GameObject Prefab;
    }
}
