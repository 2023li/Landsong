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

        public static MapRuntimeHost Active { get; private set; }
        public GridMapBehaviour GridMap => gridMap;
        public GridOverlayService OverlayService => overlayService;
        public MapContentAuthoring BoundContent => boundContent;
        public bool IsBound => boundContent != null && gridMap != null && gridMap.IsInitialized;

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

        private void OnDestroy()
        {
            if (Active == this)
            {
                Active = null;
            }
        }

        public bool TryBind(MapContentAuthoring content, out string error)
        {
            error = string.Empty;
            if (content == null || !content.IsValid)
            {
                error = "MapContentAuthoring 缺失，或 Grid/Base Tilemap 未正确绑定。";
                return false;
            }

            if (gridMap == null || ruleSet == null || overlayService == null)
            {
                error = "MapRuntimeHost 必须绑定 GridMapBehaviour、GridRuleSet 和 GridOverlayService。";
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
            boundContent = null;
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

            var markers = boundContent.GetInitialBuildingMarkers();
            if (!ValidateMarkers(markers, out error))
            {
                return false;
            }

            var parent = ResolveRuntimeBuildingsRoot();
            for (var i = 0; i < markers.Length; i++)
            {
                var marker = markers[i];
                var request = new BuildingPlacementRequest(
                    marker.BuildingPrefab,
                    gridMap,
                    marker.Origin,
                    parent,
                    1,
                    false,
                    true,
                    false);
                var result = gameSystem.Services.Buildings.TryPlace(request, out var building);
                if (!result.Succeeded)
                {
                    error = $"初始建筑生成失败：{marker.name}，{result.Message}";
                    Rollback(gameSystem.Services.Buildings, created);
                    return false;
                }

                created.Add(building);
            }

            return true;
        }

        private bool ValidateMarkers(IReadOnlyList<InitialBuildingMarker> markers, out string error)
        {
            error = string.Empty;
            var plannedCells = new HashSet<GridPosition>();
            if (markers == null)
            {
                return true;
            }

            for (var i = 0; i < markers.Count; i++)
            {
                var marker = markers[i];
                if (marker == null || !marker.IsValid)
                {
                    error = $"InitialBuildingMarker 配置无效：{(marker == null ? "<null>" : marker.name)}";
                    return false;
                }

                var definition = marker.BuildingPrefab.Definition;
                if (!gridMap.CanOccupy(
                        marker.Origin,
                        definition.Size,
                        definition.RequiredTerrainKeys,
                        out var failure))
                {
                    error = $"InitialBuildingMarker 无法放置：{marker.name}，{failure}";
                    return false;
                }

                foreach (var cell in definition.CreateFootprint(marker.Origin).Positions())
                {
                    if (!plannedCells.Add(cell))
                    {
                        error = $"InitialBuildingMarker 相互重叠：{marker.name}，Cell={cell}";
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
