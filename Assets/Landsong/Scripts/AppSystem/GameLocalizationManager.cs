using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Landsong.Localization;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;

public sealed class GameLocalizationManager : MonoSingleton<GameLocalizationManager>
{
    public const string DefaultBuiltInLocaleCode = "en";

    private readonly ExternalLanguagePackRepository externalRepository =
        new ExternalLanguagePackRepository();

    private readonly RuntimeStringTableProvider runtimeTableProvider =
        new RuntimeStringTableProvider();

    private readonly List<LanguagePackInfo> allLanguageOptions = new List<LanguagePackInfo>();
    private readonly List<LanguagePackInfo> externalLanguageOptions = new List<LanguagePackInfo>();
    private readonly List<LanguagePackInfo> invalidExternalLanguagePacks = new List<LanguagePackInfo>();
    private readonly Dictionary<string, LanguagePackReadResult> externalPacksById =
        new Dictionary<string, LanguagePackReadResult>(StringComparer.OrdinalIgnoreCase);

    private ITableProvider defaultTableProvider;
    private ITablePostprocessor defaultTablePostprocessor;
    private Locale runtimeLocale;
    private bool initialized;
    private bool applying;
    private int currentLanguageOptionIndex;

    public event Action<LocalizationSaveData> OnLanguageChanged;

    public IReadOnlyList<LanguagePackInfo> AllLanguagePacks => allLanguageOptions;
    public IReadOnlyList<LanguagePackInfo> ExternalLanguagePacks => externalLanguageOptions;
    public IReadOnlyList<LanguagePackInfo> InvalidExternalLanguagePacks => invalidExternalLanguagePacks;
    public int CurrentLanguagePackIndex => currentLanguageOptionIndex;
    public int AllLanguagePackCount => allLanguageOptions.Count;
    public bool IsInitialized => initialized;
    public bool IsApplying => applying;
    public string ExternalLanguagePacksFolderPath => IOManager.Instance.ExternalLanguagePacksFolderPath;

    public LocalizationSaveData CurrentLanguageData
    {
        get
        {
            DataManager.Instance.EnsureAppDataLoaded();
            DataManager.Instance.AppData.Language ??= LocalizationSaveData.CreateDefault();
            DataManager.Instance.AppData.Language.Validate();
            return DataManager.Instance.AppData.Language;
        }
    }

    public LanguagePackInfo CurrentLanguageOption
    {
        get
        {
            EnsureLanguageOptionsReady();
            var optionId = ResolveSavedOptionId();
            return FindOption(optionId)?.Clone();
        }
    }

    public void Initialize()
    {
        IOManager.Instance.EnsureExternalLanguagePackFolder();
        RefreshAllLanguagePacksCache();
        StartCoroutine(InitializeAndApply());
    }

    private IEnumerator InitializeAndApply()
    {
        yield return LocalizationSettings.InitializationOperation;
        CacheUnityLocalizationExtensions();
        ValidateSavedSelection();
        yield return ApplyToUnityLocalization();
        initialized = true;
    }

    private void OnDestroy()
    {
        RestoreDefaultTableExtensions();
        RemoveRuntimeLocale();
        runtimeTableProvider.Clear();
    }

    public void RefreshAllLanguagePacksCache()
    {
        allLanguageOptions.Clear();
        externalLanguageOptions.Clear();
        invalidExternalLanguagePacks.Clear();
        externalPacksById.Clear();

        allLanguageOptions.Add(LanguagePackInfo.BuiltIn("zh-Hans", "简体中文"));
        allLanguageOptions.Add(LanguagePackInfo.BuiltIn("en", "English"));

        var scanResults = externalRepository.Scan(ExternalLanguagePacksFolderPath);
        for (var i = 0; i < scanResults.Count; i++)
        {
            var result = scanResults[i];
            var info = result.Info;
            if (info == null)
            {
                continue;
            }

            if (!result.Success)
            {
                invalidExternalLanguagePacks.Add(info.Clone());
                LogPackDiagnostics(info);
                continue;
            }

            externalPacksById.Add(info.PackId, result);
            externalLanguageOptions.Add(info.Clone());
            allLanguageOptions.Add(info.Clone());
            LogPackDiagnostics(info);
        }

        externalLanguageOptions.Sort(CompareLanguageOptions);
        allLanguageOptions.Sort((left, right) =>
        {
            if (left.Source != right.Source)
            {
                return left.Source.CompareTo(right.Source);
            }

            return CompareLanguageOptions(left, right);
        });
        SyncCurrentLanguagePackIndexFromSaveData();
    }

