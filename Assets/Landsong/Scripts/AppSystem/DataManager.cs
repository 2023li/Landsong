using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.AppSystem;
using Landsong.BuildingSystem;
using Landsong.DynastySystem;
using Landsong.Persistence;
using Moyo.Unity;
using UnityEngine;

public enum GameDataSaveMode
{
    Overwrite = 0,
    NewSave = 1
}

/// <summary>
/// 存档系统统一公共门面。磁盘读写、索引维护和运行时快照分别委托给独立服务。
/// </summary>
public sealed class DataManager : MonoSingleton<DataManager>
{
    [SerializeField] private AppData appData;
    [SerializeField] private List<GameDataMeta> gameDataMetaList = new List<GameDataMeta>();

    private GameSaveRepository repository;
    private GameSaveIndexService indexService;
    private GameRuntimeSnapshotService runtimeSnapshot;

    public AppData AppData => appData;
    public bool HasLoadedAppData { get; private set; }
    public bool HasLoadedGameDataIndex { get; private set; }
    public string SaveRootPath
    {
        get
        {
            EnsureServices();
            return repository.SaveRootPath;
        }
    }
    public IReadOnlyList<GameDataMeta> GameDataMetaList => gameDataMetaList;
    public GameData CurrentGameData { get; private set; }

    public event Action<GameData> OnGameDataSave;
    public event Action<GameData> OnGameDataLoaded;
    public event Action<GameData> OnRuntimeDataRestoreStarted;
    public event Action<GameData> OnRuntimeDataRestoreCompleted;
    public event Action<AudioSaveData> OnAudioSettingsChanged;

    protected override void Init()
    {
        Initialize();
    }

    public void Initialize()
    {
        EnsureServices();
        repository.Initialize();
        LoadAppData();
        LoadGameDataIndex();
    }

    public void LoadAppData()
    {
        EnsureServices();
        try
        {
            appData = repository.LoadAppData();
            if (appData == null)
            {
                appData = AppData.CreateDefault();
                SaveAppData();
            }
            else
            {
                appData.Validate();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取 AppData 失败，将使用默认数据。\n{exception.Message}");
            appData = AppData.CreateDefault();
            SaveAppData();
        }

        HasLoadedAppData = true;
    }

    public void SaveAppData()
    {
        EnsureServices();
        appData ??= AppData.CreateDefault();
        appData.Validate();
        repository.SaveAppData(appData);
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
        UpdateAudioSettings(data => data.BgmVolume = Mathf.Clamp01(volume));
    }

    public void SetSfxVolume(float volume)
    {
        UpdateAudioSettings(data => data.SfxVolume = Mathf.Clamp01(volume));
    }

    public void SetAmbienceVolume(float volume)
    {
        UpdateAudioSettings(data => data.AmbienceVolume = Mathf.Clamp01(volume));
    }

    public void SetAudioMasterVolume(float volume)
    {
        UpdateAudioSettings(data => data.MasterVolume = Mathf.Clamp01(volume));
    }

    public void SetAudioVolumeGroup(string volumeGroupKey, float volume)
    {
        UpdateAudioSettings(data => data.SetVolumeGroup(volumeGroupKey, volume));
    }

    public void SetAudioChannelVolume(string channelKey, float volume)
    {
        UpdateAudioSettings(data => data.SetChannelVolume(channelKey, volume));
    }

    public void SetMuted(bool muted)
    {
        UpdateAudioSettings(data => data.IsMuted = muted);
    }

    public void LoadGameDataIndex()
    {
        EnsureAppDataLoaded();
        EnsureServices();
        try
        {
            var appChanged = indexService.Reload(appData);
            HasLoadedGameDataIndex = true;
            if (appChanged)
            {
                SaveAppData();
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"重建存档索引失败。\n{exception.Message}");
            gameDataMetaList.Clear();
            HasLoadedGameDataIndex = true;
        }
    }

    public void SaveGameDataIndex()
    {
        EnsureAppDataLoaded();
        EnsureServices();
        if (indexService.Save(appData))
        {
            SaveAppData();
        }
    }

    public GameData CreateNewGame(
        string playerName,
        int worldSeed,
        string mapId,
        string mapDisplayName,
        string dynastyName = "")
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        var gameData = GameData.CreateDefault();
        gameData.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        gameData.WorldSeed = worldSeed;
        gameData.MapId = NormalizeOptionalText(mapId);
        gameData.MapDisplayName = NormalizeOptionalText(mapDisplayName);
        gameData.RequiresInitialMapSetup = true;
        gameData.DynastyName = DynastyService.NormalizeDynastyName(dynastyName);
        gameData.RoundCount = Mathf.Max(1, gameData.CurrentTurn);
        gameData.SaveName = GameData.FormatDefaultSaveName(gameData.DynastyName, gameData.CurrentTurn);
        gameData.Stage = DynastyStage.营地.ToString();

        CurrentGameData = gameData;
        if (SaveCurrentGameInternal(GameDataSaveMode.NewSave, false))
        {
            return gameData;
        }

        var failedSaveGuid = gameData.SaveGuid;
        if (!string.IsNullOrWhiteSpace(failedSaveGuid))
        {
            DeleteGameData(failedSaveGuid);
        }

        CurrentGameData = null;
        return null;
    }

