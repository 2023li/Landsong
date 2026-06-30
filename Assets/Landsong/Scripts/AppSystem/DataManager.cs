using System;
using System.Collections.Generic;
using Landsong.InventorySystem;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum GameDataSaveMode
{
    Overwrite = 0,
    NewSave = 1
}

public class DataManager : MonoSingleton<DataManager>
{
    private const string AppDataKey = "AppData";
    private const string GameDataIndexKey = "GameDataIndex";
    private const string GameDataKey = "GameData";
    private const string GameDataMetaKey = "GameDataMeta";

    [SerializeField]
    private AppData appData;

    [SerializeField]
    private List<GameDataMeta> gameDataMetaList = new List<GameDataMeta>();

    public AppData AppData => appData;

    public bool HasLoadedAppData { get; private set; }

    public bool HasLoadedGameDataIndex { get; private set; }

    public string SaveRootPath => IOManager.Instance.SaveRootPath;

    public IReadOnlyList<GameDataMeta> GameDataMetaList => gameDataMetaList;

    public GameData CurrentGameData { get; private set; }

    public event Action<GameData> OnGameDataSave;

    public event Action<GameData> OnGameDataLoaded;

    private string AppDataFilePath => IOManager.Instance.AppDataFilePath;

    private string GameDataIndexFilePath => IOManager.Instance.GameDataIndexFilePath;

    protected override void Init()
    {
        Initialize();
    }

    public void Initialize()
    {
        IOManager.Instance.Initialize();
        LoadAppData();
        LoadGameDataIndex();
    }

    private void EnsureSaveFolders()
    {
        IOManager.Instance.EnsureSaveFolders();
    }

    private string GetGameSaveFolderPath(string saveGuid)
    {
        return IOManager.Instance.GetGameSaveFolderPath(saveGuid);
    }

    private string GetGameDataFilePath(string saveGuid)
    {
        return IOManager.Instance.GetGameDataFilePath(saveGuid);
    }

