using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    /// <summary>
    /// 建筑系统与库存系统之间的稳定槽位类型契约。
    /// 建筑只决定提供哪种槽位；库存系统根据类型解析损耗规则，并按物品有效损耗率选择自动入库槽位；
    /// UI 根据类型选择固定的视觉表现。
    /// </summary>
    public enum InventorySlotType
    {
        简陋库存 = 0,
        普通库存 = 10,
        高级库存 = 20,
        冻库 = 30,
        粮库 = 40
    }

    public enum ItemRequirementSelectionPolicy
    {
        PreferSoonToSpoil = 0,
        PreferVariety = 10,
        PreferQuality = 20,
        PreferLowestValue = 30
    }

    [Serializable]
    public struct InventorySlotLossModifier
    {
        [SerializeField, AssetsOnly] private ItemGroupDefinition itemGroup;
        [SerializeField, Min(0f)] private float lossRateMultiplier;

        public InventorySlotLossModifier(ItemGroupDefinition itemGroup, float lossRateMultiplier)
        {
            this.itemGroup = itemGroup;
            this.lossRateMultiplier = Mathf.Max(0f, lossRateMultiplier);
        }

        public ItemGroupDefinition ItemGroup => itemGroup;
        public float LossRateMultiplier => Mathf.Max(0f, lossRateMultiplier);
        public bool IsValid => itemGroup != null;

        public bool Matches(ItemDefinition definition)
        {
            return IsValid && definition != null && definition.BelongsTo(itemGroup);
        }
    }

    [Serializable]
    public sealed class InventorySlotProvision
    {
        [SerializeField] private string providerBuildingInstanceId;
        [SerializeField] private string providerFamilyId;
        [SerializeField] private string providerDisplayName;
        [SerializeField] private string localSlotId;
        [SerializeField] private InventorySlotType slotType;
        [SerializeField] private float runtimeLossRateMultiplier = 1f;

        public InventorySlotProvision(
            string providerBuildingInstanceId,
            string providerFamilyId,
            string providerDisplayName,
            string localSlotId,
            InventorySlotType slotType = InventorySlotType.简陋库存,
            float runtimeLossRateMultiplier = 1f)
        {
            this.providerBuildingInstanceId = NormalizeId(providerBuildingInstanceId);
            this.providerFamilyId = NormalizeId(providerFamilyId);
            this.providerDisplayName = NormalizeText(providerDisplayName);
            this.localSlotId = NormalizeId(localSlotId);
            this.slotType = slotType;
            this.runtimeLossRateMultiplier = Mathf.Max(0f, runtimeLossRateMultiplier);
        }

        public string ProviderBuildingInstanceId => providerBuildingInstanceId;
        public string ProviderFamilyId => providerFamilyId;
        public string ProviderDisplayName =>
            string.IsNullOrWhiteSpace(providerDisplayName) ? providerFamilyId : providerDisplayName;
        public string LocalSlotId => localSlotId;
        public string StorageSlotId => BuildStorageSlotId(providerBuildingInstanceId, localSlotId);
        public InventorySlotType SlotType => slotType;
        public float RuntimeLossRateMultiplier => Mathf.Max(0f, runtimeLossRateMultiplier);
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(providerBuildingInstanceId)
            && !string.IsNullOrWhiteSpace(localSlotId);

        public float CalculateLossRate(
            ItemDefinition definition,
            InventorySlotTypeCatalog slotTypeCatalog)
        {
            if (definition == null || definition.LossRatePerTurn <= 0f)
            {
                return 0f;
            }

            return slotTypeCatalog == null
                ? Mathf.Clamp01(definition.LossRatePerTurn * RuntimeLossRateMultiplier)
                : slotTypeCatalog.CalculateLossRate(
                    slotType,
                    definition,
                    RuntimeLossRateMultiplier);
        }

        public static string BuildStorageSlotId(string providerBuildingInstanceId, string localSlotId)
        {
            var provider = NormalizeId(providerBuildingInstanceId);
            var local = NormalizeId(localSlotId);
            return string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(local)
                ? string.Empty
                : $"{provider}:{local}";
        }

        private static string NormalizeId(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

        private static string NormalizeText(string value) =>
            string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    [Serializable]
    public sealed class ItemRequirement
    {
        [SerializeField, AssetsOnly] private ItemDefinition itemDefinition;
        [SerializeField, AssetsOnly] private ItemGroupDefinition itemGroup;
        [SerializeField, Min(0)] private int amount;
        [SerializeField] private ItemRequirementSelectionPolicy selectionPolicy =
            ItemRequirementSelectionPolicy.PreferSoonToSpoil;

        public ItemRequirement(
            ItemDefinition itemDefinition,
            int amount,
            ItemRequirementSelectionPolicy selectionPolicy =
                ItemRequirementSelectionPolicy.PreferSoonToSpoil)
        {
            this.itemDefinition = itemDefinition;
            itemGroup = null;
            this.amount = Mathf.Max(0, amount);
            this.selectionPolicy = selectionPolicy;
        }

        public ItemRequirement(
            ItemGroupDefinition itemGroup,
            int amount,
            ItemRequirementSelectionPolicy selectionPolicy =
                ItemRequirementSelectionPolicy.PreferSoonToSpoil)
        {
            itemDefinition = null;
            this.itemGroup = itemGroup;
            this.amount = Mathf.Max(0, amount);
            this.selectionPolicy = selectionPolicy;
        }

        public ItemDefinition ItemDefinition => itemDefinition;
        public ItemGroupDefinition ItemGroup => itemGroup;
        public int Amount => Mathf.Max(0, amount);
        public ItemRequirementSelectionPolicy SelectionPolicy => selectionPolicy;
        public bool IsExactItem => itemDefinition != null;
        public bool IsGroup => itemDefinition == null && itemGroup != null;
        public bool IsValid => Amount > 0 && (IsExactItem || IsGroup);
        public string RequirementId => IsExactItem
            ? itemDefinition.ItemId
            : itemGroup == null ? string.Empty : itemGroup.GroupId;

        public bool Matches(ItemDefinition definition)
        {
            if (definition == null)
            {
                return false;
            }

            if (IsExactItem)
            {
                return string.Equals(
                    itemDefinition.ItemId,
                    definition.ItemId,
                    StringComparison.Ordinal);
            }

            return IsGroup && definition.BelongsTo(itemGroup);
        }
    }

    public readonly struct ItemConsumptionLine
    {
        public ItemConsumptionLine(
            string itemId,
            int amount,
            string storageSlotId,
            string providerBuildingInstanceId)
        {
            ItemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            Amount = Mathf.Max(0, amount);
            StorageSlotId = string.IsNullOrWhiteSpace(storageSlotId)
                ? string.Empty
                : storageSlotId.Trim();
            ProviderBuildingInstanceId = string.IsNullOrWhiteSpace(providerBuildingInstanceId)
                ? string.Empty
                : providerBuildingInstanceId.Trim();
        }

        public string ItemId { get; }
        public int Amount { get; }
        public string StorageSlotId { get; }
        public string ProviderBuildingInstanceId { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && Amount > 0;
    }

    public sealed class ItemConsumptionReceipt
    {
        public ItemConsumptionReceipt(
            IReadOnlyList<ItemRequirement> requirements,
            IReadOnlyList<ItemConsumptionLine> lines)
        {
            Requirements = requirements ?? Array.Empty<ItemRequirement>();
            Lines = lines ?? Array.Empty<ItemConsumptionLine>();
        }

        public IReadOnlyList<ItemRequirement> Requirements { get; }
        public IReadOnlyList<ItemConsumptionLine> Lines { get; }

        public int TotalConsumed
        {
            get
            {
                var total = 0;
                for (var i = 0; i < Lines.Count; i++)
                {
                    total += Mathf.Max(0, Lines[i].Amount);
                }

                return total;
            }
        }

        public int DistinctItemCount
        {
            get
            {
                var itemIds = new HashSet<string>(StringComparer.Ordinal);
                for (var i = 0; i < Lines.Count; i++)
                {
                    if (Lines[i].IsValid)
                    {
                        itemIds.Add(Lines[i].ItemId);
                    }
                }

                return itemIds.Count;
            }
        }
    }

    public readonly struct InventorySlotLoss
    {
        public InventorySlotLoss(
            string storageSlotId,
            string providerBuildingInstanceId,
            string itemId,
            int quantityBefore,
            int amountLost,
            float effectiveLossRate)
        {
            StorageSlotId = storageSlotId ?? string.Empty;
            ProviderBuildingInstanceId = providerBuildingInstanceId ?? string.Empty;
            ItemId = itemId ?? string.Empty;
            QuantityBefore = Mathf.Max(0, quantityBefore);
            AmountLost = Mathf.Max(0, amountLost);
            EffectiveLossRate = Mathf.Clamp01(effectiveLossRate);
        }

        public string StorageSlotId { get; }
        public string ProviderBuildingInstanceId { get; }
        public string ItemId { get; }
        public int QuantityBefore { get; }
        public int AmountLost { get; }
        public float EffectiveLossRate { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(ItemId) && AmountLost > 0;
    }
}
