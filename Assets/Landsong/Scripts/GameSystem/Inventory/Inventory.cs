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
        private InventorySlotTypeCatalog slotTypeCatalog;
        private Func<float> globalLossRateMultiplierProvider;

        public Inventory(
            ItemCatalog catalog = null,
            InventorySlotTypeCatalog slotTypeCatalog = null,
            Func<float> globalLossRateMultiplierProvider = null)
        {
            this.catalog = catalog;
            this.slotTypeCatalog = slotTypeCatalog;
            this.globalLossRateMultiplierProvider = globalLossRateMultiplierProvider;
        }

        public event Action InventoryChanged;

        public ItemCatalog Catalog => catalog;
        public InventorySlotTypeCatalog SlotTypeCatalog => slotTypeCatalog;
        public int SlotCount => slots.Count;
        public IReadOnlyList<InventorySlot> Slots => slots;

        public void SetCatalog(ItemCatalog newCatalog)
        {
            catalog = newCatalog;
        }

        public void SetSlotTypeCatalog(InventorySlotTypeCatalog newCatalog)
        {
            slotTypeCatalog = newCatalog;
        }

        public void SetGlobalLossRateMultiplierProvider(Func<float> provider)
        {
            globalLossRateMultiplierProvider = provider;
        }

        public bool SynchronizeSlots(IEnumerable<InventorySlotProvision> provisions)
        {
            var desired = NormalizeProvisions(provisions);
            var desiredIds = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < desired.Count; i++)
            {
                desiredIds.Add(desired[i].StorageSlotId);
            }

            for (var i = 0; i < slots.Count; i++)
            {
                if (!desiredIds.Contains(slots[i].StorageSlotId) && !slots[i].IsEmpty)
                {
                    return false;
                }
            }

            var existingById = new Dictionary<string, InventorySlot>(StringComparer.Ordinal);
            for (var i = 0; i < slots.Count; i++)
            {
                existingById[slots[i].StorageSlotId] = slots[i];
            }

            var changed = slots.Count != desired.Count;
            var synchronized = new List<InventorySlot>(desired.Count);
            for (var i = 0; i < desired.Count; i++)
            {
                var provision = desired[i];
                if (existingById.TryGetValue(provision.StorageSlotId, out var existing))
                {
                    changed |= i >= slots.Count
                               || !ReferenceEquals(slots[i], existing)
                               || existing.SlotType != provision.SlotType
                               || !Mathf.Approximately(
                                   existing.Provision.RuntimeLossRateMultiplier,
                                   provision.RuntimeLossRateMultiplier);
                    existing.UpdateProvision(provision);
                    synchronized.Add(existing);
                }
                else
                {
                    changed = true;
                    synchronized.Add(new InventorySlot(provision));
                }
            }

            slots.Clear();
            slots.AddRange(synchronized);
            if (changed)
            {
                NotifyChanged();
            }

            return true;
        }

        public bool HasStoredItemsForProvider(string providerBuildingInstanceId)
        {
            if (string.IsNullOrWhiteSpace(providerBuildingInstanceId))
            {
                return false;
            }

            var normalized = providerBuildingInstanceId.Trim();
            for (var i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty
                    && string.Equals(
                        slots[i].ProviderBuildingInstanceId,
                        normalized,
                        StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Contains(itemId))
                {
                    total += slots[i].Quantity;
                }
            }

            return total;
        }

        public bool HasItem(string itemId, int quantity = 1)
        {
            return quantity <= 0 || GetQuantity(itemId) >= quantity;
        }

        public bool HasItems(IEnumerable<ItemAmount> requirements)
        {
            var requiredById = BuildValidItemTotals(requirements);
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
            return quantity > 0
                   && CanUseItemId(itemId)
                   && GetAvailableCapacity(itemId) >= quantity;
        }

        public bool CanAdd(ItemDefinition definition, int quantity)
        {
            return definition != null && CanAdd(definition.ItemId, quantity);
        }

        public bool CanAddItems(IEnumerable<ItemAmount> items)
        {
            var normalized = ToNormalizedAmounts(items);
            var simulation = CreateSimulation();
            for (var i = 0; i < normalized.Count; i++)
            {
                if (simulation.Add(normalized[i].ItemId, normalized[i].Amount) != normalized[i].Amount)
                {
                    return false;
                }
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
            var candidates = GetStorageCandidates(itemId);
            for (var i = 0; i < candidates.Count && remaining > 0; i++)
            {
                var candidate = candidates[i];
                var space = candidate.IsEmpty
                    ? maxStackSize
                    : Mathf.Max(0, maxStackSize - candidate.Quantity);
                if (space <= 0)
                {
                    continue;
                }

                var added = Mathf.Min(space, remaining);
                if (candidate.IsEmpty)
                {
                    candidate.Set(itemId, added);
                }
                else
                {
                    candidate.Add(added);
                }

                remaining -= added;
            }

            var totalAdded = quantity - remaining;
            if (totalAdded > 0)
            {
                NotifyChanged();
            }

            return totalAdded;
        }

        public bool TryAdd(ItemDefinition definition, int quantity)
        {
            return definition != null && TryAdd(definition.ItemId, quantity);
        }

        public bool TryAdd(string itemId, int quantity)
        {
            return CanAdd(itemId, quantity) && Add(itemId, quantity) == quantity;
        }

        public bool TryAddItems(IEnumerable<ItemAmount> items)
        {
            var normalized = ToNormalizedAmounts(items);
            if (!CanAddItems(normalized))
            {
                return false;
            }

            for (var i = 0; i < normalized.Count; i++)
            {
                Add(normalized[i].ItemId, normalized[i].Amount);
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

            var candidates = GetConsumptionCandidates(itemId);
            var remaining = quantity;
            for (var i = 0; i < candidates.Count && remaining > 0; i++)
            {
                remaining -= candidates[i].Remove(remaining);
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
            return HasItem(itemId, quantity) && Remove(itemId, quantity) == quantity;
        }

        public bool TryRemoveItems(IEnumerable<ItemAmount> requirements)
        {
            var normalized = ToNormalizedAmounts(requirements);
            if (!HasItems(normalized))
            {
                return false;
            }

            for (var i = 0; i < normalized.Count; i++)
            {
                Remove(normalized[i].ItemId, normalized[i].Amount);
            }

            return true;
        }

        public bool CanConsumeRequirements(IEnumerable<ItemRequirement> requirements)
        {
            var normalized = NormalizeRequirements(requirements);
            var simulation = CreateSimulation();
            return simulation.TryConsumeRequirementsInternal(normalized, out _);
        }

        public bool TryConsumeRequirements(
            IEnumerable<ItemRequirement> requirements,
            out ItemConsumptionReceipt receipt)
        {
            var normalized = NormalizeRequirements(requirements);
            var simulation = CreateSimulation();
            if (!simulation.TryConsumeRequirementsInternal(normalized, out var lines))
            {
                receipt = null;
                return false;
            }

            CopyContentsFrom(simulation);
            receipt = new ItemConsumptionReceipt(normalized, lines);
            return true;
        }

        public bool CanExchangeItems(
            IEnumerable<ItemAmount> inputs,
            IEnumerable<ItemAmount> outputs)
        {
            return TryBuildExchangeResult(inputs, outputs, out _);
        }

        public bool TryExchangeItems(
            IEnumerable<ItemAmount> inputs,
            IEnumerable<ItemAmount> outputs)
        {
            if (!TryBuildExchangeResult(inputs, outputs, out var projected))
            {
                return false;
            }

            RestoreSaveData(projected);
            return true;
        }

        public bool Move(int fromSlotIndex, int toSlotIndex, int quantity = int.MaxValue)
        {
            if (!TryGetSlot(fromSlotIndex, out var source)
                || !TryGetSlot(toSlotIndex, out var target)
                || fromSlotIndex == toSlotIndex)
            {
                return fromSlotIndex == toSlotIndex && IsValidSlotIndex(fromSlotIndex);
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

            return Swap(fromSlotIndex, toSlotIndex);
        }

        public bool SplitStack(int fromSlotIndex, int toSlotIndex, int quantity)
        {
            if (!TryGetSlot(fromSlotIndex, out var source)
                || !TryGetSlot(toSlotIndex, out var target)
                || fromSlotIndex == toSlotIndex
                || source.IsEmpty
                || !target.IsEmpty
                || quantity <= 0
                || quantity >= source.Quantity)
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
            if (!TryGetSlot(firstSlotIndex, out var first)
                || !TryGetSlot(secondSlotIndex, out var second))
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
            if (ClearWithoutNotify())
            {
                NotifyChanged();
            }
        }

        public IReadOnlyList<InventorySlotLoss> CalculateTurnLosses()
        {
            var losses = new List<InventorySlotLoss>();
            var globalLossRateMultiplier = GetGlobalLossRateMultiplier();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || !TryGetDefinition(slot.ItemId, out var definition))
                {
                    continue;
                }

                var rate = Mathf.Clamp01(
                    slot.GetEffectiveLossRate(definition, slotTypeCatalog)
                    * globalLossRateMultiplier);
                var amount = Mathf.Clamp(
                    Mathf.FloorToInt(slot.Quantity * rate),
                    0,
                    slot.Quantity);
                if (amount <= 0)
                {
                    continue;
                }

                losses.Add(new InventorySlotLoss(
                    slot.StorageSlotId,
                    slot.ProviderBuildingInstanceId,
                    slot.ItemId,
                    slot.Quantity,
                    amount,
                    rate));
            }

            return losses;
        }

        public IReadOnlyList<InventorySlotLoss> ProcessTurnLosses()
        {
            var losses = CalculateTurnLosses();
            for (var i = 0; i < losses.Count; i++)
            {
                if (TryGetSlotById(losses[i].StorageSlotId, out var slot))
                {
                    slot.Remove(losses[i].AmountLost);
                }
            }

            if (losses.Count > 0)
            {
                NotifyChanged();
            }

            return losses;
        }

        public InventorySaveData CaptureSaveData()
        {
            var data = new List<InventorySlotData>();
            for (var i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    data.Add(slots[i].ToData());
                }
            }

            return new InventorySaveData(data);
        }

        public void RestoreSaveData(InventorySaveData saveData)
        {
            ClearWithoutNotify();
            if (saveData != null)
            {
                for (var i = 0; i < saveData.Slots.Count; i++)
                {
                    var data = saveData.Slots[i].Normalized();
                    if (!data.IsValid
                        || !CanUseItemId(data.ItemId)
                        || !TryGetSlotById(data.StorageSlotId, out var slot))
                    {
                        continue;
                    }

                    slot.Set(data.ItemId, Mathf.Min(data.Quantity, GetMaxStackSize(data.ItemId)));
                }
            }

            NotifyChanged();
        }

        public Inventory CreateSimulation()
        {
            var simulation = new Inventory(
                catalog,
                slotTypeCatalog,
                globalLossRateMultiplierProvider);
            var provisions = new InventorySlotProvision[slots.Count];
            for (var i = 0; i < slots.Count; i++)
            {
                provisions[i] = slots[i].Provision;
            }

            simulation.SynchronizeSlots(provisions);
            for (var i = 0; i < slots.Count; i++)
            {
                if (!slots[i].IsEmpty)
                {
                    simulation.slots[i].Set(slots[i].ItemId, slots[i].Quantity);
                }
            }

            return simulation;
        }

        private float GetGlobalLossRateMultiplier()
        {
            if (globalLossRateMultiplierProvider == null)
            {
                return 1f;
            }

            try
            {
                return Mathf.Max(0f, globalLossRateMultiplierProvider());
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return 1f;
            }
        }

        private bool TryBuildExchangeResult(
            IEnumerable<ItemAmount> inputs,
            IEnumerable<ItemAmount> outputs,
            out InventorySaveData projected)
        {
            var simulation = CreateSimulation();
            if (!simulation.TryRemoveItems(ToNormalizedAmounts(inputs))
                || !simulation.TryAddItems(ToNormalizedAmounts(outputs)))
            {
                projected = null;
                return false;
            }

            projected = simulation.CaptureSaveData();
            return true;
        }

        private bool TryConsumeRequirementsInternal(
            IReadOnlyList<ItemRequirement> requirements,
            out IReadOnlyList<ItemConsumptionLine> lines)
        {
            var consumed = new List<ItemConsumptionLine>();
            for (var i = 0; i < requirements.Count; i++)
            {
                if (!requirements[i].IsValid
                    || !TryConsumeRequirement(requirements[i], consumed))
                {
                    lines = Array.Empty<ItemConsumptionLine>();
                    return false;
                }
            }

            lines = consumed;
            return true;
        }

        private bool TryConsumeRequirement(
            ItemRequirement requirement,
            List<ItemConsumptionLine> consumed)
        {
            var candidates = new List<InventorySlot>();
            var available = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot.IsEmpty || !RequirementMatches(requirement, slot.ItemId))
                {
                    continue;
                }

                candidates.Add(slot);
                available += slot.Quantity;
            }

            if (available < requirement.Amount)
            {
                return false;
            }

            if (requirement.SelectionPolicy == ItemRequirementSelectionPolicy.PreferVariety)
            {
                return ConsumeWithVariety(candidates, requirement.Amount, consumed);
            }

            candidates.Sort((left, right) =>
                CompareConsumptionCandidates(left, right, requirement.SelectionPolicy));
            var remaining = requirement.Amount;
            for (var i = 0; i < candidates.Count && remaining > 0; i++)
            {
                var consumedItemId = candidates[i].ItemId;
                var removed = candidates[i].Remove(remaining);
                remaining -= removed;
                AddConsumptionLine(consumed, candidates[i], consumedItemId, removed);
            }

            return remaining <= 0;
        }

        private bool ConsumeWithVariety(
            IReadOnlyList<InventorySlot> candidates,
            int amount,
            List<ItemConsumptionLine> consumed)
        {
            var byItemId = new Dictionary<string, List<InventorySlot>>(StringComparer.Ordinal);
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!byItemId.TryGetValue(candidates[i].ItemId, out var itemSlots))
                {
                    itemSlots = new List<InventorySlot>();
                    byItemId.Add(candidates[i].ItemId, itemSlots);
                }

                itemSlots.Add(candidates[i]);
            }

            var itemIds = new List<string>(byItemId.Keys);
            itemIds.Sort(CompareVarietyItems);
            for (var i = 0; i < itemIds.Count; i++)
            {
                byItemId[itemIds[i]].Sort((left, right) =>
                    CompareConsumptionCandidates(
                        left,
                        right,
                        ItemRequirementSelectionPolicy.PreferSoonToSpoil));
            }

            var remaining = amount;
            while (remaining > 0)
            {
                var progressed = false;
                for (var i = 0; i < itemIds.Count && remaining > 0; i++)
                {
                    var itemSlots = byItemId[itemIds[i]];
                    var source = FindFirstNonEmpty(itemSlots);
                    if (source == null)
                    {
                        continue;
                    }

                    var consumedItemId = source.ItemId;
                    var removed = source.Remove(1);
                    if (removed <= 0)
                    {
                        continue;
                    }

                    progressed = true;
                    remaining -= removed;
                    AddConsumptionLine(consumed, source, consumedItemId, removed);
                }

                if (!progressed)
                {
                    return false;
                }
            }

            return true;
        }

        private int CompareVarietyItems(string leftItemId, string rightItemId)
        {
            TryGetDefinition(leftItemId, out var left);
            TryGetDefinition(rightItemId, out var right);
            var qualityComparison = (right == null ? 0f : right.FoodProfile.DietQuality)
                .CompareTo(left == null ? 0f : left.FoodProfile.DietQuality);
            return qualityComparison != 0
                ? qualityComparison
                : string.Compare(leftItemId, rightItemId, StringComparison.Ordinal);
        }

        private int CompareConsumptionCandidates(
            InventorySlot left,
            InventorySlot right,
            ItemRequirementSelectionPolicy policy)
        {
            TryGetDefinition(left.ItemId, out var leftDefinition);
            TryGetDefinition(right.ItemId, out var rightDefinition);
            var comparison = 0;
            switch (policy)
            {
                case ItemRequirementSelectionPolicy.PreferQuality:
                    comparison = (rightDefinition == null ? 0f : rightDefinition.FoodProfile.DietQuality)
                        .CompareTo(leftDefinition == null ? 0f : leftDefinition.FoodProfile.DietQuality);
                    break;
                case ItemRequirementSelectionPolicy.PreferLowestValue:
                    comparison = (leftDefinition == null ? 0 : leftDefinition.BaseValue)
                        .CompareTo(rightDefinition == null ? 0 : rightDefinition.BaseValue);
                    break;
            }

            if (comparison != 0)
            {
                return comparison;
            }

            comparison = right.GetEffectiveLossRate(rightDefinition, slotTypeCatalog)
                .CompareTo(left.GetEffectiveLossRate(leftDefinition, slotTypeCatalog));
            return comparison != 0
                ? comparison
                : string.Compare(left.StorageSlotId, right.StorageSlotId, StringComparison.Ordinal);
        }

        private List<InventorySlot> GetStorageCandidates(string itemId)
        {
            var result = new List<InventorySlot>();
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty || slots[i].Contains(itemId))
                {
                    result.Add(slots[i]);
                }
            }

            TryGetDefinition(itemId, out var definition);
            result.Sort((left, right) =>
            {
                var comparison = left.GetEffectiveLossRate(definition, slotTypeCatalog)
                    .CompareTo(right.GetEffectiveLossRate(definition, slotTypeCatalog));
                if (comparison != 0)
                {
                    return comparison;
                }

                comparison = left.IsEmpty.CompareTo(right.IsEmpty);
                return comparison != 0
                    ? comparison
                    : string.Compare(left.StorageSlotId, right.StorageSlotId, StringComparison.Ordinal);
            });
            return result;
        }

        private List<InventorySlot> GetConsumptionCandidates(string itemId)
        {
            var result = new List<InventorySlot>();
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].Contains(itemId))
                {
                    result.Add(slots[i]);
                }
            }

            TryGetDefinition(itemId, out var definition);
            result.Sort((left, right) =>
            {
                var comparison = right.GetEffectiveLossRate(definition, slotTypeCatalog)
                    .CompareTo(left.GetEffectiveLossRate(definition, slotTypeCatalog));
                return comparison != 0
                    ? comparison
                    : string.Compare(left.StorageSlotId, right.StorageSlotId, StringComparison.Ordinal);
            });
            return result;
        }

        private int GetAvailableCapacity(string itemId)
        {
            var maxStackSize = GetMaxStackSize(itemId);
            var capacity = 0;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    capacity += maxStackSize;
                }
                else if (slots[i].Contains(itemId))
                {
                    capacity += Mathf.Max(0, maxStackSize - slots[i].Quantity);
                }
            }

            return capacity;
        }

        private bool RequirementMatches(ItemRequirement requirement, string itemId)
        {
            if (requirement.IsExactItem)
            {
                return string.Equals(
                    requirement.ItemDefinition.ItemId,
                    itemId,
                    StringComparison.Ordinal);
            }

            return TryGetDefinition(itemId, out var definition) && requirement.Matches(definition);
        }

        private bool TryGetDefinition(string itemId, out ItemDefinition definition)
        {
            if (catalog != null)
            {
                return catalog.TryGetDefinition(itemId, out definition);
            }

            definition = null;
            return false;
        }

        private bool TryGetSlotById(string storageSlotId, out InventorySlot slot)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (string.Equals(slots[i].StorageSlotId, storageSlotId, StringComparison.Ordinal))
                {
                    slot = slots[i];
                    return true;
                }
            }

            slot = null;
            return false;
        }

        private void CopyContentsFrom(Inventory source)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                if (source.TryGetSlotById(slots[i].StorageSlotId, out var sourceSlot)
                    && !sourceSlot.IsEmpty)
                {
                    slots[i].Set(sourceSlot.ItemId, sourceSlot.Quantity);
                }
                else
                {
                    slots[i].Clear();
                }
            }

            NotifyChanged();
        }

        private bool ClearWithoutNotify()
        {
            var changed = false;
            for (var i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    continue;
                }

                slots[i].Clear();
                changed = true;
            }

            return changed;
        }

        private int GetMaxStackSize(string itemId)
        {
            return catalog == null
                ? DefaultMaxStackSize
                : catalog.GetMaxStackSize(itemId, DefaultMaxStackSize);
        }

        private bool CanUseItemId(string itemId)
        {
            return !string.IsNullOrWhiteSpace(itemId)
                   && (catalog == null || catalog.Contains(itemId));
        }

        private static List<InventorySlotProvision> NormalizeProvisions(
            IEnumerable<InventorySlotProvision> provisions)
        {
            var result = new List<InventorySlotProvision>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (provisions == null)
            {
                return result;
            }

            foreach (var provision in provisions)
            {
                if (provision == null
                    || !provision.IsValid
                    || !ids.Add(provision.StorageSlotId))
                {
                    continue;
                }

                result.Add(provision);
            }

            return result;
        }

        private static IReadOnlyList<ItemAmount> ToNormalizedAmounts(IEnumerable<ItemAmount> items)
        {
            var totals = new Dictionary<string, ItemAmount>(StringComparer.Ordinal);
            if (items != null)
            {
                foreach (var item in items)
                {
                    var normalized = item.Normalized();
                    if (!normalized.IsValid)
                    {
                        continue;
                    }

                    totals.TryGetValue(normalized.ItemId, out var current);
                    totals[normalized.ItemId] = new ItemAmount(
                        normalized.ItemDefinition,
                        current.Amount + normalized.Amount);
                }
            }

            return new List<ItemAmount>(totals.Values);
        }

        private static Dictionary<string, int> BuildValidItemTotals(IEnumerable<ItemAmount> items)
        {
            var totals = new Dictionary<string, int>(StringComparer.Ordinal);
            if (items == null)
            {
                return totals;
            }

            foreach (var item in items)
            {
                var normalized = item.Normalized();
                if (!normalized.IsValid)
                {
                    continue;
                }

                totals.TryGetValue(normalized.ItemId, out var current);
                totals[normalized.ItemId] = current + normalized.Amount;
            }

            return totals;
        }

        private static IReadOnlyList<ItemRequirement> NormalizeRequirements(
            IEnumerable<ItemRequirement> requirements)
        {
            if (requirements == null)
            {
                return Array.Empty<ItemRequirement>();
            }

            var result = new List<ItemRequirement>();
            foreach (var requirement in requirements)
            {
                if (requirement != null && requirement.IsValid)
                {
                    result.Add(requirement);
                }
            }

            return result;
        }

        private static InventorySlot FindFirstNonEmpty(IReadOnlyList<InventorySlot> candidates)
        {
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].IsEmpty)
                {
                    return candidates[i];
                }
            }

            return null;
        }

        private static void AddConsumptionLine(
            List<ItemConsumptionLine> lines,
            InventorySlot slot,
            string itemId,
            int amount)
        {
            if (slot == null || string.IsNullOrWhiteSpace(itemId) || amount <= 0)
            {
                return;
            }

            for (var i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i].ItemId, itemId, StringComparison.Ordinal)
                    && string.Equals(lines[i].StorageSlotId, slot.StorageSlotId, StringComparison.Ordinal))
                {
                    lines[i] = new ItemConsumptionLine(
                        lines[i].ItemId,
                        lines[i].Amount + amount,
                        lines[i].StorageSlotId,
                        lines[i].ProviderBuildingInstanceId);
                    return;
                }
            }

            lines.Add(new ItemConsumptionLine(
                itemId,
                amount,
                slot.StorageSlotId,
                slot.ProviderBuildingInstanceId));
        }

        private static string NormalizeItemId(string itemId)
        {
            return string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        }

        private void NotifyChanged()
        {
            InventoryChanged?.Invoke();
        }
    }
}
