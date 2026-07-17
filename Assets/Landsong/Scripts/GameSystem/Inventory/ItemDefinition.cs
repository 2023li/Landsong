using System;
using Landsong.Localization;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [Serializable]
    public struct ItemFoodProfile
    {
        [SerializeField] private bool isFood;
        [SerializeField, Min(0f)] private float nutritionValue;
        [SerializeField, Range(0f, 100f)] private float dietQuality;

        public ItemFoodProfile(bool isFood, float nutritionValue, float dietQuality)
        {
            this.isFood = isFood;
            this.nutritionValue = Mathf.Max(0f, nutritionValue);
            this.dietQuality = Mathf.Clamp(dietQuality, 0f, 100f);
        }

        public bool IsFood => isFood;
        public float NutritionValue => isFood ? Mathf.Max(0f, nutritionValue) : 0f;
        public float DietQuality => isFood ? Mathf.Clamp(dietQuality, 0f, 100f) : 0f;
    }

    [CreateAssetMenu(menuName = "Landsong/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [PreviewField(72),Required(InfoMessageType.Warning)]
        [SerializeField] private Sprite icon;
     
        [SerializeField] private string itemId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
      
        [SerializeField] private ItemCategory category = ItemCategory.None;
        [SerializeField] private bool stackable = true;
        [SerializeField, Min(1)] private int maxStackSize = 99;
        [SerializeField, Min(0)] private int baseValue=1;
        [SerializeField] private string addressableKey;
        [SerializeField] private string[] tags = Array.Empty<string>();
        [SerializeField, Range(0f, 1f)] private float lossRatePerTurn;
        [SerializeField, AssetsOnly] private ItemGroupDefinition[] itemGroups =
            Array.Empty<ItemGroupDefinition>();
        [SerializeField] private ItemFoodProfile foodProfile;

        public string ItemId => itemId;
        public string DisplayName => L10n.ContentName(
            "item",
            ItemId,
            string.IsNullOrWhiteSpace(displayName) ? name : displayName);
        public string Description => L10n.ContentDescription("item", ItemId, description);
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public bool Stackable => stackable;
        public int MaxStackSize => stackable ? Mathf.Max(1, maxStackSize) : 1;
        public int BaseValue => baseValue;
        public string AddressableKey => addressableKey;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
        public float LossRatePerTurn => Mathf.Clamp01(lossRatePerTurn);
        public IReadOnlyList<ItemGroupDefinition> ItemGroups =>
            itemGroups ?? Array.Empty<ItemGroupDefinition>();
        public ItemFoodProfile FoodProfile => foodProfile;
        public bool HasIcon => icon != null;
        public bool HasAddressableKey => !string.IsNullOrWhiteSpace(addressableKey);

        public bool HasTag(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag) || tags == null)
            {
                return false;
            }

            var normalizedTag = tag.Trim();
            foreach (var itemTag in tags)
            {
                if (string.Equals(itemTag, normalizedTag, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public bool BelongsTo(ItemGroupDefinition targetGroup)
        {
            if (targetGroup == null || itemGroups == null)
            {
                return false;
            }

            for (var i = 0; i < itemGroups.Length; i++)
            {
                if (itemGroups[i] != null && itemGroups[i].IsSameOrDescendantOf(targetGroup))
                {
                    return true;
                }
            }

            return false;
        }

        public void ConfigureNumericData(
            Sprite itemIcon,
            string stableItemId,
            string itemDisplayName,
            string itemDescription,
            ItemCategory itemCategory,
            bool isStackable,
            int stackSize,
            int value,
            string itemAddressableKey,
            IEnumerable<string> itemTags,
            float turnLossRate,
            IEnumerable<ItemGroupDefinition> groups,
            ItemFoodProfile itemFoodProfile)
        {
            icon = itemIcon;
            itemId = stableItemId;
            displayName = itemDisplayName;
            description = itemDescription;
            category = itemCategory;
            stackable = isStackable;
            maxStackSize = stackSize;
            baseValue = value;
            addressableKey = itemAddressableKey;
            tags = itemTags == null ? Array.Empty<string>() : new List<string>(itemTags).ToArray();
            lossRatePerTurn = turnLossRate;
            itemGroups = groups == null
                ? Array.Empty<ItemGroupDefinition>()
                : new List<ItemGroupDefinition>(groups).ToArray();
            foodProfile = itemFoodProfile;
            OnValidate();
        }

        private void OnValidate()
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            addressableKey = string.IsNullOrWhiteSpace(addressableKey) ? string.Empty : addressableKey.Trim();
            maxStackSize = stackable ? Mathf.Max(1, maxStackSize) : 1;
            baseValue = Mathf.Max(0, baseValue);
            lossRatePerTurn = Mathf.Clamp01(lossRatePerTurn);
            itemGroups ??= Array.Empty<ItemGroupDefinition>();

            if (tags == null)
            {
                tags = Array.Empty<string>();
                return;
            }

            for (var i = 0; i < tags.Length; i++)
            {
                tags[i] = string.IsNullOrWhiteSpace(tags[i]) ? string.Empty : tags[i].Trim();
            }
        }
    }
}