    public void LoadAppData()
    {
        EnsureSaveFolders();

        if (!IOManager.Instance.ES3KeyExists(AppDataKey, AppDataFilePath))
        {
            appData = AppData.CreateDefault();
            SaveAppData();
            HasLoadedAppData = true;
            return;
        }

        try
        {
            appData = IOManager.Instance.LoadES3<AppData>(AppDataKey, AppDataFilePath);

            if (appData == null)
            {
                appData = AppData.CreateDefault();
                SaveAppData();
            }
            else
            {
                appData.Validate();
            }

            HasLoadedAppData = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"读取 AppData 失败，将使用默认数据。\n{e.Message}");

            appData = AppData.CreateDefault();
            SaveAppData();
            HasLoadedAppData = true;
        }
    }

    public void SaveAppData()
    {
        EnsureSaveFolders();

        if (appData == null)
        {
            appData = AppData.CreateDefault();
        }

        appData.Validate();
        IOManager.Instance.SaveES3(AppDataKey, appData, AppDataFilePath);
    }

    public void EnsureAppDataLoaded()
    {
        if (!HasLoadedAppData || appData == null)
        {
            LoadAppData();
        }
    }

    public void MarkFirstLaunchFinished()
    {
        EnsureAppDataLoaded();

        appData.IsFirstLaunch = false;
        SaveAppData();
    }

    public void SetBgmVolume(float volume)
    {
        EnsureAppDataLoaded();

        appData.Audio.BgmVolume = Mathf.Clamp01(volume);
        SaveAppData();
    }

    public void SetSfxVolume(float volume)
    {
        EnsureAppDataLoaded();

        appData.Audio.SfxVolume = Mathf.Clamp01(volume);
        SaveAppData();
    }

    public void SetMuted(bool muted)
    {
        EnsureAppDataLoaded();

        appData.Audio.IsMuted = muted;
        SaveAppData();
    }

    public void LoadGameDataIndex()
    {
        EnsureSaveFolders();

        gameDataMetaList = new List<GameDataMeta>();

        try
        {
            if (IOManager.Instance.ES3KeyExists(GameDataIndexKey, GameDataIndexFilePath))
            {
                gameDataMetaList = IOManager.Instance.LoadES3<List<GameDataMeta>>(GameDataIndexKey, GameDataIndexFilePath);
            }

            if (gameDataMetaList == null)
            {
                gameDataMetaList = new List<GameDataMeta>();
            }

            RebuildIndexFromSlotFiles();
            ValidateGameDataIndex();

            HasLoadedGameDataIndex = true;
            SaveGameDataIndex();
        }
        catch (Exception e)
        {
            Debug.LogWarning($"读取存档索引失败，将尝试从 Slots 文件夹重建。\n{e.Message}");

            gameDataMetaList = new List<GameDataMeta>();

            RebuildIndexFromSlotFiles();
            ValidateGameDataIndex();

            HasLoadedGameDataIndex = true;
            SaveGameDataIndex();
        }
    }

    public void SaveGameDataIndex()
    {
        EnsureSaveFolders();
        ValidateGameDataIndex();

        IOManager.Instance.SaveES3(GameDataIndexKey, gameDataMetaList, GameDataIndexFilePath);
    }

    private void ValidateGameDataIndex()
    {
        EnsureAppDataLoaded();

        if (gameDataMetaList == null)
        {
            gameDataMetaList = new List<GameDataMeta>();
        }

        for (int i = gameDataMetaList.Count - 1; i >= 0; i--)
        {
            GameDataMeta meta = gameDataMetaList[i];

            if (meta == null || string.IsNullOrEmpty(meta.SaveGuid))
            {
                gameDataMetaList.RemoveAt(i);
                continue;
            }

            meta.Validate();

            string filePath = GetGameDataFilePath(meta.SaveGuid);
            if (!IOManager.Instance.FileExists(filePath))
            {
                gameDataMetaList.RemoveAt(i);
            }
        }

        RemoveDuplicateMetas();
        gameDataMetaList.Sort((a, b) => b.LastSaveUnixTime.CompareTo(a.LastSaveUnixTime));

        if (!string.IsNullOrEmpty(appData.LastGameGuid))
        {
            bool lastSaveExists = gameDataMetaList.Exists(x => x.SaveGuid == appData.LastGameGuid);

            if (!lastSaveExists)
            {
                appData.LastGameGuid = string.Empty;
                SaveAppData();
            }
        }
    }

    private void RemoveDuplicateMetas()
    {
        Dictionary<string, GameDataMeta> metasByGuid = new Dictionary<string, GameDataMeta>(StringComparer.Ordinal);

        foreach (GameDataMeta meta in gameDataMetaList)
        {
            if (meta == null || string.IsNullOrEmpty(meta.SaveGuid))
            {
                continue;
            }

            if (!metasByGuid.TryGetValue(meta.SaveGuid, out GameDataMeta oldMeta))
            {
                metasByGuid.Add(meta.SaveGuid, meta);
                continue;
            }

            if (meta.LastSaveUnixTime > oldMeta.LastSaveUnixTime)
            {
                metasByGuid[meta.SaveGuid] = meta;
            }
        }

        gameDataMetaList = new List<GameDataMeta>(metasByGuid.Values);
    }

    private void RebuildIndexFromSlotFiles()
    {
        if (!IOManager.Instance.DirectoryExists(IOManager.Instance.SlotsFolderPath))
        {
            return;
        }

        string[] slotFolders = IOManager.Instance.GetSlotFolderPaths();

        foreach (string slotFolder in slotFolders)
        {
            string saveGuid = IOManager.Instance.GetFolderName(slotFolder);

            if (string.IsNullOrEmpty(saveGuid))
            {
                continue;
            }

            string gameDataPath = GetGameDataFilePath(saveGuid);

            if (!IOManager.Instance.FileExists(gameDataPath))
            {
                continue;
            }

            GameDataMeta meta = null;

            try
            {
                if (IOManager.Instance.ES3KeyExists(GameDataKey, gameDataPath))
                {
                    GameData gameData = IOManager.Instance.LoadES3<GameData>(GameDataKey, gameDataPath);

                    if (gameData != null)
                    {
                        if (string.IsNullOrEmpty(gameData.SaveGuid))
                        {
                            gameData.SaveGuid = saveGuid;
                        }

                        gameData.Validate();
                        meta = GameDataMeta.CreateFromGameData(gameData);
                    }
                }
                else if (IOManager.Instance.ES3KeyExists(GameDataMetaKey, gameDataPath))
                {
                    meta = IOManager.Instance.LoadES3<GameDataMeta>(GameDataMetaKey, gameDataPath);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"重建存档索引失败：{gameDataPath}\n{e.Message}");
            }

            if (meta == null)
            {
                continue;
            }

            if (string.IsNullOrEmpty(meta.SaveGuid))
            {
                meta.SaveGuid = saveGuid;
            }

            meta.Validate();
            AddOrReplaceMeta(meta);
        }
    }

    private void AddOrReplaceMeta(GameDataMeta meta)
    {
        if (meta == null || string.IsNullOrEmpty(meta.SaveGuid))
        {
            return;
        }

        int index = gameDataMetaList.FindIndex(x => x.SaveGuid == meta.SaveGuid);

        if (index >= 0)
        {
            gameDataMetaList[index] = meta;
        }
        else
        {
            gameDataMetaList.Add(meta);
        }
    }

    private void EnsureGameDataIndexLoaded()
    {
        if (!HasLoadedGameDataIndex || gameDataMetaList == null)
        {
            LoadGameDataIndex();
        }
    }

    public GameData CreateNewGame(string playerName, int worldSeed)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        GameData gameData = GameData.CreateDefault();
        gameData.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        gameData.WorldSeed = worldSeed;
     

        CurrentGameData = gameData;
        SaveNewGameData();

        return gameData;
    }

    public void SaveGameData()
    {
        OverwriteSaveGameData();
    }

    public void OverwriteSaveGameData()
    {
        SaveGameData(GameDataSaveMode.Overwrite);
    }

    public void SaveNewGameData()
    {
        SaveGameData(GameDataSaveMode.NewSave);
    }

    public void QuickSaveGameData()
    {
        OverwriteSaveGameData();
    }

    public void AutoSaveGameData()
    {
        OverwriteSaveGameData();
    }

    public void SaveGameData(GameDataSaveMode saveMode)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        if (CurrentGameData == null)
        {
            Debug.LogWarning("保存 GameData 失败：CurrentGameData 为空。");
            return;
        }

        CaptureCurrentRuntimeData(CurrentGameData);
        CurrentGameData.Validate();

        long now = DateTimeOffset.Now.ToUnixTimeSeconds();

        if (saveMode == GameDataSaveMode.NewSave)
        {
            CurrentGameData.SaveGuid = Guid.NewGuid().ToString("N");
            CurrentGameData.CreatedAtUnixTime = now;
        }
        else if (string.IsNullOrEmpty(CurrentGameData.SaveGuid))
        {
            Debug.LogWarning("覆盖保存 GameData 失败：CurrentGameData 没有 SaveGuid。请使用新建保存。");
            return;
        }

        if (CurrentGameData.CreatedAtUnixTime <= 0)
        {
            CurrentGameData.CreatedAtUnixTime = now;
        }

        CurrentGameData.LastSaveUnixTime = now;

        string saveFolderPath = GetGameSaveFolderPath(CurrentGameData.SaveGuid);
        string gameDataPath = GetGameDataFilePath(CurrentGameData.SaveGuid);

        IOManager.Instance.EnsureDirectory(saveFolderPath);

        GameDataMeta meta = GameDataMeta.CreateFromGameData(CurrentGameData);

        OnGameDataSave?.Invoke(CurrentGameData);

        IOManager.Instance.SaveES3(GameDataKey, CurrentGameData, gameDataPath);
        IOManager.Instance.SaveES3(GameDataMetaKey, meta, gameDataPath);

        AddOrReplaceMeta(meta);

        appData.LastGameGuid = CurrentGameData.SaveGuid;

        SaveGameDataIndex();
        SaveAppData();
    }

    public GameData LoadGameData(string saveGuid)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        if (string.IsNullOrEmpty(saveGuid))
        {
            Debug.LogWarning("读取 GameData 失败：saveGuid 为空。");
            return null;
        }

        string gameDataPath = GetGameDataFilePath(saveGuid);

        if (!IOManager.Instance.ES3KeyExists(GameDataKey, gameDataPath))
        {
            Debug.LogWarning($"读取 GameData 失败：文件或 Key 不存在。{gameDataPath}");
            return null;
        }

        try
        {
            GameData gameData = IOManager.Instance.LoadES3<GameData>(GameDataKey, gameDataPath);

            if (gameData == null)
            {
                Debug.LogWarning($"读取 GameData 失败：结果为空。{gameDataPath}");
                return null;
            }

            gameData.Validate();

            appData.LastGameGuid = gameData.SaveGuid;
            SaveAppData();

            CurrentGameData = gameData;
            RestoreCurrentRuntimeData(CurrentGameData);
            OnGameDataLoaded?.Invoke(CurrentGameData);

            return CurrentGameData;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"读取 GameData 失败：{gameDataPath}\n{e.Message}");
            return null;
        }
    }

    public GameData LoadLastGameData()
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        GameDataMeta meta = GetLastGameDataMeta();

        if (meta == null)
        {
            return null;
        }

        return LoadGameData(meta.SaveGuid);
    }

    public bool DeleteGameData(string saveGuid)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        if (string.IsNullOrEmpty(saveGuid))
        {
            Debug.LogWarning("删除 GameData 失败：saveGuid 为空。");
            return false;
        }

        string saveFolderPath = GetGameSaveFolderPath(saveGuid);

        try
        {
            if (IOManager.Instance.DirectoryExists(saveFolderPath))
            {
                IOManager.Instance.DeleteDirectory(saveFolderPath, true);
            }

            gameDataMetaList.RemoveAll(x => x != null && x.SaveGuid == saveGuid);

            if (appData.LastGameGuid == saveGuid)
            {
                GameDataMeta nextLastMeta = GetLastGameDataMeta();
                appData.LastGameGuid = nextLastMeta != null ? nextLastMeta.SaveGuid : string.Empty;
            }

            if (CurrentGameData != null && CurrentGameData.SaveGuid == saveGuid)
            {
                CurrentGameData = null;
            }

            SaveGameDataIndex();
            SaveAppData();

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"删除 GameData 失败：{saveFolderPath}\n{e.Message}");
            return false;
        }
    }

    public bool CreateBackup(string saveGuid)
    {
        EnsureGameDataIndexLoaded();

        if (string.IsNullOrEmpty(saveGuid))
        {
            Debug.LogWarning("创建备份失败：saveGuid 为空。");
            return false;
        }

        string sourcePath = GetGameDataFilePath(saveGuid);

        if (!IOManager.Instance.FileExists(sourcePath))
        {
            Debug.LogWarning($"创建备份失败：源文件不存在。{sourcePath}");
            return false;
        }

        try
        {
            string backupFolder = IOManager.Instance.GetBackupFolderPath(saveGuid);
            IOManager.Instance.EnsureDirectory(backupFolder);

            string backupFileName = $"{DateTime.Now:yyyyMMdd_HHmmss}_{IOManager.Instance.GetBackupGameDataFileName()}";
            string targetPath = IOManager.Instance.CombinePath(backupFolder, backupFileName);

            IOManager.Instance.CopyFile(sourcePath, targetPath, false);

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"创建备份失败：{saveGuid}\n{e.Message}");
            return false;
        }
    }

    public GameDataMeta GetLastGameDataMeta()
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        if (!string.IsNullOrEmpty(appData.LastGameGuid))
        {
            GameDataMeta lastMeta = gameDataMetaList.Find(x => x.SaveGuid == appData.LastGameGuid);

            if (lastMeta != null)
            {
                return lastMeta;
            }
        }

        if (gameDataMetaList.Count <= 0)
        {
            return null;
        }

        gameDataMetaList.Sort((a, b) => b.LastSaveUnixTime.CompareTo(a.LastSaveUnixTime));
        return gameDataMetaList[0];
    }

    public IReadOnlyList<GameDataMeta> GetAllGameDataMeta()
    {
        EnsureGameDataIndexLoaded();
        return gameDataMetaList;
    }

    private void CaptureCurrentRuntimeData(GameData gameData)
    {
        if (gameData == null)
        {
            return;
        }

    

        Landsong.GameSystem gameSystem = UnityEngine.Object.FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
        if (gameSystem != null && gameSystem.Inventory != null)
        {
            gameData.InventoryData = gameSystem.Inventory.CaptureSaveData();
            gameData.CurrentTurn = gameSystem.CurrentTurn;
        }
    }

    private void RestoreCurrentRuntimeData(GameData gameData)
    {
        if (gameData == null)
        {
            return;
        }

        Landsong.GameSystem gameSystem = UnityEngine.Object.FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
        if (gameSystem != null && gameSystem.Inventory != null && gameData.InventoryData != null)
        {
            gameSystem.Inventory.RestoreSaveData(gameData.InventoryData);
        }
    }
}

