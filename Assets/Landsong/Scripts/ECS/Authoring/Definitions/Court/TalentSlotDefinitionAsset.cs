using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Court/TalentSlot")]
    public sealed class TalentSlotDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("允许专业类别")]
        public int AcceptedSpecialty;
        [LabelText("所需特性")]
        public RoyalTraitDefinitionAsset[] RequiredTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("任职效果")]
        public TalentJobEffectsSource JobEffects = new TalentJobEffectsSource();
        [LabelText("属性效果")]
        public DefinitionEffectsSource Effects = new DefinitionEffectsSource();
    }
}
