using System;
using Landsong.AppSystem;
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
        [Header("场景引用")]
        [SerializeField, LabelText("源相机")] private Camera sourceCamera;
        [SerializeField, LabelText("网格地图")] private GridMapBehaviour gridMap;
        [SerializeField, LabelText("输入控制器")] private InputController inputController;
        [SerializeField, LabelText("App 管理器")] private AppManager appManager;
        [SerializeField, LabelText("自动查找场景引用")] private bool autoResolveSceneReferences = true;

        [Header("拖拽平移")]
        [SerializeField, LabelText("允许鼠标拖拽平移")] private bool allowMouseDragPan = true;
        [SerializeField, LabelText("允许触摸拖拽平移")] private bool allowTouchDragPan = true;
        [SerializeField, LabelText("允许键盘平移")] private bool allowKeyboardPan = true;
        [SerializeField, LabelText("键盘平移速度"), Min(0f)] private float keyboardPanSpeed = 12f;
        [SerializeField, LabelText("拖拽启动阈值"), Min(0f)] private float dragStartThresholdPixels = 6f;
        [SerializeField, LabelText("忽略从交互 UI 开始的平移")] private bool ignorePanStartedOverUi = true;
        [SerializeField, LabelText("使用网格平面模式")] private bool useGridPlaneMode = true;
        [SerializeField, LabelText("备用平面模式")] private GridPlaneMode fallbackPlaneMode = GridPlaneMode.IsometricDiamondXY;
        [SerializeField, LabelText("备用平面原点")] private Vector3 fallbackPlaneOrigin = Vector3.zero;

        [Header("缩放")]
        [SerializeField, LabelText("允许鼠标滚轮缩放")] private bool allowMouseWheelZoom = true;
        [SerializeField, LabelText("允许双指缩放")] private bool allowTouchPinchZoom = true;
        [SerializeField, LabelText("忽略从交互 UI 开始的缩放")] private bool ignoreZoomStartedOverUi = true;
        [SerializeField, LabelText("围绕指针缩放")] private bool zoomAroundPointer = true;
        [SerializeField, LabelText("滚轮缩放倍率"), Min(1.001f)] private float mouseWheelZoomStep = 1.15f;
        [SerializeField, LabelText("双指缩放灵敏度"), Min(0.01f)] private float pinchZoomSensitivity = 1f;
        [SerializeField, LabelText("最小正交视野"), Min(0.01f)] private float minOrthographicSize = 2f;
        [SerializeField, LabelText("最大正交视野"), Min(0.01f)] private float maxOrthographicSize = 12f;
        [SerializeField, LabelText("最小透视视野"), Range(1f, 179f)] private float minFieldOfView = 25f;
        [SerializeField, LabelText("最大透视视野"), Range(1f, 179f)] private float maxFieldOfView = 80f;

        [Header("视野边界")]
        [SerializeField, LabelText("限制视野范围")] private bool constrainViewToBounds = true;
        [SerializeField, LabelText("边界最小坐标")] private Vector2 viewBoundsMin = new Vector2(-50f, -50f);
        [SerializeField, LabelText("边界最大坐标")] private Vector2 viewBoundsMax = new Vector2(50f, 50f);
        [SerializeField, LabelText("缩放适配边界")] private bool limitZoomToBounds = true;

        [Header("定位")]
        [SerializeField, LabelText("平滑定位")] private bool smoothFocus = true;
        [SerializeField, LabelText("定位速度"), Min(0.01f)] private float focusLerpSpeed = 8f;
        [SerializeField, LabelText("手动输入取消定位")] private bool cancelFocusOnManualInput = true;

        private bool isPanning;
        private bool hasPanAnchor;
        private bool ignorePanUntilRelease;
        private Vector2 panStartScreenPosition;
        private Vector3 panAnchorWorldPosition;
        private Vector2 previousPanScreenPosition;
        private bool isPinching;
        private float previousPinchDistance;
        private Vector3 pinchAnchorWorldPosition;
        private bool hasFocusTarget;
        private Vector3 focusTargetPosition;
        private Vector3 lastPublishedCameraPosition;
        private Quaternion lastPublishedCameraRotation;
        private float lastPublishedOrthographicSize;
        private float lastPublishedFieldOfView;
        private bool hasPublishedCameraState;

        public static event Action<CameraController> AnyCameraViewChanged;
        public event Action<CameraController> CameraViewChanged;
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
            ClampZoomToViewBounds();
            ClampCameraToViewBounds();
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

            if (UseMobileCameraInput())
            {
                HandleTouchGesture(true);
                UpdateFocus();
                ClampZoomToViewBounds();
                ClampCameraToViewBounds();
                return;
            }

            HandleMouseZoom();
            HandleMousePan();
            HandleKeyboardPan();
            UpdateFocus();
            ClampZoomToViewBounds();
            ClampCameraToViewBounds();
        }

        private void LateUpdate()
        {
            PublishCameraViewChangedIfNeeded();
        }

        private void OnValidate()
        {
            NormalizeViewBounds();
            minOrthographicSize = Mathf.Max(0.01f, minOrthographicSize);
            maxOrthographicSize = Mathf.Max(minOrthographicSize, maxOrthographicSize);
            minFieldOfView = Mathf.Clamp(minFieldOfView, 1f, 179f);
            maxFieldOfView = Mathf.Clamp(Mathf.Max(minFieldOfView, maxFieldOfView), 1f, 179f);
            mouseWheelZoomStep = Mathf.Max(1.001f, mouseWheelZoomStep);
            pinchZoomSensitivity = Mathf.Max(0.01f, pinchZoomSensitivity);
            focusLerpSpeed = Mathf.Max(0.01f, focusLerpSpeed);
            keyboardPanSpeed = Mathf.Max(0f, keyboardPanSpeed);
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

        private bool HandleTouchGesture(bool resetPanWhenNoTouch)
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
                isPinching = false;
                if (resetPanWhenNoTouch)
                {
                    isPanning = false;
                    hasPanAnchor = false;
                    ignorePanUntilRelease = false;
                }
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
            if (!allowMouseDragPan)
            {
                isPanning = false;
                hasPanAnchor = false;
                return;
            }

            if (inputController == null)
            {
                isPanning = false;
                hasPanAnchor = false;
                return;
            }

            if (!inputController.TryGetMousePointerState(out var pointerState))
            {
                isPanning = false;
                hasPanAnchor = false;
                return;
            }

            HandlePointerPan(pointerState);
        }

        private void HandleKeyboardPan()
        {
            if (!allowKeyboardPan || inputController == null)
            {
                return;
            }

            var moveInput = inputController.ReadCameraMove();
            if (moveInput.sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            moveInput = Vector2.ClampMagnitude(moveInput, 1f);
            var cameraTransform = sourceCamera.transform;
            var worldDelta = (moveInput.x * cameraTransform.right
                              + moveInput.y * cameraTransform.up)
                             * (keyboardPanSpeed * Time.unscaledDeltaTime);
            MoveCamera(worldDelta);
            CancelFocusForManualInput();
        }

        private void HandlePointerPan(ScreenPointerState pointerState)
        {
            if (pointerState.WasReleasedThisFrame || !pointerState.IsPressed)
            {
                isPanning = false;
                hasPanAnchor = false;
                ignorePanUntilRelease = false;
                return;
            }

            if (ignorePanUntilRelease)
            {
                return;
            }

            if (pointerState.WasPressedThisFrame)
            {
                if (!CanStartPointerPan(pointerState.ScreenPosition))
                {
                    ignorePanUntilRelease = true;
                    isPanning = false;
                    hasPanAnchor = false;
                    return;
                }

                BeginPointerPanCandidate(pointerState.ScreenPosition);
                return;
            }

            if (!isPanning && !hasPanAnchor)
            {
                if (!CanStartPointerPan(pointerState.ScreenPosition))
                {
                    ignorePanUntilRelease = true;
                    return;
                }

                BeginPointerPanCandidate(pointerState.ScreenPosition);
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

        private bool CanStartPointerPan(Vector2 screenPosition)
        {
            return !ignorePanStartedOverUi || inputController == null || !inputController.IsPointerOverUi(screenPosition);
        }

        private void BeginPointerPanCandidate(Vector2 screenPosition)
        {
            isPanning = true;
            hasPanAnchor = false;
            panStartScreenPosition = screenPosition;
            previousPanScreenPosition = screenPosition;
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

            ClampZoomToViewBounds();

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
                CameraTransform.position = ClampCameraPosition(targetPosition);
                hasFocusTarget = false;
                return;
            }

            focusTargetPosition = ClampCameraPosition(targetPosition);
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
            cameraTransform.position = ClampCameraPosition(Vector3.Lerp(cameraTransform.position, focusTargetPosition, t));

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

            var currentPosition = CameraTransform.position;
            var requestedPosition = currentPosition + constrainedDelta;
            var clampedPosition = ClampCameraPosition(requestedPosition);
            if ((clampedPosition - currentPosition).sqrMagnitude <= Mathf.Epsilon)
            {
                return;
            }

            CameraTransform.position = clampedPosition;
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

        private void ClampZoomToViewBounds()
        {
            if (!constrainViewToBounds || !limitZoomToBounds || sourceCamera == null || !sourceCamera.orthographic)
            {
                return;
            }

            var boundsSize = GetNormalizedViewBoundsSize();
            var maxByHeight = boundsSize.y * 0.5f;
            var maxByWidth = boundsSize.x * 0.5f / Mathf.Max(0.01f, sourceCamera.aspect);
            var maxAllowedSize = Mathf.Max(0.01f, Mathf.Min(maxOrthographicSize, maxByHeight, maxByWidth));
            sourceCamera.orthographicSize = Mathf.Clamp(sourceCamera.orthographicSize, 0.01f, maxAllowedSize);
        }

        private void ClampCameraToViewBounds()
        {
            if (sourceCamera == null)
            {
                return;
            }

            CameraTransform.position = ClampCameraPosition(CameraTransform.position);
        }

        private Vector3 ClampCameraPosition(Vector3 cameraPosition)
        {
            if (!constrainViewToBounds || sourceCamera == null)
            {
                return cameraPosition;
            }

            var min = GetNormalizedViewBoundsMin();
            var max = GetNormalizedViewBoundsMax();
            var center = GetPlaneCenter(cameraPosition);
            var extents = GetViewExtentsOnMovementPlane();

            center.x = ClampCenterAxis(center.x, min.x + extents.x, max.x - extents.x, (min.x + max.x) * 0.5f);
            center.y = ClampCenterAxis(center.y, min.y + extents.y, max.y - extents.y, (min.y + max.y) * 0.5f);

            return SetPlaneCenter(cameraPosition, center);
        }

        private Vector2 GetViewExtentsOnMovementPlane()
        {
            if (sourceCamera == null || !sourceCamera.orthographic)
            {
                return Vector2.zero;
            }

            var yExtent = sourceCamera.orthographicSize;
            var xExtent = yExtent * Mathf.Max(0.01f, sourceCamera.aspect);
            return new Vector2(xExtent, yExtent);
        }

        private Vector2 GetPlaneCenter(Vector3 worldPosition)
        {
            switch (GetMovementPlaneMode())
            {
                case GridPlaneMode.XZ:
                case GridPlaneMode.IsometricDiamondXZ:
                    return new Vector2(worldPosition.x, worldPosition.z);
                case GridPlaneMode.XY:
                case GridPlaneMode.IsometricDiamondXY:
                default:
                    return new Vector2(worldPosition.x, worldPosition.y);
            }
        }

        private Vector3 SetPlaneCenter(Vector3 worldPosition, Vector2 center)
        {
            switch (GetMovementPlaneMode())
            {
                case GridPlaneMode.XZ:
                case GridPlaneMode.IsometricDiamondXZ:
                    return new Vector3(center.x, worldPosition.y, center.y);
                case GridPlaneMode.XY:
                case GridPlaneMode.IsometricDiamondXY:
                default:
                    return new Vector3(center.x, center.y, worldPosition.z);
            }
        }

        private Vector2 GetNormalizedViewBoundsMin()
        {
            return Vector2.Min(viewBoundsMin, viewBoundsMax);
        }

        private Vector2 GetNormalizedViewBoundsMax()
        {
            return Vector2.Max(viewBoundsMin, viewBoundsMax);
        }

        private Vector2 GetNormalizedViewBoundsSize()
        {
            return GetNormalizedViewBoundsMax() - GetNormalizedViewBoundsMin();
        }

        private static float ClampCenterAxis(float value, float min, float max, float fallback)
        {
            return min <= max ? Mathf.Clamp(value, min, max) : fallback;
        }

        private void NormalizeViewBounds()
        {
            var min = Vector2.Min(viewBoundsMin, viewBoundsMax);
            var max = Vector2.Max(viewBoundsMin, viewBoundsMax);
            viewBoundsMin = min;
            viewBoundsMax = max;
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
            ignorePanUntilRelease = false;
            isPinching = false;
        }

        private bool UseMobileCameraInput()
        {
            if (appManager != null)
            {
                return appManager.IsMobilePlatform;
            }

            return Application.isMobilePlatform;
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

            if (appManager == null)
            {
                appManager = FindFirstObjectByType<AppManager>(FindObjectsInactive.Include);
            }
        }

        private void PublishCameraViewChangedIfNeeded()
        {
            if (sourceCamera == null)
            {
                hasPublishedCameraState = false;
                return;
            }

            var cameraTransform = sourceCamera.transform;
            if (!hasPublishedCameraState)
            {
                CapturePublishedCameraState(cameraTransform);
                return;
            }

            var positionChanged = (cameraTransform.position - lastPublishedCameraPosition).sqrMagnitude > 0.000001f;
            var rotationChanged = Quaternion.Angle(cameraTransform.rotation, lastPublishedCameraRotation) > 0.001f;
            var sizeChanged = Mathf.Abs(sourceCamera.orthographicSize - lastPublishedOrthographicSize) > 0.0001f;
            var fieldOfViewChanged = Mathf.Abs(sourceCamera.fieldOfView - lastPublishedFieldOfView) > 0.0001f;
            if (!positionChanged && !rotationChanged && !sizeChanged && !fieldOfViewChanged)
            {
                return;
            }

            CapturePublishedCameraState(cameraTransform);
            CameraViewChanged?.Invoke(this);
            AnyCameraViewChanged?.Invoke(this);
        }

        private void CapturePublishedCameraState(Transform cameraTransform)
        {
            lastPublishedCameraPosition = cameraTransform.position;
            lastPublishedCameraRotation = cameraTransform.rotation;
            lastPublishedOrthographicSize = sourceCamera.orthographicSize;
            lastPublishedFieldOfView = sourceCamera.fieldOfView;
            hasPublishedCameraState = true;
        }
    }
}
