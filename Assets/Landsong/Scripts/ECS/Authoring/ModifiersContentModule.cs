using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class ModifiersContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("损耗倍率"), ShowIf(nameof(Enabled))] public InventoryLossModifier[] LossModifier = Array.Empty<InventoryLossModifier>();
        [LabelText("生产倍率加成"), ShowIf(nameof(Enabled))] public ProductionBonusModifier[] ProductionBonus = Array.Empty<ProductionBonusModifier>();
        [LabelText("攻击加成"), ShowIf(nameof(Enabled))] public AttackBonusModifier[] AttackBonus = Array.Empty<AttackBonusModifier>();
        [LabelText("生命加成"), ShowIf(nameof(Enabled))] public HealthBonusModifier[] HealthBonus = Array.Empty<HealthBonusModifier>();
        [LabelText("每回合民意"), ShowIf(nameof(Enabled))] public PublicOpinionModifier[] PublicOpinion = Array.Empty<PublicOpinionModifier>();
        [LabelText("士兵力量加成"), ShowIf(nameof(Enabled))] public SoldierAttackBonusModifier[] SoldierAttackBonus = Array.Empty<SoldierAttackBonusModifier>();
        [LabelText("士兵敏捷加成"), ShowIf(nameof(Enabled))] public SoldierSpeedBonusModifier[] SoldierSpeedBonus = Array.Empty<SoldierSpeedBonusModifier>();
        [LabelText("行动力加成"), ShowIf(nameof(Enabled))] public ActionPowerBonusModifier[] ActionPowerBonus = Array.Empty<ActionPowerBonusModifier>();
        [LabelText("作物收获加成"), ShowIf(nameof(Enabled))] public CropHarvestBonusModifier[] CropHarvestBonus = Array.Empty<CropHarvestBonusModifier>();
        [LabelText("阴谋风险加成"), ShowIf(nameof(Enabled))] public PlotRiskModifier[] PlotRisk = Array.Empty<PlotRiskModifier>();
        [LabelText("自然死亡风险加成"), ShowIf(nameof(Enabled))] public NaturalDeathRiskModifier[] NaturalDeathRisk = Array.Empty<NaturalDeathRiskModifier>();
        [LabelText("科研固定加成"), ShowIf(nameof(Enabled))] public ResearchOutputModifier[] ResearchOutput = Array.Empty<ResearchOutputModifier>();
        [LabelText("射程加成"), ShowIf(nameof(Enabled))] public RangeBonusModifier[] RangeBonus = Array.Empty<RangeBonusModifier>();
        [LabelText("攻速加成"), ShowIf(nameof(Enabled))] public AttackSpeedBonusModifier[] AttackSpeedBonus = Array.Empty<AttackSpeedBonusModifier>();
        [LabelText("移动速度加成"), ShowIf(nameof(Enabled))] public SpeedBonusModifier[] SpeedBonus = Array.Empty<SpeedBonusModifier>();
        [LabelText("护甲加成"), ShowIf(nameof(Enabled))] public ArmorBonusModifier[] ArmorBonus = Array.Empty<ArmorBonusModifier>();
        [LabelText("减伤加成"), ShowIf(nameof(Enabled))] public DamageReductionBonusModifier[] DamageReductionBonus = Array.Empty<DamageReductionBonusModifier>();
        [LabelText("穿甲加成"), ShowIf(nameof(Enabled))] public PenetrationBonusModifier[] PenetrationBonus = Array.Empty<PenetrationBonusModifier>();
        [LabelText("弹速加成"), ShowIf(nameof(Enabled))] public ProjectileSpeedBonusModifier[] ProjectileSpeedBonus = Array.Empty<ProjectileSpeedBonusModifier>();
        [LabelText("爆炸半径加成"), ShowIf(nameof(Enabled))] public BlastRadiusBonusModifier[] BlastRadiusBonus = Array.Empty<BlastRadiusBonusModifier>();
        [LabelText("固定物品产出加成"), ShowIf(nameof(Enabled))] public FlatProductionModifier[] FlatProduction = Array.Empty<FlatProductionModifier>();
        [LabelText("情报加成"), ShowIf(nameof(Enabled))] public PassiveIntelligence[] Intelligence = Array.Empty<PassiveIntelligence>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class InventoryLossModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ProductionBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class AttackBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class HealthBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PublicOpinionModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SoldierAttackBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SoldierSpeedBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ActionPowerBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class CropHarvestBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PlotRiskModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class NaturalDeathRiskModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ResearchOutputModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class RangeBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class AttackSpeedBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SpeedBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ArmorBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class DamageReductionBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PenetrationBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ProjectileSpeedBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class BlastRadiusBonusModifier : OrderedContentEntry
    {
        [LabelText("指定作用对象（空 = 全局）"), ContentReference(true)] public GameDefinitionAsset Subject;
        [LabelText("效果数值")] public float Magnitude;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class FlatProductionModifier : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("指定建筑（空 = 全部）"), ContentReference(true, ContentKind.Building)] public GameDefinitionAsset Building;
        [LabelText("额外数量")] public int Quantity;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class PassiveIntelligence : OrderedContentEntry
    {
        [LabelText("适用等级（0 = 通用）")] public int Level = 0;
        [LabelText("所需科技（可选）"), ContentReference(true, ContentKind.Technology)] public GameDefinitionAsset Technology;
        [LabelText("情报点数")] public int Points = 1;
    }
}
