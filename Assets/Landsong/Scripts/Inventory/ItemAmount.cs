using System;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public struct ItemAmount
    {
        [SerializeField] private ItemDefinition itemDefinition;
        [SerializeField, Min(0)] private int amount;

        public ItemAmount(ItemDefinition itemDefinition, int amount)
        {
            this.itemDefinition = itemDefinition;
            this.amount = Mathf.Max(0, amount);
        }

        public string ItemId => itemDefinition.ItemId;
        public int Amount => amount;
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && amount > 0;

        public ItemAmount Normalized()
        {
            return new ItemAmount(itemDefinition, amount);
        }

        public override string ToString()
        {
            return $"{itemDefinition.ItemId}: {amount}";
        }
    }
}
