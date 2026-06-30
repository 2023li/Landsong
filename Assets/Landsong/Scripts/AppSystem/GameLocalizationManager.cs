using System;
using System.Collections;
using System.Collections.Generic;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.IO;

public class GameLocalizationManager : MonoSingleton<GameLocalizationManager>
{
    [Header("内置语言包")]
    [SerializeField]
    private List<LanguagePackInfo> builtInLanguagePacks = new List<LanguagePackInfo>
    {
        new LanguagePackInfo
        {
            PackId = "builtin_zh_hans",
            LanguageCode = "zh-Hans",
            DisplayName = "简体中文",
            FileName = string.Empty,
            FullPath = string.Empty,
            Source = LanguageSource.BuiltIn
        },
        new LanguagePackInfo
        {
            PackId = "builtin_en",
            LanguageCode = "en",
            DisplayName = "English",
            FileName = string.Empty,
            FullPath = string.Empty,
            Source = LanguageSource.BuiltIn
        }
    };

    [Header("外部 CSV 默认表名")]
    [SerializeField]
    private string defaultExternalCsvTableName = "GameString";
   
    //外部语言包原文CSV文件中，前面用于存放语言包信息的行数限制（从上往下读），超过该行数仍未读到完整的语言包信息，则放弃读取该语言包。
    private const int ExternalLanguagePackMetaReadLineLimit = 10;
    private const string ExternalLanguagePackMetaKey_PackId = "PackId";
    private const string ExternalLanguagePackMetaKey_BaseLanguageCode = "LanguageCode";
    private const string ExternalLanguagePackMetaKey_DisplayName = "DisplayName";


    private readonly List<LanguagePackInfo> externalLanguagePacks = new List<LanguagePackInfo>();

    private readonly List<LanguagePackInfo> allLanguagePacks = new List<LanguagePackInfo>();

    private int currentLanguagePackIndex = 0;

    private readonly RuntimeMergedStringTableProvider runtimeMergedStringTableProvider =
        new RuntimeMergedStringTableProvider();

    private ITableProvider defaultStringTableProvider;
    private bool hasCachedDefaultStringTableProvider;

    public event Action<LocalizationSaveData> OnLanguageChanged;

    public IReadOnlyList<LanguagePackInfo> ExternalLanguagePacks => externalLanguagePacks;

    public IReadOnlyList<LanguagePackInfo> AllLanguagePacks => allLanguagePacks;

    public int CurrentLanguagePackIndex => currentLanguagePackIndex;

    public int AllLanguagePackCount => allLanguagePacks.Count;

    public string ExternalLanguagePacksFolderPath => IOManager.Instance.ExternalLanguagePacksFolderPath;

    public LocalizationSaveData CurrentLanguageData
    {
        get
        {
            DataManager.Instance.EnsureAppDataLoaded();

            if (DataManager.Instance.AppData.Language == null)
            {
                DataManager.Instance.AppData.Language = LocalizationSaveData.CreateDefault();
                DataManager.Instance.SaveAppData();
            }

            return DataManager.Instance.AppData.Language;
        }
    }

    public void Initialize()
    {
        EnsureExternalLanguagePackFolder();
        RefreshAllLanguagePacksCache();
        ValidateCurrentLanguage();
        SyncCurrentLanguagePackIndexFromSaveData();
        StartCoroutine(ApplyToUnityLocalization());
    }

    private void OnDestroy()
    {
        if (LocalizationSettings.StringDatabase != null
            && LocalizationSettings.StringDatabase.TableProvider == runtimeMergedStringTableProvider)
        {
            LocalizationSettings.StringDatabase.TableProvider = defaultStringTableProvider;
        }

        runtimeMergedStringTableProvider.Clear();
    }

    private void EnsureExternalLanguagePackFolder()
    {
        IOManager.Instance.EnsureExternalLanguagePackFolder();
    }

