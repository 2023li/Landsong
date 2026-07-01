using System.Collections;
using System.Collections.Generic;
using Landsong.CameraSystem;
using Landsong.GridSystem;
using Landsong.InputSystem;
using Landsong.InventorySystem;
using Landsong.UISystem;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private Transform previewRoot;
        [SerializeField] private Transform placedBuildingRoot;

        [Header("Preview")]
        [SerializeField] private Vector3 ghostWorldOffset = Vector3.zero;
        [SerializeField] private Vector3 controlsWorldOffset = new Vector3(0f, -1.25f, 0f);
        [SerializeField, InspectorName("放置控制 UI")] private GamePanel_BuildingPlacementControls placementControls;
        [SerializeField, Min(0f)] private float ghostHitPadding = 0.05f;
        [SerializeField] private bool cancelPlacementOnDisable = true;

        [Header("Tile Highlight")]
        [SerializeField, InspectorName("合法格子高亮瓦片")] private TileBase validHighlightTile;
        [SerializeField, InspectorName("建筑占格高亮瓦片")] private TileBase buildingFootprintHighlightTile;
        [SerializeField, InspectorName("非法占格高亮瓦片")] private TileBase invalidHighlightTile;

        private readonly List<Vector3Int> highlightedBuildableAreaCells = new List<Vector3Int>();
        private readonly List<Vector3Int> highlightedFootprintCells = new List<Vector3Int>();
        private readonly HashSet<Vector3Int> highlightedBuildableAreaCellSet = new HashSet<Vector3Int>();
        private Tilemap activeHighlightTilemap;
        private BuildingDefinition highlightedBuildableAreaDefinition;
        private InputController inputController;
        private BuildingBase activeBuildingPrefab;
        private GameObject ghostInstance;
        private BuildingView ghostView;
        private GridPosition currentOrigin;
        private bool hasCurrentOrigin;
        private bool currentCanPlace;
        private bool isPlacing;
        private bool isConfirming;
        private bool isDraggingPlacement;

        public bool IsPlacing => isPlacing;

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
            if (!isPlacing || isConfirming)
            {
                return;
            }

            UpdatePlacementDrag();
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

        public void ConfirmPlacement()
        {
            if (!isPlacing || isConfirming || activeBuildingPrefab == null || !hasCurrentOrigin || !currentCanPlace)
            {
                return;
            }

            StartCoroutine(ConfirmPlacementRoutine(activeBuildingPrefab, currentOrigin));
        }

        public void CancelPlacement()
        {
            SetCameraInputBlocked(false);

            activeBuildingPrefab = null;
            isPlacing = false;
            isConfirming = false;
            isDraggingPlacement = false;
            currentCanPlace = false;
            hasCurrentOrigin = false;

            if (ghostInstance != null)
            {
                if (ghostView != null)
                {
                    ghostView.SetPlacementPreview(false);
                    ghostView = null;
                }

                Destroy(ghostInstance);
                ghostInstance = null;
            }

            ReleasePlacementControls();

            ClearHighlightedTiles();
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

        private static bool CanSpendPlacementCosts(BuildingDefinition definition, InventoryService inventory)
        {
            if (definition == null || !HasAnyValidCost(definition.PlacementCosts))
            {
                return true;
            }

            return inventory != null && inventory.CanAffordBuildingCosts(definition.PlacementCosts);
        }

        private static bool SpendPlacementCosts(BuildingDefinition definition, InventoryService inventory)
        {
            if (definition == null || !HasAnyValidCost(definition.PlacementCosts))
            {
                return true;
            }

            return inventory != null && inventory.TrySpendBuildingCosts(definition.PlacementCosts);
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
                return;
            }

            if (pointerState.WasPressedThisFrame)
            {
                isDraggingPlacement = !IsPointerOverUi(pointerState.ScreenPosition) && IsPointerOverGhost(pointerState.ScreenPosition);
                SetCameraInputBlocked(isDraggingPlacement);
            }

            if (!isDraggingPlacement)
            {
                SetCameraInputBlocked(false);
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

            if (!gridMap.TryGetGridPointFromScreenPosition(sourceCamera, screenPosition, out var gridPoint))
            {
                SetNoCurrentPlacement();
                return;
            }

            currentOrigin = GetPlacementOrigin(gridPoint, activeDefinition.Size);
            hasCurrentOrigin = true;
            currentCanPlace = gridMap.CanOccupy(currentOrigin, activeDefinition.Size, activeDefinition.RequiredTerrainKeys, out _);

            var placementPosition = gridMap.GetFootprintCenter(currentOrigin, activeDefinition.Size);
            MovePreview(placementPosition);
            UpdateFootprintHighlight(new GridFootprint(currentOrigin, activeDefinition.Size), activeDefinition);

            SetPlacementConfirmInteractable(currentCanPlace);
        }

        private void RefreshCurrentPlacementState()
        {
            if (!hasCurrentOrigin || activeBuildingPrefab == null || gridMap == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            var activeDefinition = activeBuildingPrefab.Definition;
            currentCanPlace = gridMap.CanOccupy(currentOrigin, activeDefinition.Size, activeDefinition.RequiredTerrainKeys, out _);
            UpdateFootprintHighlight(new GridFootprint(currentOrigin, activeDefinition.Size), activeDefinition);

            SetPlacementConfirmInteractable(currentCanPlace);
        }

        private void SetNoCurrentPlacement()
        {
            hasCurrentOrigin = false;
            currentCanPlace = false;
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
            if (ghostInstance != null)
            {
                ghostInstance.SetActive(true);
                ghostInstance.transform.position = ghostPosition;
            }

            placementControls?.SetWorldPosition(sourceCamera, ghostPosition + controlsWorldOffset);
        }

        private void RefreshPlacementControlsPosition()
        {
            if (!isPlacing || activeBuildingPrefab == null || gridMap == null || !hasCurrentOrigin)
            {
                return;
            }

            ResolveSceneReferences();
            var activeDefinition = activeBuildingPrefab.Definition;
            var placementPosition = gridMap.GetFootprintCenter(currentOrigin, activeDefinition.Size);
            var ghostPosition = placementPosition + ghostWorldOffset;
            placementControls?.SetWorldPosition(sourceCamera, ghostPosition + controlsWorldOffset);
        }

        private void CreateGhost(BuildingBase buildingPrefab)
        {
            var parent = previewRoot == null ? transform : previewRoot;
            var definition = buildingPrefab.Definition;
            ghostInstance = Instantiate(buildingPrefab.gameObject, parent);
            ghostInstance.name = $"{definition.DisplayName}_PlacementGhost";
            ghostInstance.SetActive(false);

            var ghostBuilding = ghostInstance.GetComponentInChildren<BuildingBase>(true);
            DisablePreviewBuildingRuntime(ghostInstance);
            ghostView = ghostBuilding == null ? ghostInstance.GetComponentInChildren<BuildingView>(true) : ghostBuilding.View;
            if (ghostView == null)
            {
                Debug.LogWarning($"Building preview '{definition.DisplayName}' has no BuildingView.", ghostInstance);
                return;
            }

            if (!ghostView.SetPlacementPreview(true))
            {
                Debug.LogWarning(
                    $"Building preview '{definition.DisplayName}' cannot play preview animation key '{ghostView.PlacementPreviewAnimationKey}'.",
                    ghostView);
            }
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

            if (placementControls == null)
            {
                Debug.LogWarning(
                    "Cannot show building placement controls because no GamePanel_BuildingPlacementControls exists in the game UI.",
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
            if (ghostInstance == null || sourceCamera == null)
            {
                return false;
            }

            var ray = sourceCamera.ScreenPointToRay(screenPosition);
            if (TryHitGhostCollider(ray))
            {
                return true;
            }

            if (!TryGetWorldPointOnPlacementPlane(ray, out var worldPosition))
            {
                return false;
            }

            return IsWorldPointInsideGhostRenderers(worldPosition);
        }

        private bool TryHitGhostCollider(Ray ray)
        {
            var colliders = ghostInstance.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                var collider = colliders[i];
                if (collider != null && collider.enabled && collider.Raycast(ray, out _, sourceCamera.farClipPlane))
                {
                    return true;
                }
            }

            if ((gridMap == null || gridMap.PlaneMode == GridPlaneMode.XY || gridMap.PlaneMode == GridPlaneMode.IsometricDiamondXY)
                && TryGetWorldPointOnPlacementPlane(ray, out var worldPoint))
            {
                var colliders2D = ghostInstance.GetComponentsInChildren<Collider2D>(true);
                for (var i = 0; i < colliders2D.Length; i++)
                {
                    var collider = colliders2D[i];
                    if (collider != null && collider.enabled && collider.OverlapPoint(worldPoint))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TryGetWorldPointOnPlacementPlane(Ray ray, out Vector3 worldPosition)
        {
            if (gridMap != null && gridMap.IsInitialized && gridMap.Layout.TryRaycastToGridPlane(ray, out worldPosition))
            {
                return true;
            }

            var plane = new UnityEngine.Plane(Vector3.forward, Vector3.zero);
            if (!plane.Raycast(ray, out var enter))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            return true;
        }

        private bool IsWorldPointInsideGhostRenderers(Vector3 worldPosition)
        {
            var renderers = ghostInstance.GetComponentsInChildren<Renderer>(true);
            for (var i = 0; i < renderers.Length; i++)
            {
                var renderer = renderers[i];
                if (renderer == null || !renderer.enabled)
                {
                    continue;
                }

                var bounds = renderer.bounds;
                if (ghostHitPadding > 0f)
                {
                    bounds.Expand(ghostHitPadding * 2f);
                }

                if (bounds.Contains(worldPosition))
                {
                    return true;
                }
            }

            return false;
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
            if (placementControls == null)
            {
                placementControls = FindFirstObjectByType<GamePanel_BuildingPlacementControls>(FindObjectsInactive.Include);
            }
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
    }
}