[Serializable]
public class AppData
{
    public int DataVersion = 1;

    public bool IsFirstLaunch = true;

    public string LastGameGuid = string.Empty;

    public LocalizationSaveData Language = new LocalizationSaveData();

    public AudioSaveData Audio = new AudioSaveData();

    public static AppData CreateDefault()
    {
        return new AppData
        {
            DataVersion = 1,
            IsFirstLaunch = true,
            LastGameGuid = string.Empty,
            Language = LocalizationSaveData.CreateDefault(),
            Audio = AudioSaveData.CreateDefault()
        };
    }

    public void Validate()
    {
        if (DataVersion <= 0)
        {
            DataVersion = 1;
        }

        if (LastGameGuid == null)
        {
            LastGameGuid = string.Empty;
        }

        if (Language == null)
        {
            Language = LocalizationSaveData.CreateDefault();
        }

        if (Audio == null)
        {
            Audio = AudioSaveData.CreateDefault();
        }

        Language.Validate();
        Audio.Validate();
    }
}

[Serializable]
public class GameDataMeta
{
    public string SaveGuid = string.Empty;

    public string PlayerName = "Player";

    public string MapName = string.Empty;

    public int RoundCount = 0;

    public string Stage = string.Empty;

    public string CurrentSceneName = string.Empty;

