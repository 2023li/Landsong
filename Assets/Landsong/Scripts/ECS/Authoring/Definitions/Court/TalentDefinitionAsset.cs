using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Court/Talent")]
    public sealed class TalentDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("初始等级")]
        public int InitialLevel = 1;
        [LabelText("最高等级")]
        public int MaximumLevel = 1;
        [LabelText("首级升级经验")]
        public int BaseLevelExperience = 1;
        [LabelText("每级经验增长")]
        public int LevelExperienceIncrement;
        [LabelText("专业类别")]
        public int Specialty;
        [LabelText("工资")]
        public TalentWageSource Wage = new TalentWageSource();
        [LabelText("初始特性")]
        public RoyalTraitDefinitionAsset[] InitialTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("冲突特性")]
        public RoyalTraitDefinitionAsset[] ConflictingTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("所需特性")]
        public RoyalTraitDefinitionAsset[] RequiredTraits = Array.Empty<RoyalTraitDefinitionAsset>();
        [LabelText("完成前置")]
        public DefinitionPrerequisitesSource Prerequisites = new DefinitionPrerequisitesSource();
        [LabelText("人物委托")]
        public TalentSocialTaskSource[] SocialTasks = Array.Empty<TalentSocialTaskSource>();
        [LabelText("每回合收益")]
        public TalentPeriodicIncomeSource PeriodicIncome = new TalentPeriodicIncomeSource();
        [LabelText("任职效果")]
        public TalentJobEffectsSource JobEffects = new TalentJobEffectsSource();
        [LabelText("属性效果")]
        public DefinitionEffectsSource Effects = new DefinitionEffectsSource();
    }
}
