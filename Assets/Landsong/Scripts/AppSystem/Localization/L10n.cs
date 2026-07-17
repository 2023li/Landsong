using System;
using System.Globalization;
using UnityEngine.Localization.Settings;

namespace Landsong.Localization
{
    public static class L10n
    {
        public static event Action LanguageChanged;

        public static void NotifyLanguageChanged()
        {
            LanguageChanged?.Invoke();
        }

        public static string Ui(string key, string fallback = "", params object[] arguments)
        {
            return Get(LocalizationTables.Ui, key, fallback, arguments);
        }

        public static string Content(string key, string fallback = "", params object[] arguments)
        {
            return Get(LocalizationTables.Content, key, fallback, arguments);
        }

        public static string Gameplay(string key, string fallback = "", params object[] arguments)
        {
            return Get(LocalizationTables.Gameplay, key, fallback, arguments);
        }

        public static string ContentName(string contentType, string stableId, string fallback)
        {
            return Content(BuildContentKey(contentType, stableId, "name"), fallback);
        }

        public static string ContentDescription(string contentType, string stableId, string fallback)
        {
            return Content(BuildContentKey(contentType, stableId, "description"), fallback);
        }

        public static string BuildContentKey(string contentType, string stableId, string field)
        {
            return $"content.{NormalizeKeyPart(contentType)}.{NormalizeKeyPart(stableId)}.{NormalizeKeyPart(field)}";
        }

        public static string Get(
            string tableName,
            string key,
            string fallback = "",
            params object[] arguments)
        {
            if (!string.IsNullOrWhiteSpace(tableName) && !string.IsNullOrWhiteSpace(key))
            {
                try
                {
                    var database = LocalizationSettings.StringDatabase;
                    if (database != null && LocalizationSettings.InitializationOperation.IsDone)
                    {
                        var localized = database.GetLocalizedString(tableName, key, arguments);
                        if (!string.IsNullOrEmpty(localized) && !LooksLikeMissingTranslation(localized, key))
                        {
                            return localized;
                        }
                    }
                }
                catch (Exception)
                {
                    // The source-language fallback keeps UI usable during boot and asset authoring.
                }
            }

            return FormatFallback(fallback, arguments);
        }

        public static string NormalizeKeyPart(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unknown";
            }

            var source = value.Trim().ToLowerInvariant();
            var buffer = new char[source.Length];
            var length = 0;
            var previousWasSeparator = false;
            var containsUnicodeIdentifierCharacter = false;
            for (var i = 0; i < source.Length; i++)
            {
                var character = source[i];
                var isAllowed = character is >= 'a' and <= 'z'
                                || character is >= '0' and <= '9';
                if (isAllowed)
                {
                    buffer[length++] = character;
                    previousWasSeparator = false;
                    continue;
                }

                if (char.IsLetterOrDigit(character))
                {
                    containsUnicodeIdentifierCharacter = true;
                }

                if (!previousWasSeparator && length > 0)
                {
                    buffer[length++] = '_';
                    previousWasSeparator = true;
                }
            }

            while (length > 0 && buffer[length - 1] == '_')
            {
                length--;
            }

            var normalized = length <= 0 ? string.Empty : new string(buffer, 0, length);
            if (containsUnicodeIdentifierCharacter)
            {
                var suffix = $"u{StableHash(source):x8}";
                normalized = string.IsNullOrWhiteSpace(normalized) ? suffix : $"{normalized}_{suffix}";
            }

            return string.IsNullOrWhiteSpace(normalized) ? "unknown" : normalized;
        }

        private static uint StableHash(string value)
        {
            unchecked
            {
                var hash = 2166136261u;
                var source = value ?? string.Empty;
                for (var i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static string FormatFallback(string fallback, object[] arguments)
        {
            var source = fallback ?? string.Empty;
            if (arguments == null || arguments.Length <= 0 || source.Length <= 0)
            {
                return source;
            }

            try
            {
                var culture = LocalizationSettings.SelectedLocale?.Identifier.CultureInfo
                              ?? CultureInfo.InvariantCulture;
                return string.Format(culture, source, arguments);
            }
            catch (FormatException)
            {
                return source;
            }
        }

        private static bool LooksLikeMissingTranslation(string localized, string key)
        {
            return localized.IndexOf("No translation found", StringComparison.OrdinalIgnoreCase) >= 0
                   && localized.IndexOf(key, StringComparison.Ordinal) >= 0;
        }
    }
}