    public List<LanguagePackInfo> GetAllLanguagePacks()
    {
        EnsureLanguageOptionsReady();
        var result = new List<LanguagePackInfo>(allLanguageOptions.Count);
        for (var i = 0; i < allLanguageOptions.Count; i++)
        {
            result.Add(allLanguageOptions[i].Clone());
        }

        return result;
    }

    public LanguagePackInfo GetCurrentPreviewLanguagePack()
    {
        EnsureLanguageOptionsReady();
        if (allLanguageOptions.Count <= 0)
        {
            return null;
        }

        currentLanguageOptionIndex = Mathf.Clamp(
            currentLanguageOptionIndex,
            0,
            allLanguageOptions.Count - 1);
        return allLanguageOptions[currentLanguageOptionIndex].Clone();
    }

    public bool SetCurrentLanguagePackIndex(int index)
    {
        EnsureLanguageOptionsReady();
        if (index < 0 || index >= allLanguageOptions.Count)
        {
            return false;
        }

        currentLanguageOptionIndex = index;
        return true;
    }

    public bool SelectPreviousLanguagePack()
    {
        EnsureLanguageOptionsReady();
        if (allLanguageOptions.Count <= 0)
        {
            return false;
        }

        currentLanguageOptionIndex =
            (currentLanguageOptionIndex - 1 + allLanguageOptions.Count) % allLanguageOptions.Count;
        return true;
    }

    public bool SelectNextLanguagePack()
    {
        EnsureLanguageOptionsReady();
        if (allLanguageOptions.Count <= 0)
        {
            return false;
        }

        currentLanguageOptionIndex = (currentLanguageOptionIndex + 1) % allLanguageOptions.Count;
        return true;
    }

    public bool SetUseSystemLanguage()
    {
        var data = CurrentLanguageData;
        data.UseSystemLanguage = true;
        data.SelectedLanguageOptionId = string.Empty;
        DataManager.Instance.SaveAppData();
        SyncCurrentLanguagePackIndexFromSaveData();
        return true;
    }

    public IEnumerator SetUseSystemLanguageAndApply()
    {
        SetUseSystemLanguage();
        yield return ApplyToUnityLocalization();
    }

    public bool SetLanguage(LanguagePackInfo option)
    {
        if (option == null || string.IsNullOrWhiteSpace(option.OptionId))
        {
            return false;
        }

        var installedOption = FindOption(option.OptionId);
        if (installedOption == null || !installedOption.IsValid)
        {
            return false;
        }

        var data = CurrentLanguageData;
        data.UseSystemLanguage = false;
        data.SelectedLanguageOptionId = installedOption.OptionId;
        DataManager.Instance.SaveAppData();
        SyncCurrentLanguagePackIndexFromSaveData();
        return true;
    }

    public IEnumerator SetLanguageAndApply(LanguagePackInfo option)
    {
        if (!SetLanguage(option))
        {
            yield break;
        }

        yield return ApplyToUnityLocalization();
    }

    public IEnumerator ApplyCurrentPreviewLanguagePack()
    {
        var option = GetCurrentPreviewLanguagePack();
        if (option == null)
        {
            yield break;
        }

        yield return SetLanguageAndApply(option);
    }

    public void SyncCurrentLanguagePackIndexFromSaveData()
    {
        if (allLanguageOptions.Count <= 0)
        {
            currentLanguageOptionIndex = 0;
            return;
        }

        var optionId = ResolveSavedOptionId();
        for (var i = 0; i < allLanguageOptions.Count; i++)
        {
            if (string.Equals(allLanguageOptions[i].OptionId, optionId, StringComparison.OrdinalIgnoreCase))
            {
                currentLanguageOptionIndex = i;
                return;
            }
        }

        currentLanguageOptionIndex = FindBuiltInIndex(DefaultBuiltInLocaleCode);
    }

