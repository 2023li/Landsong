using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Court/Policy")]
    public sealed class PolicyDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("政策组")]
        public PolicyGroupDefinitionAsset PolicyGroup;
        [LabelText("政策层级")]
        public int PolicyTier;
        [LabelText("所需民意")]
        public int RequiredPublicOpinion;
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Prerequisites = new DefinitionPrerequisitesSource();
        [LabelText("属性效果")]
        public DefinitionEffectsSource Effects = new DefinitionEffectsSource();
    }
}
