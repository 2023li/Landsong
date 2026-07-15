using System;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.GridSystem
{
    [DisallowMultipleComponent]
    [AddComponentMenu("Landsong/Map/Map Runtime Host")]
    public sealed class MapRuntimeHost : MonoBehaviour
    {
        [SerializeField, LabelText("运行时 Grid Map")]
        private GridMapBehaviour gridMap;

        [SerializeField, LabelText("共享 Grid Rule Set")]
        private GridRuleSet ruleSet;

        [SerializeField, LabelText("Overlay Service")]
        private GridOverlayService overlayService;

        [SerializeField, LabelText("运行时建筑父节点")]
        private Transform runtimeBuildingsRoot;

        private MapContentAuthoring boundContent;
        private DataManager subscribedDataManager;
        private bool gridLinesVisible = true;
        private bool baseTilemapVisible = true;

        public static MapRuntimeHost Active { get; private set; }
        public GridMapBehaviour GridMap => gridMap;
        public GridOverlayService OverlayService => overlayService;
        public MapContentAuthoring BoundContent => boundContent;
        public bool IsBound => boundContent != null && gridMap != null && gridMap.IsInitialized;
        public bool GridLinesVisible => gridLinesVisible;
        public bool BaseTilemapVisible => baseTilemapVisible;

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Debug.LogError("场景中只能存在一个 MapRuntimeHost。", this);
                enabled = false;
                return;
            }

            Active = this;
        }

        private void Start()
        {
            SubscribeGameplayDisplaySettings();
        }

        private void OnDestroy()
        {
            UnsubscribeGameplayDisplaySettings();
            if (Active == this)
            {
                Active = null;
            }
        }

        public bool TryBind(MapContentAuthoring content, out string error)
        {
            error = string.Empty;
            if (content == null)
            {
                error = "MapContentAuthoring 缺失。";
                return false;
            }

            if (!content.TryValidateConfiguration(out error))
            {
                return false;
            }

            if (gridMap == null || ruleSet == null || overlayService == null)
            {
                error = "MapRuntimeHost 必须绑定 GridMapBehaviour、GridRuleSet 和 GridOverlayService。";
                return false;
            }

            if (!gridMap.TryValidateContentGridCompatibility(content.UnityGrid, out error))
            {
                return false;
            }

            if (boundContent != null && boundContent != content)
            {
                Unbind();
            }

            try
            {
                overlayService.BindGrid(content.UnityGrid);
                gridMap.BindContent(
                    content.UnityGrid,
                    content.BaseTilemap,
                    content.TerrainLayers,
                    ruleSet,
                    overlayService);
                boundContent = content;
                gridMap.GetComponent<GridRenderer>()?.RefreshAll();
                ApplyDisplaySettings();
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                Debug.LogException(exception, this);
                Unbind();
                return false;
            }
        }

        public void Unbind()
        {
            overlayService?.ClearAll();
            gridMap?.UnbindContent();
            gridMap?.GetComponent<GridRenderer>()?.RefreshAll();
            boundContent = null;
        }

        public void SetGridLinesVisible(bool visible)
        {
            gridLinesVisible = visible;
            gridMap?.GetComponent<GridRenderer>()?.SetGridLinesVisible(visible);
        }

        public void SetBaseTilemapVisible(bool visible)
        {
            baseTilemapVisible = visible;
            var baseTilemap = boundContent == null ? null : boundContent.BaseTilemap;
            if (baseTilemap == null)
            {
                return;
            }

            Color color = baseTilemap.color;
            color.a = visible ? 1f : 0f;
            baseTilemap.color = color;
        }

        public bool TryCreateInitialBuildings(
            Landsong.GameSystem gameSystem,
            out IReadOnlyList<BuildingBase> createdBuildings,
            out string error)
        {
            var created = new List<BuildingBase>();
            createdBuildings = created;
            error = string.Empty;
            if (!IsBound || gameSystem?.Services?.Buildings == null)
            {
                error = "地图运行时或 BuildingService 尚未准备完成。";
                return false;
            }

            var templates = boundContent.GetInitialBuildingTemplates();
            if (!ValidateTemplates(templates, out error))
            {
                return false;
            }

            var parent = ResolveRuntimeBuildingsRoot();
            for (var i = 0; i < templates.Length; i++)
            {
                var template = templates[i];
                var request = new BuildingPlacementRequest(
                    template.BuildingPrefab,
                    gridMap,
                    template.Origin,
                    parent,
                    1,
                    false,
                    false,
                    false);
                var result = gameSystem.Services.Buildings.TryPlace(request, out var building);
                if (!result.Succeeded)
                {
                    error = $"初始建筑生成失败：{template.DisplayName}，{result.Message}";
                    Rollback(gameSystem.Services.Buildings, created);
                    return false;
                }

                building.RestoreRuntimeIdentity(
                    building.InstanceId,
                    template.BuildingPrefab.Stage,
                    template.BuildingPrefab.CurrentLevel,
                    template.BuildingPrefab.StyleId,
                    template.BuildingPrefab.ConstructionProgress);
                gameSystem.RegisterBuilding(building);

                created.Add(building);
            }

            return true;
        }

        private bool ValidateTemplates(IReadOnlyList<InitialBuildingTemplate> templates, out string error)
        {
            error = string.Empty;
            var plannedCells = new HashSet<GridPosition>();
            if (templates == null)
            {
                return true;
            }

            for (var i = 0; i < templates.Count; i++)
            {
                var template = templates[i];
                if (!template.IsValid)
                {
                    error = $"初始建筑模板无效：{template.DisplayName}";
                    return false;
                }

                if (!template.PreviewAligned)
                {
                    error = $"初始建筑预览未吸附到实际占地：{template.DisplayName}";
                    return false;
                }

                var definition = template.BuildingPrefab.Definition;
                if (!gridMap.CanOccupy(
                        template.Origin,
                        definition.Size,
                        definition.RequiredTerrainKeys,
                        out var failure))
                {
                    error = $"初始建筑模板无法放置：{template.DisplayName}，{failure}";
                    return false;
                }

                foreach (var cell in definition.CreateFootprint(template.Origin).Positions())
                {
                    if (!plannedCells.Add(cell))
                    {
                        error = $"初始建筑模板相互重叠：{template.DisplayName}，Cell={cell}";
                        return false;
                    }
                }
            }

            return true;
        }

        private Transform ResolveRuntimeBuildingsRoot()
        {
            if (runtimeBuildingsRoot != null)
            {
                return runtimeBuildingsRoot;
            }

            var root = new GameObject("Runtime Buildings");
            root.transform.SetParent(transform, false);
            runtimeBuildingsRoot = root.transform;
            return runtimeBuildingsRoot;
        }

        private void SubscribeGameplayDisplaySettings()
        {
            if (!DataManager.TryGetInstance(out var dataManager))
            {
                return;
            }

            if (subscribedDataManager != dataManager)
            {
                UnsubscribeGameplayDisplaySettings();
                subscribedDataManager = dataManager;
                subscribedDataManager.OnGameplayDisplaySettingsChanged += HandleGameplayDisplaySettingsChanged;
            }

            subscribedDataManager.EnsureAppDataLoaded();
            HandleGameplayDisplaySettingsChanged(subscribedDataManager.AppData.GameplayDisplay);
        }

        private void UnsubscribeGameplayDisplaySettings()
        {
            if (subscribedDataManager == null)
            {
                return;
            }

            subscribedDataManager.OnGameplayDisplaySettingsChanged -= HandleGameplayDisplaySettingsChanged;
            subscribedDataManager = null;
        }

        private void HandleGameplayDisplaySettingsChanged(GameplayDisplaySaveData settings)
        {
            if (settings == null)
            {
                return;
            }

            gridLinesVisible = settings.MapGridLinesVisible;
            baseTilemapVisible = settings.BaseTilemapVisible;
            ApplyDisplaySettings();
        }

        private void ApplyDisplaySettings()
        {
            SetGridLinesVisible(gridLinesVisible);
            SetBaseTilemapVisible(baseTilemapVisible);
        }

        private static void Rollback(BuildingService service, IReadOnlyList<BuildingBase> created)
        {
            if (service == null || created == null)
            {
                return;
            }

            for (var i = created.Count - 1; i >= 0; i--)
            {
                service.Remove(created[i]);
            }
        }
    }
}
