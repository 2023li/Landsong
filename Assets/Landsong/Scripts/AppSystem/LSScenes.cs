using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Landsong.BuildingSystem;
using Landsong.CameraSystem;
using Landsong.GridSystem;
using Moyo.Unity;
using UnityEngine;

public class LoadScene_Start : SceneLoadingPipeline
{
    public override string TargetSceneName => "Start";


    UIPanel_Loading panel;



    public override IEnumerator OnPrepareTransition()
    {
        Task<UIPanel_Loading> openTask = UIManager.Instance.OpenAsync<UIPanel_Loading>(UIPanel_Loading.LoadingItemType.Item_标题);

        yield return LSSceneTask.WaitForTask(openTask);

        if (openTask.Status == TaskStatus.RanToCompletion)
        {
            panel = openTask.Result;
        }



    }



    public override IEnumerator OnBeginWaitPlayerConfirm()
    {
        yield return LSSceneTask.WaitForTask(UIManager.Instance.PreloadAsync<UIPanel_MainMenu>());

        panel?.BeginWaitPlayerConfirm();

        yield return base.OnBeginWaitPlayerConfirm();
    }

    public override IEnumerator OnBeforeExitCurrentScene()
    {
        yield return MapContentSceneLoader.UnloadRoutine();
    }
    public override IEnumerator OnPlayerConfirmed()
    {
        yield return LSSceneTask.WaitForTask(UIManager.Instance.OpenAsync<UIPanel_MainMenu>());
        yield return null;

        yield return LSSceneTask.WaitForTask(UIManager.Instance.CloseAsync<UIPanel_Loading>());
    }

    public static bool Load()
    {
        return SceneTransitionManager.Instance.StartTransition(new LoadScene_Start());
    }

    public static bool ReturnFromGame()
    {
        _ = UIManager.Instance.CloseAsync<UIPanel_Game>();

        return SceneTransitionManager.Instance.StartTransition(new LoadScene_Start());
    }
}

public class LoadScene_Game : SceneLoadingPipeline
{
    private const int RestoreBuildingsPerFrame = 16;

    public override string TargetSceneName => "Game";
    public override bool NeedPlayerConfirm => !loadFailed;

    internal static bool Load()
    {
        return SceneTransitionManager.Instance.StartTransition(new LoadScene_Game());
    }
    UIPanel_Loading panel;
    private bool loadFailed;
    private string loadFailureMessage = string.Empty;
    public override IEnumerator OnPrepareTransition()
    {
        Task<UIPanel_Loading> openTask = UIManager.Instance.OpenAsync<UIPanel_Loading>(UIPanel_Loading.LoadingItemType.Item_进度条);

        yield return LSSceneTask.WaitForTask(openTask);

        if (openTask.Status == TaskStatus.RanToCompletion)
        {
            panel = openTask.Result;
            SubscribeLoadingProgress();
        }
    }

    public override IEnumerator OnTargetSceneLoaded()
    {
        yield return LoadAndBindCurrentMapRoutine();
        if (loadFailed)
        {
            yield break;
        }

        RefreshCameraViewBounds();

        yield return DataManager.Instance.RestoreCurrentGameDataToRuntimeRoutine(RestoreBuildingsPerFrame);

        var gameData = DataManager.Instance.CurrentGameData;
        if (gameData != null && gameData.RequiresInitialMapSetup)
        {
            yield return CreateAndSaveInitialBuildingsRoutine(gameData);
            if (loadFailed)
            {
                yield break;
            }
        }

        RefreshCameraViewBounds();
        FocusCameraOnLoadTarget();

        yield return LSSceneTask.WaitForTask(UIManager.Instance.PreloadAsync<UIPanel_Game>());
    }

    public override IEnumerator OnWaitTargetSceneInitialize()
    {
        yield return null;
        yield return base.OnWaitTargetSceneInitialize();
    }

    public override IEnumerator OnBeginWaitPlayerConfirm()
    {
        panel?.BeginWaitPlayerConfirm();
        yield return base.OnBeginWaitPlayerConfirm();
    }