    public IEnumerator ApplyToUnityLocalization()
    {
        if (applying)
        {
            Debug.LogWarning("本地化切换正在进行，忽略重复请求。");
            yield break;
        }

        applying = true;
        yield return LocalizationSettings.InitializationOperation;
        CacheUnityLocalizationExtensions();
        ValidateSavedSelection();

        var oldLocale = LocalizationSettings.SelectedLocale;
        RestoreDefaultTableExtensions();
        if (oldLocale != null)
        {
            LocalizationSettings.StringDatabase.ReleaseAllTables(oldLocale);
        }

        RemoveRuntimeLocale();
        runtimeTableProvider.Clear();

        var selectedOption = ResolveSelectedOption();
        Locale targetLocale;
        if (selectedOption.Source == LanguageOptionSource.ExternalDirectory)
        {
            yield return BuildExternalLanguageTables(selectedOption);
            targetLocale = runtimeLocale;
            if (targetLocale == null || !runtimeTableProvider.HasTables)
            {
                Debug.LogError($"外部语言包应用失败，已回退到 {DefaultBuiltInLocaleCode}：{selectedOption.PackId}");
                SelectBuiltInFallbackAndSave();
                selectedOption = ResolveSelectedOption();
                targetLocale = GetBuiltInLocale(selectedOption.LocaleCode) ?? GetDefaultBuiltInLocale();
            }
            else
            {
                LocalizationSettings.StringDatabase.TableProvider = runtimeTableProvider;
                LocalizationSettings.StringDatabase.TablePostprocessor = defaultTablePostprocessor;
            }
        }
        else
        {
            targetLocale = GetBuiltInLocale(selectedOption.LocaleCode) ?? GetDefaultBuiltInLocale();
        }

        if (targetLocale == null)
        {
            applying = false;
            Debug.LogError("应用本地化失败：项目中没有可用的内置 Locale。");
            yield break;
        }

        LocalizationSettings.StringDatabase.ReleaseAllTables(targetLocale);
        LocalizationSettings.SelectedLocale = targetLocale;
        yield return null;

        applying = false;
        L10n.NotifyLanguageChanged();
        OnLanguageChanged?.Invoke(CurrentLanguageData);
    }

    private IEnumerator BuildExternalLanguageTables(LanguagePackInfo option)
    {
        if (!externalPacksById.TryGetValue(option.PackId, out var packResult) || !packResult.Success)
        {
            yield break;
        }

        var fallbackLocale = GetBuiltInLocale(option.FallbackLocaleCode);
        if (fallbackLocale == null)
        {
            yield break;
        }

        runtimeLocale = GetBuiltInLocale(option.LocaleCode);
        if (runtimeLocale == null)
        {
            runtimeLocale = Locale.CreateLocale(option.LocaleCode);
            runtimeLocale.name = $"Runtime Locale ({option.LocaleCode})";
            runtimeLocale.hideFlags = HideFlags.DontSave;
            runtimeLocale.Metadata.AddMetadata(new FallbackLocale(fallbackLocale));
            LocalizationSettings.AvailableLocales.AddLocale(runtimeLocale);
        }

        var entriesByTable = GroupEntriesByTable(packResult.Entries);
        option.MissingKeyCount = 0;
        option.UnknownKeyCount = 0;
        for (var i = 0; i < LocalizationTables.All.Length; i++)
        {
            var tableName = LocalizationTables.All[i];
            var operation = LocalizationSettings.StringDatabase.GetTableAsync(tableName, fallbackLocale);
            yield return operation;
            if (operation.Status != AsyncOperationStatus.Succeeded || operation.Result == null)
            {
                Debug.LogError($"外部语言包回退表加载失败：Table={tableName}, Locale={fallbackLocale.Identifier.Code}");
                continue;
            }

            var runtimeTable = RuntimeStringTableProvider.CloneForLocale(
                tableName,
                runtimeLocale,
                operation.Result);
            if (runtimeTable == null)
            {
                continue;
            }

            if (entriesByTable.TryGetValue(tableName, out var entries))
            {
                ApplyExternalEntries(option, runtimeTable, entries);
                option.MissingKeyCount += Mathf.Max(0, runtimeTable.Count - entries.Count);
            }
            else
            {
                option.MissingKeyCount += runtimeTable.Count;
            }

            runtimeTableProvider.SetTable(tableName, runtimeLocale, runtimeTable);
        }

        if (option.HasErrors)
        {
            runtimeTableProvider.Clear();
            RemoveRuntimeLocale();
        }
    }