    /// <summary>
    /// 当前存档的唯一保存入口。
    /// </summary>
    public void SaveCurrentGame(GameDataSaveMode saveMode = GameDataSaveMode.Overwrite)
    {
        SaveCurrentGameInternal(saveMode, true);
    }

    public bool TrySaveCurrentGame(GameDataSaveMode saveMode = GameDataSaveMode.Overwrite)
    {
        return SaveCurrentGameInternal(saveMode, true);
    }

    public bool SetCurrentGameSaveName(string saveName)
    {
        if (CurrentGameData == null)
        {
            Debug.LogWarning("设置存档名称失败：CurrentGameData 为空。");
            return false;
        }

        saveName = NormalizeRequiredText(saveName);
        if (string.IsNullOrEmpty(saveName))
        {
            Debug.LogWarning("设置存档名称失败：存档名称不能为空。");
            return false;
        }

        CurrentGameData.SaveName = saveName;
        return true;
    }

    public string GetDefaultCurrentGameSaveName()
    {
        var gameSystem = UnityEngine.Object.FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
        if (gameSystem != null)
        {
            var runtimeDynastyName = gameSystem.Services.Dynasty == null
                ? CurrentGameData?.DynastyName
                : gameSystem.Services.Dynasty.DynastyName;
            return GameData.FormatDefaultSaveName(runtimeDynastyName, gameSystem.Services.Turn.CurrentTurn);
        }

        if (CurrentGameData == null)
        {
            return GameData.FormatDefaultSaveName(DynastyService.DefaultDynastyName, 1);
        }

        CurrentGameData.Validate();
        return GameData.FormatDefaultSaveName(CurrentGameData.DynastyName, CurrentGameData.CurrentTurn);
    }

    public void SetLastSelectedBuilding(BuildingBase building)
    {
        EnsureServices();
        runtimeSnapshot.SetLastSelectedBuilding(CurrentGameData, building);
    }

    public GameData LoadGameData(string saveGuid)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();
        if (string.IsNullOrWhiteSpace(saveGuid))
        {
            Debug.LogWarning("读取 GameData 失败：saveGuid 为空。");
            return null;
        }

