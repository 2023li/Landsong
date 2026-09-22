using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Combat/Loot")]
    public sealed class LootDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("完成奖励")]
        public DefinitionRewardsSource Rewards = new DefinitionRewardsSource();
        [LabelText("实体预制体")]
        public GameObject Prefab;
    }
}
