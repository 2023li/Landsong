using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.InventorySystem
{
    public sealed class InventoryService
    {
        private ItemCatalog itemCatalog;
        private InventorySlotTypeCatalog slotTypeCatalog;
        private Func<float> globalLossRateMultiplierProvider;
        private ItemAmount[] startingItems;
        private Inventory inventory;
        private bool startingItemsApplied;
        private bool restoringRuntime;

        public InventoryService(
            ItemCatalog itemCatalog,
            InventorySlotTypeCatalog slotTypeCatalog,
            IEnumerable<ItemAmount> startingItems = null,
            Func<float> globalLossRateMultiplierProvider = null)
        {
            this.itemCatalog = itemCatalog;
            this.slotTypeCatalog = slotTypeCatalog;
            this.globalLossRateMultiplierProvider =
                globalLossRateMultiplierProvider;
            this.startingItems = NormalizeStartingItems(startingItems);
            Initialize();
        }

        public event Action<InventoryService> InventoryChanged;
        public event Action<InventoryService, IReadOnlyList<InventorySlotLoss>> TurnLossProcessed;

        public ItemCatalog ItemCatalog => itemCatalog;
        public InventorySlotTypeCatalog SlotTypeCatalog => slotTypeCatalog;
        public int SlotCount => Inventory.SlotCount;
        public Inventory Inventory
        {
            get
            {
                EnsureInitialized();
                return inventory;
            }
        }

        public bool IsInitialized => inventory != null;
        public bool StartingItemsApplied => startingItemsApplied;
        public bool IsRestoringRuntime => restoringRuntime;

        public void Initialize()
        {
            UnsubscribeInventory();
            inventory = new Inventory(
                itemCatalog,
                slotTypeCatalog,
                globalLossRateMultiplierProvider);
            inventory.InventoryChanged += HandleInventoryChanged;
            HandleInventoryChanged();
        }

        public void SetCatalog(ItemCatalog newCatalog)
        {
            itemCatalog = newCatalog;
            inventory?.SetCatalog(itemCatalog);
        }

        public void SetSlotTypeCatalog(InventorySlotTypeCatalog newCatalog)
        {
            slotTypeCatalog = newCatalog;
            inventory?.SetSlotTypeCatalog(slotTypeCatalog);
        }

        public void SetGlobalLossRateMultiplierProvider(Func<float> provider)
        {
            globalLossRateMultiplierProvider = provider;
            inventory?.SetGlobalLossRateMultiplierProvider(provider);
        }

        public bool SynchronizeSlots(IEnumerable<InventorySlotProvision> provisions)
        {
            var synchronized = Inventory.SynchronizeSlots(provisions);
            if (synchronized)
            {
                TryApplyStartingItems();
            }

            return synchronized;
        }

        public void SetStartingItems(IEnumerable<ItemAmount> newStartingItems)
        {
            startingItems = NormalizeStartingItems(newStartingItems);
            startingItemsApplied = false;
            TryApplyStartingItems();
        }

        public bool TryApplyStartingItems()
        {
            if (startingItemsApplied || restoringRuntime)
            {
                return true;
            }

            if (!Inventory.CanAddItems(startingItems))
            {
                return false;
            }

            if (!Inventory.TryAddItems(startingItems))
            {
                return false;
            }

            startingItemsApplied = true;
            return true;
        }

        public void BeginRuntimeRestore()
        {
            restoringRuntime = true;
            startingItemsApplied = true;
            Inventory.Clear();
        }

        public void CompleteRuntimeRestore(InventorySaveData saveData)
        {
            Inventory.RestoreSaveData(saveData);
            startingItemsApplied = true;
            restoringRuntime = false;
        }

        public void CancelRuntimeRestore()
        {
            restoringRuntime = false;
            startingItemsApplied = true;
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

        public bool CanAddItems(IEnumerable<ItemAmount> items)
        {
            return Inventory.CanAddItems(items);
        }

        public bool TryAddItem(ItemDefinition definition, int quantity)
        {
            return Inventory.TryAdd(definition, quantity);
        }

        public bool TryAddItem(string itemId, int quantity)
        {
            return Inventory.TryAdd(itemId, quantity);
        }

        public bool TryAddItems(IEnumerable<ItemAmount> items)
        {
            return Inventory.TryAddItems(items);
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

        public bool CanConsumeRequirements(IEnumerable<ItemRequirement> requirements)
        {
            return Inventory.CanConsumeRequirements(requirements);
        }

        public bool TryConsumeRequirements(
            IEnumerable<ItemRequirement> requirements,
            out ItemConsumptionReceipt receipt)
        {
            return Inventory.TryConsumeRequirements(requirements, out receipt);
        }

        public bool CanExchangeItems(
            IEnumerable<ItemAmount> inputs,
            IEnumerable<ItemAmount> outputs)
        {
            return Inventory.CanExchangeItems(inputs, outputs);
        }

        public bool TryExchangeItems(
            IEnumerable<ItemAmount> inputs,
            IEnumerable<ItemAmount> outputs)
        {
            return Inventory.TryExchangeItems(inputs, outputs);
        }

        public int GetQuantity(string itemId)
        {
            return Inventory.GetQuantity(itemId);
        }

        public bool HasStoredItemsForProvider(string providerBuildingInstanceId)
        {
            return Inventory.HasStoredItemsForProvider(providerBuildingInstanceId);
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

        public IReadOnlyList<InventorySlotLoss> CalculateTurnLosses()
        {
            return Inventory.CalculateTurnLosses();
        }

        public IReadOnlyList<InventorySlotLoss> ProcessTurnLosses()
        {
            var losses = Inventory.ProcessTurnLosses();
            TurnLossProcessed?.Invoke(this, losses);
            return losses;
        }

        public InventorySaveData CaptureSaveData()
        {
            return Inventory.CaptureSaveData();
        }

        public void RestoreSaveData(InventorySaveData saveData)
        {
            Inventory.RestoreSaveData(saveData);
            startingItemsApplied = true;
        }

        public void LogInventoryContents()
        {
            var totalsByItemId = BuildInventoryTotals();
            if (totalsByItemId.Count == 0)
            {
                Debug.Log("Inventory is empty.");
                return;
            }

            var lines = new List<string>();
            foreach (var itemTotal in totalsByItemId)
            {
                var label = itemTotal.Key;
                if (itemCatalog != null
                    && itemCatalog.TryGetDefinition(itemTotal.Key, out var definition))
                {
                    label = definition.DisplayName;
                }

                lines.Add($"{label} ({itemTotal.Key}): {itemTotal.Value}");
            }

            Debug.Log($"Inventory contents:\n{string.Join("\n", lines)}");
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
            for (var i = 0; i < currentInventory.Slots.Count; i++)
            {
                var slot = currentInventory.Slots[i];
                if (slot.IsEmpty)
                {
                    continue;
                }

                totalsByItemId.TryGetValue(slot.ItemId, out var current);
                totalsByItemId[slot.ItemId] = current + slot.Quantity;
            }

            return totalsByItemId;
        }

        private static ItemAmount[] NormalizeStartingItems(IEnumerable<ItemAmount> source)
        {
            if (source == null)
            {
                return Array.Empty<ItemAmount>();
            }

            var normalizedItems = new List<ItemAmount>();
            foreach (var item in source)
            {
                var normalized = item.Normalized();
                if (normalized.IsValid)
                {
                    normalizedItems.Add(normalized);
                }
            }

            return normalizedItems.ToArray();
        }
    }
}
