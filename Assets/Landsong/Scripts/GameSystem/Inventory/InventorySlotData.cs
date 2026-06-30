using System;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public struct InventorySlotData
    {
        [SerializeField, Min(0)] private int slotIndex;
        [SerializeField] private string itemId;
        [SerializeField, Min(0)] private int quantity;

        public InventorySlotData(int slotIndex, string itemId, int quantity)
        {
            this.slotIndex = Mathf.Max(0, slotIndex);
            this.itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            this.quantity = Mathf.Max(0, quantity);
        }

        public int SlotIndex => slotIndex;
        public string ItemId => itemId;
        public int Quantity => quantity;
        public bool IsValid => slotIndex >= 0 && !string.IsNullOrWhiteSpace(itemId) && quantity > 0;

        public InventorySlotData Normalized()
        {
            return new InventorySlotData(slotIndex, itemId, quantity);
        }
    }
}
