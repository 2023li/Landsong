using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class CropsContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("种植费用"), ShowIf(nameof(Enabled))] public SeedsCost[] Seeds = Array.Empty<SeedsCost>();
        [LabelText("自动收获费用"), ShowIf(nameof(Enabled))] public AutomaticHarvestCost[] AutomaticHarvest = Array.Empty<AutomaticHarvestCost>();
        [LabelText("收获产物"), ShowIf(nameof(Enabled))] public CropYield[] Yields = Array.Empty<CropYield>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class SeedsCost : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class AutomaticHarvestCost : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("数量")] public int Quantity = 1;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class CropYield : OrderedContentEntry
    {
        [LabelText("物品"), ContentReference(false, ContentKind.Item)] public GameDefinitionAsset Item;
        [LabelText("最少数量")] public int MinimumQuantity = 1;
        [LabelText("最多数量（0 = 固定最少数量）")] public int MaximumQuantity;
    }
}