    public long CreatedAtUnixTime;

    public long LastSaveUnixTime;

    public float TotalPlayTimeSeconds;

    public int CurrentTurn = 1;

    public int DataVersion = 1;

    public static GameDataMeta CreateFromGameData(GameData gameData)
    {
        if (gameData == null)
        {
            return null;
        }

        return new GameDataMeta
        {
            SaveGuid = gameData.SaveGuid,
            PlayerName = gameData.PlayerName,
            MapName = gameData.MapName,
            RoundCount = gameData.RoundCount,
            Stage = gameData.Stage,
           
            CreatedAtUnixTime = gameData.CreatedAtUnixTime,
            LastSaveUnixTime = gameData.LastSaveUnixTime,
            TotalPlayTimeSeconds = gameData.TotalPlayTimeSeconds,
            CurrentTurn = gameData.CurrentTurn,
            DataVersion = gameData.DataVersion
        };
    }

    public void Validate()
    {
        if (SaveGuid == null)
        {
            SaveGuid = string.Empty;
        }

        if (PlayerName == null)
        {
            PlayerName = "Player";
        }

        if (MapName == null)
        {
            MapName = string.Empty;
        }

        if (Stage == null)
        {
            Stage = string.Empty;
        }

        if (CurrentSceneName == null)
        {
            CurrentSceneName = string.Empty;
        }

        if (DataVersion <= 0)
        {
            DataVersion = 1;
        }

        if (CreatedAtUnixTime <= 0)
        {
            CreatedAtUnixTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        if (LastSaveUnixTime < CreatedAtUnixTime)
        {
            LastSaveUnixTime = CreatedAtUnixTime;
        }

        CurrentTurn = Mathf.Max(1, CurrentTurn);
        RoundCount = Mathf.Max(0, RoundCount);
        TotalPlayTimeSeconds = Mathf.Max(0f, TotalPlayTimeSeconds);
    }

    public DateTime GetLastSaveLocalTime()
    {
        return DateTimeOffset.FromUnixTimeSeconds(LastSaveUnixTime).LocalDateTime;
    }

    public DateTime GetCreatedLocalTime()
    {
        return DateTimeOffset.FromUnixTimeSeconds(CreatedAtUnixTime).LocalDateTime;
    }
}

[Serializable]
public class GameData
{
    //data 版本号
    public const int CurrentDataVersion = 1;

