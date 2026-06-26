using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [DisallowMultipleComponent]
    public sealed class InventoryBehaviour : MonoBehaviour
    {
        [SerializeField] private ItemCatalog itemCatalog;
        [SerializeField, Min(0)] private int slotCount = 24;
        [SerializeField] private bool initializeOnAwake = true;
        [SerializeField] private ItemAmount[] startingItems = Array.Empty<ItemAmount>();

        private Inventory inventory;

        public event Action<InventoryBehaviour> InventoryChanged;

        public ItemCatalog ItemCatalog => itemCatalog;
        public int SlotCount => slotCount;
        public Inventory Inventory
        {
            get
            {
                EnsureInitialized();
                return inventory;
            }
        }

        public bool IsInitialized => inventory != null;

        private void Awake()
        {
            if (initializeOnAwake)
            {
                Initialize();
            }
        }

        private void OnValidate()
        {
            slotCount = Mathf.Max(0, slotCount);

            if (startingItems == null)
            {
                startingItems = Array.Empty<ItemAmount>();
                return;
            }

            for (var i = 0; i < startingItems.Length; i++)
            {
                startingItems[i] = startingItems[i].Normalized();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeInventory();
        }

        public void Initialize()
        {
            UnsubscribeInventory();

            inventory = new Inventory(slotCount, itemCatalog);
            ApplyStartingItems(inventory);
            inventory.InventoryChanged += HandleInventoryChanged;
            HandleInventoryChanged();
        }

        public void SetCatalog(ItemCatalog newCatalog)
        {
            itemCatalog = newCatalog;

            if (inventory != null)
            {
                inventory.SetCatalog(itemCatalog);
            }
        }

        public void SetSlotCount(int newSlotCount)
        {
            slotCount = Mathf.Max(0, newSlotCount);
            Inventory.Resize(slotCount);
        }

        public int AddItem(ItemDefinition definition, int quantity)
        {
            return Inventory.Add(definition, quantity);
        }

        public int AddItem(string itemId, int quantity)
        {
            return Inventory.Add(itemId, quantity);
        }

        public bool CanAddItem(ItemDefinition definition, int quantity)
        {
            return Inventory.CanAdd(definition, quantity);
        }

        public bool CanAddItem(string itemId, int quantity)
        {
            return Inventory.CanAdd(itemId, quantity);
        }

        public bool TryAddItem(ItemDefinition definition, int quantity)
        {
            return Inventory.TryAdd(definition, quantity);
        }

        public bool TryAddItem(string itemId, int quantity)
        {
            return Inventory.TryAdd(itemId, quantity);
        }

        public int RemoveItem(string itemId, int quantity)
        {
            return Inventory.Remove(itemId, quantity);
        }

        public bool TryRemoveItem(string itemId, int quantity)
        {
            return Inventory.TryRemove(itemId, quantity);
        }

        public bool HasItem(string itemId, int quantity = 1)
        {
            return Inventory.HasItem(itemId, quantity);
        }

        public bool HasItems(IEnumerable<ItemAmount> requirements)
        {
            return Inventory.HasItems(requirements);
        }

        public bool TryRemoveItems(IEnumerable<ItemAmount> requirements)
        {
            return Inventory.TryRemoveItems(requirements);
        }

        public int GetQuantity(string itemId)
        {
            return Inventory.GetQuantity(itemId);
        }

        public bool Move(int fromSlotIndex, int toSlotIndex, int quantity = int.MaxValue)
        {
            return Inventory.Move(fromSlotIndex, toSlotIndex, quantity);
        }

        public bool SplitStack(int fromSlotIndex, int toSlotIndex, int quantity)
        {
            return Inventory.SplitStack(fromSlotIndex, toSlotIndex, quantity);
        }

        public bool Swap(int firstSlotIndex, int secondSlotIndex)
        {
            return Inventory.Swap(firstSlotIndex, secondSlotIndex);
        }

        public bool ClearSlot(int slotIndex)
        {
            return Inventory.ClearSlot(slotIndex);
        }

        public void Clear()
        {
            Inventory.Clear();
        }

        public InventorySaveData CaptureSaveData()
        {
            return Inventory.CaptureSaveData();
        }

        public void RestoreSaveData(InventorySaveData saveData, bool resizeToSaveSlotCount = true)
        {
            Inventory.RestoreSaveData(saveData, resizeToSaveSlotCount);
            slotCount = Inventory.SlotCount;
        }

        [Button]
        public void LogInventoryContents()
        {
            var totalsByItemId = BuildInventoryTotals();
            if (totalsByItemId.Count == 0)
            {
                Debug.Log($"Inventory '{name}' is empty.", this);
                return;
            }

            var lines = new List<string>();
            foreach (var itemTotal in totalsByItemId)
            {
                var label = itemTotal.Key;
                if (itemCatalog != null && itemCatalog.TryGetDefinition(itemTotal.Key, out var definition))
                {
                    label = definition.DisplayName;
                }

                lines.Add($"{label} ({itemTotal.Key}): {itemTotal.Value}");
            }

            Debug.Log($"Inventory '{name}' contents:\n{string.Join("\n", lines)}", this);
        }

        private void ApplyStartingItems(Inventory targetInventory)
        {
            if (startingItems == null)
            {
                return;
            }

            foreach (var item in startingItems)
            {
                var normalized = item.Normalized();
                if (!normalized.IsValid)
                {
                    continue;
                }

                var added = targetInventory.Add(normalized.ItemId, normalized.Amount);
                if (added < normalized.Amount)
                {
                    Debug.LogWarning($"Inventory '{name}' could not fit all starting item '{normalized.ItemId}'. Requested {normalized.Amount}, added {added}.", this);
                }
            }
        }

        private void EnsureInitialized()
        {
            if (inventory == null)
            {
                Initialize();
            }
        }

        private void UnsubscribeInventory()
        {
            if (inventory != null)
            {
                inventory.InventoryChanged -= HandleInventoryChanged;
            }
        }

        private void HandleInventoryChanged()
        {
            InventoryChanged?.Invoke(this);
        }

        private Dictionary<string, int> BuildInventoryTotals()
        {
            var totalsByItemId = new Dictionary<string, int>(StringComparer.Ordinal);
            var currentInventory = Inventory;

            foreach (var slot in currentInventory.Slots)
            {
                if (slot.IsEmpty)
                {
                    continue;
                }

                if (!totalsByItemId.ContainsKey(slot.ItemId))
                {
                    totalsByItemId.Add(slot.ItemId, 0);
                }

                totalsByItemId[slot.ItemId] += slot.Quantity;
            }

            return totalsByItemId;
        }
    }
}