    private void ValidateCurrentLanguage()
    {
        LocalizationSaveData languageData = CurrentLanguageData;
        languageData.Validate();

        if (languageData.UseSystemLanguage)
        {
            DataManager.Instance.SaveAppData();
            return;
        }

        if (string.IsNullOrEmpty(languageData.CurrentLanguageCode))
        {
            languageData.UseSystemLanguage = true;
            languageData.Source = LanguageSource.BuiltIn;
            languageData.ExternalPackId = string.Empty;
            languageData.ExternalPackFileName = string.Empty;

            DataManager.Instance.SaveAppData();
            return;
        }

        if (languageData.Source == LanguageSource.ExternalCsv)
        {
            string fullPath = GetCurrentExternalLanguagePackFullPath();

            if (!IOManager.Instance.FileExists(fullPath))
            {
                Debug.LogWarning($"当前外部语言包不存在，恢复为内置语言：{fullPath}");

                languageData.Source = LanguageSource.BuiltIn;
                languageData.ExternalPackId = string.Empty;
                languageData.ExternalPackFileName = string.Empty;

                DataManager.Instance.SaveAppData();
            }
        }
    }

    public bool SetUseSystemLanguage()
    {
        LocalizationSaveData languageData = CurrentLanguageData;

        languageData.UseSystemLanguage = true;
        languageData.Source = LanguageSource.BuiltIn;
        languageData.CurrentLanguageCode = string.Empty;
        languageData.ExternalPackId = string.Empty;
        languageData.ExternalPackFileName = string.Empty;

        DataManager.Instance.SaveAppData();
        SyncCurrentLanguagePackIndexFromSaveData();

        return true;
    }

    public IEnumerator SetUseSystemLanguageAndApply()
    {
        if (!SetUseSystemLanguage())
        {
            yield break;
        }

        yield return ApplyToUnityLocalization();
    }

    public void RefreshAllLanguagePacksCache()
    {
        RefreshExternalLanguagePacks();
    }

    public List<LanguagePackInfo> GetAllLanguagePacks()
    {
        EnsureAllLanguagePacksCacheReady();

        List<LanguagePackInfo> result = new List<LanguagePackInfo>();

        foreach (LanguagePackInfo languagePack in allLanguagePacks)
        {
            result.Add(CloneLanguagePackInfo(languagePack));
        }

        return result;
    }

    public LanguagePackInfo GetCurrentPreviewLanguagePack()
    {
        EnsureAllLanguagePacksCacheReady();

        if (allLanguagePacks.Count <= 0)
        {
            return null;
        }

        currentLanguagePackIndex = Mathf.Clamp(currentLanguagePackIndex, 0, allLanguagePacks.Count - 1);

        return CloneLanguagePackInfo(allLanguagePacks[currentLanguagePackIndex]);
    }

    public bool SetCurrentLanguagePackIndex(int index)
    {
        EnsureAllLanguagePacksCacheReady();

        if (allLanguagePacks.Count <= 0)
        {
            currentLanguagePackIndex = 0;
            return false;
        }

        if (index < 0 || index >= allLanguagePacks.Count)
        {
            Debug.LogWarning($"设置语言下标失败：Index={index}, Count={allLanguagePacks.Count}");
            return false;
        }

        currentLanguagePackIndex = index;
        return true;
    }

    public bool SelectPreviousLanguagePack()
    {
        EnsureAllLanguagePacksCacheReady();

        if (allLanguagePacks.Count <= 0)
        {
            currentLanguagePackIndex = 0;
            return false;
        }

        currentLanguagePackIndex--;

        if (currentLanguagePackIndex < 0)
        {
            currentLanguagePackIndex = allLanguagePacks.Count - 1;
        }

        return true;
    }

    public bool SelectNextLanguagePack()
    {
        EnsureAllLanguagePacksCacheReady();

        if (allLanguagePacks.Count <= 0)
        {
            currentLanguagePackIndex = 0;
            return false;
        }

        currentLanguagePackIndex++;

        if (currentLanguagePackIndex >= allLanguagePacks.Count)
        {
            currentLanguagePackIndex = 0;
        }

        return true;
    }

    public IEnumerator ApplyCurrentPreviewLanguagePack()
    {
        LanguagePackInfo languagePack = GetCurrentPreviewLanguagePack();

        if (languagePack == null)
        {
            Debug.LogWarning("应用当前预览语言失败：没有可用语言包。");
            yield break;
        }

        yield return SetLanguageAndApply(languagePack);
    }