    public int DataVersion = CurrentDataVersion;

    public string SaveGuid = string.Empty;

    public string PlayerName = "Player";


    //----------------新增字段------------------
    public string MapName = string.Empty;

    public int RoundCount = 0;

    public string Stage = string.Empty;



    public long CreatedAtUnixTime;

    public long LastSaveUnixTime;

    public float TotalPlayTimeSeconds;

    public int CurrentTurn = 1;

    public int WorldSeed;

    public InventorySaveData InventoryData;

    public static GameData CreateDefault()
    {
        long now = DateTimeOffset.Now.ToUnixTimeSeconds();

        return new GameData
        {
            DataVersion = CurrentDataVersion,
            SaveGuid = string.Empty,
            PlayerName = "Player",
            MapName = string.Empty,
            RoundCount = 0,
            Stage = string.Empty,
            CreatedAtUnixTime = now,
            LastSaveUnixTime = now,
            TotalPlayTimeSeconds = 0f,
            CurrentTurn = 1,
            WorldSeed = 0,
            InventoryData = null
        };
    }

    public void Validate()
    {
        if (DataVersion < CurrentDataVersion)
        {
            DataVersion = CurrentDataVersion;
        }

        if (SaveGuid == null)
        {
            SaveGuid = string.Empty;
        }

        if (PlayerName == null)
        {
            PlayerName = "Player";
        }

        if (MapName == null)
        {
            MapName = string.Empty;
        }

        if (Stage == null)
        {
            Stage = string.Empty;
        }

        RoundCount = Mathf.Max(0, RoundCount);

        long now = DateTimeOffset.Now.ToUnixTimeSeconds();

        if (CreatedAtUnixTime <= 0)
        {
            CreatedAtUnixTime = now;
        }

        if (LastSaveUnixTime < CreatedAtUnixTime)
        {
            LastSaveUnixTime = CreatedAtUnixTime;
        }

        CurrentTurn = Mathf.Max(1, CurrentTurn);
        TotalPlayTimeSeconds = Mathf.Max(0f, TotalPlayTimeSeconds);
    }
}

[Serializable]
public class LocalizationSaveData
{
    public bool UseSystemLanguage = true;

