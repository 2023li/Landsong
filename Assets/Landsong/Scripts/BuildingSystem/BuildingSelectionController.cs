using System;
using System.Collections;
using System.Collections.Generic;
using Landsong.CameraSystem;
using Landsong.GridSystem;
using Landsong.UISystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingSelectionController : MonoBehaviour
    {
        private const string DetailMarkerRootName = "BuildingDetailMarkerRoot";

        [SerializeField, Required] private GamePanel_BuildingDetailMarker detailMarkerPrefab;

        [FoldoutGroup("选填")]
        [SerializeField] private TileBase selectionHighlightTile;

        [FoldoutGroup("选填")]
        [SerializeField] private Vector3 detailMarkerWorldOffset = new Vector3(0f, -0.75f, 0f);

        [FoldoutGroup("选填")]
        [SerializeField] private Vector2 detailMarkerScreenOffset;

        [FoldoutGroup("选填")]
        [SerializeField] private bool hideMarkerWhenOffscreen = true;

        [FoldoutGroup("选填")]
        [SerializeField] private CameraController cameraController;

        private readonly HashSet<BuildingBase> subscribedBuildings = new HashSet<BuildingBase>();
        private readonly List<Vector3Int> highlightedCells = new List<Vector3Int>();
        private Landsong.GameSystem gameSystem;
        private BuildingService buildings;
        private BuildingService subscribedBuildingService;
        private Canvas markerCanvas;
        private RectTransform markerRoot;
        private GamePanel_BuildingDetailMarker activeDetailMarker;
        private Tilemap activeHighlightTilemap;
        private BuildingBase selectedBuilding;
        private CameraController subscribedCameraController;
        private bool subscribedToBuildings;
        private bool subscribedToCamera;
        private Coroutine delayedResolveCoroutine;

        public event Action<BuildingSelectionController, BuildingBase> SelectionChanged;
        public event Action<BuildingSelectionController, BuildingBase> SelectedBuildingStateChanged;
        public event Action<BuildingSelectionController, BuildingBase> DetailRequested;

        public BuildingBase SelectedBuilding => selectedBuilding;

        private void Reset()
        {
            detailMarkerPrefab = GetComponentInChildren<GamePanel_BuildingDetailMarker>(true);
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

        private void OnDisable()
        {
            StopDelayedResolve();
            BuildingBase previousSelectedBuilding = selectedBuilding;
            selectedBuilding = null;
            ClearSelectionVisuals();
            UnsubscribeCamera();
            UnsubscribeBuildings();
            UnsubscribeBuildingEvents();
            UnregisterSelf();

            if (previousSelectedBuilding != null)
            {
                SelectionChanged?.Invoke(this, null);
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
            RefreshSelectionVisuals();
            SelectionChanged?.Invoke(this, selectedBuilding);
        }

        public void ClearSelection()
        {
            if (selectedBuilding == null)
            {
                ClearSelectionVisuals();
                return;
            }

            selectedBuilding = null;
            ClearSelectionVisuals();
            SelectionChanged?.Invoke(this, null);
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
            SelectedBuildingStateChanged?.Invoke(this, selectedBuilding);
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

            DetailRequested?.Invoke(this, selectedBuilding);
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            buildings = gameSystem == null ? null : gameSystem.Buildings;

            if (TryGetGamePanelFromUIManager(out UIPanel_Game gamePanel))
            {
                markerCanvas = gamePanel.GetComponentInParent<Canvas>(true);
            }

            EnsureMarkerRoot();

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

        private void ShowDetailMarker()
        {
            BuildingBase building = selectedBuilding;
            if (!CanSelectBuilding(building) || detailMarkerPrefab == null)
            {
                ClearDetailMarker();
                return;
            }

            ResolveReferences();
            if (markerRoot == null)
            {
                ClearDetailMarker();
                return;
            }

            if (activeDetailMarker == null)
            {
                activeDetailMarker = Instantiate(detailMarkerPrefab, markerRoot);
            }

            activeDetailMarker.gameObject.SetActive(true);
            activeDetailMarker.transform.SetParent(markerRoot, false);
            activeDetailMarker.Bind(building, HandleDetailMarkerClicked);
            UpdateDetailMarkerPosition();
        }

        private void ClearDetailMarker()
        {
            if (activeDetailMarker == null)
            {
                return;
            }

            activeDetailMarker.Unbind();
            activeDetailMarker.gameObject.SetActive(false);
        }

        private void RefreshSelectionVisuals()
        {
            if (!CanSelectBuilding(selectedBuilding))
            {
                ClearSelectionVisuals();
                return;
            }

            HighlightSelectedFootprint();
            ShowDetailMarker();
        }

        private void ClearSelectionVisuals()
        {
            ClearDetailMarker();
            ClearSelectionHighlight();
        }

        private void UpdateDetailMarkerPosition()
        {
            if (activeDetailMarker == null)
            {
                return;
            }

            RectTransform markerRect = activeDetailMarker.transform as RectTransform;
            if (markerRect == null || !TryGetDetailMarkerAnchoredPosition(out Vector2 anchoredPosition))
            {
                activeDetailMarker.gameObject.SetActive(false);
                return;
            }

            activeDetailMarker.gameObject.SetActive(true);
            markerRect.anchoredPosition = anchoredPosition;
        }

        private bool TryGetDetailMarkerAnchoredPosition(out Vector2 anchoredPosition)
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

            Vector3 screenPosition = sourceCamera.WorldToScreenPoint(GetDetailMarkerWorldPosition(selectedBuilding));
            if (screenPosition.z < 0f)
            {
                return false;
            }

            if (hideMarkerWhenOffscreen && !IsScreenPositionInsideCamera(sourceCamera, screenPosition))
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

            anchoredPosition = localPosition + detailMarkerScreenOffset;
            return true;
        }

        private Vector3 GetDetailMarkerWorldPosition(BuildingBase building)
        {
            if (building.HasPlacement && building.GridMap != null && building.Definition != null)
            {
                return building.GridMap.GetFootprintCenter(building.Origin, building.Definition.Size)
                       + detailMarkerWorldOffset;
            }

            return building.transform.position + detailMarkerWorldOffset;
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

        private void EnsureMarkerRoot()
        {
            if (markerCanvas == null)
            {
                markerRoot = null;
                return;
            }

            if (markerRoot != null && markerRoot.parent == markerCanvas.transform)
            {
                markerRoot.SetAsFirstSibling();
                return;
            }

            Transform existingRoot = markerCanvas.transform.Find(DetailMarkerRootName);
            if (existingRoot != null)
            {
                markerRoot = existingRoot as RectTransform;
            }

            if (markerRoot == null)
            {
                GameObject rootObject = new GameObject(DetailMarkerRootName, typeof(RectTransform));
                markerRoot = rootObject.GetComponent<RectTransform>();
                markerRoot.SetParent(markerCanvas.transform, false);
            }

            markerRoot.gameObject.SetActive(true);
            markerRoot.SetAsFirstSibling();
            StretchToParent(markerRoot);
        }

        private void HandleDetailMarkerClicked(BuildingBase building)
        {
            RequestBuildingDetail(building);
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
            SelectedBuildingStateChanged?.Invoke(this, selectedBuilding);
        }

        private void HandleCameraViewChanged(CameraController changedCameraController)
        {
            UpdateDetailMarkerPosition();
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
            if (uiManager != null && uiManager.TryGetActivePanel(out gamePanel))
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

        private static void StretchToParent(RectTransform rectTransform)
        {
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.offsetMin = Vector2.zero;
            rectTransform.offsetMax = Vector2.zero;
            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
