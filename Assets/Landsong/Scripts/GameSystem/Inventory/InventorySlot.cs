using System;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public sealed class InventorySlot
    {
        [SerializeField] private InventorySlotProvision provision;
        [SerializeField] private string itemId;
        [SerializeField, Min(0)] private int quantity;

        public InventorySlot(InventorySlotProvision provision)
        {
            UpdateProvision(provision);
            Clear();
        }

        public InventorySlot(InventorySlotProvision provision, string itemId, int quantity)
        {
            UpdateProvision(provision);
            Set(itemId, quantity);
        }

        public InventorySlotProvision Provision => provision;
        public string StorageSlotId => provision == null ? string.Empty : provision.StorageSlotId;
        public string ProviderBuildingInstanceId =>
            provision == null ? string.Empty : provision.ProviderBuildingInstanceId;
        public string ProviderFamilyId => provision == null ? string.Empty : provision.ProviderFamilyId;
        public string ProviderDisplayName =>
            provision == null ? string.Empty : provision.ProviderDisplayName;
        public string LocalSlotId => provision == null ? string.Empty : provision.LocalSlotId;
        public InventorySlotType SlotType =>
            provision == null ? InventorySlotType.Default : provision.SlotType;
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

        public float GetEffectiveLossRate(
            ItemDefinition definition,
            InventorySlotTypeCatalog slotTypeCatalog)
        {
            return provision == null
                ? definition == null ? 0f : definition.LossRatePerTurn
                : provision.CalculateLossRate(definition, slotTypeCatalog);
        }

        public int GetAutoStorePriority(InventorySlotTypeCatalog slotTypeCatalog)
        {
            return provision == null
                ? 0
                : provision.GetAutoStorePriority(slotTypeCatalog);
        }

        public InventorySlotData ToData()
        {
            return new InventorySlotData(
                ProviderBuildingInstanceId,
                LocalSlotId,
                itemId,
                quantity);
        }

        internal void UpdateProvision(InventorySlotProvision newProvision)
        {
            if (newProvision == null || !newProvision.IsValid)
            {
                throw new ArgumentException("Inventory slots require a valid building slot provision.", nameof(newProvision));
            }

            provision = newProvision;
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