    public string CurrentLanguageCode = string.Empty;

    public LanguageSource Source = LanguageSource.BuiltIn;

    public string ExternalPackId = string.Empty;

    public string ExternalPackFileName = string.Empty;

    public static LocalizationSaveData CreateDefault()
    {
        return new LocalizationSaveData
        {
            UseSystemLanguage = true,
            CurrentLanguageCode = string.Empty,
            Source = LanguageSource.BuiltIn,
            ExternalPackId = string.Empty,
            ExternalPackFileName = string.Empty
        };
    }

    public void Validate()
    {
        if (CurrentLanguageCode == null)
        {
            CurrentLanguageCode = string.Empty;
        }

        if (ExternalPackId == null)
        {
            ExternalPackId = string.Empty;
        }

        if (ExternalPackFileName == null)
        {
            ExternalPackFileName = string.Empty;
        }

        if (Source == LanguageSource.BuiltIn)
        {
            ExternalPackId = string.Empty;
            ExternalPackFileName = string.Empty;
        }
    }
}

[Serializable]
public class AudioSaveData
{
    public float BgmVolume = 1f;

    public float SfxVolume = 1f;

    public bool IsMuted;

    public static AudioSaveData CreateDefault()
    {
        return new AudioSaveData
        {
            BgmVolume = 1f,
            SfxVolume = 1f,
            IsMuted = false
        };
    }

    public void Validate()
    {
        BgmVolume = Mathf.Clamp01(BgmVolume);
        SfxVolume = Mathf.Clamp01(SfxVolume);
    }
}