        try
        {
            var gameData = repository.LoadGame(saveGuid);
            if (gameData == null)
            {
                Debug.LogWarning($"读取 GameData 失败：文件或 Key 不存在。{saveGuid}");
                return null;
            }

            if (string.IsNullOrWhiteSpace(gameData.SaveGuid))
            {
                gameData.SaveGuid = saveGuid;
            }

            gameData.Validate();
            appData.LastGameGuid = gameData.SaveGuid;
            CurrentGameData = gameData;
            indexService.AddOrReplace(GameDataMeta.CreateFromGameData(gameData));
            SaveGameDataIndex();
            SaveAppData();
            OnGameDataLoaded?.Invoke(CurrentGameData);
            return CurrentGameData;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取 GameData 失败：{saveGuid}\n{exception.Message}");
            return null;
        }
    }

    public GameData LoadLastGameData()
    {
        var meta = GetLastGameDataMeta();
        return meta == null ? null : LoadGameData(meta.SaveGuid);
    }

    public bool DeleteGameData(string saveGuid)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();
        if (string.IsNullOrWhiteSpace(saveGuid))
        {
            Debug.LogWarning("删除 GameData 失败：saveGuid 为空。");
            return false;
        }

        try
        {
            if (!repository.DeleteGame(saveGuid))
            {
                return false;
            }

            indexService.Remove(saveGuid);
            if (CurrentGameData != null && CurrentGameData.SaveGuid == saveGuid)
            {
                CurrentGameData = null;
            }

            if (appData.LastGameGuid == saveGuid)
            {
                appData.LastGameGuid = string.Empty;
                var next = indexService.GetLast(appData);
                appData.LastGameGuid = next == null ? string.Empty : next.SaveGuid;
            }

            SaveGameDataIndex();
            SaveAppData();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"删除 GameData 失败：{saveGuid}\n{exception.Message}");
            return false;
        }
    }

    public bool CreateBackup(string saveGuid)
    {
        EnsureGameDataIndexLoaded();
        try
        {
            var created = repository.CreateBackup(saveGuid);
            if (!created)
            {
                Debug.LogWarning($"创建备份失败：{saveGuid}");
            }
            return created;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"创建备份失败：{saveGuid}\n{exception.Message}");
            return false;
        }
    }

    public GameDataMeta GetLastGameDataMeta()
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();
        return indexService.GetLast(appData);
    }

    public void RestoreCurrentGameDataToRuntime()
    {
        if (CurrentGameData == null)
        {
            return;
        }

        EnsureServices();
        OnRuntimeDataRestoreStarted?.Invoke(CurrentGameData);
        if (!runtimeSnapshot.Restore(CurrentGameData))
        {
            Debug.LogWarning("恢复 GameData 运行时状态失败：GameSystem 不存在。");
            return;
        }

        OnRuntimeDataRestoreCompleted?.Invoke(CurrentGameData);
    }

    public IEnumerator RestoreCurrentGameDataToRuntimeRoutine(int buildingsPerFrame = 16)
    {
        if (CurrentGameData == null)
        {
            yield break;
        }

        EnsureServices();
        OnRuntimeDataRestoreStarted?.Invoke(CurrentGameData);
        var succeeded = false;
        yield return runtimeSnapshot.RestoreRoutine(
            CurrentGameData,
            buildingsPerFrame,
            result => succeeded = result);
        if (!succeeded)
        {
            Debug.LogWarning("恢复 GameData 运行时状态失败：GameSystem 不存在。");
            yield break;
        }

        OnRuntimeDataRestoreCompleted?.Invoke(CurrentGameData);
    }

    private bool SaveCurrentGameInternal(GameDataSaveMode saveMode, bool captureRuntimeData)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();
        if (CurrentGameData == null)
        {
            Debug.LogWarning("保存 GameData 失败：CurrentGameData 为空。");
            return false;
        }

        EnsureServices();
        if (captureRuntimeData)
        {
            runtimeSnapshot.Capture(CurrentGameData);
        }

        CurrentGameData.Validate();
        var now = DateTimeOffset.Now.ToUnixTimeSeconds();
        if (saveMode == GameDataSaveMode.NewSave)
        {
            CurrentGameData.SaveGuid = Guid.NewGuid().ToString("N");
            CurrentGameData.CreatedAtUnixTime = now;
        }
        else if (string.IsNullOrWhiteSpace(CurrentGameData.SaveGuid))
        {
            Debug.LogWarning("覆盖保存 GameData 失败：CurrentGameData 没有 SaveGuid。请使用新建保存。");
            return false;
        }

        if (CurrentGameData.CreatedAtUnixTime <= 0)
        {
            CurrentGameData.CreatedAtUnixTime = now;
        }

        CurrentGameData.LastSaveUnixTime = now;
        var meta = GameDataMeta.CreateFromGameData(CurrentGameData);
        try
        {
            OnGameDataSave?.Invoke(CurrentGameData);
            repository.SaveGame(CurrentGameData, meta);
            indexService.AddOrReplace(meta);
            appData.LastGameGuid = CurrentGameData.SaveGuid;
            SaveGameDataIndex();
            SaveAppData();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"保存 GameData 失败：{CurrentGameData.SaveGuid}\n{exception.Message}");
            return false;
        }
    }

    private void UpdateAudioSettings(Action<AudioSaveData> update)
    {
        EnsureAppDataLoaded();
        appData.Audio ??= AudioSaveData.CreateDefault();
        update?.Invoke(appData.Audio);
        appData.Audio.Validate();
        SaveAppData();
        OnAudioSettingsChanged?.Invoke(appData.Audio);
    }

    private void EnsureServices()
    {
        gameDataMetaList ??= new List<GameDataMeta>();
        repository ??= new GameSaveRepository();
        indexService ??= new GameSaveIndexService(repository, gameDataMetaList);
        runtimeSnapshot ??= new GameRuntimeSnapshotService();
    }

    private void EnsureGameDataIndexLoaded()
    {
        if (!HasLoadedGameDataIndex)
        {
            LoadGameDataIndex();
        }
    }

    private static string NormalizeOptionalText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }

    private static string NormalizeRequiredText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
    }
}