    private static Dictionary<string, List<ExternalLanguageTextEntry>> GroupEntriesByTable(
        List<ExternalLanguageTextEntry> entries)
    {
        var result = new Dictionary<string, List<ExternalLanguageTextEntry>>(StringComparer.Ordinal);
        if (entries == null)
        {
            return result;
        }

        for (var i = 0; i < entries.Count; i++)
        {
            var entry = entries[i];
            if (entry == null || !LocalizationTables.IsKnown(entry.TableName))
            {
                continue;
            }

            if (!result.TryGetValue(entry.TableName, out var tableEntries))
            {
                tableEntries = new List<ExternalLanguageTextEntry>();
                result.Add(entry.TableName, tableEntries);
            }

            tableEntries.Add(entry);
        }

        return result;
    }

    private static void ApplyExternalEntries(
        LanguagePackInfo option,
        StringTable runtimeTable,
        List<ExternalLanguageTextEntry> entries)
    {
        for (var i = 0; i < entries.Count; i++)
        {
            var patch = entries[i];
            var tableEntry = runtimeTable.GetEntry(patch.Key);
            if (tableEntry == null)
            {
                option.UnknownKeyCount++;
                option.Diagnostics.Add(LocalizationDiagnostic.Warning(
                    "pack.key_unknown",
                    $"未知 Key：{patch.TableName}/{patch.Key}",
                    patch.SourceLine));
                Debug.LogWarning(
                    $"语言包包含未知 Key，已忽略：Pack={option.PackId}, Table={patch.TableName}, Key={patch.Key}, Line={patch.SourceLine}");
                continue;
            }

            if (tableEntry.IsSmart && !HaveMatchingSmartArguments(tableEntry.Value, patch.Text))
            {
                option.Diagnostics.Add(LocalizationDiagnostic.Error(
                    "pack.smart_arguments_mismatch",
                    $"Smart String 参数与内置条目不一致：{patch.TableName}/{patch.Key}",
                    patch.SourceLine));
                continue;
            }

            tableEntry.Value = patch.Text ?? string.Empty;
        }
    }

    private static bool HaveMatchingSmartArguments(string baseline, string candidate)
    {
        return ExtractSmartArguments(baseline).SetEquals(ExtractSmartArguments(candidate));
    }

    private static HashSet<string> ExtractSmartArguments(string value)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        var matches = Regex.Matches(value ?? string.Empty, @"(?<!\{)\{([A-Za-z0-9_]+)(?:[^}]*)\}(?!\})");
        for (var i = 0; i < matches.Count; i++)
        {
            result.Add(matches[i].Groups[1].Value);
        }