    public void SyncCurrentLanguagePackIndexFromSaveData()
    {
        if (allLanguagePacks.Count <= 0)
        {
            currentLanguagePackIndex = 0;
            return;
        }

        LocalizationSaveData languageData = CurrentLanguageData;

        int index = FindLanguagePackIndex(languageData);

        if (index >= 0)
        {
            currentLanguagePackIndex = index;
            return;
        }

        currentLanguagePackIndex = Mathf.Clamp(currentLanguagePackIndex, 0, allLanguagePacks.Count - 1);
    }

    private void EnsureAllLanguagePacksCacheReady()
    {
        if (allLanguagePacks.Count > 0)
        {
            return;
        }

        RefreshAllLanguagePacksCache();
    }

    private void RebuildAllLanguagePackCache()
    {
        allLanguagePacks.Clear();

        if (builtInLanguagePacks != null)
        {
            foreach (LanguagePackInfo builtInPack in builtInLanguagePacks)
            {
                if (builtInPack == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(builtInPack.LanguageCode))
                {
                    continue;
                }

                LanguagePackInfo packInfo = CloneLanguagePackInfo(builtInPack);

                packInfo.Source = LanguageSource.BuiltIn;
                packInfo.FileName = string.Empty;
                packInfo.FullPath = string.Empty;
                packInfo.LanguageCode = packInfo.LanguageCode.Trim();

                if (string.IsNullOrWhiteSpace(packInfo.PackId))
                {
                    packInfo.PackId = $"builtin_{packInfo.LanguageCode}";
                }

                if (string.IsNullOrWhiteSpace(packInfo.DisplayName))
                {
                    packInfo.DisplayName = packInfo.LanguageCode;
                }

                allLanguagePacks.Add(packInfo);
            }
        }

        foreach (LanguagePackInfo externalPack in externalLanguagePacks)
        {
            if (externalPack == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(externalPack.LanguageCode))
            {
                continue;
            }

            allLanguagePacks.Add(CloneLanguagePackInfo(externalPack));
        }

        if (allLanguagePacks.Count <= 0)
        {
            currentLanguagePackIndex = 0;
            return;
        }

        currentLanguagePackIndex = Mathf.Clamp(currentLanguagePackIndex, 0, allLanguagePacks.Count - 1);
    }

    private int FindLanguagePackIndex(LocalizationSaveData languageData)
    {
        if (languageData == null)
        {
            return 0;
        }

        if (string.IsNullOrWhiteSpace(languageData.CurrentLanguageCode))
        {
            return 0;
        }

        for (int i = 0; i < allLanguagePacks.Count; i++)
        {
            LanguagePackInfo languagePack = allLanguagePacks[i];

            if (IsSameLanguagePack(languagePack, languageData))
            {
                return i;
            }
        }

        return -1;
    }

    private bool IsSameLanguagePack(LanguagePackInfo languagePack, LocalizationSaveData languageData)
    {
        if (languagePack == null || languageData == null)
        {
            return false;
        }

        bool sameCode = string.Equals(
            languagePack.LanguageCode,
            languageData.CurrentLanguageCode,
            StringComparison.OrdinalIgnoreCase
        );

        if (!sameCode)
        {
            return false;
        }

        bool sameSource = languagePack.Source == languageData.Source;

        if (!sameSource)
        {
            return false;
        }

        if (languagePack.Source != LanguageSource.ExternalCsv)
        {
            return true;
        }

        bool hasSavedPackId = !string.IsNullOrWhiteSpace(languageData.ExternalPackId);
        bool hasSavedFileName = !string.IsNullOrWhiteSpace(languageData.ExternalPackFileName);

        if (!hasSavedPackId && !hasSavedFileName)
        {
            return false;
        }

        if (hasSavedPackId)
        {
            bool samePackId = string.Equals(
                languagePack.PackId,
                languageData.ExternalPackId,
                StringComparison.OrdinalIgnoreCase
            );

            if (!samePackId)
            {
                return false;
            }
        }

        if (hasSavedFileName)
        {
            bool sameFileName = string.Equals(
                languagePack.FileName,
                languageData.ExternalPackFileName,
                StringComparison.OrdinalIgnoreCase
            );

            if (!sameFileName)
            {
                return false;
            }
        }

        return true;
    }

