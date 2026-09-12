using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class PeopleContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("人才工资"), ShowIf(nameof(Enabled))] public TalentWage[] Wages = Array.Empty<TalentWage>();
        [LabelText("初始特性"), ShowIf(nameof(Enabled))] public TraitsTraitReference[] Traits = Array.Empty<TraitsTraitReference>();
        [LabelText("冲突特性"), ShowIf(nameof(Enabled))] public ConflictsTraitReference[] Conflicts = Array.Empty<ConflictsTraitReference>();
        [LabelText("依赖特性"), ShowIf(nameof(Enabled))] public DependenciesTraitReference[] Dependencies = Array.Empty<DependenciesTraitReference>();
        [LabelText("人物委托"), ShowIf(nameof(Enabled))] public SocialTaskConfiguration[] SocialTasks = Array.Empty<SocialTaskConfiguration>();
        [LabelText("人才每回合产物"), ShowIf(nameof(Enabled))] public TalentItemIncome[] PeriodicItems = Array.Empty<TalentItemIncome>();
        [LabelText("人才任职效果"), ShowIf(nameof(Enabled))] public TalentJobEffect[] Effects = Array.Empty<TalentJobEffect>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class TalentWage : OrderedContentEntry
    {
        [LabelText("基础工资")] public int BaseAmount;
        [LabelText("每级增加工资")] public int PerLevel;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class TraitsTraitReference : OrderedContentEntry
    {
        [LabelText("王室特性"), ContentReference(false, ContentKind.RoyalTrait)] public GameDefinitionAsset Trait;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ConflictsTraitReference : OrderedContentEntry
    {
        [LabelText("王室特性"), ContentReference(false, ContentKind.RoyalTrait)] public GameDefinitionAsset Trait;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class DependenciesTraitReference : OrderedContentEntry
    {
        [LabelText("王室特性"), ContentReference(false, ContentKind.RoyalTrait)] public GameDefinitionAsset Trait;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SocialTaskConfiguration : OrderedContentEntry
    {
        [LabelText("提交物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
        [LabelText("好感奖励")] public int AffectionReward = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class TalentItemIncome : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("基础数量")] public int BaseQuantity = 1;
        [LabelText("每级增加数量")] public float PerLevel;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class TalentJobEffect : OrderedContentEntry
    {
        [LabelText("指定作用对象"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果")] public TalentEffectType Effect;
        [LabelText("生效时机")] public TalentEffectTiming Timing;
        [LabelText("数值缩放依据")] public TalentEffectScaling Scaling;
        [LabelText("基础效果")] public float BaseValue;
        [LabelText("每级增长")] public float PerLevel;
    }
}
