using System;
using Landsong.InventorySystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [Serializable]
    [InlineProperty]
    public struct BuildingCost
    {
        [HorizontalGroup("Cost", Width = 0.72f)]
        [LabelText("物品")]
        [AssetsOnly]
        [SerializeField] private ItemDefinition itemDefinition;

        [HorizontalGroup("Cost")]
        [LabelText("数量")]
        [MinValue(0)]
        [SerializeField] private int amount;

        public BuildingCost(ItemDefinition itemDefinition, int amount)
        {
            this.itemDefinition = itemDefinition;
            this.amount = Mathf.Max(0, amount);
        }

        public ItemDefinition ItemDefinition => itemDefinition;
        public string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;
        public int Amount => amount;
        public bool IsValid => itemDefinition != null && !string.IsNullOrWhiteSpace(itemDefinition.ItemId) && amount > 0;

        public BuildingCost Normalized()
        {
            return new BuildingCost(itemDefinition, amount);
        }

        public override string ToString()
        {
            return itemDefinition == null ? $"None: {amount}" : $"{itemDefinition.DisplayName}: {amount}";
        }
    }
}
