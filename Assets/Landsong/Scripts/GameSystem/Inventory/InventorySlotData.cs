using System;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public struct InventorySlotData
    {
        [SerializeField] private string providerBuildingInstanceId;
        [SerializeField] private string localSlotId;
        [SerializeField] private string itemId;
        [SerializeField, Min(0)] private int quantity;

        public InventorySlotData(
            string providerBuildingInstanceId,
            string localSlotId,
            string itemId,
            int quantity)
        {
            this.providerBuildingInstanceId = Normalize(providerBuildingInstanceId);
            this.localSlotId = Normalize(localSlotId);
            this.itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            this.quantity = Mathf.Max(0, quantity);
        }

        public string ProviderBuildingInstanceId => providerBuildingInstanceId;
        public string LocalSlotId => localSlotId;
        public string StorageSlotId =>
            InventorySlotProvision.BuildStorageSlotId(providerBuildingInstanceId, localSlotId);
        public string ItemId => itemId;
        public int Quantity => quantity;
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(StorageSlotId)
            && !string.IsNullOrWhiteSpace(itemId)
            && quantity > 0;

        public InventorySlotData Normalized()
        {
            return new InventorySlotData(
                providerBuildingInstanceId,
                localSlotId,
                itemId,
                quantity);
        }

        private static string Normalize(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }
}
