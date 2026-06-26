using System;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public sealed class InventorySlot
    {
        [SerializeField] private string itemId;
        [SerializeField, Min(0)] private int quantity;

        public InventorySlot()
        {
            Clear();
        }

        public InventorySlot(string itemId, int quantity)
        {
            Set(itemId, quantity);
        }

        public string ItemId => itemId;
        public int Quantity => quantity;
        public bool IsEmpty => string.IsNullOrWhiteSpace(itemId) || quantity <= 0;

        public bool Contains(string targetItemId)
        {
            return !IsEmpty && string.Equals(itemId, targetItemId, StringComparison.Ordinal);
        }

        public bool CanStackWith(string targetItemId)
        {
            return IsEmpty || Contains(targetItemId);
        }

        public InventorySlotData ToData(int slotIndex)
        {
            return new InventorySlotData(slotIndex, itemId, quantity);
        }

        internal void Set(string newItemId, int newQuantity)
        {
            itemId = string.IsNullOrWhiteSpace(newItemId) ? string.Empty : newItemId.Trim();
            quantity = Mathf.Max(0, newQuantity);

            if (quantity <= 0)
            {
                Clear();
            }
        }

        internal int Add(int amount)
        {
            if (amount <= 0 || IsEmpty)
            {
                return 0;
            }

            quantity += amount;
            return amount;
        }

        internal int Remove(int amount)
        {
            if (amount <= 0 || IsEmpty)
            {
                return 0;
            }

            var removed = Mathf.Min(amount, quantity);
            quantity -= removed;

            if (quantity <= 0)
            {
                Clear();
            }

            return removed;
        }

        internal void Clear()
        {
            itemId = string.Empty;
            quantity = 0;
        }
    }
}
