using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    [InlineProperty]
    public struct ItemAmount
    {
        [HorizontalGroup("Amount", Width = 0.72f)]
        [LabelText("物品")]
        [AssetsOnly]
        [SerializeField] private ItemDefinition itemDefinition;

        [HorizontalGroup("Amount")]
        [LabelText("数量")]
        [MinValue(0)]
        [SerializeField] private int amount;

        public ItemAmount(ItemDefinition itemDefinition, int amount)
        {
            this.itemDefinition = itemDefinition;
            this.amount = Mathf.Max(0, amount);
        }

        public ItemDefinition ItemDefinition => itemDefinition;
        public string ItemId => itemDefinition == null ? string.Empty : itemDefinition.ItemId;
        public int Amount => amount;
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && amount > 0;

        public ItemAmount Normalized()
        {
            return new ItemAmount(itemDefinition, amount);
        }

        public override string ToString()
        {
            return $"{ItemId}: {amount}";
        }
    }
}
