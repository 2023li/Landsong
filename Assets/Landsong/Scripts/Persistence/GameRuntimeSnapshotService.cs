using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.GridSystem;
using UnityEngine;

namespace Landsong.Persistence
{
    /// <summary>
    /// 在 GameData 与当前运行时世界之间建立快照。它不读写磁盘，也不维护存档索引。
    /// </summary>
    public sealed class GameRuntimeSnapshotService
    {
        public void Capture(GameData gameData)
        {
            if (gameData == null)
            {
                return;
            }

            var gameSystem = ResolveGameSystem();
            if (gameSystem != null)
            {
                var services = gameSystem.Services;
                gameData.CurrentTurn = services.Turn == null ? gameSystem.CurrentTurn : services.Turn.CurrentTurn;
                gameData.RoundCount = Mathf.Max(1, gameData.CurrentTurn);

                gameData.TechnologyData = services.Technology == null
                    ? gameSystem.CaptureTechnologyData()
                    : services.Technology.CaptureSaveData();
                gameData.UnlockedTechnologies = gameData.TechnologyData == null
                    ? gameSystem.CaptureUnlockedTechnologies()
                    : new List<string>(gameData.TechnologyData.UnlockedTechnologyIds);
                gameData.QuestData = services.Quest == null
                    ? gameSystem.CaptureQuestData()
                    : services.Quest.CaptureSaveData();
                gameData.ExpeditionData = services.Expeditions == null
                    ? gameSystem.CaptureExpeditionData()
                    : services.Expeditions.CaptureSaveData();
                gameData.TalentData = services.Talents == null
                    ? gameSystem.CaptureTalentData()
                    : services.Talents.CaptureSaveData();
                gameData.RoyalInheritanceData = services.Inheritance == null
                    ? gameSystem.CaptureInheritanceData()
                    : services.Inheritance.CaptureSaveData();
                gameData.UnlockedBuildingBlueprintIds = gameSystem.CaptureUnlockedBuildingBlueprints();

                if (services.Dynasty != null)
                {
                    gameData.DynastyName = services.Dynasty.DynastyName;
                    gameData.Stage = services.Dynasty.Stage.ToString();
                    gameData.BasePopulation = services.Dynasty.BasePopulation;
                }

                if (services.Inventory != null)
                {
                    gameData.InventoryData = services.Inventory.CaptureSaveData();
                }

                if (services.Buildings != null)
                {
                    gameData.BuildingInstances = CaptureBuildingInstances(services.Buildings.Buildings);
                }
            }

            CaptureSoftData(gameData, gameSystem);
        }

        public bool Restore(GameData gameData)
        {
            if (!PrepareRestore(gameData, out var gameSystem))
            {
                return false;
            }

            var succeeded = false;
            try
            {
                RestoreBuildingInstances(gameData, gameSystem);
                succeeded = true;
                return true;
            }
            finally
            {
                gameSystem.Services.Quest?.EndRuntimeRestore();
                if (!succeeded)
                {
                    Debug.LogWarning("运行时快照恢复未完成，任务系统已退出恢复抑制状态。");
                }
            }
        }

        public IEnumerator RestoreRoutine(GameData gameData, int buildingsPerFrame, Action<bool> completed)
        {
            if (!PrepareRestore(gameData, out var gameSystem))
            {
                completed?.Invoke(false);
                yield break;
            }

            var succeeded = false;
            try
            {
                yield return RestoreBuildingInstancesRoutine(gameData, gameSystem, buildingsPerFrame);
                succeeded = true;
            }
            finally
            {
                gameSystem.Services.Quest?.EndRuntimeRestore();
                completed?.Invoke(succeeded);
            }
        }

        public void SetLastSelectedBuilding(GameData gameData, BuildingBase building)
        {
            if (gameData == null || !CanCaptureSoftBuildingReference(building))
            {
                return;
            }

            gameData.SoftData ??= GameSoftData.CreateDefault();
            gameData.SoftData.LastSelectedBuilding = BuildingSoftReferenceSaveData.CreateFromBuilding(building);
            gameData.SoftData.Validate();
        }

        private static GameSystem ResolveGameSystem()
        {
            if (GameSystem.TryGetInstance(out var gameSystem))
            {
                return gameSystem;
            }

            return UnityEngine.Object.FindFirstObjectByType<GameSystem>(FindObjectsInactive.Include);
        }

