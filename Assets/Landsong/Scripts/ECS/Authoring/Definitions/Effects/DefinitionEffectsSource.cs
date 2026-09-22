using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class DefinitionEffectsSource
    {
        [LabelText("物品")]
        public ItemNumericEffectSource[] Items = Array.Empty<ItemNumericEffectSource>();
        [LabelText("建筑")]
        public BuildingNumericEffectSource[] Buildings = Array.Empty<BuildingNumericEffectSource>();
        [LabelText("士兵")]
        public SoldierNumericEffectSource[] Soldiers = Array.Empty<SoldierNumericEffectSource>();
        [LabelText("英雄")]
        public HeroNumericEffectSource[] Heroes = Array.Empty<HeroNumericEffectSource>();
        [LabelText("人才")]
        public TalentNumericEffectSource[] Talents = Array.Empty<TalentNumericEffectSource>();
        [LabelText("王国全局")]
        public KingdomNumericEffectSource[] Kingdom = Array.Empty<KingdomNumericEffectSource>();
        [LabelText("情报")]
        public IntelligenceEffectSource[] Intelligence = Array.Empty<IntelligenceEffectSource>();
        [LabelText("固定产出")]
        public FlatProductionEffectSource[] FlatProduction = Array.Empty<FlatProductionEffectSource>();
    }
}