        return result;
    }

    private void ValidateSavedSelection()
    {
        var data = CurrentLanguageData;
        if (data.UseSystemLanguage)
        {
            return;
        }

        if (FindOption(data.SelectedLanguageOptionId) != null)
        {
            return;
        }

        SelectBuiltInFallbackAndSave();
    }

    private void SelectBuiltInFallbackAndSave()
    {
        var data = CurrentLanguageData;
        data.UseSystemLanguage = false;
        data.SelectedLanguageOptionId = $"builtin.{DefaultBuiltInLocaleCode}";
        DataManager.Instance.SaveAppData();
        SyncCurrentLanguagePackIndexFromSaveData();
    }

    private LanguagePackInfo ResolveSelectedOption()
    {
        var data = CurrentLanguageData;
        if (!data.UseSystemLanguage)
        {
            return FindOption(data.SelectedLanguageOptionId)
                   ?? FindOption($"builtin.{DefaultBuiltInLocaleCode}");
        }

        var systemLocale = LocalizationSettings.AvailableLocales.GetLocale(Application.systemLanguage);
        if (systemLocale != null)
        {
            var systemOption = FindOption($"builtin.{systemLocale.Identifier.Code}");
            if (systemOption != null && systemOption.Source == LanguageOptionSource.BuiltIn)
            {
                return systemOption;
            }
        }

        return FindOption($"builtin.{DefaultBuiltInLocaleCode}") ?? allLanguageOptions[0];
    }

    private string ResolveSavedOptionId()
    {
        var data = CurrentLanguageData;
        return data.UseSystemLanguage
            ? ResolveSelectedOption()?.OptionId ?? $"builtin.{DefaultBuiltInLocaleCode}"
            : data.SelectedLanguageOptionId;
    }

    private LanguagePackInfo FindOption(string optionId)
    {
        if (string.IsNullOrWhiteSpace(optionId))
        {
            return null;
        }

        for (var i = 0; i < allLanguageOptions.Count; i++)
        {
            if (string.Equals(allLanguageOptions[i].OptionId, optionId, StringComparison.OrdinalIgnoreCase))
            {
                return allLanguageOptions[i];
            }
        }

        return null;
    }

    private Locale GetBuiltInLocale(string localeCode)
    {
        return string.IsNullOrWhiteSpace(localeCode)
            ? null
            : LocalizationSettings.AvailableLocales.GetLocale(localeCode);
    }

    private Locale GetDefaultBuiltInLocale()
    {
        return GetBuiltInLocale(DefaultBuiltInLocaleCode)
               ?? GetBuiltInLocale("zh-Hans")
               ?? (LocalizationSettings.AvailableLocales.Locales.Count > 0
                   ? LocalizationSettings.AvailableLocales.Locales[0]
                   : null);
    }

    private void CacheUnityLocalizationExtensions()
    {
        if (defaultTableProvider == null && LocalizationSettings.StringDatabase.TableProvider != runtimeTableProvider)
        {
            defaultTableProvider = LocalizationSettings.StringDatabase.TableProvider;
        }

        if (LocalizationSettings.StringDatabase.TablePostprocessor != defaultTablePostprocessor)
        {
            defaultTablePostprocessor = LocalizationSettings.StringDatabase.TablePostprocessor;
        }
    }

    private void RestoreDefaultTableExtensions()
    {
        if (LocalizationSettings.StringDatabase == null)
        {
            return;
        }

        LocalizationSettings.StringDatabase.TableProvider = defaultTableProvider;
        LocalizationSettings.StringDatabase.TablePostprocessor = defaultTablePostprocessor;
    }

    private void RemoveRuntimeLocale()
    {
        if (runtimeLocale == null)
        {
            return;
        }

        var isBuiltIn = string.Equals(runtimeLocale.Identifier.Code, "en", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(runtimeLocale.Identifier.Code, "zh-Hans", StringComparison.OrdinalIgnoreCase);
        if (!isBuiltIn)
        {
            LocalizationSettings.AvailableLocales.RemoveLocale(runtimeLocale);
            if (Application.isPlaying)
            {
                Destroy(runtimeLocale);
            }
            else
            {
                DestroyImmediate(runtimeLocale);
            }
        }

        runtimeLocale = null;
    }

    private void EnsureLanguageOptionsReady()
    {
        if (allLanguageOptions.Count <= 0)
        {
            RefreshAllLanguagePacksCache();
        }
    }

    private int FindBuiltInIndex(string localeCode)
    {
        for (var i = 0; i < allLanguageOptions.Count; i++)
        {
            if (allLanguageOptions[i].Source == LanguageOptionSource.BuiltIn
                && string.Equals(allLanguageOptions[i].LocaleCode, localeCode, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private static int CompareLanguageOptions(LanguagePackInfo left, LanguagePackInfo right)
    {
        return string.Compare(left?.DisplayName, right?.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    private static void LogPackDiagnostics(LanguagePackInfo info)
    {
        if (info?.Diagnostics == null)
        {
            return;
        }

        for (var i = 0; i < info.Diagnostics.Count; i++)
        {
            var diagnostic = info.Diagnostics[i];
            if (diagnostic == null)
            {
                continue;
            }

            var lineText = diagnostic.Line > 0 ? $", Line={diagnostic.Line}" : string.Empty;
            var message = $"LanguagePack[{info.RootPath}] {diagnostic.Code}{lineText}: {diagnostic.Message}";
            if (diagnostic.Severity == LocalizationDiagnosticSeverity.Error)
            {
                Debug.LogError(message);
            }
            else if (diagnostic.Severity == LocalizationDiagnosticSeverity.Warning)
            {
                Debug.LogWarning(message);
            }
            else
            {
                Debug.Log(message);
            }
        }
    }
}
