using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Court/RoyalTrait")]
    public sealed class RoyalTraitDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("特性揭示年龄")]
        public int RevealAge;
        [LabelText("最低激活年龄")]
        public int MinimumActivationAge;
        [LabelText("可遗传")]
        public bool Heritable;
        [LabelText("遗传概率")]
        public float InheritanceChance;
        [LabelText("附带特性")]
        public RoyalTraitDefinitionAsset[] GrantedTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("冲突特性")]
        public RoyalTraitDefinitionAsset[] ConflictingTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("所需特性")]
        public RoyalTraitDefinitionAsset[] RequiredTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Prerequisites = new DefinitionPrerequisitesSource();
        [LabelText("属性效果")]
        public DefinitionEffectsSource Effects = new DefinitionEffectsSource();
    }
}
