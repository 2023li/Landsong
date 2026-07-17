using System;
using Landsong.Localization;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.InventorySystem
{
    [CreateAssetMenu(menuName = "Landsong/Inventory/Item Group", fileName = "ItemGroupDefinition")]
    public sealed class ItemGroupDefinition : ScriptableObject
    {
        [SerializeField] private string groupId;
        [SerializeField] private string displayName;
        [SerializeField, TextArea] private string description;
        [SerializeField, PreviewField(48), AssetsOnly] private Sprite icon;
        [SerializeField, AssetsOnly] private ItemGroupDefinition parentGroup;

        public string GroupId => string.IsNullOrWhiteSpace(groupId) ? string.Empty : groupId.Trim();
        public string DisplayName => L10n.ContentName(
            "item_group",
            GroupId,
            string.IsNullOrWhiteSpace(displayName) ? name : displayName.Trim());
        public string Description => L10n.ContentDescription("item_group", GroupId, description);
        public Sprite Icon => icon;
        public ItemGroupDefinition ParentGroup => parentGroup;
        public bool IsValid => !string.IsNullOrWhiteSpace(GroupId);

        public bool IsSameOrDescendantOf(ItemGroupDefinition target)
        {
            if (target == null)
            {
                return false;
            }

            var current = this;
            var guard = 0;
            while (current != null && guard++ < 64)
            {
                if (ReferenceEquals(current, target)
                    || string.Equals(current.GroupId, target.GroupId, StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.parentGroup;
            }

            return false;
        }

        public void Configure(
            string stableGroupId,
            string groupDisplayName,
            string groupDescription,
            Sprite groupIcon,
            ItemGroupDefinition parent)
        {
            groupId = stableGroupId;
            displayName = groupDisplayName;
            description = groupDescription;
            icon = groupIcon;
            parentGroup = parent;
            OnValidate();
        }

        private void OnValidate()
        {
            groupId = string.IsNullOrWhiteSpace(groupId) ? string.Empty : groupId.Trim();
            displayName = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim();
            description = string.IsNullOrWhiteSpace(description) ? string.Empty : description.Trim();

            if (ReferenceEquals(parentGroup, this))
            {
                parentGroup = null;
            }
        }
    }
}
