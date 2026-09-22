using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Inventory/StorageSlot")]
    public sealed class StorageSlotDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("默认损耗倍率")]
        public float DefaultLossMultiplier;
        [LabelText("允许收纳物品")]
        public ItemDefinitionAsset[] AcceptedItems = Array.Empty<ItemDefinitionAsset>();
        [LabelText("允许收纳物品组")]
        public ItemGroupDefinitionAsset[] AcceptedGroups = Array.Empty<ItemGroupDefinitionAsset>();
        [LabelText("物品损耗倍率")]
        public ItemLossOverrideSource[] ItemLosses = Array.Empty<ItemLossOverrideSource>();
        [LabelText("物品组损耗倍率")]
        public ItemGroupLossOverrideSource[] GroupLosses = Array.Empty<ItemGroupLossOverrideSource>();
    }
}
