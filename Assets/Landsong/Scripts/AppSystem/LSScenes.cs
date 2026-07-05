using System;
using System.Collections;
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

    internal static bool Load()
    {
        return SceneTransitionManager.Instance.StartTransition(new LoadScene_Game());
    }
    UIPanel_Loading panel;
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
        SpawnCurrentMap();
        RefreshCameraViewBounds();

        yield return DataManager.Instance.RestoreCurrentGameDataToRuntimeRoutine(RestoreBuildingsPerFrame);

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
        yield return base.OnCompleted();
    }

    private void SpawnCurrentMap()
    {
        if (UnityEngine.Object.FindFirstObjectByType<GridMapBehaviour>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        MapDataCatalog catalog = MapDataCatalog.Instance;
        if (catalog == null)
        {
            Debug.LogWarning("加载地图失败：MapDataCatalog 尚未加载。");
            return;
        }

        GameData gameData = DataManager.Instance.CurrentGameData;
        MapDataCatalog.MapData mapData = null;

        if (gameData != null && !string.IsNullOrWhiteSpace(gameData.MapName))
        {
            catalog.TryGetMapData(gameData.MapName, out mapData);

            if (mapData == null)
            {
                Debug.LogWarning($"加载地图失败：存档地图不在 MapCatalog 中。MapName = {gameData.MapName}");
            }
        }

        if (mapData == null)
        {
            mapData = catalog.GetFirstValidMapData();
        }

        if (mapData == null || !mapData.IsValid)
        {
            Debug.LogWarning("加载地图失败：MapCatalog 中没有有效地图。");
            return;
        }

        if (gameData != null && string.IsNullOrWhiteSpace(gameData.MapName))
        {
            gameData.MapName = mapData.DisplayName;
        }

        GridMapBehaviour mapInstance = UnityEngine.Object.Instantiate(mapData.Map);
        mapInstance.name = mapData.DisplayName;
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

        var defaultFocusBuildingId = cameraController == null ? "PlayerHomeLV1" : cameraController.DefaultFocusBuildingId;
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
                         && string.Equals(candidate.Definition.BuildingId, buildingId, StringComparison.Ordinal),
            out building);
    }

    private static bool TryFindBuildingInternal(Predicate<BuildingBase> predicate, out BuildingBase building)
    {
        building = null;
        if (predicate == null)
        {
            return false;
        }

        if (Landsong.GameSystem.TryGetInstance(out var gameSystem) && gameSystem.Buildings != null)
        {
            var buildings = gameSystem.Buildings.Buildings;
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
