using Landsong.BuildingSystem;
using Landsong.GridSystem;
using Landsong.InputSystem;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Landsong.CameraSystem
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class CameraController : MonoBehaviour
    {
        [Header("Scene")]
        [SerializeField] private Camera sourceCamera;
        [SerializeField] private GridMapBehaviour gridMap;
        [SerializeField] private InputController inputController;
        [SerializeField] private bool autoResolveSceneReferences = true;

        [Header("Pan")]
        [SerializeField] private bool allowMouseDragPan = true;
        [SerializeField] private bool allowTouchDragPan = true;
        [SerializeField, Min(0f)] private float dragStartThresholdPixels = 6f;
        [SerializeField] private bool ignorePanStartedOverUi = true;
        [SerializeField] private bool useGridPlaneMode = true;
        [SerializeField] private GridPlaneMode fallbackPlaneMode = GridPlaneMode.IsometricDiamondXY;
        [SerializeField] private Vector3 fallbackPlaneOrigin = Vector3.zero;

        [Header("Zoom")]
        [SerializeField] private bool allowMouseWheelZoom = true;
        [SerializeField] private bool allowTouchPinchZoom = true;
        [SerializeField] private bool ignoreZoomStartedOverUi = true;
        [SerializeField] private bool zoomAroundPointer = true;
        [SerializeField, Min(1.001f)] private float mouseWheelZoomStep = 1.15f;
        [SerializeField, Min(0.01f)] private float pinchZoomSensitivity = 1f;
        [SerializeField, Min(0.01f)] private float minOrthographicSize = 2f;
        [SerializeField, Min(0.01f)] private float maxOrthographicSize = 12f;
        [SerializeField, Range(1f, 179f)] private float minFieldOfView = 25f;
        [SerializeField, Range(1f, 179f)] private float maxFieldOfView = 80f;

        [Header("Focus")]
        [SerializeField] private bool smoothFocus = true;
        [SerializeField, Min(0.01f)] private float focusLerpSpeed = 8f;
        [SerializeField] private bool cancelFocusOnManualInput = true;

        private bool isPanning;
        private bool hasPanAnchor;
        private Vector2 panStartScreenPosition;
        private Vector3 panAnchorWorldPosition;
        private Vector2 previousPanScreenPosition;
        private bool isPinching;
        private float previousPinchDistance;
        private Vector3 pinchAnchorWorldPosition;
        private bool hasFocusTarget;
        private Vector3 focusTargetPosition;

        public Camera SourceCamera => sourceCamera;
        public bool HasFocusTarget => hasFocusTarget;

        private Transform CameraTransform => sourceCamera == null ? transform : sourceCamera.transform;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InitializeRuntimeCameraControllerBootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            EnsureRuntimeCameraController();
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            EnsureRuntimeCameraController();
        }

        private static void EnsureRuntimeCameraController()
        {
            if (FindFirstObjectByType<CameraController>(FindObjectsInactive.Include) != null)
            {
                return;
            }

            var hasGameCameraContext =
                FindFirstObjectByType<GridMapBehaviour>(FindObjectsInactive.Include) != null
                || FindFirstObjectByType<BuildingPlacementController>(FindObjectsInactive.Include) != null;
            if (!hasGameCameraContext)
            {
                return;
            }

            var mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            mainCamera.gameObject.AddComponent<CameraController>();
        }

        private void Reset()
        {
            sourceCamera = GetComponent<Camera>();
            gridMap = FindFirstObjectByType<GridMapBehaviour>();
        }

        private void Awake()
        {
            ResolveSceneReferences();
        }

        private void Update()
        {
            ResolveSceneReferences();
            if (sourceCamera == null)
            {
                return;
            }

            if (inputController != null && inputController.IsCameraInputBlocked)
            {
                ResetGestureState();
                UpdateFocus();
                return;
            }

            if (HandleTouchGesture())
            {
                UpdateFocus();
                return;
            }

            HandleMouseZoom();
            HandleMousePan();
            UpdateFocus();
        }

        private void OnValidate()
        {
            minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
            maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            minFieldOfView = Mathf.Clamp(minFieldOfView, 1f, 179f);
            maxFieldOfView = Mathf.Clamp(Mathf.Max(minFieldOfView, maxFieldOfView), 1f, 179f);
            mouseWheelZoomStep = Mathf.Max(1.001f, mouseWheelZoomStep);
            pinchZoomSensitivity = Mathf.Max(0.01f, pinchZoomSensitivity);
            focusLerpSpeed = Mathf.Max(0.01f, focusLerpSpeed);
        }

        [Button("定位到建筑")]
        public void FocusOnBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            SetFocusTarget(GetBuildingFocusPosition(building), false);
        }

        public void SnapToBuilding(BuildingBase building)
        {
            if (building == null)
            {
                return;
            }

            SetFocusTarget(GetBuildingFocusPosition(building), true);
        }

        public void FocusOnTransform(Transform target)
        {
            if (target == null)
            {
                return;
            }

            SetFocusTarget(target.position, false);
        }

        public void SnapToTransform(Transform target)
        {
            if (target == null)
            {
                return;
            }

            SetFocusTarget(target.position, true);
        }

        public void FocusOnWorldPosition(Vector3 worldPosition)
        {
            SetFocusTarget(worldPosition, false);
        }

        public void SnapToWorldPosition(Vector3 worldPosition)
        {
            SetFocusTarget(worldPosition, true);
        }

        public void CancelFocus()
        {
            hasFocusTarget = false;
        }

        private bool HandleTouchGesture()
        {
            if (inputController == null)
            {
                return false;
            }

            var touchCount = inputController.ActiveTouchCount;
            if (touchCount >= 2)
            {
                isPanning = false;
                hasPanAnchor = false;
                return HandlePinchGesture();
            }

            if (isPinching)
            {
                isPinching = false;
                return touchCount > 0;
            }

            if (touchCount == 1 && allowTouchDragPan && inputController.TryGetPrimaryPointerState(out var pointerState) && pointerState.Kind == PointerDeviceKind.Touch)
            {
                HandlePointerPan(pointerState);
                return true;
            }

            if (touchCount == 0)
            {
                isPanning = false;
                hasPanAnchor = false;
            }

            return false;
        }

        private bool HandlePinchGesture()
        {
            if (!allowTouchPinchZoom || inputController == null || !inputController.TryGetTwoTouchPositions(out var firstPosition, out var secondPosition))
            {
                isPinching = false;
                return false;
            }

            if (!isPinching)
            {
                if (ignoreZoomStartedOverUi && (inputController.IsPointerOverUi(firstPosition) || inputController.IsPointerOverUi(secondPosition)))
                {
                    return true;
                }

                var midpoint = (firstPosition + secondPosition) * 0.5f;
                if (!TryGetWorldPointOnMovementPlane(midpoint, out pinchAnchorWorldPosition))
                {
                    return true;
                }

                previousPinchDistance = Vector2.Distance(firstPosition, secondPosition);
                isPinching = previousPinchDistance > 0.01f;
                return true;
            }

            var currentDistance = Vector2.Distance(firstPosition, secondPosition);
            if (currentDistance > 0.01f && previousPinchDistance > 0.01f)
            {
                var zoomScale = Mathf.Pow(previousPinchDistance / currentDistance, pinchZoomSensitivity);
                ApplyZoomScale(zoomScale, (firstPosition + secondPosition) * 0.5f);
                previousPinchDistance = currentDistance;
                CancelFocusForManualInput();
            }

            var midpointPosition = (firstPosition + secondPosition) * 0.5f;
            if (TryGetWorldPointOnMovementPlane(midpointPosition, out var currentMidpointWorld))
            {
                MoveCamera(pinchAnchorWorldPosition - currentMidpointWorld);
                CancelFocusForManualInput();
            }

            return true;
        }

        private void HandleMouseZoom()
        {
            if (!allowMouseWheelZoom || inputController == null || !inputController.TryGetScrollDelta(out var scrollDelta))
            {
                return;
            }

            if (!inputController.TryGetMousePointerState(out var pointerState))
            {
                return;
            }

            if (ignoreZoomStartedOverUi && inputController.IsPointerOverUi(pointerState.ScreenPosition))
            {
                return;
            }

            ApplyZoomSteps(scrollDelta.y, pointerState.ScreenPosition);
            CancelFocusForManualInput();
        }

        private void HandleMousePan()
        {
            if (!allowMouseDragPan || inputController == null || !inputController.TryGetPrimaryPointerState(out var pointerState) || pointerState.Kind != PointerDeviceKind.Mouse)
            {
                isPanning = false;
                hasPanAnchor = false;
                return;
            }

            HandlePointerPan(pointerState);
        }

        private void HandlePointerPan(ScreenPointerState pointerState)
        {
            if (pointerState.WasReleasedThisFrame || !pointerState.IsPressed)
            {
                isPanning = false;
                hasPanAnchor = false;
                return;
            }

            if (pointerState.WasPressedThisFrame)
            {
                isPanning = !ignorePanStartedOverUi || inputController == null || !inputController.IsPointerOverUi(pointerState.ScreenPosition);
                hasPanAnchor = false;
                panStartScreenPosition = pointerState.ScreenPosition;
                previousPanScreenPosition = pointerState.ScreenPosition;
                return;
            }

            if (!isPanning && !hasPanAnchor)
            {
                isPanning = !ignorePanStartedOverUi || inputController == null || !inputController.IsPointerOverUi(pointerState.ScreenPosition);
                panStartScreenPosition = pointerState.ScreenPosition;
                previousPanScreenPosition = pointerState.ScreenPosition;
                return;
            }

            if (!isPanning)
            {
                return;
            }

            if (!hasPanAnchor)
            {
                if ((pointerState.ScreenPosition - panStartScreenPosition).sqrMagnitude < dragStartThresholdPixels * dragStartThresholdPixels)
                {
                    return;
                }

                if (!TryGetWorldPointOnMovementPlane(pointerState.ScreenPosition, out panAnchorWorldPosition))
                {
                    isPanning = false;
                    return;
                }

                hasPanAnchor = true;
                previousPanScreenPosition = pointerState.ScreenPosition;
                CancelFocusForManualInput();
                return;
            }

            if (TryGetWorldPointOnMovementPlane(pointerState.ScreenPosition, out var currentWorldPosition))
            {
                MoveCamera(panAnchorWorldPosition - currentWorldPosition);
            }
            else
            {
                MoveCamera(GetPanDeltaFromScreenDelta(pointerState.ScreenPosition - previousPanScreenPosition));
            }

            previousPanScreenPosition = pointerState.ScreenPosition;
            CancelFocusForManualInput();
        }

        private void ApplyZoomSteps(float zoomSteps, Vector2 screenFocus)
        {
            if (Mathf.Approximately(zoomSteps, 0f))
            {
                return;
            }

            var scale = Mathf.Pow(mouseWheelZoomStep, -zoomSteps);
            ApplyZoomScale(scale, screenFocus);
        }

        private void ApplyZoomScale(float zoomScale, Vector2 screenFocus)
        {
            if (zoomScale <= 0f || Mathf.Approximately(zoomScale, 1f))
            {
                return;
            }

            var beforeZoomWorldPosition = default(Vector3);
            var hasFocusWorldPosition = zoomAroundPointer && TryGetWorldPointOnMovementPlane(screenFocus, out beforeZoomWorldPosition);

            if (sourceCamera.orthographic)
            {
                sourceCamera.orthographicSize = Mathf.Clamp(sourceCamera.orthographicSize * zoomScale, minOrthographicSize, maxOrthographicSize);
            }
            else
            {
                sourceCamera.fieldOfView = Mathf.Clamp(sourceCamera.fieldOfView * zoomScale, minFieldOfView, maxFieldOfView);
            }

            if (!hasFocusWorldPosition || !TryGetWorldPointOnMovementPlane(screenFocus, out var afterZoomWorldPosition))
            {
                return;
            }

            MoveCamera(beforeZoomWorldPosition - afterZoomWorldPosition);
        }

        private void SetFocusTarget(Vector3 worldPosition, bool snap)
        {
            var targetPosition = GetCameraPositionForFocus(worldPosition);
            if (snap || !smoothFocus)
            {
                CameraTransform.position = targetPosition;
                hasFocusTarget = false;
                return;
            }

            focusTargetPosition = targetPosition;
            hasFocusTarget = true;
        }

        private void UpdateFocus()
        {
            if (!hasFocusTarget)
            {
                return;
            }

            var cameraTransform = CameraTransform;
            var t = 1f - Mathf.Exp(-focusLerpSpeed * Time.unscaledDeltaTime);
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, focusTargetPosition, t);

            if ((cameraTransform.position - focusTargetPosition).sqrMagnitude <= 0.0001f)
            {
                cameraTransform.position = focusTargetPosition;
                hasFocusTarget = false;
            }
        }

        private void MoveCamera(Vector3 worldDelta)
        {
            var constrainedDelta = ConstrainDeltaToMovementPlane(worldDelta);
            if (constrainedDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            CameraTransform.position += constrainedDelta;
        }

        private Vector3 GetCameraPositionForFocus(Vector3 worldPosition)
        {
            var cameraPosition = CameraTransform.position;
            switch (GetMovementPlaneMode())
            {
                case GridPlaneMode.XZ:
                case GridPlaneMode.IsometricDiamondXZ:
                    return new Vector3(worldPosition.x, cameraPosition.y, worldPosition.z);
                case GridPlaneMode.XY:
                case GridPlaneMode.IsometricDiamondXY:
                default:
                    return new Vector3(worldPosition.x, worldPosition.y, cameraPosition.z);
            }
        }

        private Vector3 ConstrainDeltaToMovementPlane(Vector3 delta)
        {
            switch (GetMovementPlaneMode())
            {
                case GridPlaneMode.XZ:
                case GridPlaneMode.IsometricDiamondXZ:
                    return new Vector3(delta.x, 0f, delta.z);
                case GridPlaneMode.XY:
                case GridPlaneMode.IsometricDiamondXY:
                default:
                    return new Vector3(delta.x, delta.y, 0f);
            }
        }

        private Vector3 GetPanDeltaFromScreenDelta(Vector2 screenDelta)
        {
            if (sourceCamera == null || screenDelta.sqrMagnitude <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            var pixelHeight = Mathf.Max(1, sourceCamera.pixelHeight);
            var unitsPerPixel = sourceCamera.orthographic
                ? sourceCamera.orthographicSize * 2f / pixelHeight
                : Mathf.Max(0.01f, sourceCamera.transform.position.magnitude) * Mathf.Tan(sourceCamera.fieldOfView * Mathf.Deg2Rad * 0.5f) * 2f / pixelHeight;

            var cameraTransform = sourceCamera.transform;
            var worldDelta = (-screenDelta.x * unitsPerPixel * cameraTransform.right)
                             + (-screenDelta.y * unitsPerPixel * cameraTransform.up);
            return worldDelta;
        }

        private bool TryGetWorldPointOnMovementPlane(Vector2 screenPosition, out Vector3 worldPosition)
        {
            var ray = sourceCamera.ScreenPointToRay(screenPosition);
            var planeOrigin = gridMap == null ? fallbackPlaneOrigin : gridMap.WorldOrigin;
            var plane = GetMovementPlaneMode() == GridPlaneMode.XZ || GetMovementPlaneMode() == GridPlaneMode.IsometricDiamondXZ
                ? new UnityEngine.Plane(Vector3.up, planeOrigin)
                : new UnityEngine.Plane(Vector3.forward, planeOrigin);

            if (!plane.Raycast(ray, out var enter))
            {
                worldPosition = default;
                return false;
            }

            worldPosition = ray.GetPoint(enter);
            return true;
        }

        private GridPlaneMode GetMovementPlaneMode()
        {
            return useGridPlaneMode && gridMap != null ? gridMap.PlaneMode : fallbackPlaneMode;
        }

        private static Vector3 GetBuildingFocusPosition(BuildingBase building)
        {
            if (building == null)
            {
                return Vector3.zero;
            }

            if (building.HasPlacement && building.GridMap != null && building.Definition != null)
            {
                return building.GridMap.GetFootprintCenter(building.Origin, building.Definition.Size);
            }

            return building.transform.position;
        }

        private void CancelFocusForManualInput()
        {
            if (cancelFocusOnManualInput)
            {
                hasFocusTarget = false;
            }
        }

        private void ResetGestureState()
        {
            isPanning = false;
            hasPanAnchor = false;
            isPinching = false;
        }

        private void ResolveSceneReferences()
        {
            if (!autoResolveSceneReferences)
            {
                return;
            }

            if (sourceCamera == null)
            {
                sourceCamera = GetComponent<Camera>();
            }

            if (sourceCamera == null)
            {
                sourceCamera = Camera.main;
            }

            if (gridMap == null)
            {
                gridMap = FindFirstObjectByType<GridMapBehaviour>();
            }

            if (inputController == null)
            {
                inputController = InputController.Instance;
            }
        }
    }
}
