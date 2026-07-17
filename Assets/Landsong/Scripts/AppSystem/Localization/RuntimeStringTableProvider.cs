using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Landsong.Localization
{
    public sealed class RuntimeStringTableProvider : ITableProvider
    {
        private readonly Dictionary<string, StringTable> runtimeTables =
            new Dictionary<string, StringTable>(StringComparer.OrdinalIgnoreCase);

        public bool HasTables => runtimeTables.Count > 0;

        public void SetTable(string tableCollectionName, Locale locale, StringTable table)
        {
            if (string.IsNullOrWhiteSpace(tableCollectionName) || locale == null || table == null)
            {
                return;
            }

            var key = MakeKey(tableCollectionName, locale.Identifier.Code);
            if (runtimeTables.TryGetValue(key, out var oldTable) && oldTable != table)
            {
                DestroyRuntimeTable(oldTable);
            }

            runtimeTables[key] = table;
        }

        public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(
            string tableCollectionName,
            Locale locale) where TTable : LocalizationTable
        {
            if (typeof(TTable) != typeof(StringTable) || locale == null)
            {
                return default;
            }

            var key = MakeKey(tableCollectionName, locale.Identifier.Code);
            if (!runtimeTables.TryGetValue(key, out var table) || table == null)
            {
                return default;
            }

            return Addressables.ResourceManager.CreateCompletedOperation(table as TTable, null);
        }

        public void Clear()
        {
            foreach (var table in runtimeTables.Values)
            {
                DestroyRuntimeTable(table);
            }

            runtimeTables.Clear();
        }

        public static StringTable CloneForLocale(
            string tableCollectionName,
            Locale targetLocale,
            StringTable sourceTable)
        {
            if (targetLocale == null || sourceTable == null)
            {
                return null;
            }

            var runtimeTable = UnityEngine.Object.Instantiate(sourceTable);
            runtimeTable.SharedData = UnityEngine.Object.Instantiate(sourceTable.SharedData);
            runtimeTable.name = $"{tableCollectionName}_{targetLocale.Identifier.Code}_Runtime";
            runtimeTable.LocaleIdentifier = targetLocale.Identifier;
            runtimeTable.hideFlags = HideFlags.DontSave;
            runtimeTable.SharedData.TableCollectionName = tableCollectionName;
            runtimeTable.SharedData.hideFlags = HideFlags.DontSave;
            return runtimeTable;
        }

        private static void DestroyRuntimeTable(StringTable table)
        {
            if (table == null)
            {
                return;
            }

            var sharedData = table.SharedData;
            if (Application.isPlaying)
            {
                if (sharedData != null)
                {
                    UnityEngine.Object.Destroy(sharedData);
                }

                UnityEngine.Object.Destroy(table);
            }
            else
            {
                if (sharedData != null)
                {
                    UnityEngine.Object.DestroyImmediate(sharedData);
                }

                UnityEngine.Object.DestroyImmediate(table);
            }
        }

        private static string MakeKey(string tableCollectionName, string localeCode)
        {
            return $"{tableCollectionName}||{localeCode}";
        }
    }
}