        private static bool PrepareRestore(GameData gameData, out GameSystem gameSystem)
        {
            gameSystem = ResolveGameSystem();
            if (gameData == null || gameSystem == null)
            {
                return false;
            }

            gameData.Validate();
            gameSystem.RestoreCurrentTurn(gameData.CurrentTurn);
            gameSystem.RestoreDynastyData(gameData.DynastyName, gameData.Stage, gameData.BasePopulation);
            gameSystem.RestoreTechnologyData(gameData.TechnologyData, gameData.UnlockedTechnologies);
            gameSystem.RestoreBuildingBlueprintData(gameData.UnlockedBuildingBlueprintIds);

            var services = gameSystem.Services;
            if (services.Inventory != null && gameData.InventoryData != null)
            {
                services.Inventory.RestoreSaveData(gameData.InventoryData);
            }

            gameSystem.RestoreExpeditionData(gameData.ExpeditionData);
            gameSystem.RestoreTalentData(gameData.TalentData);
            gameSystem.RestoreInheritanceData(gameData.RoyalInheritanceData);
            services.Quest?.BeginRuntimeRestore();
            if (services.Quest == null)
            {
                gameSystem.BeginQuestRuntimeRestore();
                gameSystem.RestoreQuestData(gameData.QuestData);
            }
            else
            {
                services.Quest.RestoreSaveData(gameData.QuestData);
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

        private static bool CanCaptureSoftBuildingReference(BuildingBase building)
        {
            return building != null
                   && building.isActiveAndEnabled
                   && !building.IsDemolishing
                   && building.HasDefinition;
        }

        private static void CaptureSoftData(GameData gameData, GameSystem gameSystem)
        {
            if (gameData == null)
            {
                return;
            }

            gameData.SoftData ??= GameSoftData.CreateDefault();
            var selectedBuilding = gameSystem == null || gameSystem.BuildingSelection == null
                ? null
                : gameSystem.BuildingSelection.SelectedBuilding;
            if (CanCaptureSoftBuildingReference(selectedBuilding))
            {
                gameData.SoftData.LastSelectedBuilding = BuildingSoftReferenceSaveData.CreateFromBuilding(selectedBuilding);
            }

            gameData.SoftData.Validate();
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

            var typeId = BuildingSaveDataRegistry.GetTypeId(data);
            if (string.IsNullOrWhiteSpace(typeId))
            {
                return;
            }

            saveData.BuildingStateTypeId = typeId;
            saveData.BuildingStateJson = JsonUtility.ToJson(data);
        }

        private static void RestoreBuildingInstances(GameData gameData, GameSystem gameSystem)
        {
            if (gameData == null || gameSystem == null || gameData.BuildingInstances == null)
            {
                return;
            }

            ClearCurrentRuntimeBuildings(gameSystem);
            for (var i = 0; i < gameData.BuildingInstances.Count; i++)
            {
                RestoreBuildingInstance(gameData.BuildingInstances[i], gameSystem);
            }
        }

        private static IEnumerator RestoreBuildingInstancesRoutine(
            GameData gameData,
            GameSystem gameSystem,
            int buildingsPerFrame)
        {
            if (gameData == null || gameSystem == null || gameData.BuildingInstances == null)
            {
                yield break;
            }

            ClearCurrentRuntimeBuildings(gameSystem);
            yield return null;

            buildingsPerFrame = Mathf.Max(1, buildingsPerFrame);
            var restoredThisFrame = 0;
            for (var i = 0; i < gameData.BuildingInstances.Count; i++)
            {
                if (RestoreBuildingInstance(gameData.BuildingInstances[i], gameSystem))
                {
                    restoredThisFrame++;
                }

                if (restoredThisFrame < buildingsPerFrame || i >= gameData.BuildingInstances.Count - 1)
                {
                    continue;
                }

                restoredThisFrame = 0;
                yield return null;
            }
        }

        private static bool RestoreBuildingInstance(BuildingInstanceSaveData saveData, GameSystem gameSystem)
        {
            if (saveData == null || gameSystem == null || gameSystem.Services.Buildings == null)
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

            var catalog = gameSystem.BuildingCatalog == null ? BuildingCatalog.Instance : gameSystem.BuildingCatalog;
            if (catalog == null || !catalog.TryGetBuildingPrefab(saveData.BuildingId, out var buildingPrefab))
            {
                Debug.LogWarning($"恢复建筑失败：建筑目录中找不到 BuildingId = {saveData.BuildingId}");
                return false;
            }

            var parent = ResolveRestoredBuildingParent(gridMap);
            if (!gameSystem.Services.Buildings.TryPlace(
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

        private static Transform ResolveRestoredBuildingParent(GridMapBehaviour gridMap)
        {
            const string rootName = "Restored Buildings";
            var existingRoot = gridMap.transform.Find(rootName);
            if (existingRoot != null)
            {
                return existingRoot;
            }

            var root = new GameObject(rootName);
            root.transform.SetParent(gridMap.transform, false);
            return root.transform;
        }

        private static BuildingDataBase RestoreBuildingData(BuildingInstanceSaveData saveData)
        {
            if (saveData == null
                || string.IsNullOrWhiteSpace(saveData.BuildingStateTypeId)
                || string.IsNullOrWhiteSpace(saveData.BuildingStateJson))
            {
                return null;
            }

            if (!BuildingSaveDataRegistry.TryCreate(saveData.BuildingStateTypeId, out var data))
            {
                Debug.LogWarning($"恢复建筑数据失败：找不到数据类型 ID {saveData.BuildingStateTypeId}");
                return null;
            }

            try
            {
                JsonUtility.FromJsonOverwrite(saveData.BuildingStateJson, data);
                return data;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"恢复建筑数据失败：{saveData.BuildingId}\n{exception.Message}");
                return null;
            }
        }

        private static void ClearCurrentRuntimeBuildings(GameSystem gameSystem)
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

            gameSystem?.Services.Buildings?.ClearBuildings();
        }
    }
}
