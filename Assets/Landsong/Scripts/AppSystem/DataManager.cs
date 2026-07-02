using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GridSystem;
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

    public event Action<GameData> OnRuntimeDataRestoreStarted;

    public event Action<GameData> OnRuntimeDataRestoreCompleted;

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

    public GameData CreateNewGame(string playerName, int worldSeed, string mapName = "")
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        GameData gameData = GameData.CreateDefault();
        gameData.PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        gameData.WorldSeed = worldSeed;
        gameData.MapName = NormalizeOptionalText(mapName);

        CurrentGameData = gameData;
        SaveGameData(GameDataSaveMode.NewSave, false);

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
        SaveGameData(saveMode, true);
    }

    private void SaveGameData(GameDataSaveMode saveMode, bool captureRuntimeData)
    {
        EnsureAppDataLoaded();
        EnsureGameDataIndexLoaded();

        if (CurrentGameData == null)
        {
            Debug.LogWarning("保存 GameData 失败：CurrentGameData 为空。");
            return;
        }

        if (captureRuntimeData)
        {
            CaptureCurrentRuntimeData(CurrentGameData);
        }

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

    public void RestoreCurrentGameDataToRuntime()
    {
        if (CurrentGameData == null)
        {
            return;
        }

        if (!RestoreCurrentRuntimeData(CurrentGameData))
        {
            Debug.LogWarning("恢复 GameData 运行时状态失败：GameSystem 不存在。");
        }
    }

    public IEnumerator RestoreCurrentGameDataToRuntimeRoutine(int buildingsPerFrame = 16)
    {
        if (CurrentGameData == null)
        {
            yield break;
        }

        yield return RestoreCurrentRuntimeDataRoutine(CurrentGameData, buildingsPerFrame);
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

        if (gameSystem != null && gameSystem.Buildings != null)
        {
            gameData.BuildingInstances = CaptureBuildingInstances(gameSystem.Buildings.Buildings);
        }
    }

    private bool RestoreCurrentRuntimeData(GameData gameData)
    {
        if (!PrepareRuntimeRestore(gameData, out var gameSystem))
        {
            return false;
        }

        RestoreBuildingInstances(gameData, gameSystem);
        OnRuntimeDataRestoreCompleted?.Invoke(gameData);
        return true;
    }

    private IEnumerator RestoreCurrentRuntimeDataRoutine(GameData gameData, int buildingsPerFrame)
    {
        if (!PrepareRuntimeRestore(gameData, out var gameSystem))
        {
            Debug.LogWarning("恢复 GameData 运行时状态失败：GameSystem 不存在。");
            yield break;
        }

        yield return RestoreBuildingInstancesRoutine(gameData, gameSystem, buildingsPerFrame);
        OnRuntimeDataRestoreCompleted?.Invoke(gameData);
    }

    private bool PrepareRuntimeRestore(GameData gameData, out Landsong.GameSystem gameSystem)
    {
        gameSystem = null;
        if (gameData == null)
        {
            return false;
        }

        gameSystem = UnityEngine.Object.FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
        if (gameSystem == null)
        {
            return false;
        }

        gameData.Validate();
        OnRuntimeDataRestoreStarted?.Invoke(gameData);

        gameSystem.RestoreCurrentTurn(gameData.CurrentTurn);

        if (gameSystem.Inventory != null && gameData.InventoryData != null)
        {
            gameSystem.Inventory.RestoreSaveData(gameData.InventoryData);
        }

        return true;
    }

    private static List<BuildingInstanceSaveData> CaptureBuildingInstances(IReadOnlyList<BuildingBase> buildings)
    {
        var saveData = new List<BuildingInstanceSaveData>();
        if (buildings == null)
        {
            return saveData;
        }

        for (var i = 0; i < buildings.Count; i++)
        {
            var building = buildings[i];
            if (!CanCaptureBuilding(building))
            {
                continue;
            }

            var buildingSaveData = BuildingInstanceSaveData.CreateFromBuilding(building);
            CaptureBuildingData(building, buildingSaveData);
            saveData.Add(buildingSaveData);
        }

        return saveData;
    }

    private static bool CanCaptureBuilding(BuildingBase building)
    {
        return building != null
               && building.isActiveAndEnabled
               && !building.IsDemolishing
               && building.HasDefinition
               && building.HasPlacement;
    }

    private static void CaptureBuildingData(BuildingBase building, BuildingInstanceSaveData saveData)
    {
        if (building == null || saveData == null)
        {
            return;
        }

        var data = building.CaptureSaveData();
        if (data == null)
        {
            return;
        }

        saveData.BuildingDataType = data.GetType().AssemblyQualifiedName;
        saveData.BuildingDataJson = JsonUtility.ToJson(data);
    }

    private void RestoreBuildingInstances(GameData gameData, Landsong.GameSystem gameSystem)
    {
        if (gameData == null || gameSystem == null || gameData.BuildingInstances == null)
        {
            return;
        }

        ClearCurrentRuntimeBuildings(gameSystem);
        RestoreBuildingInstancesInternal(gameData.BuildingInstances, gameSystem);
    }

    private IEnumerator RestoreBuildingInstancesRoutine(GameData gameData, Landsong.GameSystem gameSystem, int buildingsPerFrame)
    {
        if (gameData == null || gameSystem == null || gameData.BuildingInstances == null)
        {
            yield break;
        }

        ClearCurrentRuntimeBuildings(gameSystem);
        yield return null;

        buildingsPerFrame = Mathf.Max(1, buildingsPerFrame);
        var restoredThisFrame = 0;
        var buildingInstances = gameData.BuildingInstances;

        for (var i = 0; i < buildingInstances.Count; i++)
        {
            if (RestoreBuildingInstance(buildingInstances[i], gameSystem))
            {
                restoredThisFrame++;
            }

            if (restoredThisFrame < buildingsPerFrame || i >= buildingInstances.Count - 1)
            {
                continue;
            }

            restoredThisFrame = 0;
            yield return null;
        }
    }

    private void RestoreBuildingInstancesInternal(
        IReadOnlyList<BuildingInstanceSaveData> buildingInstances,
        Landsong.GameSystem gameSystem)
    {
        if (buildingInstances == null || gameSystem == null)
        {
            return;
        }

        for (var i = 0; i < buildingInstances.Count; i++)
        {
            RestoreBuildingInstance(buildingInstances[i], gameSystem);
        }
    }

    private bool RestoreBuildingInstance(BuildingInstanceSaveData saveData, Landsong.GameSystem gameSystem)
    {
        if (saveData == null || gameSystem == null || gameSystem.Buildings == null)
        {
            return false;
        }

        saveData.Validate();
        if (!saveData.IsValid)
        {
            return false;
        }

        var gridMap = UnityEngine.Object.FindFirstObjectByType<GridMapBehaviour>(FindObjectsInactive.Include);
        if (gridMap == null)
        {
            Debug.LogWarning($"恢复建筑失败：场景中没有 GridMapBehaviour。BuildingId = {saveData.BuildingId}");
            return false;
        }

        var catalog = ResolveBuildingCatalog(gameSystem);
        if (catalog == null || !catalog.TryGetBuildingPrefab(saveData.BuildingId, out var buildingPrefab))
        {
            Debug.LogWarning($"恢复建筑失败：建筑目录中找不到 BuildingId = {saveData.BuildingId}");
            return false;
        }

        var parent = ResolveRestoredBuildingParent(gridMap);
        if (!gameSystem.Buildings.TryPlace(
                buildingPrefab,
                gridMap,
                saveData.Origin,
                saveData.Rotation,
                parent,
                out var building))
        {
            Debug.LogWarning($"恢复建筑失败：无法放置 BuildingId = {saveData.BuildingId}, Origin = {saveData.Origin}");
            return false;
        }

        var buildingData = RestoreBuildingData(saveData);
        if (buildingData != null)
        {
            building.RestoreSaveData(buildingData);
        }

        gameSystem.RegisterBuilding(building);
        return true;
    }

    private static BuildingCatalog ResolveBuildingCatalog(Landsong.GameSystem gameSystem)
    {
        if (gameSystem != null && gameSystem.BuildingCatalog != null)
        {
            return gameSystem.BuildingCatalog;
        }

        return BuildingCatalog.Instance;
    }

    private static Transform ResolveRestoredBuildingParent(GridMapBehaviour gridMap)
    {
        if (gridMap == null)
        {
            return null;
        }

        const string restoredBuildingRootName = "Restored Buildings";
        var existingRoot = gridMap.transform.Find(restoredBuildingRootName);
        if (existingRoot != null)
        {
            return existingRoot;
        }

        var root = new GameObject(restoredBuildingRootName);
        root.transform.SetParent(gridMap.transform, false);
        return root.transform;
    }

    private static BuildingDataBase RestoreBuildingData(BuildingInstanceSaveData saveData)
    {
        if (saveData == null
            || string.IsNullOrWhiteSpace(saveData.BuildingDataType)
            || string.IsNullOrWhiteSpace(saveData.BuildingDataJson))
        {
            return null;
        }

        var dataType = Type.GetType(saveData.BuildingDataType);
        if (dataType == null)
        {
            Debug.LogWarning($"恢复建筑数据失败：找不到数据类型 {saveData.BuildingDataType}");
            return null;
        }

        if (!typeof(BuildingDataBase).IsAssignableFrom(dataType))
        {
            Debug.LogWarning($"恢复建筑数据失败：{saveData.BuildingDataType} 不是 BuildingDataBase。");
            return null;
        }

        try
        {
            var data = (BuildingDataBase)Activator.CreateInstance(dataType, true);
            JsonUtility.FromJsonOverwrite(saveData.BuildingDataJson, data);
            return data;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"恢复建筑数据失败：{saveData.BuildingId}\n{e.Message}");
            return null;
        }
    }

    private static void ClearCurrentRuntimeBuildings(Landsong.GameSystem gameSystem)
    {
        var sceneBuildings = UnityEngine.Object.FindObjectsByType<BuildingBase>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (var i = 0; i < sceneBuildings.Length; i++)
        {
            var building = sceneBuildings[i];
            if (building == null || building.IsDemolishing || !building.gameObject.scene.IsValid())
            {
                continue;
            }

            gameSystem?.UnregisterBuilding(building);
            building.ClearPlacement();
            UnityEngine.Object.Destroy(building.gameObject);
        }

        gameSystem?.Turn?.ClearBuildings();
    }

    private static string NormalizeOptionalText(string text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim();
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
    public const int CurrentDataVersion = 2;

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

    public List<BuildingInstanceSaveData> BuildingInstances;

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
            InventoryData = null,
            BuildingInstances = null
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

        if (BuildingInstances != null)
        {
            for (var i = BuildingInstances.Count - 1; i >= 0; i--)
            {
                var building = BuildingInstances[i];
                if (building == null)
                {
                    BuildingInstances.RemoveAt(i);
                    continue;
                }

                building.Validate();
            }
        }
    }
}