    public override IEnumerator OnPlayerConfirmed()
    {
        UnsubscribeLoadingProgress();

        yield return LSSceneTask.WaitForTask(UIManager.Instance.CloseAsync<UIPanel_LoadGame>());
        yield return LSSceneTask.WaitForTask(UIManager.Instance.CloseAsync<UIPanel_MainMenu>());

        Task<UIPanel_Game> openTask = UIManager.Instance.OpenAsync<UIPanel_Game>();
        yield return LSSceneTask.WaitForTask(openTask);

        if (openTask.Status == TaskStatus.RanToCompletion && openTask.Result != null)
        {
            openTask.Result.Show_HUD();
        }

        yield return null;
        yield return LSSceneTask.WaitForTask(UIManager.Instance.CloseAsync<UIPanel_Loading>());
    }

    public override IEnumerator OnCompleted()
    {
        UnsubscribeLoadingProgress();
        if (loadFailed)
        {
            Debug.LogError($"进入游戏失败：{loadFailureMessage}");
            SceneTransitionManager.Instance.StartCoroutine(ReturnToStartAfterTransition());
        }

        yield return base.OnCompleted();
    }

    private IEnumerator LoadAndBindCurrentMapRoutine()
    {
        var catalog = MapDataCatalog.Instance;
        var gameData = DataManager.Instance.CurrentGameData;
        if (catalog == null)
        {
            FailLoad("MapDataCatalog 尚未加载。");
            yield break;
        }

        if (gameData == null || string.IsNullOrWhiteSpace(gameData.MapId))
        {
            FailLoad("当前存档没有有效 mapId。");
            yield break;
        }

        if (!catalog.TryGetMapDefinition(gameData.MapId, out var definition))
        {
            FailLoad($"MapCatalog 中不存在 mapId：{gameData.MapId}");
            yield break;
        }

        var loaded = false;
        var error = string.Empty;
        yield return MapContentSceneLoader.LoadRoutine(
            definition,
            (succeeded, message) =>
            {
                loaded = succeeded;
                error = message;
            });
        if (!loaded)
        {
            FailLoad(error);
            yield break;
        }

        var host = MapRuntimeHost.Active;
        if (host == null || !host.TryBind(MapContentSceneLoader.ActiveContent, out error))
        {
            yield return MapContentSceneLoader.UnloadRoutine();
            FailLoad(string.IsNullOrWhiteSpace(error) ? "Game Scene 中缺少 MapRuntimeHost。" : error);
            yield break;
        }

        gameData.MapDisplayName = definition.DisplayName;
    }

    private IEnumerator CreateAndSaveInitialBuildingsRoutine(GameData gameData)
    {
        var host = MapRuntimeHost.Active;
        var gameSystem = UnityEngine.Object.FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
        var error = string.Empty;
        IReadOnlyList<BuildingBase> created = null;
        if (host == null
            || gameSystem == null
            || !host.TryCreateInitialBuildings(gameSystem, out created, out error))
        {
            RollbackFailedNewGame(gameData, created: null);
            FailLoad(string.IsNullOrWhiteSpace(error) ? "初始建筑生成失败。" : error);
            yield break;
        }

        gameData.RequiresInitialMapSetup = false;
        if (!DataManager.Instance.TrySaveCurrentGame(GameDataSaveMode.Overwrite))
        {
            gameData.RequiresInitialMapSetup = true;
            RollbackFailedNewGame(gameData, created);
            FailLoad("初始建筑快照保存失败。");
        }

        yield break;
    }

    private static void RollbackFailedNewGame(GameData gameData, IReadOnlyList<BuildingBase> created)
    {
        var gameSystem = UnityEngine.Object.FindFirstObjectByType<Landsong.GameSystem>(FindObjectsInactive.Include);
        var service = gameSystem?.Services?.Buildings;
        if (created != null && service != null)
        {
            for (var i = created.Count - 1; i >= 0; i--)
            {
                service.Remove(created[i]);
            }
        }

        var saveGuid = gameData == null ? string.Empty : gameData.SaveGuid;
        if (!string.IsNullOrWhiteSpace(saveGuid) && !DataManager.Instance.DeleteGameData(saveGuid))
        {
            Debug.LogError($"删除半初始化新游戏存档失败：{saveGuid}");
        }
    }

    private void FailLoad(string message)
    {
        loadFailed = true;
        loadFailureMessage = string.IsNullOrWhiteSpace(message) ? "未知地图加载错误。" : message.Trim();
    }

    private static IEnumerator ReturnToStartAfterTransition()
    {
        yield return null;
        LoadScene_Start.Load();
    }

