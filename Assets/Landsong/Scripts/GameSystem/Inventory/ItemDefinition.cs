using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [CreateAssetMenu(menuName = "Landsong/Inventory/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        [PreviewField(72)]
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

        public string ItemId => itemId;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public bool Stackable => stackable;
        public int MaxStackSize => stackable ? Mathf.Max(1, maxStackSize) : 1;
        public int BaseValue => baseValue;
        public string AddressableKey => addressableKey;
        public IReadOnlyList<string> Tags => tags ?? Array.Empty<string>();
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

        private void OnValidate()
        {
            itemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();
            addressableKey = string.IsNullOrWhiteSpace(addressableKey) ? string.Empty : addressableKey.Trim();
            maxStackSize = stackable ? Mathf.Max(1, maxStackSize) : 1;
            baseValue = Mathf.Max(0, baseValue);

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
