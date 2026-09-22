using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [Serializable]
    public sealed class TalentJobEffectsSource
    {
        [LabelText("物品")]
        public TalentItemJobEffectSource[] Items = Array.Empty<TalentItemJobEffectSource>();
        [LabelText("士兵")]
        public TalentSoldierJobEffectSource[] Soldiers = Array.Empty<TalentSoldierJobEffectSource>();
        [LabelText("英雄")]
        public TalentHeroJobEffectSource[] Heroes = Array.Empty<TalentHeroJobEffectSource>();
        [LabelText("王国全局")]
        public TalentKingdomJobEffectSource[] Kingdom = Array.Empty<TalentKingdomJobEffectSource>();
    }
}