    private void RefreshCameraViewBounds()
    {
        var cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        cameraController?.RefreshGridMapViewBounds();
    }

    private void FocusCameraOnLoadTarget()
    {
        var cameraController = UnityEngine.Object.FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        cameraController?.RefreshGridMapViewBounds();

        if (!TryResolveLoadFocusBuilding(cameraController, out var focusBuilding))
        {
            return;
        }

        cameraController?.SnapToBuilding(focusBuilding);

        var selectionController = UnityEngine.Object.FindFirstObjectByType<BuildingSelectionController>(FindObjectsInactive.Include);
        if (selectionController != null && selectionController.isActiveAndEnabled)
        {
            selectionController.SelectBuilding(focusBuilding);
        }
    }

    private static bool TryResolveLoadFocusBuilding(CameraController cameraController, out BuildingBase building)
    {
        building = null;

        var gameData = DataManager.Instance.CurrentGameData;
        var lastSelectedBuilding = gameData == null ? null : gameData.SoftData?.LastSelectedBuilding;
        if (lastSelectedBuilding != null && !lastSelectedBuilding.IsEmpty && TryFindBuilding(lastSelectedBuilding, out building))
        {
            return true;
        }

        var defaultFocusBuildingId = cameraController == null ? "building.player_home" : cameraController.DefaultFocusBuildingId;
        return TryFindBuildingById(defaultFocusBuildingId, out building);
    }

    private static bool TryFindBuilding(BuildingSoftReferenceSaveData softReference, out BuildingBase building)
    {
        building = null;
        if (softReference == null || softReference.IsEmpty)
        {
            return false;
        }

        softReference.Validate();
        return TryFindBuildingInternal(softReference.Matches, out building);
    }

    private static bool TryFindBuildingById(string buildingId, out BuildingBase building)
    {
        building = null;
        buildingId = string.IsNullOrWhiteSpace(buildingId) ? string.Empty : buildingId.Trim();
        if (string.IsNullOrEmpty(buildingId))
        {
            return false;
        }

        return TryFindBuildingInternal(
            candidate => candidate.HasDefinition
                         && string.Equals(candidate.FamilyId, buildingId, StringComparison.Ordinal),
            out building);
    }

    private static bool TryFindBuildingInternal(Predicate<BuildingBase> predicate, out BuildingBase building)
    {
        building = null;
        if (predicate == null)
        {
            return false;
        }

        if (Landsong.GameSystem.TryGetInstance(out var gameSystem) && gameSystem.Services.Buildings != null)
        {
            var buildings = gameSystem.Services.Buildings.Buildings;
            for (var i = 0; i < buildings.Count; i++)
            {
                var candidate = buildings[i];
                if (CanFocusBuilding(candidate) && predicate(candidate))
                {
                    building = candidate;
                    return true;
                }
            }
        }

        var sceneBuildings = UnityEngine.Object.FindObjectsByType<BuildingBase>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);
        for (var i = 0; i < sceneBuildings.Length; i++)
        {
            var candidate = sceneBuildings[i];
            if (CanFocusBuilding(candidate) && predicate(candidate))
            {
                building = candidate;
                return true;
            }
        }

        return false;
    }

    private static bool CanFocusBuilding(BuildingBase building)
    {
        return building != null && building.isActiveAndEnabled && !building.IsDemolishing && building.HasDefinition;
    }

    private void SubscribeLoadingProgress()
    {
        if (panel == null || SceneTransitionManager.Instance == null)
        {
            return;
        }

        SceneTransitionManager.Instance.OnProgressChanged -= panel.ProgressChanged;
        SceneTransitionManager.Instance.OnProgressChanged += panel.ProgressChanged;
        panel.ProgressChanged(SceneTransitionManager.Instance.Progress);
    }

    private void UnsubscribeLoadingProgress()
    {
        if (panel == null || SceneTransitionManager.Instance == null)
        {
            return;
        }

        SceneTransitionManager.Instance.OnProgressChanged -= panel.ProgressChanged;
    }

}

internal static class LSSceneTask
{
    public static IEnumerator WaitForTask(Task task)
    {
        if (task == null)
        {
            yield break;
        }

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsFaulted)
        {
            Debug.LogException(task.Exception);
        }
    }
}