    private LanguagePackInfo CloneLanguagePackInfo(LanguagePackInfo source)
    {
        if (source == null)
        {
            return null;
        }

        return new LanguagePackInfo
        {
            PackId = source.PackId ?? string.Empty,
            LanguageCode = source.LanguageCode ?? string.Empty,
            DisplayName = source.DisplayName ?? string.Empty,
            FileName = source.FileName ?? string.Empty,
            FullPath = source.FullPath ?? string.Empty,
            Source = source.Source
        };
    }

    public bool SetLanguage(LanguagePackInfo packInfo)
    {
        if (packInfo == null)
        {
            Debug.LogWarning("设置语言失败：packInfo 为空。");
            return false;
        }

        if (string.IsNullOrWhiteSpace(packInfo.LanguageCode))
        {
            Debug.LogWarning("设置语言失败：LanguageCode 为空。");
            return false;
        }

        LocalizationSaveData languageData = CurrentLanguageData;

        switch (packInfo.Source)
        {
            case LanguageSource.BuiltIn:
                WriteBuiltInLanguageData(languageData, packInfo);
                break;

            case LanguageSource.ExternalCsv:
                if (!TryWriteExternalCsvLanguageData(languageData, packInfo))
                {
                    return false;
                }

                break;

            case LanguageSource.ExternalJson:
                Debug.LogWarning("设置语言失败：暂未支持 ExternalJson。");
                return false;

            default:
                Debug.LogWarning($"设置语言失败：未知语言来源：{packInfo.Source}");
                return false;
        }

        DataManager.Instance.SaveAppData();
        SyncCurrentLanguagePackIndexFromSaveData();

        return true;
    }

    public IEnumerator SetLanguageAndApply(LanguagePackInfo packInfo)
    {
        if (!SetLanguage(packInfo))
        {
            yield break;
        }

        yield return ApplyToUnityLocalization();
    }

    private void WriteBuiltInLanguageData(LocalizationSaveData languageData, LanguagePackInfo packInfo)
    {
        languageData.UseSystemLanguage = false;
        languageData.Source = LanguageSource.BuiltIn;
        languageData.CurrentLanguageCode = packInfo.LanguageCode.Trim();
        languageData.ExternalPackId = string.Empty;
        languageData.ExternalPackFileName = string.Empty;
    }

    private bool TryWriteExternalCsvLanguageData(LocalizationSaveData languageData, LanguagePackInfo packInfo)
    {
        if (string.IsNullOrWhiteSpace(packInfo.FileName))
        {
            Debug.LogWarning("设置外部语言包失败：FileName 为空。");
            return false;
        }

        string fileName = IOManager.Instance.GetSafeFileName(packInfo.FileName);
        string fullPath = IOManager.Instance.GetExternalLanguagePackFullPath(fileName);

        if (!IOManager.Instance.FileExists(fullPath))
        {
            Debug.LogWarning($"设置外部语言包失败：文件不存在：{fullPath}");
            return false;
        }

        languageData.UseSystemLanguage = false;
        languageData.Source = LanguageSource.ExternalCsv;
        languageData.CurrentLanguageCode = packInfo.LanguageCode.Trim();
        languageData.ExternalPackId = packInfo.PackId ?? string.Empty;
        languageData.ExternalPackFileName = fileName;

        return true;
    }

    public string GetCurrentExternalLanguagePackFullPath()
    {
        LocalizationSaveData languageData = CurrentLanguageData;

        if (languageData == null)
        {
            return string.Empty;
        }

        if (string.IsNullOrEmpty(languageData.ExternalPackFileName))
        {
            return string.Empty;
        }

        return IOManager.Instance.GetExternalLanguagePackFullPath(languageData.ExternalPackFileName);
    }

