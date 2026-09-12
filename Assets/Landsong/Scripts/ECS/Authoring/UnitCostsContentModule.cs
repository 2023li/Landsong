using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class UnitCostsContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("募兵费用"), ShowIf(nameof(Enabled))] public RecruitmentCost[] Recruitment = Array.Empty<RecruitmentCost>();
        [LabelText("唤醒费用"), ShowIf(nameof(Enabled))] public AwakeningCost[] Awakening = Array.Empty<AwakeningCost>();
        [LabelText("供奉费用"), ShowIf(nameof(Enabled))] public OfferingsCost[] Offerings = Array.Empty<OfferingsCost>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class RecruitmentCost : OrderedContentEntry
    {
        [LabelText("适用等级（0 = 通用）")] public int Level = 0;
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class AwakeningCost : OrderedContentEntry
    {
        [LabelText("适用等级（0 = 通用）")] public int Level = 0;
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class OfferingsCost : OrderedContentEntry
    {
        [LabelText("适用等级（0 = 通用）")] public int Level = 0;
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
}
