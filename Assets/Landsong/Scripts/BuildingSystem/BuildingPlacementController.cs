using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem.Buildings;
using Landsong.CameraSystem;
using Landsong.GridSystem;
using Landsong.InputSystem;
using Landsong.InventorySystem;
using Landsong.UISystem;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.Tilemaps;

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
        [SerializeField, InspectorName("放置控制 UI")] private GamePanel_BuildingPlacementControls placementControls;
        [SerializeField] private bool cancelPlacementOnDisable = true;

        [Header("Demolition")]
        [SerializeField, InspectorName("拆除点击最大移动像素"), Min(0f)] private float demolitionClickMaxMovementPixels = 8f;

        [Header("Tile Highlight")]
        [SerializeField, InspectorName("合法格子高亮瓦片")] private TileBase validHighlightTile;
        [SerializeField, InspectorName("建筑占格高亮瓦片")] private TileBase buildingFootprintHighlightTile;
        [SerializeField, InspectorName("非法占格高亮瓦片")] private TileBase invalidHighlightTile;
        [SerializeField, InspectorName("道路连接预览瓦片")] private TileBase roadConnectionHighlightTile;

        private readonly List<Vector3Int> highlightedBuildableAreaCells = new List<Vector3Int>();
        private readonly List<Vector3Int> highlightedFootprintCells = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> highlightedBuildableAreaCellSet = new HashSet<Vector3Int>();
        private readonly List<GridPosition> currentRoadPath = new List<GridPosition>();
        private readonly List<GridPosition> roadPathCandidateA = new List<GridPosition>();
        private readonly List<GridPosition> roadPathCandidateB = new List<GridPosition>();
        private Tilemap activeHighlightTilemap;
        private BuildingDefinition highlightedBuildableAreaDefinition;
        private InputController inputController;
        private BuildingBase activeBuildingPrefab;
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

        public event Action<bool> DemolitionModeChanged;

        public bool IsPlacing => isPlacing;
        public bool IsDemolitionMode => isDemolitionMode;
        public bool IsActive => isPlacing || isDemolitionMode;

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

            CancelPlacement();

            activeBuildingPrefab = buildingPrefab;
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

            var gameSystem = Landsong.GameSystem.Instance;
            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            if (!CanSpendPlacementCosts(definition, inventory))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because placement costs are missing.", buildingPrefab);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var buildingService = gameSystem == null ? null : gameSystem.Buildings;
            if (buildingService == null)
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because BuildingService is missing.", buildingPrefab);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            if (!buildingService.TryPlace(buildingPrefab, gridMap, origin, placedBuildingRoot, out var placedBuilding))
            {
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            if (!SpendPlacementCosts(definition, inventory))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because placement costs could not be spent.", buildingPrefab);
                buildingService.Remove(placedBuilding);
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
            if (roadPath == null || roadPath.Count == 0 || !CanPlaceRoadPath(roadPath, definition))
            {
                Debug.LogWarning($"Cannot place road '{definition.DisplayName}' because the selected path is invalid.", this);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var roadOriginsToPlace = new List<GridPosition>(roadPath.Count);
            CollectRoadOriginsToPlace(roadPath, definition, roadOriginsToPlace);

            var gameSystem = Landsong.GameSystem.Instance;
            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            if (!CanSpendPlacementCosts(definition, inventory, roadOriginsToPlace.Count))
            {
                Debug.LogWarning($"Cannot place road '{definition.DisplayName}' because placement costs are missing.", buildingPrefab);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var buildingService = gameSystem == null ? null : gameSystem.Buildings;
            if (buildingService == null)
            {
                Debug.LogWarning($"Cannot place road '{definition.DisplayName}' because BuildingService is missing.", buildingPrefab);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var placedBuildings = new List<BuildingBase>(roadOriginsToPlace.Count);
            for (var i = 0; i < roadOriginsToPlace.Count; i++)
            {
                if (!buildingService.TryPlace(buildingPrefab, gridMap, roadOriginsToPlace[i], placedBuildingRoot, out var placedBuilding))
                {
                    RollbackPlacedBuildings(buildingService, placedBuildings);
                    isConfirming = false;
                    RefreshCurrentPlacementState();
                    yield break;
                }

                placedBuildings.Add(placedBuilding);
            }

            if (!SpendPlacementCosts(definition, inventory, roadOriginsToPlace.Count))
            {
                Debug.LogWarning($"Cannot place road '{definition.DisplayName}' because placement costs could not be spent.", buildingPrefab);
                RollbackPlacedBuildings(buildingService, placedBuildings);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            CancelPlacement();
        }

        private static void RollbackPlacedBuildings(BuildingService buildingService, IReadOnlyList<BuildingBase> placedBuildings)
        {
            if (buildingService == null || placedBuildings == null)
            {
                return;
            }

            for (var i = placedBuildings.Count - 1; i >= 0; i--)
            {
                buildingService.Remove(placedBuildings[i]);
            }
        }

        private static bool CanSpendPlacementCosts(BuildingDefinition definition, InventoryService inventory)
        {
            return CanSpendPlacementCosts(definition, inventory, 1);
        }

        private static bool CanSpendPlacementCosts(BuildingDefinition definition, InventoryService inventory, int multiplier)
        {
            if (multiplier <= 0)
            {
                return true;
            }

            if (definition == null || !HasAnyValidCost(definition.PlacementCosts))
            {
                return true;
            }

            var normalizedMultiplier = multiplier;
            IEnumerable<BuildingCost> costs = normalizedMultiplier <= 1
                ? definition.PlacementCosts
                : RepeatPlacementCosts(definition.PlacementCosts, normalizedMultiplier);
            return inventory != null && inventory.CanAffordBuildingCosts(costs);
        }

        private static bool SpendPlacementCosts(BuildingDefinition definition, InventoryService inventory)
        {
            return SpendPlacementCosts(definition, inventory, 1);
        }

        private static bool SpendPlacementCosts(BuildingDefinition definition, InventoryService inventory, int multiplier)
        {
            if (multiplier <= 0)
            {
                return true;
            }

            if (definition == null || !HasAnyValidCost(definition.PlacementCosts))
            {
                return true;
            }

            var normalizedMultiplier = multiplier;
            IEnumerable<BuildingCost> costs = normalizedMultiplier <= 1
                ? definition.PlacementCosts
                : RepeatPlacementCosts(definition.PlacementCosts, normalizedMultiplier);
            return inventory != null && inventory.TrySpendBuildingCosts(costs);
        }

        private static bool HasAnyValidCost(IReadOnlyList<BuildingCost> costs)
        {
            if (costs == null)
            {
                return false;
            }

            for (var i = 0; i < costs.Count; i++)
            {
                if (costs[i].IsValid)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<BuildingCost> RepeatPlacementCosts(IReadOnlyList<BuildingCost> costs, int multiplier)
        {
            if (costs == null || multiplier <= 0)
            {
                yield break;
            }

            for (var repeatIndex = 0; repeatIndex < multiplier; repeatIndex++)
            {
                for (var i = 0; i < costs.Count; i++)
                {
                    yield return costs[i];
                }
            }
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
            currentCanPlace = gridMap.CanOccupy(currentOrigin, activeDefinition.Size, activeDefinition.RequiredTerrainKeys, out _);

            var placementPosition = gridMap.GetFootprintCenter(currentOrigin, activeDefinition.Size);
            MovePreview(placementPosition);
            UpdateFootprintHighlight(new GridFootprint(currentOrigin, activeDefinition.Size), activeDefinition);

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

            currentCanPlace = gridMap.CanOccupy(currentOrigin, activeDefinition.Size, activeDefinition.RequiredTerrainKeys, out _);
            UpdateFootprintHighlight(new GridFootprint(currentOrigin, activeDefinition.Size), activeDefinition);

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
            SelectRoadPath(roadStartOrigin, roadEndOrigin, activeDefinition, currentRoadPath, out currentCanPlace);

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
            currentRoadPath.Clear();
            if (activeBuildingPrefab != null)
            {
                EnsureBuildableAreaHighlight(activeBuildingPrefab.Definition);
                ClearFootprintHighlight();
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
            ghostInstance = CreateGhostInstance(
                buildingPrefab,
                isRoadPlacement ? $"{definition.DisplayName}_PlacementHeadGhost" : $"{definition.DisplayName}_PlacementGhost",
                out ghostView);

            if (isRoadPlacement)
            {
                roadTailGhostInstance = CreateGhostInstance(
                    buildingPrefab,
                    $"{definition.DisplayName}_PlacementTailGhost",
                    out roadTailGhostView);
            }
        }

        private GameObject CreateGhostInstance(BuildingBase buildingPrefab, string ghostName, out BuildingView view)
        {
            var parent = previewRoot == null ? transform : previewRoot;
            var definition = buildingPrefab.Definition;
            var instance = Instantiate(buildingPrefab.gameObject, parent);
            instance.name = ghostName;
            instance.SetActive(false);

            var ghostBuilding = instance.GetComponentInChildren<BuildingBase>(true);
            DisablePreviewBuildingRuntime(instance);
            view = ghostBuilding == null ? instance.GetComponentInChildren<BuildingView>(true) : ghostBuilding.View;
            if (view == null)
            {
                Debug.LogWarning($"Building preview '{definition.DisplayName}' has no BuildingView.", instance);
                return instance;
            }

            if (!view.SetPlacementPreview(true))
            {
                Debug.LogWarning(
                    $"Building preview '{definition.DisplayName}' cannot play preview animation key '{view.PlacementPreviewAnimationKey}'.",
                    view);
            }

            return instance;
        }

        private void DestroyGhost(ref GameObject instance, ref BuildingView view)
        {
            if (instance == null)
            {
                view = null;
                return;
            }

            if (view != null)
            {
                view.SetPlacementPreview(false);
            }

            Destroy(instance);
            instance = null;
            view = null;
        }

        private static void DisablePreviewBuildingRuntime(GameObject previewInstance)
        {
            if (previewInstance == null)
            {
                return;
            }

            var buildings = previewInstance.GetComponentsInChildren<BuildingBase>(true);
            for (var i = 0; i < buildings.Length; i++)
            {
                buildings[i].enabled = false;
            }
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

        private void UpdateDemolitionHighlight(BuildingBase building)
        {
            ClearHighlightedTiles();
            if (!CanDemolishBuilding(building) || building.GridMap == null || building.GridMap.HighlightTilemap == null)
            {
                return;
            }

            var highlightTile = invalidHighlightTile ?? buildingFootprintHighlightTile ?? validHighlightTile;
            if (highlightTile == null)
            {
                return;
            }

            activeHighlightTilemap = building.GridMap.HighlightTilemap;
            foreach (var position in building.Footprint.Positions())
            {
                var tilemapCell = GridPositionToHighlightTilemapCell(position);
                activeHighlightTilemap.SetTile(tilemapCell, highlightTile);
                highlightedFootprintCells.Add(tilemapCell);
            }
        }

        private void UpdateFootprintHighlight(GridFootprint footprint, BuildingDefinition definition)
        {
            if (gridMap == null || gridMap.HighlightTilemap == null)
            {
                ClearHighlightedTiles();
                return;
            }

            var highlightTilemap = gridMap.HighlightTilemap;
            EnsureBuildableAreaHighlight(definition);
            ClearFootprintHighlight();
            activeHighlightTilemap = highlightTilemap;

            var highlightTile = currentCanPlace
                ? buildingFootprintHighlightTile ?? validHighlightTile
                : invalidHighlightTile ?? buildingFootprintHighlightTile ?? validHighlightTile;
            if (highlightTile == null)
            {
                return;
            }

            foreach (var position in footprint.Positions())
            {
                var tilemapCell = GridPositionToHighlightTilemapCell(position);
                highlightTilemap.SetTile(tilemapCell, highlightTile);
                highlightedFootprintCells.Add(tilemapCell);
            }
        }

        private void UpdateRoadPathHighlight(BuildingDefinition definition)
        {
            if (gridMap == null || gridMap.HighlightTilemap == null)
            {
                ClearHighlightedTiles();
                return;
            }

            var highlightTilemap = gridMap.HighlightTilemap;
            EnsureBuildableAreaHighlight(definition);
            ClearFootprintHighlight();
            activeHighlightTilemap = highlightTilemap;

            if (definition == null || currentRoadPath.Count == 0)
            {
                return;
            }

            for (var i = 0; i < currentRoadPath.Count; i++)
            {
                var origin = currentRoadPath[i];
                var canPlaceCell = IsRoadPathOriginValid(origin, definition);
                var isEndpoint = i == 0 || i == currentRoadPath.Count - 1;
                var highlightTile = GetRoadPathHighlightTile(isEndpoint, canPlaceCell);
                if (highlightTile == null)
                {
                    continue;
                }

                var footprint = new GridFootprint(origin, definition.Size);
                foreach (var position in footprint.Positions())
                {
                    var tilemapCell = GridPositionToHighlightTilemapCell(position);
                    highlightTilemap.SetTile(tilemapCell, highlightTile);
                    highlightedFootprintCells.Add(tilemapCell);
                }
            }
        }

        private TileBase GetRoadPathHighlightTile(bool isEndpoint, bool canPlaceCell)
        {
            if (!canPlaceCell)
            {
                return invalidHighlightTile ?? buildingFootprintHighlightTile ?? roadConnectionHighlightTile ?? validHighlightTile;
            }

            if (isEndpoint)
            {
                return buildingFootprintHighlightTile ?? validHighlightTile;
            }

            return roadConnectionHighlightTile ?? buildingFootprintHighlightTile ?? validHighlightTile;
        }

        private void EnsureBuildableAreaHighlight(BuildingDefinition definition)
        {
            if (gridMap == null || gridMap.HighlightTilemap == null)
            {
                ClearHighlightedTiles();
                return;
            }

            var highlightTilemap = gridMap.HighlightTilemap;
            if (activeHighlightTilemap != null && activeHighlightTilemap != highlightTilemap)
            {
                ClearHighlightedTiles();
            }

            activeHighlightTilemap = highlightTilemap;
            if (highlightedBuildableAreaDefinition == definition)
            {
                return;
            }

            ClearFootprintHighlight();
            ClearBuildableAreaHighlight();
            highlightedBuildableAreaDefinition = definition;

            if (definition == null || validHighlightTile == null)
            {
                return;
            }

            var bounds = gridMap.BaseCellBounds;
            for (var y = bounds.yMin; y < bounds.yMax; y++)
            {
                for (var x = bounds.xMin; x < bounds.xMax; x++)
                {
                    var position = new GridPosition(x, y);
                    if (!IsPlacementCellValid(position, definition))
                    {
                        continue;
                    }

                    var tilemapCell = GridPositionToHighlightTilemapCell(position);
                    highlightTilemap.SetTile(tilemapCell, validHighlightTile);
                    highlightedBuildableAreaCells.Add(tilemapCell);
                    highlightedBuildableAreaCellSet.Add(tilemapCell);
                }
            }
        }

        private void ClearFootprintHighlight()
        {
            if (activeHighlightTilemap == null || highlightedFootprintCells.Count == 0)
            {
                highlightedFootprintCells.Clear();
                return;
            }

            for (var i = 0; i < highlightedFootprintCells.Count; i++)
            {
                var tilemapCell = highlightedFootprintCells[i];
                var restoreTile = highlightedBuildableAreaCellSet.Contains(tilemapCell) ? validHighlightTile : null;
                activeHighlightTilemap.SetTile(tilemapCell, restoreTile);
            }

            highlightedFootprintCells.Clear();
        }

        private void ClearBuildableAreaHighlight()
        {
            if (activeHighlightTilemap != null)
            {
                for (var i = 0; i < highlightedBuildableAreaCells.Count; i++)
                {
                    activeHighlightTilemap.SetTile(highlightedBuildableAreaCells[i], null);
                }
            }

            highlightedBuildableAreaCells.Clear();
            highlightedBuildableAreaCellSet.Clear();
            highlightedBuildableAreaDefinition = null;
        }

        private bool IsPlacementCellValid(GridPosition position, BuildingDefinition definition)
        {
            return definition != null
                   && gridMap != null
                   && gridMap.CanOccupy(position, Vector2Int.one, definition.RequiredTerrainKeys, out _);
        }

        private void SelectRoadPath(
            GridPosition start,
            GridPosition end,
            BuildingDefinition definition,
            List<GridPosition> selectedPath,
            out bool canPlace)
        {
            selectedPath.Clear();
            canPlace = false;

            if (definition == null || gridMap == null)
            {
                return;
            }

            BuildSingleTurnRoadPath(start, end, true, roadPathCandidateA);
            BuildSingleTurnRoadPath(start, end, false, roadPathCandidateB);

            var invalidHorizontalFirstCells = CountInvalidRoadPathCells(roadPathCandidateA, definition);
            var invalidVerticalFirstCells = CountInvalidRoadPathCells(roadPathCandidateB, definition);

            var useHorizontalFirst = invalidHorizontalFirstCells == 0
                                     || (invalidVerticalFirstCells != 0
                                         && invalidHorizontalFirstCells <= invalidVerticalFirstCells);
            var selectedCandidate = useHorizontalFirst ? roadPathCandidateA : roadPathCandidateB;
            var selectedInvalidCells = useHorizontalFirst ? invalidHorizontalFirstCells : invalidVerticalFirstCells;

            selectedPath.AddRange(selectedCandidate);
            canPlace = selectedPath.Count > 0 && selectedInvalidCells == 0;
        }

        private bool CanPlaceRoadPath(IReadOnlyList<GridPosition> roadPath, BuildingDefinition definition)
        {
            return roadPath != null
                   && roadPath.Count > 0
                   && CountInvalidRoadPathCells(roadPath, definition) == 0;
        }

        private int CountInvalidRoadPathCells(IReadOnlyList<GridPosition> roadPath, BuildingDefinition definition)
        {
            if (roadPath == null || roadPath.Count == 0 || definition == null || gridMap == null)
            {
                return int.MaxValue;
            }

            var invalidCells = 0;
            for (var i = 0; i < roadPath.Count; i++)
            {
                if (!IsRoadPathOriginValid(roadPath[i], definition))
                {
                    invalidCells++;
                }
            }

            return invalidCells;
        }

        private void CollectRoadOriginsToPlace(
            IReadOnlyList<GridPosition> roadPath,
            BuildingDefinition definition,
            List<GridPosition> output)
        {
            output.Clear();
            if (roadPath == null || definition == null)
            {
                return;
            }

            for (var i = 0; i < roadPath.Count; i++)
            {
                var origin = roadPath[i];
                if (IsExistingRoadAt(origin, definition))
                {
                    continue;
                }

                output.Add(origin);
            }
        }

        private bool IsRoadPathOriginValid(GridPosition origin, BuildingDefinition definition)
        {
            if (definition == null || gridMap == null)
            {
                return false;
            }

            return gridMap.CanOccupy(origin, definition.Size, definition.RequiredTerrainKeys, out _)
                   || IsExistingRoadAt(origin, definition);
        }

        private bool IsExistingRoadAt(GridPosition origin, BuildingDefinition definition)
        {
            if (definition == null || gridMap == null)
            {
                return false;
            }

            var footprint = new GridFootprint(origin, definition.Size);
            foreach (var position in footprint.Positions())
            {
                if (!TryGetExistingRoadAt(position, out _))
                {
                    return false;
                }
            }

            return true;
        }

        private bool TryGetExistingRoadAt(GridPosition position, out RoadBuilding road)
        {
            road = null;
            if (gridMap == null || !gridMap.TryGetOccupantId(position, out var occupantId) || string.IsNullOrWhiteSpace(occupantId))
            {
                return false;
            }

            var gameSystem = Landsong.GameSystem.Instance;
            var buildingService = gameSystem == null ? null : gameSystem.Buildings;
            var buildings = buildingService == null ? null : buildingService.Buildings;
            if (buildings == null)
            {
                return false;
            }

            for (var i = 0; i < buildings.Count; i++)
            {
                if (buildings[i] is not RoadBuilding candidate
                    || !candidate.HasPlacement
                    || candidate.GridMap != gridMap
                    || candidate.IsDemolishing
                    || !string.Equals(candidate.GridOccupancyId, occupantId, System.StringComparison.Ordinal))
                {
                    continue;
                }

                road = candidate;
                return true;
            }

            return false;
        }

        private static void BuildSingleTurnRoadPath(
            GridPosition start,
            GridPosition end,
            bool horizontalFirst,
            List<GridPosition> output)
        {
            output.Clear();

            var corner = horizontalFirst
                ? new GridPosition(end.X, start.Y)
                : new GridPosition(start.X, end.Y);

            AppendGridLine(start, corner, output, false);
            AppendGridLine(corner, end, output, true);
        }

        private static void AppendGridLine(GridPosition from, GridPosition to, List<GridPosition> output, bool skipFirst)
        {
            var x = from.X;
            var y = from.Y;
            if (!skipFirst)
            {
                output.Add(new GridPosition(x, y));
            }

            var stepX = to.X.CompareTo(from.X);
            var stepY = to.Y.CompareTo(from.Y);
            while (x != to.X || y != to.Y)
            {
                if (x != to.X)
                {
                    x += stepX;
                }
                else
                {
                    y += stepY;
                }

                output.Add(new GridPosition(x, y));
            }
        }

        private Vector3Int GridPositionToHighlightTilemapCell(GridPosition position)
        {
            return new Vector3Int(position.X, position.Y, 0);
        }

        private void ClearHighlightedTiles()
        {
            if (activeHighlightTilemap == null)
            {
                highlightedFootprintCells.Clear();
                highlightedBuildableAreaCells.Clear();
                highlightedBuildableAreaCellSet.Clear();
                highlightedBuildableAreaDefinition = null;
                activeHighlightTilemap = null;
                return;
            }

            for (var i = 0; i < highlightedFootprintCells.Count; i++)
            {
                activeHighlightTilemap.SetTile(highlightedFootprintCells[i], null);
            }

            for (var i = 0; i < highlightedBuildableAreaCells.Count; i++)
            {
                activeHighlightTilemap.SetTile(highlightedBuildableAreaCells[i], null);
            }

            highlightedFootprintCells.Clear();
            highlightedBuildableAreaCells.Clear();
            highlightedBuildableAreaCellSet.Clear();
            highlightedBuildableAreaDefinition = null;
            activeHighlightTilemap = null;
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
            return buildingPrefab is RoadBuilding;
        }

        private static bool CanDemolishBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private static BuildingService ResolveBuildingService(BuildingBase building)
        {
            var gameSystem = building == null ? null : building.GameSystem;
            if (gameSystem != null && gameSystem.Buildings != null)
            {
                return gameSystem.Buildings;
            }

            gameSystem = Landsong.GameSystem.Instance;
            return gameSystem == null ? null : gameSystem.Buildings;
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
