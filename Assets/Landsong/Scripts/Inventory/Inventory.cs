using System;
using System.Collections.Generic;
using UnityEngine;

namespace Landsong.InventorySystem
{
    public sealed class Inventory
    {
        private const int DefaultMaxStackSize = 99;

        private readonly List<InventorySlot> slots = new List<InventorySlot>();
        private ItemCatalog catalog;

        public Inventory(int slotCount, ItemCatalog catalog = null)
        {
            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "Inventory slot count cannot be negative.");
            }

            this.catalog = catalog;
            Resize(slotCount, false);
        }

        public event Action InventoryChanged;

        public ItemCatalog Catalog => catalog;
        public int SlotCount => slots.Count;
        public IReadOnlyList<InventorySlot> Slots => slots;

        public void SetCatalog(ItemCatalog newCatalog)
        {
            catalog = newCatalog;
        }

        public bool IsValidSlotIndex(int slotIndex)
        {
            return slotIndex >= 0 && slotIndex < slots.Count;
        }

        public bool TryGetSlot(int slotIndex, out InventorySlot slot)
        {
            if (!IsValidSlotIndex(slotIndex))
            {
                slot = null;
                return false;
            }

            slot = slots[slotIndex];
            return true;
        }

        public int GetQuantity(string itemId)
        {
            itemId = NormalizeItemId(itemId);
            if (!CanUseItemId(itemId))
            {
                return 0;
            }

            var total = 0;
            foreach (var slot in slots)
            {
                if (slot.Contains(itemId))
                {
                    total += slot.Quantity;
                }
            }

            return total;
        }

        public bool HasItem(string itemId, int quantity = 1)
        {
            if (quantity <= 0)
            {
                return true;
            }

            return GetQuantity(itemId) >= quantity;
        }

        public bool HasItems(IEnumerable<ItemAmount> requirements)
        {
            if (requirements == null)
            {
                return true;
            }

            var requiredById = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var requirement in requirements)
            {
                var normalized = requirement.Normalized();
                if (!normalized.IsValid)
                {
                    continue;
                }

                if (!requiredById.ContainsKey(normalized.ItemId))
                {
                    requiredById.Add(normalized.ItemId, 0);
                }

                requiredById[normalized.ItemId] += normalized.Amount;
            }

            foreach (var requirement in requiredById)
            {
                if (!HasItem(requirement.Key, requirement.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public bool CanAdd(string itemId, int quantity)
        {
            itemId = NormalizeItemId(itemId);
            if (quantity <= 0 || !CanUseItemId(itemId))
            {
                return false;
            }

            return GetAvailableCapacity(itemId) >= quantity;
        }

        public bool CanAdd(ItemDefinition definition, int quantity)
        {
            return definition != null && CanAdd(definition.ItemId, quantity);
        }

        public bool CanAddItems(IEnumerable<ItemAmount> items)
        {
            var totalsByItemId = BuildValidItemTotals(items);
            var emptySlotCount = 0;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty)
                {
                    emptySlotCount++;
                }
            }

            foreach (var itemTotal in totalsByItemId)
            {
                var itemId = itemTotal.Key;
                var remaining = itemTotal.Value;
                var maxStackSize = GetMaxStackSize(itemId);

                foreach (var slot in slots)
                {
                    if (!slot.Contains(itemId))
                    {
                        continue;
                    }

                    remaining -= Mathf.Max(0, maxStackSize - slot.Quantity);
                    if (remaining <= 0)
                    {
                        break;
                    }
                }

                if (remaining <= 0)
                {
                    continue;
                }

                var requiredEmptySlots = Mathf.CeilToInt((float)remaining / maxStackSize);
                if (requiredEmptySlots > emptySlotCount)
                {
                    return false;
                }

                emptySlotCount -= requiredEmptySlots;
            }

            return true;
        }

        public int Add(ItemDefinition definition, int quantity)
        {
            return definition == null ? 0 : Add(definition.ItemId, quantity);
        }

        public int Add(string itemId, int quantity)
        {
            itemId = NormalizeItemId(itemId);
            if (quantity <= 0 || !CanUseItemId(itemId))
            {
                return 0;
            }

            var remaining = quantity;
            var maxStackSize = GetMaxStackSize(itemId);

            foreach (var slot in slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!slot.Contains(itemId))
                {
                    continue;
                }

                var space = maxStackSize - slot.Quantity;
                if (space <= 0)
                {
                    continue;
                }

                var addedToStack = Mathf.Min(space, remaining);
                slot.Add(addedToStack);
                remaining -= addedToStack;
            }

            foreach (var slot in slots)
            {
                if (remaining <= 0)
                {
                    break;
                }

                if (!slot.IsEmpty)
                {
                    continue;
                }

                var addedToSlot = Mathf.Min(maxStackSize, remaining);
                slot.Set(itemId, addedToSlot);
                remaining -= addedToSlot;
            }

            var added = quantity - remaining;
            if (added > 0)
            {
                NotifyChanged();
            }

            return added;
        }

        public bool TryAdd(ItemDefinition definition, int quantity)
        {
            return definition != null && TryAdd(definition.ItemId, quantity);
        }

        public bool TryAdd(string itemId, int quantity)
        {
            if (!CanAdd(itemId, quantity))
            {
                return false;
            }

            return Add(itemId, quantity) == quantity;
        }

        public bool TryAddItems(IEnumerable<ItemAmount> items)
        {
            if (!CanAddItems(items))
            {
                return false;
            }

            if (items == null)
            {
                return true;
            }

            foreach (var item in items)
            {
                var normalized = item.Normalized();
                if (normalized.IsValid)
                {
                    Add(normalized.ItemId, normalized.Amount);
                }
            }

            return true;
        }

        public int Remove(string itemId, int quantity)
        {
            itemId = NormalizeItemId(itemId);
            if (quantity <= 0 || !CanUseItemId(itemId))
            {
                return 0;
            }

            var remaining = quantity;
            for (var i = slots.Count - 1; i >= 0; i--)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var slot = slots[i];
                if (!slot.Contains(itemId))
                {
                    continue;
                }

                remaining -= slot.Remove(remaining);
            }

            var removed = quantity - remaining;
            if (removed > 0)
            {
                NotifyChanged();
            }

            return removed;
        }

        public bool TryRemove(string itemId, int quantity)
        {
            if (!HasItem(itemId, quantity))
            {
                return false;
            }

            return Remove(itemId, quantity) == quantity;
        }

        public bool TryRemoveItems(IEnumerable<ItemAmount> requirements)
        {
            if (!HasItems(requirements))
            {
                return false;
            }

            if (requirements == null)
            {
                return true;
            }

            foreach (var requirement in requirements)
            {
                var normalized = requirement.Normalized();
                if (normalized.IsValid)
                {
                    Remove(normalized.ItemId, normalized.Amount);
                }
            }

            return true;
        }

        private static Dictionary<string, int> BuildValidItemTotals(IEnumerable<ItemAmount> items)
        {
            var totalsByItemId = new Dictionary<string, int>(StringComparer.Ordinal);
            if (items == null)
            {
                return totalsByItemId;
            }

            foreach (var item in items)
            {
                var normalized = item.Normalized();
                if (!normalized.IsValid)
                {
                    continue;
                }

                if (!totalsByItemId.ContainsKey(normalized.ItemId))
                {
                    totalsByItemId.Add(normalized.ItemId, 0);
                }

                totalsByItemId[normalized.ItemId] += normalized.Amount;
            }

            return totalsByItemId;
        }

        public bool Move(int fromSlotIndex, int toSlotIndex, int quantity = int.MaxValue)
        {
            if (!TryGetSlot(fromSlotIndex, out var source) || !TryGetSlot(toSlotIndex, out var target))
            {
                return false;
            }

            if (fromSlotIndex == toSlotIndex)
            {
                return true;
            }

            if (source.IsEmpty || quantity <= 0)
            {
                return false;
            }

            var amountToMove = Mathf.Min(quantity, source.Quantity);
            if (target.IsEmpty)
            {
                target.Set(source.ItemId, amountToMove);
                source.Remove(amountToMove);
                NotifyChanged();
                return true;
            }

            if (target.Contains(source.ItemId))
            {
                var space = GetMaxStackSize(target.ItemId) - target.Quantity;
                if (space < amountToMove)
                {
                    return false;
                }

                target.Add(amountToMove);
                source.Remove(amountToMove);
                NotifyChanged();
                return true;
            }

            if (amountToMove != source.Quantity)
            {
                return false;
            }

            Swap(fromSlotIndex, toSlotIndex);
            return true;
        }

        public bool SplitStack(int fromSlotIndex, int toSlotIndex, int quantity)
        {
            if (!TryGetSlot(fromSlotIndex, out var source) || !TryGetSlot(toSlotIndex, out var target))
            {
                return false;
            }

            if (fromSlotIndex == toSlotIndex || source.IsEmpty || !target.IsEmpty)
            {
                return false;
            }

            if (quantity <= 0 || quantity >= source.Quantity)
            {
                return false;
            }

            target.Set(source.ItemId, quantity);
            source.Remove(quantity);
            NotifyChanged();
            return true;
        }

        public bool Swap(int firstSlotIndex, int secondSlotIndex)
        {
            if (!TryGetSlot(firstSlotIndex, out var first) || !TryGetSlot(secondSlotIndex, out var second))
            {
                return false;
            }

            if (firstSlotIndex == secondSlotIndex)
            {
                return true;
            }

            var firstItemId = first.ItemId;
            var firstQuantity = first.Quantity;

            first.Set(second.ItemId, second.Quantity);
            second.Set(firstItemId, firstQuantity);
            NotifyChanged();
            return true;
        }

        public bool ClearSlot(int slotIndex)
        {
            if (!TryGetSlot(slotIndex, out var slot) || slot.IsEmpty)
            {
                return false;
            }

            slot.Clear();
            NotifyChanged();
            return true;
        }

        public void Clear()
        {
            var changed = false;
            foreach (var slot in slots)
            {
                if (slot.IsEmpty)
                {
                    continue;
                }

                slot.Clear();
                changed = true;
            }

            if (changed)
            {
                NotifyChanged();
            }
        }

        public void Resize(int slotCount)
        {
            Resize(slotCount, true);
        }

        public InventorySaveData CaptureSaveData()
        {
            var slotData = new List<InventorySlotData>();
            for (var i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    slotData.Add(slots[i].ToData(i));
                }
            }

            return new InventorySaveData(slots.Count, slotData);
        }

        public void RestoreSaveData(InventorySaveData saveData, bool resizeToSaveSlotCount = true)
        {
            if (saveData == null)
            {
                Clear();
                return;
            }

            if (resizeToSaveSlotCount)
            {
                Resize(saveData.SlotCount, false);
            }

            ClearWithoutNotify();

            foreach (var slotData in saveData.Slots)
            {
                var normalized = slotData.Normalized();
                if (!normalized.IsValid || !IsValidSlotIndex(normalized.SlotIndex) || !CanUseItemId(normalized.ItemId))
                {
                    continue;
                }

                var quantity = Mathf.Min(normalized.Quantity, GetMaxStackSize(normalized.ItemId));
                slots[normalized.SlotIndex].Set(normalized.ItemId, quantity);
            }

            NotifyChanged();
        }

        private void Resize(int slotCount, bool notify)
        {
            if (slotCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(slotCount), "Inventory slot count cannot be negative.");
            }

            if (slotCount == slots.Count)
            {
                return;
            }

            while (slots.Count < slotCount)
            {
                slots.Add(new InventorySlot());
            }

            while (slots.Count > slotCount)
            {
                slots.RemoveAt(slots.Count - 1);
            }

            if (notify)
            {
                NotifyChanged();
            }
        }

        private int GetAvailableCapacity(string itemId)
        {
            var maxStackSize = GetMaxStackSize(itemId);
            var capacity = 0;

            foreach (var slot in slots)
            {
                if (slot.IsEmpty)
                {
                    capacity += maxStackSize;
                    continue;
                }

                if (slot.Contains(itemId))
                {
                    capacity += Mathf.Max(0, maxStackSize - slot.Quantity);
                }
            }

            return capacity;
        }

        private int GetMaxStackSize(string itemId)
        {
            return catalog == null ? DefaultMaxStackSize : catalog.GetMaxStackSize(itemId, DefaultMaxStackSize);
        }

        private bool CanUseItemId(string itemId)
        {
            if (string.IsNullOrWhiteSpace(itemId))
            {
                return false;
            }

            return catalog == null || catalog.Contains(itemId);
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        }

        private void ClearWithoutNotify()
        {
            foreach (var slot in slots)
            {
                slot.Clear();
            }
        }

        private void NotifyChanged()
        {
            InventoryChanged?.Invoke();
        }
    }
}
