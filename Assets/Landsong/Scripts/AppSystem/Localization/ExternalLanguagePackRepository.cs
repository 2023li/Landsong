using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Landsong.Localization
{
    public sealed class ExternalLanguagePackRepository
    {
        private const int SupportedSchemaVersion = 1;
        private const long MaxManifestBytes = 64 * 1024;
        private const long MaxStringsBytes = 8 * 1024 * 1024;
        private const int MaxEntries = 50000;
        private const int MaxTextLength = 16384;

        private static readonly Regex SafePackIdPattern =
            new Regex("^[A-Za-z0-9][A-Za-z0-9._-]{1,63}$", RegexOptions.CultureInvariant);

        private static readonly Regex SafeKeyPattern =
            new Regex("^[a-z0-9][a-z0-9._-]{1,191}$", RegexOptions.CultureInvariant);

        private static readonly HashSet<string> SupportedFallbackLocales =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "en",
                "zh-Hans"
            };

        public IReadOnlyList<LanguagePackReadResult> Scan(string rootPath)
        {
            var results = new List<LanguagePackReadResult>();
            if (string.IsNullOrWhiteSpace(rootPath))
            {
                return results;
            }

            Directory.CreateDirectory(rootPath);
            var directories = Directory.GetDirectories(rootPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(directories, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < directories.Length; i++)
            {
                results.Add(Read(directories[i]));
            }

            RejectDuplicatePackIds(results);
            return results;
        }

        public LanguagePackReadResult Read(string rootPath)
        {
            var result = new LanguagePackReadResult();
            var info = new LanguagePackInfo
            {
                RootPath = rootPath ?? string.Empty,
                Source = LanguageOptionSource.ExternalDirectory,
                IsValid = false
            };
            result.Info = info;

            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.directory_missing",
                    "语言包目录不存在。"));
                return result;
            }

            var manifestPath = Path.Combine(rootPath, "manifest.json");
            var stringsPath = Path.Combine(rootPath, "strings.csv");
            if (!TryReadBoundedText(manifestPath, MaxManifestBytes, out var manifestJson, out var manifestError))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error("pack.manifest_read_failed", manifestError));
                return result;
            }

            LanguagePackManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<LanguagePackManifest>(manifestJson);
            }
            catch (Exception exception)
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.manifest_invalid_json",
                    $"manifest.json 不是有效 JSON：{exception.Message}"));
                return result;
            }

            if (manifest == null)
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.manifest_empty",
                    "manifest.json 没有可读取的对象。"));
                return result;
            }

            CopyManifest(info, manifest);
            ValidateManifest(info);

            if (!TryReadBoundedText(stringsPath, MaxStringsBytes, out var csvText, out var stringsError))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error("pack.strings_read_failed", stringsError));
                return result;
            }

            ParseStrings(csvText, result);
            info.TranslatedEntryCount = result.Entries.Count;
            if (manifest.targetKeysetVersion != LocalizationKeyset.CurrentVersion)
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Warning(
                    "pack.keyset_version_mismatch",
                    $"语言包目标 Keyset={manifest.targetKeysetVersion}，游戏 Keyset={LocalizationKeyset.CurrentVersion}。"));
            }

            info.IsValid = !info.HasErrors;
            return result;
        }

        private static void CopyManifest(LanguagePackInfo info, LanguagePackManifest manifest)
        {
            info.SchemaVersion = manifest.schemaVersion;
            info.PackId = (manifest.packId ?? string.Empty).Trim();
            info.OptionId = info.PackId;
            info.DisplayName = (manifest.displayName ?? string.Empty).Trim();
            info.LocaleCode = (manifest.localeCode ?? string.Empty).Trim();
            info.FallbackLocaleCode = (manifest.fallbackLocaleCode ?? string.Empty).Trim();
            info.Author = (manifest.author ?? string.Empty).Trim();
            info.Description = (manifest.description ?? string.Empty).Trim();
            info.TargetKeysetVersion = manifest.targetKeysetVersion;
        }

        private static void ValidateManifest(LanguagePackInfo info)
        {
            if (info.SchemaVersion != SupportedSchemaVersion)
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.schema_unsupported",
                    $"不支持 schemaVersion={info.SchemaVersion}，当前只支持 {SupportedSchemaVersion}。"));
            }

            if (!SafePackIdPattern.IsMatch(info.PackId))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.id_invalid",
                    "packId 必须是 2-64 位安全 ASCII 标识，只能包含字母、数字、点、下划线和连字符。"));
            }

            if (string.IsNullOrWhiteSpace(info.DisplayName))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.display_name_missing",
                    "displayName 不能为空。"));
            }

            if (!TryNormalizeCultureCode(info.LocaleCode, out var normalizedLocaleCode))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.locale_invalid",
                    $"localeCode '{info.LocaleCode}' 不是当前运行平台可识别的语言标签。"));
            }
            else
            {
                info.LocaleCode = normalizedLocaleCode;
            }

            if (!SupportedFallbackLocales.Contains(info.FallbackLocaleCode))
            {
                info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.fallback_invalid",
                    "fallbackLocaleCode 首版只允许 en 或 zh-Hans。"));
            }
        }

        private static bool TryNormalizeCultureCode(string localeCode, out string normalized)
        {
            normalized = string.Empty;
            if (string.IsNullOrWhiteSpace(localeCode))
            {
                return false;
            }

            try
            {
                normalized = CultureInfo.GetCultureInfo(localeCode.Trim()).Name;
                return !string.IsNullOrWhiteSpace(normalized);
            }
            catch (CultureNotFoundException)
            {
                return false;
            }
        }

        private static void ParseStrings(string csvText, LanguagePackReadResult result)
        {
            List<CsvRow> rows;
            try
            {
                rows = CsvDocumentParser.Parse(csvText ?? string.Empty);
            }
            catch (FormatException exception)
            {
                result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.csv_invalid",
                    exception.Message));
                return;
            }

            if (rows.Count <= 0)
            {
                result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.csv_empty",
                    "strings.csv 为空。"));
                return;
            }

            var header = rows[0];
            var tableIndex = FindColumn(header, "Table");
            var keyIndex = FindColumn(header, "Key");
            var textIndex = FindColumn(header, "Text");
            if (tableIndex < 0 || keyIndex < 0 || textIndex < 0)
            {
                result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.csv_header_invalid",
                    "strings.csv 必须包含 Table、Key、Text 三列。",
                    header.Line));
                return;
            }

            var uniqueKeys = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 1; i < rows.Count; i++)
            {
                if (result.Entries.Count >= MaxEntries)
                {
                    result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                        "pack.entry_limit",
                        $"语言包条目数超过上限 {MaxEntries}。",
                        rows[i].Line));
                    return;
                }

                var row = rows[i];
                if (row.IsBlank)
                {
                    continue;
                }

                if (row.Values.Count <= Math.Max(tableIndex, Math.Max(keyIndex, textIndex)))
                {
                    result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                        "pack.csv_row_short",
                        "CSV 行缺少必要列。",
                        row.Line));
                    continue;
                }

                var tableName = row.Values[tableIndex].Trim();
                var key = row.Values[keyIndex].Trim();
                var text = row.Values[textIndex];
                if (!LocalizationTables.IsKnown(tableName))
                {
                    result.Info.Diagnostics.Add(LocalizationDiagnostic.Warning(
                        "pack.table_unknown",
                        $"未知 Table '{tableName}'，该条目不会生效。",
                        row.Line));
                    continue;
                }

                if (!SafeKeyPattern.IsMatch(key))
                {
                    result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                        "pack.key_invalid",
                        $"Key '{key}' 不是合法的稳定 Key。",
                        row.Line));
                    continue;
                }

                if (text.Length > MaxTextLength)
                {
                    result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                        "pack.text_too_long",
                        $"文本长度超过上限 {MaxTextLength}。",
                        row.Line));
                    continue;
                }

                var compositeKey = $"{tableName}\n{key}";
                if (!uniqueKeys.Add(compositeKey))
                {
                    result.Info.Diagnostics.Add(LocalizationDiagnostic.Error(
                        "pack.key_duplicate",
                        $"重复条目 {tableName}/{key}。",
                        row.Line));
                    continue;
                }

                result.Entries.Add(new ExternalLanguageTextEntry
                {
                    TableName = tableName,
                    Key = key,
                    Text = text,
                    SourceLine = row.Line
                });
            }
        }

        private static int FindColumn(CsvRow row, string name)
        {
            for (var i = 0; i < row.Values.Count; i++)
            {
                if (string.Equals(row.Values[i].Trim(), name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool TryReadBoundedText(
            string filePath,
            long maxBytes,
            out string text,
            out string error)
        {
            text = string.Empty;
            error = string.Empty;
            try
            {
                if (!File.Exists(filePath))
                {
                    error = $"缺少文件：{filePath}";
                    return false;
                }

                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Length > maxBytes)
                {
                    error = $"文件超过大小限制 {maxBytes} bytes：{filePath}";
                    return false;
                }

                text = File.ReadAllText(filePath, new UTF8Encoding(false, true));
                return true;
            }
            catch (Exception exception)
            {
                error = $"读取文件失败：{filePath}\n{exception.Message}";
                return false;
            }
        }

        private static void RejectDuplicatePackIds(List<LanguagePackReadResult> results)
        {
            var byPackId = new Dictionary<string, List<LanguagePackInfo>>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < results.Count; i++)
            {
                var info = results[i].Info;
                if (info == null || string.IsNullOrWhiteSpace(info.PackId))
                {
                    continue;
                }

                if (!byPackId.TryGetValue(info.PackId, out var list))
                {
                    list = new List<LanguagePackInfo>();
                    byPackId.Add(info.PackId, list);
                }

                list.Add(info);
            }

            foreach (var pair in byPackId)
            {
                if (pair.Value.Count <= 1)
                {
                    continue;
                }

                for (var i = 0; i < pair.Value.Count; i++)
                {
                    pair.Value[i].Diagnostics.Add(LocalizationDiagnostic.Error(
                        "pack.id_duplicate",
                        $"发现多个 packId='{pair.Key}' 的语言包，所有冲突包均已禁用。"));
                    pair.Value[i].IsValid = false;
                }
            }
        }

        private sealed class CsvRow
        {
            public int Line;
            public readonly List<string> Values = new List<string>();

            public bool IsBlank
            {
                get
                {
                    for (var i = 0; i < Values.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(Values[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        private static class CsvDocumentParser
        {
            public static List<CsvRow> Parse(string text)
            {
                var rows = new List<CsvRow>();
                var row = NewRow(1);
                var field = new StringBuilder();
                var insideQuotes = false;
                var line = 1;

                for (var i = 0; i < text.Length; i++)
                {
                    var character = text[i];
                    if (insideQuotes)
                    {
                        if (character == '"')
                        {
                            if (i + 1 < text.Length && text[i + 1] == '"')
                            {
                                field.Append('"');
                                i++;
                            }
                            else
                            {
                                insideQuotes = false;
                            }
                        }
                        else
                        {
                            field.Append(character);
                            if (character == '\n')
                            {
                                line++;
                            }
                        }

                        continue;
                    }

                    switch (character)
                    {
                        case '"':
                            if (field.Length != 0)
                            {
                                throw new FormatException($"CSV 第 {line} 行的引号位置无效。");
                            }

                            insideQuotes = true;
                            break;

                        case ',':
                            row.Values.Add(field.ToString());
                            field.Clear();
                            break;

                        case '\r':
                            if (i + 1 < text.Length && text[i + 1] == '\n')
                            {
                                i++;
                            }

                            FinishRow(rows, row, field);
                            line++;
                            row = NewRow(line);
                            break;

                        case '\n':
                            FinishRow(rows, row, field);
                            line++;
                            row = NewRow(line);
                            break;

                        default:
                            field.Append(character);
                            break;
                    }
                }

                if (insideQuotes)
                {
                    throw new FormatException($"CSV 在第 {row.Line} 行开始的引号字段没有闭合。");
                }

                if (field.Length > 0 || row.Values.Count > 0)
                {
                    FinishRow(rows, row, field);
                }

                return rows;
            }

            private static CsvRow NewRow(int line)
            {
                return new CsvRow { Line = line };
            }

            private static void FinishRow(List<CsvRow> rows, CsvRow row, StringBuilder field)
            {
                row.Values.Add(field.ToString());
                field.Clear();
                rows.Add(row);
            }
        }
    }
}