    public void RefreshExternalLanguagePacks()
    {
        EnsureExternalLanguagePackFolder();

        externalLanguagePacks.Clear();

        string[] csvFiles = IOManager.Instance.GetExternalLanguagePackCsvFiles();

        foreach (string csvFile in csvFiles)
        {
            if (TryReadExternalLanguagePackInfo(csvFile, out LanguagePackInfo packInfo))
            {
                externalLanguagePacks.Add(packInfo);
            }
        }

        externalLanguagePacks.Sort((a, b) =>
            string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

        RebuildAllLanguagePackCache();
        SyncCurrentLanguagePackIndexFromSaveData();
    }

    private bool TryReadExternalLanguagePackInfo(string filePath, out LanguagePackInfo packInfo)
    {
        packInfo = null;

        if (string.IsNullOrEmpty(filePath) || !IOManager.Instance.FileExists(filePath))
        {
            return false;
        }

        string packId = string.Empty;
        string languageCode = string.Empty;
        string displayName = string.Empty;

        try
        {
            using (StreamReader reader = new StreamReader(filePath, System.Text.Encoding.UTF8, true))
            {
                for (int i = 0; i < ExternalLanguagePackMetaReadLineLimit; i++)
                {
                    string line = reader.ReadLine();

                    if (line == null)
                    {
                        break;
                    }

                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    line = line.Trim();

                    if (line.StartsWith("#"))
                    {
                        line = line.Substring(1).Trim();
                    }

                    TryReadMetaLine(line, ExternalLanguagePackMetaKey_PackId, ref packId);
                    TryReadMetaLine(line, ExternalLanguagePackMetaKey_BaseLanguageCode, ref languageCode);
                    TryReadMetaLine(line, ExternalLanguagePackMetaKey_DisplayName, ref displayName);

                    if (!string.IsNullOrEmpty(languageCode)
                        && !string.IsNullOrEmpty(packId)
                        && !string.IsNullOrEmpty(displayName))
                    {
                        break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"读取外部语言包信息失败：{filePath}\n{e.Message}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            Debug.LogWarning($"外部语言包缺少 LanguageCode：{filePath}");
            return false;
        }

        if (string.IsNullOrWhiteSpace(packId))
        {
            packId = IOManager.Instance.GetFileNameWithoutExtension(filePath);
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = languageCode;
        }

        packInfo = new LanguagePackInfo
        {
            PackId = packId.Trim(),
            LanguageCode = languageCode.Trim(),
            DisplayName = displayName.Trim(),
            FileName = IOManager.Instance.GetSafeFileName(filePath),
            FullPath = filePath,
            Source = LanguageSource.ExternalCsv
        };

        return true;
    }

    private void TryReadMetaLine(string line, string targetKey, ref string result)
    {
        if (!string.IsNullOrEmpty(result))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        int colonIndex = line.IndexOf(':');

        if (colonIndex >= 0)
        {
            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();

            if (string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                result = value;
            }

            return;
        }

        string[] parts = SplitCsvLine(line);

        if (parts.Length >= 2)
        {
            string key = parts[0].Trim();
            string value = parts[1].Trim();

            if (string.Equals(key, targetKey, StringComparison.OrdinalIgnoreCase))
            {
                result = value;
            }
        }
    }

    public IEnumerator ApplyToUnityLocalization()
    {
        yield return LocalizationSettings.InitializationOperation;

        CacheDefaultStringTableProvider();

        LocalizationSaveData languageData = CurrentLanguageData;
        languageData.Validate();

        Locale targetLocale = GetTargetUnityLocale(languageData);

        if (targetLocale == null)
        {
            Debug.LogWarning("应用本地化失败：没有找到可用的 Unity Locale。");
            yield break;
        }

        Locale oldLocale = LocalizationSettings.SelectedLocale;

        LocalizationSettings.StringDatabase.TableProvider = defaultStringTableProvider;

        LocalizationSettings.StringDatabase.TablePostprocessor = null;

        runtimeMergedStringTableProvider.Clear();

        bool useRuntimeMergedTable = false;

        if (!languageData.UseSystemLanguage && languageData.Source == LanguageSource.ExternalCsv)
        {
            string externalCsvPath = GetCurrentExternalLanguagePackFullPath();

            if (IOManager.Instance.FileExists(externalCsvPath))
            {
                List<ExternalLanguageTextPatch> patches = LoadExternalCsvTextPatches(externalCsvPath);

                yield return BuildRuntimeMergedStringTables(targetLocale, patches);

                useRuntimeMergedTable = runtimeMergedStringTableProvider.HasAnyTable;
            }
            else
            {
                Debug.LogWarning($"外部语言包文件不存在，将只使用 Unity 内置语言表：{externalCsvPath}");
            }
        }

        if (oldLocale != null)
        {
            LocalizationSettings.StringDatabase.ReleaseAllTables(oldLocale);
        }

        LocalizationSettings.StringDatabase.ReleaseAllTables(targetLocale);

        if (useRuntimeMergedTable)
        {
            LocalizationSettings.StringDatabase.TableProvider = runtimeMergedStringTableProvider;
        }
        else
        {
            LocalizationSettings.StringDatabase.TableProvider = defaultStringTableProvider;
        }

        LocalizationSettings.SelectedLocale = targetLocale;

        yield return null;

        yield return ForceReloadAllLocalizeStringEvents(targetLocale);

        OnLanguageChanged?.Invoke(languageData);
    }

    private void CacheDefaultStringTableProvider()
    {
        if (hasCachedDefaultStringTableProvider)
        {
            return;
        }

        if (LocalizationSettings.StringDatabase.TableProvider != runtimeMergedStringTableProvider)
        {
            defaultStringTableProvider = LocalizationSettings.StringDatabase.TableProvider;
        }

        hasCachedDefaultStringTableProvider = true;
    }

   

    private IEnumerator BuildRuntimeMergedStringTables(
        Locale targetLocale,
        List<ExternalLanguageTextPatch> patches)
    {
        if (targetLocale == null)
        {
            yield break;
        }

        if (patches == null || patches.Count <= 0)
        {
            yield break;
        }

        Dictionary<string, List<ExternalLanguageTextPatch>> patchesByTable =
            GroupExternalPatchesByTable(patches);

        foreach (KeyValuePair<string, List<ExternalLanguageTextPatch>> pair in patchesByTable)
        {
            string tableName = pair.Key;
            List<ExternalLanguageTextPatch> tablePatches = pair.Value;

            if (string.IsNullOrWhiteSpace(tableName))
            {
                continue;
            }

            AsyncOperationHandle<StringTable> operation =
                LocalizationSettings.StringDatabase.GetTableAsync(tableName, targetLocale);

            yield return operation;

            StringTable sourceTable = null;

            if (operation.Status == AsyncOperationStatus.Succeeded)
            {
                sourceTable = operation.Result;
            }
            else
            {
                Debug.LogWarning($"加载内置 StringTable 失败，将创建空运行时表。Table={tableName}, Locale={targetLocale.Identifier.Code}");
            }

            StringTable runtimeTable = CreateRuntimeMergedStringTable(
                tableName,
                targetLocale,
                sourceTable,
                tablePatches
            );

            runtimeMergedStringTableProvider.SetTable(tableName, targetLocale, runtimeTable);

            if (operation.IsValid())
            {
                Addressables.Release(operation);
            }
        }
    }

    private Dictionary<string, List<ExternalLanguageTextPatch>> GroupExternalPatchesByTable(List<ExternalLanguageTextPatch> patches)
    {
        Dictionary<string, List<ExternalLanguageTextPatch>> result =
            new Dictionary<string, List<ExternalLanguageTextPatch>>(StringComparer.OrdinalIgnoreCase);

        foreach (ExternalLanguageTextPatch patch in patches)
        {
            if (patch == null)
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(patch.Key))
            {
                continue;
            }

            string tableName = patch.TableName;

            if (string.IsNullOrWhiteSpace(tableName))
            {
                tableName = defaultExternalCsvTableName;
            }

            tableName = tableName.Trim();

            if (!result.TryGetValue(tableName, out List<ExternalLanguageTextPatch> list))
            {
                list = new List<ExternalLanguageTextPatch>();
                result.Add(tableName, list);
            }

            list.Add(patch);
        }

        return result;
    }

    private StringTable CreateRuntimeMergedStringTable(
        string tableName,
        Locale targetLocale,
        StringTable sourceTable,
        List<ExternalLanguageTextPatch> patches)
    {
        StringTable runtimeTable;

        if (sourceTable != null)
        {
            runtimeTable = UnityEngine.Object.Instantiate(sourceTable);
            runtimeTable.SharedData = UnityEngine.Object.Instantiate(sourceTable.SharedData);
        }
        else
        {
            runtimeTable = ScriptableObject.CreateInstance<StringTable>();

            SharedTableData sharedData = ScriptableObject.CreateInstance<SharedTableData>();
            sharedData.TableCollectionName = tableName;

            runtimeTable.SharedData = sharedData;
        }

        runtimeTable.name = $"{tableName}_{targetLocale.Identifier.Code}_Runtime";
        runtimeTable.LocaleIdentifier = targetLocale.Identifier;
        runtimeTable.hideFlags = HideFlags.DontSave;

        if (runtimeTable.SharedData != null)
        {
            runtimeTable.SharedData.TableCollectionName = tableName;
            runtimeTable.SharedData.hideFlags = HideFlags.DontSave;
        }

        if (patches != null)
        {
            foreach (ExternalLanguageTextPatch patch in patches)
            {
                if (patch == null)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(patch.Key))
                {
                    continue;
                }

                string key = patch.Key.Trim();
                string text = patch.Text ?? string.Empty;

                StringTableEntry oldEntry = sourceTable != null ? sourceTable.GetEntry(key) : null;
                StringTableEntry newEntry = runtimeTable.AddEntry(key, text);

                if (oldEntry != null && newEntry != null)
                {
                    newEntry.IsSmart = oldEntry.IsSmart;
                }
            }
        }

        return runtimeTable;
    }

    private Locale GetTargetUnityLocale(LocalizationSaveData languageData)
    {
        if (languageData == null)
        {
            return GetFallbackUnityLocale();
        }

        if (languageData.UseSystemLanguage)
        {
            Locale systemLocale = LocalizationSettings.AvailableLocales.GetLocale(Application.systemLanguage);

            if (systemLocale != null)
            {
                return systemLocale;
            }

            return GetFallbackUnityLocale();
        }

        if (!string.IsNullOrWhiteSpace(languageData.CurrentLanguageCode))
        {
            Locale locale = LocalizationSettings.AvailableLocales.GetLocale(languageData.CurrentLanguageCode);

            if (locale != null)
            {
                return locale;
            }

            Debug.LogWarning($"Unity Localization 中没有找到 Locale：{languageData.CurrentLanguageCode}");
        }

        return GetFallbackUnityLocale();
    }

    private Locale GetFallbackUnityLocale()
    {
        if (LocalizationSettings.SelectedLocale != null)
        {
            return LocalizationSettings.SelectedLocale;
        }

        var locales = LocalizationSettings.AvailableLocales.Locales;

        if (locales != null && locales.Count > 0)
        {
            return locales[0];
        }

        return null;
    }

    private IEnumerator ForceReloadAllLocalizeStringEvents(Locale targetLocale)
    {
        LocalizeStringEvent[] events = UnityEngine.Object.FindObjectsByType<LocalizeStringEvent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );

        foreach (LocalizeStringEvent localizeStringEvent in events)
        {
            if (localizeStringEvent == null)
            {
                continue;
            }

            LocalizedString localizedString = localizeStringEvent.StringReference;

            if (localizedString == null || localizedString.IsEmpty)
            {
                continue;
            }

            Locale oldLocaleOverride = localizedString.LocaleOverride;

            // 重点：
            // 通过临时改变 LocaleOverride，强制 LocalizedString 内部 ClearLoadingOperation，
            // 然后重新走 StringDatabase.GetTableEntryAsync。
            if (oldLocaleOverride == null)
            {
                localizedString.LocaleOverride = targetLocale;
                localizedString.LocaleOverride = null;
            }
            else
            {
                localizedString.LocaleOverride = null;
                localizedString.LocaleOverride = oldLocaleOverride;
            }

            localizeStringEvent.RefreshString();
        }

        yield return null;
    }

    private List<ExternalLanguageTextPatch> LoadExternalCsvTextPatches(string filePath)
    {
        List<ExternalLanguageTextPatch> result = new List<ExternalLanguageTextPatch>();

        if (string.IsNullOrEmpty(filePath) || !IOManager.Instance.FileExists(filePath))
        {
            return result;
        }

        try
        {
            string[] lines = IOManager.Instance.ReadAllLines(filePath);

            int tableIndex = -1;
            int keyIndex = -1;
            int textIndex = -1;
            bool hasHeader = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                line = line.Trim();

                if (line.StartsWith("#"))
                {
                    continue;
                }

                string[] parts = SplitCsvLine(line);

                if (parts.Length < 2)
                {
                    continue;
                }

                if (!hasHeader)
                {
                    bool foundHeader = TryFindCsvHeader(parts, out tableIndex, out keyIndex, out textIndex);

                    if (foundHeader)
                    {
                        hasHeader = true;
                        continue;
                    }

                    hasHeader = true;
                    tableIndex = -1;
                    keyIndex = 0;
                    textIndex = 1;
                }

                if (keyIndex < 0 || textIndex < 0)
                {
                    continue;
                }

                if (parts.Length <= keyIndex || parts.Length <= textIndex)
                {
                    continue;
                }

                string tableName = string.Empty;

                if (tableIndex >= 0 && parts.Length > tableIndex)
                {
                    tableName = parts[tableIndex].Trim();
                }

                string key = parts[keyIndex].Trim();
                string text = parts[textIndex].Trim();

                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                result.Add(new ExternalLanguageTextPatch
                {
                    TableName = tableName,
                    Key = key,
                    Text = text
                });
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"读取外部语言包 CSV 失败：{filePath}\n{e.Message}");
        }

        return result;
    }

