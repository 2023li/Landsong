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
        private const string MarkerRootName = "BuildingStatusMarkerRoot";

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

        [FoldoutGroup("选填")]
        [SerializeField] private bool showMessageOnMarkerClick = true;

        private readonly Dictionary<BuildingBase, BuildingStatusMarker> activeMarkers = new Dictionary<BuildingBase, BuildingStatusMarker>();
        private readonly List<BuildingStatusMarker> markerPool = new List<BuildingStatusMarker>();
        private readonly HashSet<BuildingBase> subscribedBuildings = new HashSet<BuildingBase>();
        private Landsong.GameSystem gameSystem;
        private BuildingService buildings;
        private Canvas markerCanvas;
        private RectTransform markerRoot;
        private GamePanel_BuildingMessageBar messageBar;
        private bool subscribedToBuildings;
        private bool subscribedToCamera;
        private Coroutine delayedResolveCoroutine;

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeBuildings();
            SubscribeCamera();
            RefreshBuildingSubscriptions();
            RefreshMarkers();
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
            buildings = gameSystem == null ? null : gameSystem.Buildings;

            if (TryGetGamePanelFromUIManager(out var gamePanel))
            {
                markerCanvas = gamePanel.GetComponentInParent<Canvas>(true);
                messageBar = gamePanel.BuildingMessageBar;
            }

            EnsureMarkerRoot();

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }
        }

        private static bool TryGetGamePanelFromUIManager(out UIPanel_Game gamePanel)
        {
            var uiManager = UIManager.Instance;
            if (uiManager != null && uiManager.TryGetActivePanel(out gamePanel))
            {
                return gamePanel != null;
            }

            gamePanel = null;
            return false;
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

            var existingRoot = markerCanvas.transform.Find(MarkerRootName);
            if (existingRoot != null)
            {
                markerRoot = existingRoot as RectTransform;
            }

            if (markerRoot == null)
            {
                var rootObject = new GameObject(MarkerRootName, typeof(RectTransform));
                markerRoot = rootObject.GetComponent<RectTransform>();
                markerRoot.SetParent(markerCanvas.transform, false);
            }

            markerRoot.gameObject.SetActive(true);
            markerRoot.SetAsFirstSibling();
            StretchToParent(markerRoot);
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

            marker.transform.SetParent(markerRoot == null ? transform : markerRoot, false);
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
                marker.transform.SetParent(markerRoot == null ? transform : markerRoot, false);
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

            if (showMessageOnMarkerClick)
            {
                ResolveReferences();
                messageBar?.ShowBuildingMessage(building);
            }
        }

        private static bool CanShowMarker(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private void SubscribeBuildings()
        {
            if (subscribedToBuildings || buildings == null)
            {
                return;
            }

            buildings.BuildingsChanged += HandleBuildingsChanged;
            subscribedToBuildings = true;
        }

        private void UnsubscribeBuildings()
        {
            if (!subscribedToBuildings || buildings == null)
            {
                subscribedToBuildings = false;
                return;
            }

            buildings.BuildingsChanged -= HandleBuildingsChanged;
            subscribedToBuildings = false;
        }

        private void SubscribeCamera()
        {
            if (subscribedToCamera)
            {
                return;
            }

            if (cameraController == null)
            {
                cameraController = FindFirstObjectByType<CameraController>(FindObjectsInactive.Include);
            }

            if (cameraController == null)
            {
                return;
            }

            cameraController.CameraViewChanged += HandleCameraViewChanged;
            subscribedToCamera = true;
        }

        private void UnsubscribeCamera()
        {
            if (!subscribedToCamera || cameraController == null)
            {
                subscribedToCamera = false;
                return;
            }

            cameraController.CameraViewChanged -= HandleCameraViewChanged;
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
            buildings = changedBuildings;
            RefreshBuildingSubscriptions();
            RefreshMarkers();
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
            if (markerCanvas != null && markerRoot != null && messageBar != null)
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
            SubscribeCamera();
            RefreshMarkers();
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
