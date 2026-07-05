using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.CameraSystem;
using Landsong.GridSystem;
using Landsong.InputSystem;
using Landsong.UISystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingSelectionController : MonoSingleton<BuildingSelectionController>
    {
        [SerializeField, Required, LabelText("操作条预制体")]
        private GamePanel_BuildingOperationBar operationBarPrefab;

        [FoldoutGroup("选填")]
        [SerializeField] private TileBase selectionHighlightTile;

        [FoldoutGroup("选填")]
        [SerializeField] private TileBase reachableRangeHighlightTile;

        [FoldoutGroup("选填")]
        [SerializeField] private Vector3 operationBarWorldOffset = new Vector3(0f, -0.75f, 0f);

        [FoldoutGroup("选填")]
        [SerializeField] private Vector2 operationBarScreenOffset;

        [FoldoutGroup("选填")]
        [SerializeField] private bool hideOperationBarWhenOffscreen = true;

        [FoldoutGroup("选填")]
        [SerializeField] private CameraController cameraController;

        [FoldoutGroup("选择输入")]
        [SerializeField, LabelText("点击世界更新选择")] private bool updateSelectionOnWorldClick = true;

        [FoldoutGroup("选择输入")]
        [SerializeField, Min(0f), LabelText("选择点击最大移动像素")] private float selectionClickMaxMovementPixels = 8f;

        private readonly HashSet<BuildingBase> subscribedBuildings = new HashSet<BuildingBase>();
        private readonly List<Vector3Int> highlightedCells = new List<Vector3Int>();
        private readonly List<Vector3Int> highlightedReachableRangeCells = new List<Vector3Int>();
        private Landsong.GameSystem gameSystem;
        private BuildingService buildings;
        private BuildingService subscribedBuildingService;
        private InputController inputController;
        private BuildingPlacementController placementController;
        private Canvas markerCanvas;
        private RectTransform markerRoot;
        private GamePanel_BuildingOperationBar activeOperationBar;
        private Tilemap activeHighlightTilemap;
        private Tilemap activeReachableRangeHighlightTilemap;
        private BuildingBase selectedBuilding;
        private CameraController subscribedCameraController;
        private bool subscribedToBuildings;
        private bool subscribedToCamera;
        private Coroutine delayedResolveCoroutine;
        private bool hasPendingSelectionClick;
        private bool selectionClickStartedOverUi;
        private BuildingBase selectionClickStartedBuilding;
        private Vector2 selectionClickStartPosition;
        private BuildingBase lastClickedBuilding;
        private float lastBuildingClickTime = float.NegativeInfinity;
        private bool showReachableRange;

        public event Action<BuildingBase> SelectionChanged;
        public event Action<BuildingBase> SelectedBuildingStateChanged;
        public event Action<BuildingBase> DetailRequested;

        public BuildingBase SelectedBuilding => selectedBuilding;

        private void Reset()
        {
            operationBarPrefab = GetComponentInChildren<GamePanel_BuildingOperationBar>(true);
            cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
        }

        private void OnEnable()
        {
            ResolveReferences();
            RegisterSelf();
            SubscribeBuildings();
            SubscribeCamera();
            RefreshBuildingSubscriptions();
            RefreshSelectionVisuals();
            QueueDelayedResolveIfNeeded();
        }

        private void LateUpdate()
        {
            HandleSelectionClick();
        }

        private void OnDisable()
        {
            StopDelayedResolve();
            BuildingBase previousSelectedBuilding = selectedBuilding;
            selectedBuilding = null;
            showReachableRange = false;
            ClearSelectionVisuals();
            UnsubscribeCamera();
            UnsubscribeBuildings();
            UnsubscribeBuildingEvents();
            UnregisterSelf();

            if (previousSelectedBuilding != null)
            {
                SelectionChanged?.Invoke(null);
            }
        }

        public void SelectBuilding(BuildingBase building)
        {
            if (!CanSelectBuilding(building))
            {
                ClearSelection();
                return;
            }

            if (selectedBuilding == building)
            {
                RefreshSelectionVisuals();
                return;
            }

            selectedBuilding = building;
            showReachableRange = false;
            RefreshSelectionVisuals();
            SelectionChanged?.Invoke(selectedBuilding);
        }

        public void ClearSelection()
        {
            if (selectedBuilding == null)
            {
                ClearSelectionVisuals();
                return;
            }

            selectedBuilding = null;
            showReachableRange = false;
            ClearSelectionVisuals();
            SelectionChanged?.Invoke(null);
        }

        public void Refresh()
        {
            ResolveReferences();
            RegisterSelf();
            SubscribeBuildings();
            SubscribeCamera();
            RefreshBuildingSubscriptions();

            if (!CanSelectBuilding(selectedBuilding))
            {
                ClearSelection();
                return;
            }

            RefreshSelectionVisuals();
            SelectedBuildingStateChanged?.Invoke(selectedBuilding);
        }

        public void RequestSelectedBuildingDetail()
        {
            RequestBuildingDetail(selectedBuilding);
        }

        public void RequestBuildingDetail(BuildingBase building)
        {
            if (!CanSelectBuilding(building))
            {
                return;
            }

            if (selectedBuilding != building)
            {
                SelectBuilding(building);
            }

            DetailRequested?.Invoke(selectedBuilding);
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            buildings = gameSystem == null ? null : gameSystem.Buildings;
            inputController ??= InputController.Instance;
            placementController ??= FindFirstObjectByType<BuildingPlacementController>(FindObjectsInactive.Include);

            if (TryGetGamePanelFromUIManager(out UIPanel_Game gamePanel))
            {
                ResolveMarkerRoot(gamePanel);
            }
            else
            {
                markerCanvas = null;
                markerRoot = null;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }
        }

        private void RegisterSelf()
        {
            if (gameSystem != null)
            {
                gameSystem.RegisterBuildingSelectionController(this);
            }
        }

        private void UnregisterSelf()
        {
            if (gameSystem != null)
            {
                gameSystem.UnregisterBuildingSelectionController(this);
            }
        }

        private void HighlightSelectedFootprint()
        {
            ClearSelectionHighlight();
            BuildingBase building = selectedBuilding;
            if (!CanSelectBuilding(building)
                || building.GridMap == null
                || building.GridMap.HighlightTilemap == null
                || selectionHighlightTile == null)
            {
                return;
            }

            activeHighlightTilemap = building.GridMap.HighlightTilemap;
            foreach (GridPosition position in building.Footprint.Positions())
            {
                Vector3Int tilemapCell = GridPositionToTilemapCell(position);
                activeHighlightTilemap.SetTile(tilemapCell, selectionHighlightTile);
                highlightedCells.Add(tilemapCell);
            }
        }

        private void ClearSelectionHighlight()
        {
            if (activeHighlightTilemap != null)
            {
                for (int i = 0; i < highlightedCells.Count; i++)
                {
                    activeHighlightTilemap.SetTile(highlightedCells[i], null);
                }
            }

            highlightedCells.Clear();
            activeHighlightTilemap = null;
        }

        private void ShowOperationBar()
        {
            BuildingBase building = selectedBuilding;
            if (!CanSelectBuilding(building) || operationBarPrefab == null)
            {
                ClearOperationBar();
                return;
            }

            ResolveReferences();
            if (markerRoot == null)
            {
                ClearOperationBar();
                return;
            }

            if (activeOperationBar == null)
            {
                activeOperationBar = Instantiate(operationBarPrefab, markerRoot);
            }

            activeOperationBar.gameObject.SetActive(true);
            activeOperationBar.transform.SetParent(markerRoot, false);
            activeOperationBar.Bind(
                building,
                showReachableRange,
                HandleOperationBarDetailClicked,
                HandleOperationBarReachableRangeClicked);
            UpdateOperationBarPosition();
        }

        private void ClearOperationBar()
        {
            if (activeOperationBar == null)
            {
                return;
            }

            activeOperationBar.Unbind();
            activeOperationBar.gameObject.SetActive(false);
        }

        private void RefreshSelectionVisuals()
        {
            if (!CanSelectBuilding(selectedBuilding))
            {
                ClearSelectionVisuals();
                return;
            }

            HighlightSelectedFootprint();
            ShowOperationBar();
            RefreshReachableRangeHighlight();
        }

        private void ClearSelectionVisuals()
        {
            ClearOperationBar();
            ClearReachableRangeHighlight();
            ClearSelectionHighlight();
        }

        private void UpdateOperationBarPosition()
        {
            if (activeOperationBar == null)
            {
                return;
            }

            RectTransform operationBarRect = activeOperationBar.transform as RectTransform;
            if (operationBarRect == null || !TryGetOperationBarAnchoredPosition(out Vector2 anchoredPosition))
            {
                activeOperationBar.gameObject.SetActive(false);
                return;
            }

            activeOperationBar.gameObject.SetActive(true);
            operationBarRect.anchoredPosition = anchoredPosition;
        }

        private bool TryGetOperationBarAnchoredPosition(out Vector2 anchoredPosition)
        {
            anchoredPosition = default;

            if (!CanSelectBuilding(selectedBuilding) || markerRoot == null)
            {
                return false;
            }

            Camera sourceCamera = cameraController == null ? Camera.main : cameraController.SourceCamera;
            if (sourceCamera == null)
            {
                return false;
            }

            Vector3 screenPosition = sourceCamera.WorldToScreenPoint(GetOperationBarWorldPosition(selectedBuilding));
            if (screenPosition.z < 0f)
            {
                return false;
            }

            if (hideOperationBarWhenOffscreen && !IsScreenPositionInsideCamera(sourceCamera, screenPosition))
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    markerRoot,
                    screenPosition,
                    GetUiCamera(),
                    out Vector2 localPosition))
            {
                return false;
            }

            anchoredPosition = localPosition + operationBarScreenOffset;
            return true;
        }

        private Vector3 GetOperationBarWorldPosition(BuildingBase building)
        {
            if (building.HasPlacement && building.GridMap != null && building.Definition != null)
            {
                return building.GridMap.GetFootprintCenter(building.Origin, building.Definition.Size)
                       + operationBarWorldOffset;
            }

            return building.transform.position + operationBarWorldOffset;
        }

        private Camera GetUiCamera()
        {
            if (markerCanvas == null || markerCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            if (markerCanvas.worldCamera != null)
            {
                return markerCanvas.worldCamera;
            }

            return cameraController == null ? Camera.main : cameraController.SourceCamera;
        }

        private void ResolveMarkerRoot(UIPanel_Game gamePanel)
        {
            markerRoot = gamePanel == null ? null : gamePanel.GameMarkRoot;
            markerCanvas = markerRoot == null ? null : markerRoot.GetComponentInParent<Canvas>(true);

            if (markerRoot != null)
            {
                markerRoot.gameObject.SetActive(true);
            }
        }

        private void RefreshReachableRangeHighlight()
        {
            ClearReachableRangeHighlight();
            if (!showReachableRange)
            {
                return;
            }

            BuildingBase building = selectedBuilding;
            if (!CanSelectBuilding(building)
                || building.GridMap == null
                || building.GridMap.HighlightTilemap == null)
            {
                return;
            }

            var highlightTile = reachableRangeHighlightTile == null ? selectionHighlightTile : reachableRangeHighlightTile;
            if (highlightTile == null)
            {
                return;
            }

            var footprintCells = CreateFootprintCellSet(building);
            if (footprintCells.Count == 0)
            {
                return;
            }

            var reachable = GridManhattanPathfinder.FindReachable(
                building.GridMap,
                footprintCells,
                building.BuildingActionPower,
                position => CanEnterReachableRangeCell(building, footprintCells, position),
                position => building.GridMap.GetTraversalActionCost(position, building.GridOccupancyId));

            activeReachableRangeHighlightTilemap = building.GridMap.HighlightTilemap;
            for (var i = 0; i < reachable.Count; i++)
            {
                var position = reachable[i].Position;
                if (footprintCells.Contains(position))
                {
                    continue;
                }

                var tilemapCell = GridPositionToTilemapCell(position);
                activeReachableRangeHighlightTilemap.SetTile(tilemapCell, highlightTile);
                highlightedReachableRangeCells.Add(tilemapCell);
            }
        }

        private void ClearReachableRangeHighlight()
        {
            if (activeReachableRangeHighlightTilemap != null)
            {
                for (var i = 0; i < highlightedReachableRangeCells.Count; i++)
                {
                    activeReachableRangeHighlightTilemap.SetTile(highlightedReachableRangeCells[i], null);
                }
            }

            highlightedReachableRangeCells.Clear();
            activeReachableRangeHighlightTilemap = null;
        }

        private bool CanEnterReachableRangeCell(
            BuildingBase building,
            HashSet<GridPosition> footprintCells,
            GridPosition position)
        {
            if (building == null || building.GridMap == null || !building.GridMap.HasBaseTileAt(position))
            {
                return false;
            }

            if (footprintCells.Contains(position))
            {
                return true;
            }

            return building.GridMap.CanTraverse(position, building.GridOccupancyId);
        }

        private static HashSet<GridPosition> CreateFootprintCellSet(BuildingBase building)
        {
            var cells = new HashSet<GridPosition>();
            if (building == null || !building.HasPlacement)
            {
                return cells;
            }

            foreach (var position in building.Footprint.Positions())
            {
                cells.Add(position);
            }

            return cells;
        }

        private void HandleOperationBarDetailClicked(BuildingBase building)
        {
            RequestBuildingDetail(building);
        }

        private void HandleOperationBarReachableRangeClicked(BuildingBase building)
        {
            ToggleReachableRange(building);
        }

        private void ToggleReachableRange(BuildingBase building)
        {
            if (!CanSelectBuilding(building))
            {
                return;
            }

            if (selectedBuilding != building)
            {
                SelectBuilding(building);
            }

            showReachableRange = !showReachableRange;
            activeOperationBar?.SetReachableRangeVisible(showReachableRange);
            RefreshReachableRangeHighlight();
        }

        private void SubscribeBuildings()
        {
            if (buildings == null)
            {
                return;
            }

            if (subscribedToBuildings && subscribedBuildingService == buildings)
            {
                return;
            }

            UnsubscribeBuildings();
            buildings.BuildingsChanged += HandleBuildingsChanged;
            subscribedBuildingService = buildings;
            subscribedToBuildings = true;
        }

        private void UnsubscribeBuildings()
        {
            if (!subscribedToBuildings || subscribedBuildingService == null)
            {
                subscribedBuildingService = null;
                subscribedToBuildings = false;
                return;
            }

            subscribedBuildingService.BuildingsChanged -= HandleBuildingsChanged;
            subscribedBuildingService = null;
            subscribedToBuildings = false;
        }

        private void RefreshBuildingSubscriptions()
        {
            UnsubscribeBuildingEvents();
            if (buildings == null)
            {
                return;
            }

            IReadOnlyList<BuildingBase> source = buildings.Buildings;
            for (int i = 0; i < source.Count; i++)
            {
                BuildingBase building = source[i];
                if (building == null || !subscribedBuildings.Add(building))
                {
                    continue;
                }

                building.StateChanged += HandleBuildingStateChanged;
            }
        }

        private void UnsubscribeBuildingEvents()
        {
            foreach (BuildingBase building in subscribedBuildings)
            {
                if (building == null)
                {
                    continue;
                }

                building.StateChanged -= HandleBuildingStateChanged;
            }

            subscribedBuildings.Clear();
        }

        private void SubscribeCamera()
        {
            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }

            if (cameraController == null)
            {
                return;
            }

            if (subscribedToCamera && subscribedCameraController == cameraController)
            {
                return;
            }

            UnsubscribeCamera();
            cameraController.CameraViewChanged += HandleCameraViewChanged;
            subscribedCameraController = cameraController;
            subscribedToCamera = true;
        }

        private void UnsubscribeCamera()
        {
            if (!subscribedToCamera || subscribedCameraController == null)
            {
                subscribedCameraController = null;
                subscribedToCamera = false;
                return;
            }

            subscribedCameraController.CameraViewChanged -= HandleCameraViewChanged;
            subscribedCameraController = null;
            subscribedToCamera = false;
        }

        private void HandleBuildingsChanged(BuildingService changedBuildings)
        {
            buildings = changedBuildings;
            RefreshBuildingSubscriptions();

            if (!CanSelectBuilding(selectedBuilding))
            {
                ClearSelection();
                return;
            }

            RefreshSelectionVisuals();
        }

        private void HandleBuildingStateChanged(BuildingBase building)
        {
            if (building != selectedBuilding)
            {
                return;
            }

            if (!CanSelectBuilding(selectedBuilding))
            {
                ClearSelection();
                return;
            }

            RefreshSelectionVisuals();
            SelectedBuildingStateChanged?.Invoke(selectedBuilding);
        }

        private void HandleCameraViewChanged(CameraController changedCameraController)
        {
            UpdateOperationBarPosition();
        }

        private void HandleSelectionClick()
        {
            if (!updateSelectionOnWorldClick || IsPlacementActive())
            {
                ResetPendingSelectionClick();
                return;
            }

            if (!TryReadPointerState(out ScreenPointerState pointerState))
            {
                ResetPendingSelectionClick();
                return;
            }

            if (pointerState.WasPressedThisFrame)
            {
                hasPendingSelectionClick = true;
                selectionClickStartPosition = pointerState.ScreenPosition;
                selectionClickStartedOverUi = IsPointerOverUi(pointerState.ScreenPosition);
                TryGetBuildingAtScreenPosition(pointerState.ScreenPosition, out selectionClickStartedBuilding);
                return;
            }

            if (!hasPendingSelectionClick)
            {
                return;
            }

            if (!pointerState.WasReleasedThisFrame && pointerState.IsPressed)
            {
                return;
            }

            bool isClick = (pointerState.ScreenPosition - selectionClickStartPosition).sqrMagnitude
                           <= selectionClickMaxMovementPixels * selectionClickMaxMovementPixels;
            bool releasedOverUi = IsPointerOverUi(pointerState.ScreenPosition);
            bool releasedOverBuilding = TryGetBuildingAtScreenPosition(pointerState.ScreenPosition, out BuildingBase releasedBuilding);

            if (!isClick || selectionClickStartedOverUi || releasedOverUi)
            {
                ResetPendingSelectionClick();
                return;
            }

            if (releasedOverBuilding && releasedBuilding == selectionClickStartedBuilding)
            {
                SelectBuilding(releasedBuilding);
                DispatchBuildingPointerClick(releasedBuilding);
            }
            else if (!releasedOverBuilding && selectionClickStartedBuilding == null)
            {
                ClearSelection();
                ResetLastBuildingClick();
            }

            ResetPendingSelectionClick();
        }

        private void ResetPendingSelectionClick()
        {
            hasPendingSelectionClick = false;
            selectionClickStartedOverUi = false;
            selectionClickStartedBuilding = null;
            selectionClickStartPosition = default;
        }

        private bool TryReadPointerState(out ScreenPointerState pointerState)
        {
            inputController ??= InputController.Instance;
            if (inputController != null && inputController.TryGetPrimaryPointerState(out pointerState))
            {
                return true;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                pointerState = new ScreenPointerState(
                    PointerDeviceKind.Mouse,
                    mouse.position.ReadValue(),
                    mouse.leftButton.isPressed,
                    mouse.leftButton.wasPressedThisFrame,
                    mouse.leftButton.wasReleasedThisFrame);
                return true;
            }

            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touch = touchscreen.primaryTouch;
                if (touch.press.isPressed || touch.press.wasPressedThisFrame || touch.press.wasReleasedThisFrame)
                {
                    pointerState = new ScreenPointerState(
                        PointerDeviceKind.Touch,
                        touch.position.ReadValue(),
                        touch.press.isPressed,
                        touch.press.wasPressedThisFrame,
                        touch.press.wasReleasedThisFrame);
                    return true;
                }
            }

            pointerState = default;
            return false;
        }

        private bool IsPointerOverUi(Vector2 screenPosition)
        {
            inputController ??= InputController.Instance;
            return inputController != null && inputController.IsPointerOverUi(screenPosition);
        }

        private bool TryGetBuildingAtScreenPosition(Vector2 screenPosition, out BuildingBase building)
        {
            return BuildingPointerHitUtility.TryGetBuilding(GetSelectionCamera(), screenPosition, out building);
        }

        private Camera GetSelectionCamera()
        {
            if (cameraController != null && cameraController.SourceCamera != null)
            {
                return cameraController.SourceCamera;
            }

            return Camera.main;
        }

        private void DispatchBuildingPointerClick(BuildingBase building)
        {
            if (!CanSelectBuilding(building))
            {
                ResetLastBuildingClick();
                return;
            }

            float now = Time.unscaledTime;
            bool isDoubleClick = lastClickedBuilding == building
                                 && now - lastBuildingClickTime <= Mathf.Max(0.05f, building.DoubleClickInterval);
            lastClickedBuilding = building;
            lastBuildingClickTime = now;
            building.DispatchPointerClick(isDoubleClick);
        }

        private void ResetLastBuildingClick()
        {
            lastClickedBuilding = null;
            lastBuildingClickTime = float.NegativeInfinity;
        }

        private bool IsPlacementActive()
        {
            if (placementController == null)
            {
                placementController = FindFirstObjectByType<BuildingPlacementController>(FindObjectsInactive.Include);
            }

            return placementController != null && placementController.isActiveAndEnabled && placementController.IsActive;
        }

        private void QueueDelayedResolveIfNeeded()
        {
            if (gameSystem != null && buildings != null && markerCanvas != null && markerRoot != null)
            {
                return;
            }

            if (delayedResolveCoroutine == null && isActiveAndEnabled)
            {
                delayedResolveCoroutine = StartCoroutine(ResolveReferencesNextFrame());
            }
        }

        private IEnumerator ResolveReferencesNextFrame()
        {
            yield return null;
            delayedResolveCoroutine = null;

            if (!isActiveAndEnabled)
            {
                yield break;
            }

            ResolveReferences();
            RegisterSelf();
            SubscribeBuildings();
            SubscribeCamera();
            RefreshBuildingSubscriptions();
            RefreshSelectionVisuals();
        }

        private void StopDelayedResolve()
        {
            if (delayedResolveCoroutine == null)
            {
                return;
            }

            StopCoroutine(delayedResolveCoroutine);
            delayedResolveCoroutine = null;
        }

        private static bool TryGetGamePanelFromUIManager(out UIPanel_Game gamePanel)
        {
            UIManager uiManager = UIManager.Instance;
            if (uiManager != null && uiManager.TryGetActivePanel<UIPanel_Game>(out gamePanel))
            {
                return gamePanel != null;
            }

            gamePanel = null;
            return false;
        }

        private static bool CanSelectBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private static bool IsScreenPositionInsideCamera(Camera sourceCamera, Vector3 screenPosition)
        {
            Rect pixelRect = sourceCamera.pixelRect;
            return screenPosition.x >= pixelRect.xMin
                   && screenPosition.x <= pixelRect.xMax
                   && screenPosition.y >= pixelRect.yMin
                   && screenPosition.y <= pixelRect.yMax;
        }

        private static Vector3Int GridPositionToTilemapCell(GridPosition position)
        {
            return new Vector3Int(position.X, position.Y, 0);
        }

    }
}
