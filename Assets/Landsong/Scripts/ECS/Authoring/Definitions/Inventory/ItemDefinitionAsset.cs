using System;
using UnityEngine;
using Sirenix.OdinInspector;
using Landsong.ECS.Definitions;

namespace Landsong.ECS.Authoring.Definitions
{
    [CreateAssetMenu(menuName = "Landsong/Definitions/Inventory/Item")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [LabelText("基本信息")]
        public DefinitionMetadataSource Metadata = new DefinitionMetadataSource();
        [LabelText("主要物品组")]
        public ItemGroupDefinitionAsset PrimaryGroup;
        [LabelText("额外物品组")]
        public ItemGroupDefinitionAsset[] AdditionalGroups = Array.Empty<ItemGroupDefinitionAsset>();
        [LabelText("最大堆叠数量")]
        public int MaximumStack = 1;
        [LabelText("交易价值")]
        public int TradeValue;
        [LabelText("自然损耗率")]
        public float NaturalLossRate;
        [LabelText("被盗规则")]
        public TheftProfile Theft = TheftProfile.Default;
    }
}
