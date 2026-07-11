using System.Collections;
using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.CameraSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class BuildingStatusMarkerManager : MonoBehaviour
    {
        [SerializeField, Required] private BuildingStatusMarker markerPrefab;

        [FoldoutGroup("选填")]
        [SerializeField] private Vector3 markerWorldOffset = new Vector3(0f, 1.5f, 0f);

        [FoldoutGroup("选填")]
        [SerializeField] private Vector2 markerScreenOffset;

        [FoldoutGroup("选填")]
        [SerializeField] private bool hideWhenOffscreen = true;

        [FoldoutGroup("选填")]
        [SerializeField] private CameraController cameraController;

        [FoldoutGroup("选填")]
        [SerializeField] private bool focusBuildingOnMarkerClick = true;

        private readonly Dictionary<BuildingBase, BuildingStatusMarker> activeMarkers = new Dictionary<BuildingBase, BuildingStatusMarker>();
        private readonly List<BuildingStatusMarker> markerPool = new List<BuildingStatusMarker>();
        private readonly HashSet<BuildingBase> subscribedBuildings = new HashSet<BuildingBase>();
        private Landsong.GameSystem gameSystem;
        private BuildingService buildings;
        private BuildingService subscribedBuildingService;
        private Canvas markerCanvas;
        private RectTransform markerRoot;
        private CameraController subscribedCameraController;
        private bool subscribedToBuildings;
        private bool subscribedToCamera;
        private Coroutine delayedResolveCoroutine;

        private void OnEnable()
        {
            RefreshRuntimeBindingsAndMarkers();
        }

        private void RefreshRuntimeBindingsAndMarkers()
        {
            ResolveReferences();
            SubscribeBuildings();
            SubscribeCamera();
            RefreshBuildingSubscriptions();
            RefreshMarkersFromCurrentReferences();
            QueueDelayedResolveIfNeeded();
        }

        private void OnDisable()
        {
            StopDelayedResolve();
            UnsubscribeCamera();
            UnsubscribeBuildings();
            UnsubscribeBuildingStates();
            ReleaseAllMarkers();
        }

        public void RefreshMarkers()
        {
            ResolveReferences();
            RefreshMarkersFromCurrentReferences();
            QueueDelayedResolveIfNeeded();
        }

        private void RefreshMarkersFromCurrentReferences()
        {
            ReleaseAllMarkers();

            if (markerPrefab == null || buildings == null || markerRoot == null)
            {
                return;
            }

            var source = buildings.Buildings;
            for (var i = 0; i < source.Count; i++)
            {
                var building = source[i];
                if (!CanShowMarker(building))
                {
                    continue;
                }

                var data = BuildingStatusUIFormatter.CreateDisplayData(building);
                if (!data.HasAbnormalStatus)
                {
                    continue;
                }

                var marker = GetMarkerFromPool();
                marker.Bind(building, data, HandleMarkerClicked);
                activeMarkers.Add(building, marker);
            }

            UpdateMarkerPositions();
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            SetBuildingService(gameSystem == null ? null : gameSystem.Services.Buildings);

            if (TryGetGamePanelFromUIManager(out var gamePanel))
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

        private static bool TryGetGamePanelFromUIManager(out UIPanel_Game gamePanel)
        {
            var uiManager = UIManager.Instance;
            if (uiManager != null && uiManager.TryGetActivePanel<UIPanel_Game>(out gamePanel))
            {
                return gamePanel != null;
            }

            gamePanel = null;
            return false;
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

        private void SetBuildingService(BuildingService resolvedBuildings)
        {
            if (buildings == resolvedBuildings)
            {
                return;
            }

            UnsubscribeBuildings();
            UnsubscribeBuildingStates();
            buildings = resolvedBuildings;
        }

        private BuildingStatusMarker GetMarkerFromPool()
        {
            BuildingStatusMarker marker;
            var lastIndex = markerPool.Count - 1;
            if (lastIndex >= 0)
            {
                marker = markerPool[lastIndex];
                markerPool.RemoveAt(lastIndex);
            }
            else
            {
                marker = Instantiate(markerPrefab);
            }

            marker.transform.SetParent(markerRoot, false);
            marker.gameObject.SetActive(true);
            return marker;
        }

        private void ReleaseAllMarkers()
        {
            foreach (var pair in activeMarkers)
            {
                var marker = pair.Value;
                if (marker == null)
                {
                    continue;
                }

                marker.Unbind();
                marker.gameObject.SetActive(false);
                if (markerRoot != null)
                {
                    marker.transform.SetParent(markerRoot, false);
                }
                markerPool.Add(marker);
            }

            activeMarkers.Clear();
        }

        private void UpdateMarkerPositions()
        {
            foreach (var pair in activeMarkers)
            {
                if (pair.Key == null || pair.Value == null)
                {
                    continue;
                }

                UpdateMarkerPosition(pair.Key, pair.Value);
            }
        }

        private void UpdateMarkerPosition(BuildingBase building, BuildingStatusMarker marker)
        {
            if (markerRoot == null || marker == null)
            {
                return;
            }

            var markerRect = marker.transform as RectTransform;
            if (markerRect == null || !TryGetMarkerAnchoredPosition(building, out var anchoredPosition))
            {
                marker.gameObject.SetActive(false);
                return;
            }

            marker.gameObject.SetActive(true);
            markerRect.anchoredPosition = anchoredPosition;
        }

        private bool TryGetMarkerAnchoredPosition(BuildingBase building, out Vector2 anchoredPosition)
        {
            anchoredPosition = default;

            var sourceCamera = cameraController == null ? Camera.main : cameraController.SourceCamera;
            if (sourceCamera == null || markerRoot == null)
            {
                return false;
            }

            var screenPosition = sourceCamera.WorldToScreenPoint(GetMarkerWorldPosition(building));
            if (screenPosition.z < 0f)
            {
                return false;
            }

            if (hideWhenOffscreen && !IsScreenPositionInsideCamera(sourceCamera, screenPosition))
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    markerRoot,
                    screenPosition,
                    GetUiCamera(),
                    out var localPosition))
            {
                return false;
            }

            anchoredPosition = localPosition + markerScreenOffset;
            return true;
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

        private static bool IsScreenPositionInsideCamera(Camera sourceCamera, Vector3 screenPosition)
        {
            var pixelRect = sourceCamera.pixelRect;
            return screenPosition.x >= pixelRect.xMin
                   && screenPosition.x <= pixelRect.xMax
                   && screenPosition.y >= pixelRect.yMin
                   && screenPosition.y <= pixelRect.yMax;
        }

        private Vector3 GetMarkerWorldPosition(BuildingBase building)
        {
            if (building == null)
            {
                return markerWorldOffset;
            }

            if (building.HasPlacement && building.GridMap != null && building.Definition != null)
            {
                return building.GridMap.GetFootprintCenter(building.Origin, building.Definition.Size) + markerWorldOffset;
            }

            return building.transform.position + markerWorldOffset;
        }

        private void HandleMarkerClicked(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            if (focusBuildingOnMarkerClick)
            {
                if (cameraController == null)
                {
                    cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
                    SubscribeCamera();
                }

                cameraController?.FocusOnBuilding(building);
            }

        }

        private static bool CanShowMarker(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
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

        private void RefreshBuildingSubscriptions()
        {
            UnsubscribeBuildingStates();
            if (buildings == null)
            {
                return;
            }

            var source = buildings.Buildings;
            for (var i = 0; i < source.Count; i++)
            {
                var building = source[i];
                if (building == null || !subscribedBuildings.Add(building))
                {
                    continue;
                }

                building.StateChanged += HandleBuildingStateChanged;
            }
        }

        private void UnsubscribeBuildingStates()
        {
            foreach (var building in subscribedBuildings)
            {
                if (building != null)
                {
                    building.StateChanged -= HandleBuildingStateChanged;
                }
            }

            subscribedBuildings.Clear();
        }

        private void HandleBuildingsChanged(BuildingService changedBuildings)
        {
            SetBuildingService(changedBuildings);
            SubscribeBuildings();
            RefreshBuildingSubscriptions();
            RefreshMarkersFromCurrentReferences();
            QueueDelayedResolveIfNeeded();
        }

        private void HandleBuildingStateChanged(BuildingBase changedBuilding)
        {
            RefreshMarkers();
        }

        private void HandleCameraViewChanged(CameraController changedCameraController)
        {
            UpdateMarkerPositions();
        }

        private void QueueDelayedResolveIfNeeded()
        {
            if (!NeedsDelayedResolve())
            {
                return;
            }

            if (delayedResolveCoroutine == null && isActiveAndEnabled)
            {
                delayedResolveCoroutine = StartCoroutine(ResolveReferencesUntilReady());
            }
        }

        private bool NeedsDelayedResolve()
        {
            return gameSystem == null
                   || buildings == null
                   || markerCanvas == null
                   || markerRoot == null
                   || !HasPositionCamera()
                   || !subscribedToBuildings
                   || (cameraController != null && !subscribedToCamera);
        }

        private bool HasPositionCamera()
        {
            var sourceCamera = cameraController == null ? Camera.main : cameraController.SourceCamera;
            return sourceCamera != null;
        }

        private IEnumerator ResolveReferencesUntilReady()
        {
            while (isActiveAndEnabled && NeedsDelayedResolve())
            {
                yield return null;
                RefreshRuntimeBindingsAndMarkers();
            }

            delayedResolveCoroutine = null;
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

    }
}