    private bool TryFindCsvHeader(
        string[] parts,
        out int tableIndex,
        out int keyIndex,
        out int textIndex)
    {
        tableIndex = -1;
        keyIndex = -1;
        textIndex = -1;

        if (parts == null || parts.Length <= 0)
        {
            return false;
        }

        for (int i = 0; i < parts.Length; i++)
        {
            string columnName = parts[i].Trim();

            if (string.Equals(columnName, "Table", StringComparison.OrdinalIgnoreCase))
            {
                tableIndex = i;
            }
            else if (string.Equals(columnName, "Key", StringComparison.OrdinalIgnoreCase))
            {
                keyIndex = i;
            }
            else if (string.Equals(columnName, "Text", StringComparison.OrdinalIgnoreCase))
            {
                textIndex = i;
            }
        }

        return keyIndex >= 0 && textIndex >= 0;
    }

    private string[] SplitCsvLine(string line)
    {
        List<string> result = new List<string>();
        if (line == null) return result.ToArray();

        bool insideQuote = false;
        System.Text.StringBuilder current = new System.Text.StringBuilder();

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (insideQuote && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    insideQuote = !insideQuote;
                }
                continue;
            }

            if (c == ',' && !insideQuote)
            {
                result.Add(current.ToString());
                current.Clear(); // 清空复用，避免重新 new StringBuilder
                continue;
            }

