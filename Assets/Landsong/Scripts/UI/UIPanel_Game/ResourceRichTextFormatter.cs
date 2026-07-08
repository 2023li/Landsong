using Landsong.InventorySystem;
using TMPro;

namespace Landsong.UISystem
{
    public static class ResourceRichTextFormatter
    {
        public static void ApplySpriteAsset(TMP_Text target)
        {
            ApplySpriteAsset(target, ResourceRichTextSettings.GlobalSpriteAsset);
        }

        public static void ApplySpriteAsset(TMP_Text target, TMP_SpriteAsset spriteAsset)
        {
            if (target == null)
            {
                return;
            }

            target.richText = true;
            if (spriteAsset != null)
            {
                target.spriteAsset = spriteAsset;
            }
        }

        public static string FormatResourceName(
            ItemDefinition definition,
            string itemId,
            string fallbackDisplayName)
        {
            return FormatResourceName(
                definition,
                itemId,
                fallbackDisplayName,
                ResourceRichTextSettings.GlobalSpriteAsset,
                ResourceRichTextSettings.GlobalShowNameAfterIcon);
        }

        public static string FormatResourceName(
            ItemDefinition definition,
            string itemId,
            string fallbackDisplayName,
            TMP_SpriteAsset spriteAsset,
            bool showNameAfterIcon)
        {
            var resolvedItemId = ResolveItemId(definition, itemId);
            var displayName = ResolveDisplayName(definition, fallbackDisplayName, resolvedItemId);
            if (HasSprite(spriteAsset, resolvedItemId))
            {
                var icon = $"<sprite name=\"{EscapeTagAttribute(resolvedItemId)}\">";
                return showNameAfterIcon && !string.IsNullOrWhiteSpace(displayName)
                    ? $"{icon} {EscapeText(displayName)}"
                    : icon;
            }

            return EscapeText(displayName);
        }

        public static string FormatProgress(
            ItemDefinition definition,
            string itemId,
            string fallbackDisplayName,
            int currentAmount,
            int requiredAmount,
            int inventoryAmount,
            bool includeInventory)
        {
            return FormatProgress(
                definition,
                itemId,
                fallbackDisplayName,
                currentAmount,
                requiredAmount,
                inventoryAmount,
                includeInventory,
                ResourceRichTextSettings.GlobalSpriteAsset,
                ResourceRichTextSettings.GlobalShowNameAfterIcon);
        }

        public static string FormatAmount(
            ItemDefinition definition,
            string itemId,
            string fallbackDisplayName,
            int amount)
        {
            return FormatAmount(
                definition,
                itemId,
                fallbackDisplayName,
                amount,
                ResourceRichTextSettings.GlobalSpriteAsset,
                ResourceRichTextSettings.GlobalShowNameAfterIcon);
        }

        public static string FormatAmount(
            ItemDefinition definition,
            string itemId,
            string fallbackDisplayName,
            int amount,
            TMP_SpriteAsset spriteAsset,
            bool showNameAfterIcon)
        {
            var resourceName = FormatResourceName(
                definition,
                itemId,
                fallbackDisplayName,
                spriteAsset,
                showNameAfterIcon);
            return $"{resourceName} x{amount}";
        }

        public static string FormatProgress(
            ItemDefinition definition,
            string itemId,
            string fallbackDisplayName,
            int currentAmount,
            int requiredAmount,
            int inventoryAmount,
            bool includeInventory,
            TMP_SpriteAsset spriteAsset,
            bool showNameAfterIcon)
        {
            var resourceName = FormatResourceName(
                definition,
                itemId,
                fallbackDisplayName,
                spriteAsset,
                showNameAfterIcon);
            var text = $"{resourceName} {currentAmount}/{requiredAmount}";
            return includeInventory ? $"{text}  库存 {inventoryAmount}" : text;
        }

        private static bool HasSprite(TMP_SpriteAsset spriteAsset, string spriteName)
        {
            return spriteAsset != null
                   && !string.IsNullOrWhiteSpace(spriteName)
                   && spriteAsset.GetSpriteIndexFromName(spriteName) >= 0;
        }

        private static string ResolveItemId(ItemDefinition definition, string itemId)
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.ItemId))
            {
                return definition.ItemId.Trim();
            }

            return string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
        }

        private static string ResolveDisplayName(
            ItemDefinition definition,
            string fallbackDisplayName,
            string fallbackItemId)
        {
            if (definition != null && !string.IsNullOrWhiteSpace(definition.DisplayName))
            {
                return definition.DisplayName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fallbackDisplayName))
            {
                return fallbackDisplayName.Trim();
            }

            return string.IsNullOrWhiteSpace(fallbackItemId) ? string.Empty : fallbackItemId.Trim();
        }

        private static string EscapeText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }

        private static string EscapeTagAttribute(string text)
        {
            return string.IsNullOrEmpty(text) ? string.Empty : text.Replace("\"", string.Empty);
        }
    }
}
