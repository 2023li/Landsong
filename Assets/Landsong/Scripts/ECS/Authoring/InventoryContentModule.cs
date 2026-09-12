using System;
using Sirenix.OdinInspector;
namespace Landsong.ECS.Authoring
{
    [Serializable, HideReferenceObjectPicker] public sealed class InventoryContentModule
    {
        [LabelText("启用模块")] public bool Enabled;
        [LabelText("额外所属物品组"), ShowIf(nameof(Enabled))] public ItemMembership[] Groups = Array.Empty<ItemMembership>();
        [LabelText("允许收纳"), ShowIf(nameof(Enabled))] public StorageAcceptance[] Accepted = Array.Empty<StorageAcceptance>();
        [LabelText("指定物品损耗倍率"), ShowIf(nameof(Enabled))] public StorageLoss[] Losses = Array.Empty<StorageLoss>();
    }
    [Serializable, HideReferenceObjectPicker] public sealed class ItemMembership : OrderedContentEntry
    {
        [LabelText("物品组"), ContentReference(false, ContentKind.ItemGroup)] public GameDefinitionAsset Group;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class StorageAcceptance : OrderedContentEntry
    {
        [LabelText("物品或物品组"), ContentReference(false, ContentKind.Item, ContentKind.ItemGroup)] public GameDefinitionAsset Content;
    }
    [Serializable, HideReferenceObjectPicker] public sealed class StorageLoss : OrderedContentEntry
    {
        [LabelText("物品或物品组"), ContentReference(false, ContentKind.Item, ContentKind.ItemGroup)] public GameDefinitionAsset Content;
        [LabelText("损耗倍率")] public float Multiplier = 1f;
    }
}