            current.Append(c);
        }
        result.Add(current.ToString());
        return result.ToArray();
    }
}

public class RuntimeMergedStringTableProvider : ITableProvider
{
    private readonly Dictionary<string, StringTable> runtimeTables =
        new Dictionary<string, StringTable>(StringComparer.OrdinalIgnoreCase);

    public bool HasAnyTable => runtimeTables.Count > 0;

    public void Clear()
    {
        foreach (StringTable table in runtimeTables.Values)
        {
            if (table == null)
            {
                continue;
            }

            SharedTableData sharedData = table.SharedData;

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

        runtimeTables.Clear();
    }

    public void SetTable(string tableCollectionName, Locale locale, StringTable table)
    {
        if (string.IsNullOrWhiteSpace(tableCollectionName))
        {
            return;
        }

        if (locale == null || table == null)
        {
            return;
        }

        string key = MakeKey(tableCollectionName, locale.Identifier.Code);
        runtimeTables[key] = table;
    }

    public AsyncOperationHandle<TTable> ProvideTableAsync<TTable>(
        string tableCollectionName,
        Locale locale) where TTable : LocalizationTable
    {
        if (typeof(TTable) != typeof(StringTable))
        {
            return default;
        }

        if (locale == null)
        {
            return default;
        }

        string key = MakeKey(tableCollectionName, locale.Identifier.Code);

        if (!runtimeTables.TryGetValue(key, out StringTable table))
        {
            return default;
        }

        return Addressables.ResourceManager.CreateCompletedOperation(table as TTable, null);
    }

    private string MakeKey(string tableCollectionName, string localeCode)
    {
        return $"{tableCollectionName}||{localeCode}";
    }
}

[Serializable]
public class ExternalLanguageTextPatch
{
    public string TableName = string.Empty;

    public string Key = string.Empty;

    public string Text = string.Empty;
}

[Serializable]
public class LanguagePackInfo
{
    public string PackId = string.Empty;

    public string LanguageCode = string.Empty;

    public string DisplayName = string.Empty;

    public string FileName = string.Empty;

    public string FullPath = string.Empty;

    public LanguageSource Source = LanguageSource.ExternalCsv;
}

public enum LanguageSource
{
    BuiltIn,
    ExternalCsv,
    ExternalJson
}
