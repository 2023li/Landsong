using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Landsong.CameraSystem;
using Landsong.GridSystem;
using Landsong.InputSystem;
using Landsong.UISystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementController : MonoBehaviour
    {
        private enum RoadPlacementEndpoint
        {
            None,
            Head,
            Tail
        }

        [Header("Scene")]
        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private Transform placedBuildingRoot;

        [Header("Preview")]
        [SerializeField] private Vector3 ghostWorldOffset = Vector3.zero;
        [SerializeField] private Vector3 controlsWorldOffset = new Vector3(0f, -1.25f, 0f);
        [SerializeField, LabelText("放置控制 UI")] private GamePanel_BuildingPlacementControls placementControls;
        [SerializeField] private bool cancelPlacementOnDisable = true;

        [Header("Demolition")]
        [SerializeField, LabelText("拆除点击最大移动像素"), Min(0f)] private float demolitionClickMaxMovementPixels = 8f;

        [Header("Grid Overlay Channels")]
        [SerializeField, LabelText("合法占地通道")] private GridOverlayChannelDefinition validFootprintChannel;
        [SerializeField, LabelText("非法占地通道")] private GridOverlayChannelDefinition invalidFootprintChannel;
        [SerializeField, LabelText("道路路径通道")] private GridOverlayChannelDefinition roadPathChannel;
        [SerializeField, LabelText("拆除目标通道")] private GridOverlayChannelDefinition demolitionChannel;
        [SerializeField, LabelText("资源可达范围通道")] private GridOverlayChannelDefinition resourceReachableChannel;
        [SerializeField, LabelText("可用资源点通道")] private GridOverlayChannelDefinition resourceProviderChannel;
        [SerializeField, LabelText("最终资源点通道")] private GridOverlayChannelDefinition selectedResourceProviderChannel;
        [SerializeField, LabelText("最终资源路径通道")] private GridOverlayChannelDefinition resourcePathChannel;
        [SerializeField, LabelText("Buff 范围通道")] private GridOverlayChannelDefinition buffRangeChannel;

        private readonly List<GridPosition> currentRoadPath = new List<GridPosition>();
        private GridOverlayOwnerHandle validFootprintHandle;
        private GridOverlayOwnerHandle invalidFootprintHandle;
        private GridOverlayOwnerHandle roadPathHandle;
        private GridOverlayOwnerHandle demolitionHandle;
        private GridOverlayOwnerHandle resourceReachableHandle;
        private GridOverlayOwnerHandle resourceProviderHandle;
        private GridOverlayOwnerHandle selectedResourceProviderHandle;
        private GridOverlayOwnerHandle resourcePathHandle;
        private GridOverlayOwnerHandle buffRangeHandle;
        private InputController inputController;
        private BuildingBase activeBuildingPrefab;
        private string activeStyleId = string.Empty;
        private BuildingBase selectedDemolitionBuilding;
        private BuildingBase demolitionClickStartedBuilding;
        private GameObject ghostInstance;
        private BuildingView ghostView;
        private GameObject roadTailGhostInstance;
        private BuildingView roadTailGhostView;
        private RectTransform activeGameMarkRoot;
        private GridPosition currentOrigin;
        private GridPosition roadStartOrigin;
        private GridPosition roadEndOrigin;
        private bool hasCurrentOrigin;
        private bool hasRoadStartOrigin;
        private bool hasRoadEndOrigin;
        private bool currentCanPlace;
        private bool isPlacing;
        private bool isDemolitionMode;
        private bool isConfirming;
        private bool isDraggingPlacement;
        private bool hasPendingDemolitionClick;
        private bool demolitionClickStartedOverUi;
        private Vector2 demolitionClickStartPosition;
        private RoadPlacementEndpoint roadDraggedEndpoint;
        private BuildingPlacementEvaluation currentEvaluation;
        private GridPosition evaluatedOrigin;
        private BuildingBase evaluatedPrefab;
        private int evaluatedOccupancyVersion = -1;

        public event Action<bool> DemolitionModeChanged;
        public event Action<BuildingPlacementEvaluation> PlacementEvaluationChanged;

        public bool IsPlacing => isPlacing;
        public bool IsDemolitionMode => isDemolitionMode;
        public bool IsActive => isPlacing || isDemolitionMode;
        public BuildingPlacementEvaluation CurrentEvaluation => currentEvaluation;
        public string ActiveStyleId => activeStyleId;

        private void Reset()
        {
            gridMap = FindFirstObjectByType<GridMapBehaviour>();
            sourceCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveSceneReferences();
            ResolveInputController();
            ResolvePlacementControls();
            ValidateOverlayBindings();
        }

        private void ValidateOverlayBindings()
        {
            if (validFootprintChannel == null
                || invalidFootprintChannel == null
                || roadPathChannel == null
                || demolitionChannel == null
                || resourceReachableChannel == null
                || resourceProviderChannel == null
                || selectedResourceProviderChannel == null
                || resourcePathChannel == null
                || buffRangeChannel == null)
            {
                Debug.LogError(
                    "BuildingPlacementController 的 Overlay Channel 尚未完整绑定。请按重构文档绑定全部九个通道。",
                    this);
            }
        }

        private void Update()
        {
            if (isConfirming)
            {
                return;
            }

            if (isPlacing)
            {
                UpdatePlacementDrag();
                return;
            }

            if (isDemolitionMode)
            {
                UpdateDemolitionSelection();
            }
        }

        private void OnEnable()
        {
            CameraController.AnyCameraViewChanged += HandleCameraViewChanged;

            if (isPlacing && isDraggingPlacement)
            {
                SetCameraInputBlocked(true);
            }
        }

        private void OnDisable()
        {
            CameraController.AnyCameraViewChanged -= HandleCameraViewChanged;

            if (cancelPlacementOnDisable)
            {
                CancelPlacement();
                return;
            }

            SetCameraInputBlocked(false);
            isDraggingPlacement = false;
        }

        private void OnDestroy()
        {
            CameraController.AnyCameraViewChanged -= HandleCameraViewChanged;
            SetCameraInputBlocked(false);
            ReleasePlacementControls();
            ClearHighlightedTiles();
        }

        public void BeginPlacement(BuildingBase buildingPrefab)
        {
            BeginPlacement(buildingPrefab, ResolveDefaultStyleId(buildingPrefab));
        }

        public void BeginPlacement(BuildingBase buildingPrefab, string styleId)
        {
            if (buildingPrefab == null)
            {
                return;
            }

            ResolveSceneReferences();
            if (gridMap == null || sourceCamera == null)
            {
                Debug.LogWarning("Cannot begin building placement without a grid map and camera.", this);
                return;
            }

            if (!buildingPrefab.HasDefinition)
            {
                Debug.LogWarning($"Building prefab '{buildingPrefab.name}' has no valid BuildingDefinition data.", buildingPrefab);
                return;
            }

            WarnIfPrefabMissingCollider(buildingPrefab);

            var presentation = buildingPrefab.FamilyDefinition?.Presentation;
            styleId = string.IsNullOrWhiteSpace(styleId) ? string.Empty : styleId.Trim();
            if (presentation != null && !presentation.HasStyle(styleId))
            {
                Debug.LogWarning(
                    $"Building family '{buildingPrefab.FamilyId}' does not define style '{styleId}'.",
                    buildingPrefab);
                return;
            }

            CancelPlacement();

            activeBuildingPrefab = buildingPrefab;
            activeStyleId = styleId;
            isPlacing = true;
            isConfirming = false;
            isDraggingPlacement = false;
            currentCanPlace = false;
            hasCurrentOrigin = false;
            SetCameraInputBlocked(false);

            CreateGhost(buildingPrefab);
            PreparePlacementControls();
            PlaceAtScreenPosition(GetScreenCenter());
        }

        public void BeginDemolitionMode()
        {
            ResolveSceneReferences();
            if (sourceCamera == null)
            {
                Debug.LogWarning("Cannot begin building demolition mode without a camera.", this);
                return;
            }

            CancelPlacement();

            isDemolitionMode = true;
            isConfirming = false;
            selectedDemolitionBuilding = null;
            ResetPendingDemolitionClick();
            SetCameraInputBlocked(false);

            ClearCurrentBuildingSelection();
            PreparePlacementControls();
            SetPlacementConfirmInteractable(false);
            NotifyDemolitionModeChanged(true);
        }

        public void ConfirmPlacement()
        {
            if (isDemolitionMode)
            {
                ConfirmDemolition();
                return;
            }

            if (!isPlacing || isConfirming || activeBuildingPrefab == null || !hasCurrentOrigin || !currentCanPlace)
            {
                return;
            }

            if (IsRoadPlacementActive)
            {
                StartCoroutine(ConfirmRoadPlacementRoutine(activeBuildingPrefab, new List<GridPosition>(currentRoadPath)));
                return;
            }

            StartCoroutine(ConfirmPlacementRoutine(activeBuildingPrefab, currentOrigin));
        }

        public void CancelPlacement()
        {
            var wasDemolitionMode = isDemolitionMode;
            SetCameraInputBlocked(false);

            activeBuildingPrefab = null;
            activeStyleId = string.Empty;
            selectedDemolitionBuilding = null;
            demolitionClickStartedBuilding = null;
            isPlacing = false;
            isDemolitionMode = false;
            isConfirming = false;
            isDraggingPlacement = false;
            hasPendingDemolitionClick = false;
            demolitionClickStartedOverUi = false;
            demolitionClickStartPosition = default;
            roadDraggedEndpoint = RoadPlacementEndpoint.None;
            currentCanPlace = false;
            hasCurrentOrigin = false;
            hasRoadStartOrigin = false;
            hasRoadEndOrigin = false;
            currentRoadPath.Clear();

            DestroyGhost(ref ghostInstance, ref ghostView);
            DestroyGhost(ref roadTailGhostInstance, ref roadTailGhostView);

            ReleasePlacementControls();

            ClearHighlightedTiles();
            InvalidateEvaluationCache();
            SetCurrentEvaluation(null);

            if (wasDemolitionMode)
            {
                NotifyDemolitionModeChanged(false);
            }
        }

        private void ConfirmDemolition()
        {
            if (!isDemolitionMode || isConfirming || !CanDemolishBuilding(selectedDemolitionBuilding))
            {
                return;
            }

            isConfirming = true;
            var building = selectedDemolitionBuilding;
            var buildingService = ResolveBuildingService(building);
            if (buildingService != null)
            {
                buildingService.Demolish(building);
            }
            else
            {
                building.Demolish();
            }

            CancelPlacement();
        }

        private IEnumerator ConfirmPlacementRoutine(BuildingBase buildingPrefab, GridPosition origin)
        {
            isConfirming = true;

            if (buildingPrefab == null || !buildingPrefab.HasDefinition)
            {
                Debug.LogWarning("Cannot place building because the selected prefab has no valid BuildingDefinition data.", this);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var definition = buildingPrefab.Definition;
            if (!gridMap.CanOccupy(origin, definition.Size, definition.RequiredTerrainKeys, out var failureReason))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' at {origin}: {failureReason}.", this);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var buildingService = ResolveRuntimeBuildingService();
            if (buildingService == null)
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because BuildingService is missing.", buildingPrefab);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var request = new BuildingPlacementRequest(
                buildingPrefab,
                gridMap,
                origin,
                placedBuildingRoot,
                1,
                true,
                true,
                true,
                activeStyleId);
            var result = buildingService.TryPlace(request, out _);
            if (!result.Succeeded)
            {
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            CancelPlacement();
        }

        private IEnumerator ConfirmRoadPlacementRoutine(BuildingBase buildingPrefab, List<GridPosition> roadPath)
        {
            isConfirming = true;

            if (buildingPrefab == null || !buildingPrefab.HasDefinition)
            {
                Debug.LogWarning("Cannot place road because the selected prefab has no valid BuildingDefinition data.", this);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var definition = buildingPrefab.Definition;
            var buildingService = ResolveRuntimeBuildingService();
            if (buildingService == null)
            {
                Debug.LogWarning($"Cannot place road '{definition.DisplayName}' because BuildingService is missing.", buildingPrefab);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            if (roadPath == null
                || roadPath.Count == 0
                || !BuildingRoadPlacementPlanner.CanPlaceRoadPath(gridMap, buildingService.Buildings, roadPath, definition))
            {
                Debug.LogWarning($"Cannot place road '{definition.DisplayName}' because the selected path is invalid.", this);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var roadOriginsToPlace = new List<GridPosition>(roadPath.Count);
            BuildingRoadPlacementPlanner.CollectRoadOriginsToPlace(
                gridMap,
                buildingService.Buildings,
                roadPath,
                definition,
                roadOriginsToPlace);

            var result = buildingService.TryPlaceBatch(
                buildingPrefab,
                gridMap,
                roadOriginsToPlace,
                placedBuildingRoot,
                out _,
                true,
                true);
            if (!result.Succeeded)
            {
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            CancelPlacement();
        }

        private void UpdateDemolitionSelection()
        {
            if (sourceCamera == null)
            {
                ClearSelectedDemolitionBuilding();
                ResetPendingDemolitionClick();
                SetCameraInputBlocked(false);
                return;
            }

            if (!TryReadPointerState(out var pointerState))
            {
                ResetPendingDemolitionClick();
                SetCameraInputBlocked(false);
                return;
            }

            if (pointerState.WasPressedThisFrame)
            {
                hasPendingDemolitionClick = true;
                demolitionClickStartPosition = pointerState.ScreenPosition;
                demolitionClickStartedOverUi = IsPointerOverUi(pointerState.ScreenPosition);
                demolitionClickStartedBuilding = null;

                if (!demolitionClickStartedOverUi)
                {
                    TryGetDemolitionBuildingAtScreenPosition(pointerState.ScreenPosition, out demolitionClickStartedBuilding);
                }

                SetCameraInputBlocked(demolitionClickStartedBuilding != null);
                return;
            }

            if (!hasPendingDemolitionClick)
            {
                return;
            }

            if (!pointerState.WasReleasedThisFrame && pointerState.IsPressed)
            {
                return;
            }

            SetCameraInputBlocked(false);

            var isClick = (pointerState.ScreenPosition - demolitionClickStartPosition).sqrMagnitude
                          <= demolitionClickMaxMovementPixels * demolitionClickMaxMovementPixels;
            var releasedOverUi = IsPointerOverUi(pointerState.ScreenPosition);
            BuildingBase releasedBuilding = null;
            var releasedOverBuilding = !releasedOverUi
                                      && TryGetDemolitionBuildingAtScreenPosition(pointerState.ScreenPosition, out releasedBuilding);

            if (isClick && !demolitionClickStartedOverUi && !releasedOverUi)
            {
                if (releasedOverBuilding && releasedBuilding == demolitionClickStartedBuilding)
                {
                    SetSelectedDemolitionBuilding(releasedBuilding);
                }
                else if (!releasedOverBuilding && demolitionClickStartedBuilding == null)
                {
                    ClearSelectedDemolitionBuilding();
                }
            }

            ResetPendingDemolitionClick();
        }

        private void SetSelectedDemolitionBuilding(BuildingBase building)
        {
            if (!CanDemolishBuilding(building))
            {
                ClearSelectedDemolitionBuilding();
                return;
            }

            selectedDemolitionBuilding = building;
            RefreshDemolitionSelection();
        }

        private void ClearSelectedDemolitionBuilding()
        {
            selectedDemolitionBuilding = null;
            SetPlacementConfirmInteractable(false);
            placementControls?.Hide();
            ClearHighlightedTiles();
        }

        private void RefreshDemolitionSelection()
        {
            if (!isDemolitionMode)
            {
                return;
            }

            if (!CanDemolishBuilding(selectedDemolitionBuilding))
            {
                ClearSelectedDemolitionBuilding();
                return;
            }

            UpdateDemolitionHighlight(selectedDemolitionBuilding);
            SetPlacementConfirmInteractable(true);
            RefreshDemolitionControlsPosition();
        }

        private void ResetPendingDemolitionClick()
        {
            hasPendingDemolitionClick = false;
            demolitionClickStartedOverUi = false;
            demolitionClickStartedBuilding = null;
            demolitionClickStartPosition = default;
        }

        private bool TryGetDemolitionBuildingAtScreenPosition(Vector2 screenPosition, out BuildingBase building)
        {
            if (BuildingPointerHitUtility.TryGetBuilding(sourceCamera, screenPosition, out building)
                && CanDemolishBuilding(building))
            {
                return true;
            }

            building = null;
            return false;
        }

        private void UpdatePlacementDrag()
        {
            if (activeBuildingPrefab == null || gridMap == null || sourceCamera == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            if (!TryReadPointerState(out var pointerState))
            {
                SetCameraInputBlocked(false);
                isDraggingPlacement = false;
                return;
            }

            if (pointerState.WasReleasedThisFrame || !pointerState.IsPressed)
            {
                SetCameraInputBlocked(false);
                isDraggingPlacement = false;
                roadDraggedEndpoint = RoadPlacementEndpoint.None;
                return;
            }

            if (pointerState.WasPressedThisFrame)
            {
                roadDraggedEndpoint = IsRoadPlacementActive
                    ? GetRoadPlacementEndpointAtScreenPosition(pointerState.ScreenPosition)
                    : RoadPlacementEndpoint.None;
                isDraggingPlacement = !IsPointerOverUi(pointerState.ScreenPosition)
                                      && (IsRoadPlacementActive
                                          ? roadDraggedEndpoint != RoadPlacementEndpoint.None
                                          : IsPointerOverGhost(pointerState.ScreenPosition));
                SetCameraInputBlocked(isDraggingPlacement);
            }

            if (!isDraggingPlacement)
            {
                SetCameraInputBlocked(false);
                return;
            }

            if (IsRoadPlacementActive)
            {
                PlaceRoadEndpointAtScreenPosition(pointerState.ScreenPosition, roadDraggedEndpoint);
                return;
            }

            PlaceAtScreenPosition(pointerState.ScreenPosition);
        }

        private void PlaceAtScreenPosition(Vector2 screenPosition)
        {
            if (activeBuildingPrefab == null || gridMap == null || sourceCamera == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            var activeDefinition = activeBuildingPrefab.Definition;
            if (IsRoadPlacementActive)
            {
                PlaceRoadEndpointAtScreenPosition(screenPosition, RoadPlacementEndpoint.Tail);
                return;
            }

            if (!TryGetPlacementOriginFromScreenPosition(screenPosition, activeDefinition.Size, out var origin))
            {
                SetNoCurrentPlacement();
                return;
            }

            currentOrigin = origin;
            hasCurrentOrigin = true;
            var evaluation = EvaluateCurrentPlacement();
            currentCanPlace = evaluation?.CanConfirm == true;

            var placementPosition = gridMap.GetFootprintCenter(currentOrigin, activeDefinition.Size);
            MovePreview(placementPosition);
            UpdatePlacementOverlays(evaluation);

            SetPlacementConfirmInteractable(currentCanPlace);
        }

        private void PlaceRoadEndpointAtScreenPosition(Vector2 screenPosition, RoadPlacementEndpoint endpoint)
        {
            if (activeBuildingPrefab == null || gridMap == null || sourceCamera == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            var activeDefinition = activeBuildingPrefab.Definition;
            if (!TryGetPlacementOriginFromScreenPosition(screenPosition, activeDefinition.Size, out var origin))
            {
                SetNoCurrentPlacement();
                return;
            }

            if (!hasRoadStartOrigin)
            {
                roadStartOrigin = origin;
                hasRoadStartOrigin = true;
            }

            if (!hasRoadEndOrigin)
            {
                roadEndOrigin = origin;
                hasRoadEndOrigin = true;
            }

            if (endpoint == RoadPlacementEndpoint.Head)
            {
                roadStartOrigin = origin;
            }
            else
            {
                roadEndOrigin = origin;
            }

            currentOrigin = roadEndOrigin;
            hasCurrentOrigin = true;
            RefreshRoadPlacementState();
        }

        private bool TryGetPlacementOriginFromScreenPosition(Vector2 screenPosition, Vector2Int size, out GridPosition origin)
        {
            if (gridMap != null && gridMap.TryGetGridPointFromScreenPosition(sourceCamera, screenPosition, out var gridPoint))
            {
                origin = GetPlacementOrigin(gridPoint, size);
                return true;
            }

            origin = default;
            return false;
        }

        private void RefreshCurrentPlacementState()
        {
            if (!hasCurrentOrigin || activeBuildingPrefab == null || gridMap == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            var activeDefinition = activeBuildingPrefab.Definition;
            if (IsRoadPlacementActive)
            {
                RefreshRoadPlacementState();
                return;
            }

            var evaluation = EvaluateCurrentPlacement();
            currentCanPlace = evaluation?.CanConfirm == true;
            UpdatePlacementOverlays(evaluation);

            SetPlacementConfirmInteractable(currentCanPlace);
        }

        private void RefreshRoadPlacementState()
        {
            if (!hasRoadStartOrigin || !hasRoadEndOrigin || activeBuildingPrefab == null || gridMap == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            var activeDefinition = activeBuildingPrefab.Definition;
            InvalidateEvaluationCache();
            SetCurrentEvaluation(null);
            var buildingService = ResolveRuntimeBuildingService();
            BuildingRoadPlacementPlanner.SelectRoadPath(
                gridMap,
                buildingService == null ? null : buildingService.Buildings,
                roadStartOrigin,
                roadEndOrigin,
                activeDefinition,
                currentRoadPath,
                out currentCanPlace);

            var headPosition = gridMap.GetFootprintCenter(roadStartOrigin, activeDefinition.Size);
            var tailPosition = gridMap.GetFootprintCenter(roadEndOrigin, activeDefinition.Size);
            MoveRoadPreview(headPosition, tailPosition);
            UpdateRoadPathHighlight(activeDefinition);

            SetPlacementConfirmInteractable(currentCanPlace);
        }

        private void SetNoCurrentPlacement()
        {
            hasCurrentOrigin = false;
            currentCanPlace = false;
            InvalidateEvaluationCache();
            SetCurrentEvaluation(null);
            currentRoadPath.Clear();
            if (activeBuildingPrefab != null)
            {
                ClearHighlightedTiles();
            }
            else
            {
                ClearHighlightedTiles();
            }

            SetPlacementConfirmInteractable(false);
        }

        private static GridPosition GetPlacementOrigin(Vector2 gridPoint, Vector2Int size)
        {
            var anchorX = size.x % 2 == 0
                ? Mathf.RoundToInt(gridPoint.x)
                : Mathf.FloorToInt(gridPoint.x);
            var anchorY = size.y % 2 == 0
                ? Mathf.RoundToInt(gridPoint.y)
                : Mathf.FloorToInt(gridPoint.y);

            return new GridPosition(anchorX - size.x / 2, anchorY - size.y / 2);
        }

        private void MovePreview(Vector3 placementPosition)
        {
            var ghostPosition = placementPosition + ghostWorldOffset;
            MoveGhost(ghostInstance, ghostPosition);

            placementControls?.SetWorldPosition(sourceCamera, ghostPosition + controlsWorldOffset);
        }

        private void MoveRoadPreview(Vector3 headPosition, Vector3 tailPosition)
        {
            var headGhostPosition = headPosition + ghostWorldOffset;
            var tailGhostPosition = tailPosition + ghostWorldOffset;
            MoveGhost(ghostInstance, headGhostPosition);
            MoveGhost(roadTailGhostInstance, tailGhostPosition);

            placementControls?.SetWorldPosition(sourceCamera, tailGhostPosition + controlsWorldOffset);
        }

        private static void MoveGhost(GameObject ghost, Vector3 position)
        {
            if (ghost == null)
            {
                return;
            }

            ghost.SetActive(true);
            ghost.transform.position = position;
        }

        private void RefreshPlacementControlsPosition()
        {
            if (isDemolitionMode)
            {
                RefreshDemolitionControlsPosition();
                return;
            }

            if (!isPlacing || activeBuildingPrefab == null || gridMap == null || !hasCurrentOrigin)
            {
                return;
            }

            ResolveSceneReferences();
            var activeDefinition = activeBuildingPrefab.Definition;
            if (IsRoadPlacementActive)
            {
                if (!hasRoadEndOrigin)
                {
                    return;
                }

                var tailPosition = gridMap.GetFootprintCenter(roadEndOrigin, activeDefinition.Size);
                placementControls?.SetWorldPosition(sourceCamera, tailPosition + ghostWorldOffset + controlsWorldOffset);
                return;
            }

            var placementPosition = gridMap.GetFootprintCenter(currentOrigin, activeDefinition.Size);
            var ghostPosition = placementPosition + ghostWorldOffset;
            placementControls?.SetWorldPosition(sourceCamera, ghostPosition + controlsWorldOffset);
        }

        private void RefreshDemolitionControlsPosition()
        {
            if (!isDemolitionMode || !CanDemolishBuilding(selectedDemolitionBuilding))
            {
                placementControls?.Hide();
                return;
            }

            ResolveSceneReferences();
            if (sourceCamera == null)
            {
                placementControls?.Hide();
                return;
            }

            placementControls?.SetWorldPosition(
                sourceCamera,
                GetDemolitionControlsWorldPosition(selectedDemolitionBuilding) + controlsWorldOffset);
        }

        private static Vector3 GetDemolitionControlsWorldPosition(BuildingBase building)
        {
            if (building != null && building.HasPlacement && building.GridMap != null && building.Definition != null)
            {
                return building.GridMap.GetFootprintCenter(building.Origin, building.Definition.Size);
            }

            return building == null ? Vector3.zero : building.transform.position;
        }

        private void CreateGhost(BuildingBase buildingPrefab)
        {
            var definition = buildingPrefab.Definition;
            var isRoadPlacement = IsRoadPlacementPrefab(buildingPrefab);
            ghostInstance = BuildingPlacementPreviewFactory.Create(
                buildingPrefab,
                previewRoot == null ? transform : previewRoot,
                isRoadPlacement ? $"{definition.DisplayName}_PlacementHeadGhost" : $"{definition.DisplayName}_PlacementGhost",
                activeStyleId,
                out ghostView);

            if (isRoadPlacement)
            {
                roadTailGhostInstance = BuildingPlacementPreviewFactory.Create(
                    buildingPrefab,
                    previewRoot == null ? transform : previewRoot,
                    $"{definition.DisplayName}_PlacementTailGhost",
                    activeStyleId,
                    out roadTailGhostView);
            }
        }

        private void DestroyGhost(ref GameObject instance, ref BuildingView view)
        {
            BuildingPlacementPreviewFactory.Destroy(ref instance, ref view);
        }

        private void PreparePlacementControls()
        {
            ResolveInputController();
            inputController?.EnsureEventSystemExists();
            ResolvePlacementControls();

            if (placementControls == null || activeGameMarkRoot == null)
            {
                Debug.LogWarning(
                    "Cannot show building placement controls because the active UIPanel_Game has no GameMarkRoot or placement controls.",
                    this);
                return;
            }

            placementControls.Bind(this);
            placementControls.SetConfirmInteractable(false);
            placementControls.Hide();
        }

        private void ReleasePlacementControls()
        {
            if (placementControls == null)
            {
                return;
            }

            placementControls.Unbind(this);
            placementControls.SetConfirmInteractable(false);
            placementControls.Hide();
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            ResolveInputController();
            return inputController != null && inputController.IsPointerOverUi(screenPosition);
        }

        private bool IsPointerOverGhost(Vector2 screenPosition)
        {
            return BuildingPointerHitUtility.TryHitObjectCollider(ghostInstance, sourceCamera, screenPosition);
        }

        private RoadPlacementEndpoint GetRoadPlacementEndpointAtScreenPosition(Vector2 screenPosition)
        {
            if (!IsRoadPlacementActive)
            {
                return RoadPlacementEndpoint.None;
            }

            if (BuildingPointerHitUtility.TryHitObjectCollider(roadTailGhostInstance, sourceCamera, screenPosition))
            {
                return RoadPlacementEndpoint.Tail;
            }

            if (BuildingPointerHitUtility.TryHitObjectCollider(ghostInstance, sourceCamera, screenPosition))
            {
                return RoadPlacementEndpoint.Head;
            }

            return RoadPlacementEndpoint.None;
        }

        private static void WarnIfPrefabMissingCollider(BuildingBase buildingPrefab)
        {
            if (buildingPrefab == null || BuildingPointerHitUtility.HasEnabledCollider(buildingPrefab.gameObject))
            {
                return;
            }

            Debug.LogWarning(
                $"建筑 prefab '{buildingPrefab.name}' 缺少启用的 Collider/Collider2D，统一建筑点击链路无法拖拽它的放置预览。",
                buildingPrefab);
        }

        private BuildingPlacementEvaluation EvaluateCurrentPlacement()
        {
            if (!hasCurrentOrigin || activeBuildingPrefab == null || gridMap == null)
            {
                return null;
            }

            if (currentEvaluation != null
                && evaluatedPrefab == activeBuildingPrefab
                && evaluatedOrigin == currentOrigin
                && evaluatedOccupancyVersion == gridMap.OccupancyVersion)
            {
                return currentEvaluation;
            }

            var buildingService = ResolveRuntimeBuildingService();
            var buildings = buildingService == null ? null : buildingService.Buildings;
            var evaluation = BuildingPlacementEvaluator.Evaluate(
                activeBuildingPrefab,
                gridMap,
                currentOrigin,
                buildings);
            evaluatedPrefab = activeBuildingPrefab;
            evaluatedOrigin = currentOrigin;
            evaluatedOccupancyVersion = gridMap.OccupancyVersion;
            SetCurrentEvaluation(evaluation);
            return evaluation;
        }

        private void UpdatePlacementOverlays(BuildingPlacementEvaluation evaluation)
        {
            ClearHighlightedTiles();
            if (evaluation == null || gridMap?.OverlayService == null)
            {
                return;
            }

            UpdateFootprintHighlight(evaluation.Footprint, activeBuildingPrefab?.Definition);

            var connection = evaluation.ResourceConnection;
            if (connection != null)
            {
                var reachableCells = new List<GridPosition>(connection.ReachableNodes.Count);
                for (var i = 0; i < connection.ReachableNodes.Count; i++)
                {
                    reachableCells.Add(connection.ReachableNodes[i].Position);
                }

                resourceReachableHandle = gridMap.OverlayService.AcquireOwner(
                    resourceReachableChannel,
                    "building-placement-resource-reachable");
                resourceReachableHandle?.SetCells(reachableCells);

                var providerCells = new List<GridPosition>();
                for (var i = 0; i < connection.ReachableProviders.Count; i++)
                {
                    var provider = connection.ReachableProviders[i].Provider;
                    if (provider == null)
                    {
                        continue;
                    }

                    foreach (var cell in provider.Footprint.Positions())
                    {
                        providerCells.Add(cell);
                    }
                }

                resourceProviderHandle = gridMap.OverlayService.AcquireOwner(
                    resourceProviderChannel,
                    "building-placement-resource-providers");
                resourceProviderHandle?.SetCells(providerCells);

                if (connection.Selection.IsValid)
                {
                    var selectedCells = new List<GridPosition>();
                    foreach (var cell in connection.Selection.Provider.Footprint.Positions())
                    {
                        selectedCells.Add(cell);
                    }

                    selectedResourceProviderHandle = gridMap.OverlayService.AcquireOwner(
                        selectedResourceProviderChannel,
                        "building-placement-resource-selected");
                    selectedResourceProviderHandle?.SetCells(selectedCells, 100);
                    resourcePathHandle = gridMap.OverlayService.AcquireOwner(
                        resourcePathChannel,
                        "building-placement-resource-path");
                    resourcePathHandle?.SetCells(connection.Selection.Path);
                }
            }

            var buffCells = new HashSet<GridPosition>();
            for (var i = 0; i < evaluation.SpatialEffects.Count; i++)
            {
                var cells = evaluation.SpatialEffects[i].AffectedCells;
                for (var j = 0; j < cells.Count; j++)
                {
                    buffCells.Add(cells[j]);
                }
            }

            buffRangeHandle = gridMap.OverlayService.AcquireOwner(
                buffRangeChannel,
                "building-placement-buff-range");
            buffRangeHandle?.SetCells(buffCells);
        }

        private void SetCurrentEvaluation(BuildingPlacementEvaluation evaluation)
        {
            if (ReferenceEquals(currentEvaluation, evaluation))
            {
                return;
            }

            currentEvaluation = evaluation;
            placementControls?.SetPlacementInfo(FormatPlacementInfo(evaluation));
            PlacementEvaluationChanged?.Invoke(evaluation);
        }

        private static string FormatPlacementInfo(BuildingPlacementEvaluation evaluation)
        {
            if (evaluation == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(evaluation.IsSpatiallyLegal ? "占地合法" : $"占地非法：{evaluation.GridFailure}");
            if (evaluation.RequiresResourceConnection)
            {
                builder.AppendLine();
                if (evaluation.ResourceProviderFound)
                {
                    var selection = evaluation.ResourceConnection.Selection;
                    var providerName = selection.Provider.HasDefinition
                        ? selection.Provider.Definition.DisplayName
                        : selection.Provider.name;
                    builder.Append($"资源点：{providerName}（行动力 {selection.ActionCost}）");
                }
                else
                {
                    builder.Append("资源点：当前范围内无连接（不阻止放置）");
                }
            }

            for (var i = 0; i < evaluation.SpatialEffects.Count; i++)
            {
                var preview = evaluation.SpatialEffects[i];
                if (preview == null || string.IsNullOrWhiteSpace(preview.Description))
                {
                    continue;
                }

                builder.AppendLine();
                builder.Append($"Buff：{preview.Description}，范围 {preview.Definition.Range}");
            }

            return builder.ToString();
        }

        private void InvalidateEvaluationCache()
        {
            evaluatedPrefab = null;
            evaluatedOrigin = default;
            evaluatedOccupancyVersion = -1;
        }

        private void UpdateDemolitionHighlight(BuildingBase building)
        {
            ClearHighlightedTiles();
            if (!CanDemolishBuilding(building)
                || building.GridMap == null
                || building.GridMap.OverlayService == null
                || demolitionChannel == null)
            {
                return;
            }

            demolitionHandle = building.GridMap.OverlayService.AcquireOwner(
                demolitionChannel,
                "building-placement-demolition");
            if (demolitionHandle == null)
            {
                return;
            }

            var cells = new List<GridPosition>();
            foreach (var position in building.Footprint.Positions())
            {
                cells.Add(position);
            }

            demolitionHandle.SetCells(cells);
        }

        private void UpdateFootprintHighlight(GridFootprint footprint, BuildingDefinition definition)
        {
            if (gridMap == null || gridMap.OverlayService == null)
            {
                return;
            }

            var channel = currentCanPlace ? validFootprintChannel : invalidFootprintChannel;
            if (channel == null)
            {
                return;
            }

            var cells = new List<GridPosition>();
            foreach (var position in footprint.Positions())
            {
                cells.Add(position);
            }

            var handle = gridMap.OverlayService.AcquireOwner(channel, "building-placement-footprint");
            handle?.SetCells(cells);
            if (currentCanPlace)
            {
                validFootprintHandle = handle;
            }
            else
            {
                invalidFootprintHandle = handle;
            }
        }

        private void UpdateRoadPathHighlight(BuildingDefinition definition)
        {
            ClearHighlightedTiles();
            if (gridMap == null || gridMap.OverlayService == null)
            {
                return;
            }

            if (definition == null || currentRoadPath.Count == 0)
            {
                return;
            }

            var validCells = new List<GridPosition>();
            var invalidCells = new List<GridPosition>();
            var roadCells = new List<GridPosition>();
            for (var i = 0; i < currentRoadPath.Count; i++)
            {
                var origin = currentRoadPath[i];
                var buildingService = ResolveRuntimeBuildingService();
                var canPlaceCell = BuildingRoadPlacementPlanner.IsRoadPathOriginValid(
                    gridMap,
                    buildingService == null ? null : buildingService.Buildings,
                    origin,
                    definition);
                var isEndpoint = i == 0 || i == currentRoadPath.Count - 1;

                var footprint = new GridFootprint(origin, definition.Size);
                foreach (var position in footprint.Positions())
                {
                    if (!canPlaceCell)
                    {
                        invalidCells.Add(position);
                    }
                    else if (isEndpoint)
                    {
                        validCells.Add(position);
                    }
                    else
                    {
                        roadCells.Add(position);
                    }
                }
            }

            validFootprintHandle = gridMap.OverlayService.AcquireOwner(
                validFootprintChannel,
                "building-placement-road-endpoints");
            validFootprintHandle?.SetCells(validCells);
            invalidFootprintHandle = gridMap.OverlayService.AcquireOwner(
                invalidFootprintChannel,
                "building-placement-road-invalid");
            invalidFootprintHandle?.SetCells(invalidCells);
            roadPathHandle = gridMap.OverlayService.AcquireOwner(
                roadPathChannel,
                "building-placement-road-path");
            roadPathHandle?.SetCells(roadCells);
        }

        private void ClearHighlightedTiles()
        {
            validFootprintHandle?.Dispose();
            invalidFootprintHandle?.Dispose();
            roadPathHandle?.Dispose();
            demolitionHandle?.Dispose();
            resourceReachableHandle?.Dispose();
            resourceProviderHandle?.Dispose();
            selectedResourceProviderHandle?.Dispose();
            resourcePathHandle?.Dispose();
            buffRangeHandle?.Dispose();
            validFootprintHandle = null;
            invalidFootprintHandle = null;
            roadPathHandle = null;
            demolitionHandle = null;
            resourceReachableHandle = null;
            resourceProviderHandle = null;
            selectedResourceProviderHandle = null;
            resourcePathHandle = null;
            buffRangeHandle = null;
        }

        private void ResolveSceneReferences()
        {
            if (gridMap == null)
            {
                gridMap = FindFirstObjectByType<GridMapBehaviour>();
            }

            if (sourceCamera == null)
            {
                sourceCamera = Camera.main;
            }
        }

        private void ResolveInputController()
        {
            if (inputController == null)
            {
                inputController = InputController.Instance;
            }
        }

        private void ResolvePlacementControls()
        {
            if (!TryGetActiveGamePanel(out var gamePanel))
            {
                activeGameMarkRoot = null;
                placementControls = null;
                return;
            }

            var resolvedControls = gamePanel.BuildingPlacementControls;
            if (placementControls != null && placementControls != resolvedControls)
            {
                placementControls.Unbind(this);
                placementControls.Hide();
            }

            placementControls = resolvedControls;
            activeGameMarkRoot = gamePanel.GameMarkRoot;

            if (placementControls != null && activeGameMarkRoot != null && placementControls.transform.parent != activeGameMarkRoot)
            {
                placementControls.transform.SetParent(activeGameMarkRoot, false);
            }
        }

        private static bool TryGetActiveGamePanel(out UIPanel_Game gamePanel)
        {
            var uiManager = UIManager.Instance;
            if (uiManager != null && uiManager.TryGetActivePanel<UIPanel_Game>(out gamePanel))
            {
                return gamePanel != null;
            }

            gamePanel = null;
            return false;
        }

        private void SetPlacementConfirmInteractable(bool interactable)
        {
            placementControls?.SetConfirmInteractable(interactable);
        }

        private void HandleCameraViewChanged(CameraController cameraController)
        {
            if (cameraController != null
                && sourceCamera != null
                && cameraController.SourceCamera != null
                && cameraController.SourceCamera != sourceCamera)
            {
                return;
            }

            RefreshPlacementControlsPosition();
        }

        private void SetCameraInputBlocked(bool blocked)
        {
            if (blocked)
            {
                ResolveInputController();
            }

            inputController?.SetCameraInputBlocked(this, blocked);
        }

        private Vector2 GetScreenCenter()
        {
            if (sourceCamera != null)
            {
                return sourceCamera.pixelRect.center;
            }

            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        private bool TryReadPointerState(out ScreenPointerState pointerState)
        {
            ResolveInputController();
            if (inputController == null || inputController.ActiveTouchCount > 1)
            {
                pointerState = default;
                return false;
            }

            return inputController.TryGetPrimaryPointerState(out pointerState);
        }

        private bool IsRoadPlacementActive => IsRoadPlacementPrefab(activeBuildingPrefab);

        private static bool IsRoadPlacementPrefab(BuildingBase buildingPrefab)
        {
            return buildingPrefab?.Definition != null
                   && (buildingPrefab.Definition.Category & BuildingCategory.交通) != 0;
        }

        private static string ResolveDefaultStyleId(BuildingBase buildingPrefab)
        {
            var styles = buildingPrefab?.FamilyDefinition?.Presentation?.Styles;
            if (styles == null || styles.Count == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < styles.Count; i++)
            {
                if (styles[i] != null && styles[i].IsValid)
                {
                    return styles[i].StyleId;
                }
            }

            return string.Empty;
        }

        private static bool CanDemolishBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private static BuildingService ResolveBuildingService(BuildingBase building)
        {
            var gameSystem = building == null ? null : building.GameSystem;
            if (gameSystem != null && gameSystem.Services.Buildings != null)
            {
                return gameSystem.Services.Buildings;
            }

            gameSystem = Landsong.GameSystem.Instance;
            return gameSystem == null ? null : gameSystem.Services.Buildings;
        }

        private static BuildingService ResolveRuntimeBuildingService()
        {
            var gameSystem = Landsong.GameSystem.Instance;
            return gameSystem == null ? null : gameSystem.Services.Buildings;
        }

        private static void ClearCurrentBuildingSelection()
        {
            var gameSystem = Landsong.GameSystem.Instance;
            gameSystem?.BuildingSelection?.ClearSelection();
        }

        private void NotifyDemolitionModeChanged(bool isActive)
        {
            DemolitionModeChanged?.Invoke(isActive);
        }
    }
}