[Serializable]
public class BuildingInstanceSaveData
{
    public string BuildingId = string.Empty;

    public int OriginX;

    public int OriginY;

    public float RotationX;

    public float RotationY;

    public float RotationZ;

    public float RotationW = 1f;

    public string BuildingDataType = string.Empty;

    public string BuildingDataJson = string.Empty;

    public bool IsValid => !string.IsNullOrWhiteSpace(BuildingId);

    public GridPosition Origin => new GridPosition(OriginX, OriginY);

    public Quaternion Rotation => new Quaternion(RotationX, RotationY, RotationZ, RotationW);

    public static BuildingInstanceSaveData CreateFromBuilding(BuildingBase building)
    {
        if (building == null || !building.HasDefinition || !building.HasPlacement)
        {
            return null;
        }

        var rotation = building.transform.rotation;
        return new BuildingInstanceSaveData
        {
            BuildingId = building.Definition.BuildingId,
            OriginX = building.Origin.X,
            OriginY = building.Origin.Y,
            RotationX = rotation.x,
            RotationY = rotation.y,
            RotationZ = rotation.z,
            RotationW = rotation.w,
            BuildingDataType = string.Empty,
            BuildingDataJson = string.Empty
        };
    }

    public void Validate()
    {
        BuildingId = string.IsNullOrWhiteSpace(BuildingId) ? string.Empty : BuildingId.Trim();
        BuildingDataType = string.IsNullOrWhiteSpace(BuildingDataType) ? string.Empty : BuildingDataType.Trim();
        BuildingDataJson ??= string.Empty;

        if (Mathf.Approximately(RotationX, 0f)
            && Mathf.Approximately(RotationY, 0f)
            && Mathf.Approximately(RotationZ, 0f)
            && Mathf.Approximately(RotationW, 0f))
        {
            RotationW = 1f;
        }
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
