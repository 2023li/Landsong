using System;
using System.Collections.Generic;
using Moyo.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Landsong.InputSystem
{
    public enum PointerDeviceKind
    {
        None = 0,
        Mouse = 1,
        Touch = 2
    }

    public readonly struct ScreenPointerState
    {
        public ScreenPointerState(
            PointerDeviceKind kind,
            Vector2 screenPosition,
            bool isPressed,
            bool wasPressedThisFrame,
            bool wasReleasedThisFrame)
        {
            Kind = kind;
            ScreenPosition = screenPosition;
            IsPressed = isPressed;
            WasPressedThisFrame = wasPressedThisFrame;
            WasReleasedThisFrame = wasReleasedThisFrame;
        }

        public PointerDeviceKind Kind { get; }
        public Vector2 ScreenPosition { get; }
        public bool IsPressed { get; }
        public bool WasPressedThisFrame { get; }
        public bool WasReleasedThisFrame { get; }
    }

    [DisallowMultipleComponent]
    public sealed class InputController : MonoSingleton<InputController>
    {
        private readonly HashSet<object> cameraInputBlockers = new HashSet<object>();
        private readonly List<object> staleBlockers = new List<object>();
        private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();
        private PointerEventData uiPointerEventData;
        private global::LSActionMap actionMap;
        private bool subscribedToGamePlayActions;

        public bool IsCameraInputBlocked => cameraInputBlockers.Count > 0;
        public bool CanUseCameraInput => !IsCameraInputBlocked;
        public int ActiveTouchCount => CountActiveTouches();

        public event Action OpenBuildingPanelRequested;
        public event Action OpenInventoryPanelRequested;
        public event Action BackRequested;

        private void OnEnable()
        {
            EnableGamePlayActions();
        }

        private void OnDisable()
        {
            UnsubscribeGamePlayActions();
            actionMap?.Disable();
        }

        private void OnDestroy()
        {
            UnsubscribeGamePlayActions();
            actionMap?.Disable();
            actionMap?.Dispose();
            actionMap = null;
        }

        public bool InputAnyKeyDown()
        {
            foreach (var device in global::UnityEngine.InputSystem.InputSystem.devices)
            {
                if (device == null || !device.enabled)
                {
                    continue;
                }

                foreach (var control in device.allControls)
                {
                    if (control is ButtonControl button && button.wasPressedThisFrame)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void LateUpdate()
        {
            RemoveDestroyedUnityObjectBlockers(cameraInputBlockers);
        }

        public void SetCameraInputBlocked(object owner, bool blocked)
        {
            if (owner == null)
            {
                return;
            }

            if (blocked)
            {
                cameraInputBlockers.Add(owner);
                return;
            }

            cameraInputBlockers.Remove(owner);
        }

        public Vector2 ReadCameraMove()
        {
            EnsureActionMap();
            return actionMap == null ? Vector2.zero : actionMap.GamePlay.Move.ReadValue<Vector2>();
        }

        public bool TryGetPrimaryPointerState(out ScreenPointerState pointerState)
        {
            var touchscreen = Touchscreen.current;
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

            var mouse = Mouse.current;
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

            pointerState = default;
            return false;
        }

        public bool TryGetMousePointerState(out ScreenPointerState pointerState)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                pointerState = default;
                return false;
            }

            pointerState = new ScreenPointerState(
                PointerDeviceKind.Mouse,
                mouse.position.ReadValue(),
                mouse.leftButton.isPressed,
                mouse.leftButton.wasPressedThisFrame,
                mouse.leftButton.wasReleasedThisFrame);
            return true;
        }

        public bool TryGetTwoTouchPositions(out Vector2 firstPosition, out Vector2 secondPosition)
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                firstPosition = default;
                secondPosition = default;
                return false;
            }

            var found = 0;
            firstPosition = default;
            secondPosition = default;
            foreach (var touch in touchscreen.touches)
            {
                if (!touch.press.isPressed)
                {
                    continue;
                }

                if (found == 0)
                {
                    firstPosition = touch.position.ReadValue();
                }
                else
                {
                    secondPosition = touch.position.ReadValue();
                    return true;
                }

                found++;
            }

            return false;
        }

        public bool TryGetScrollDelta(out Vector2 scrollDelta)
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                scrollDelta = default;
                return false;
            }

            scrollDelta = mouse.scroll.ReadValue();
            if (Mathf.Abs(scrollDelta.x) > 10f || Mathf.Abs(scrollDelta.y) > 10f)
            {
                scrollDelta *= 1f / 120f;
            }

            return scrollDelta.sqrMagnitude > Mathf.Epsilon;
        }

        public bool IsPointerOverUi(Vector2 screenPosition)
        {
            EnsureEventSystemExists();
            if (EventSystem.current == null)
            {
                return false;
            }

            uiPointerEventData ??= new PointerEventData(EventSystem.current);
            uiPointerEventData.Reset();
            uiPointerEventData.position = screenPosition;
            uiRaycastResults.Clear();
            EventSystem.current.RaycastAll(uiPointerEventData, uiRaycastResults);

            for (var i = 0; i < uiRaycastResults.Count; i++)
            {
                var result = uiRaycastResults[i];
                if (IsUiRaycastResult(result) && IsInteractiveUi(result.gameObject))
                {
                    return true;
                }
            }

            return false;
        }

        public void EnsureEventSystemExists()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
            eventSystemObject.AddComponent<InputSystemUIInputModule>();
        }

        private static int CountActiveTouches()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                return 0;
            }

            var count = 0;
            foreach (var touch in touchscreen.touches)
            {
                if (touch.press.isPressed)
                {
                    count++;
                }
            }

            return count;
        }

        private void EnableGamePlayActions()
        {
            EnsureActionMap();
            SubscribeGamePlayActions();
            actionMap?.GamePlay.Enable();
        }

        private void EnsureActionMap()
        {
            actionMap ??= new global::LSActionMap();
        }

        private void SubscribeGamePlayActions()
        {
            if (actionMap == null || subscribedToGamePlayActions)
            {
                return;
            }

            actionMap.GamePlay.OpenBuildingPanel.performed += HandleOpenBuildingPanelPerformed;
            actionMap.GamePlay.OpenInventoryPanel.performed += HandleOpenInventoryPanelPerformed;
            actionMap.GamePlay.Back.performed += HandleBackPerformed;
            subscribedToGamePlayActions = true;
        }

        private void UnsubscribeGamePlayActions()
        {
            if (actionMap == null || !subscribedToGamePlayActions)
            {
                return;
            }

            actionMap.GamePlay.OpenBuildingPanel.performed -= HandleOpenBuildingPanelPerformed;
            actionMap.GamePlay.OpenInventoryPanel.performed -= HandleOpenInventoryPanelPerformed;
            actionMap.GamePlay.Back.performed -= HandleBackPerformed;
            subscribedToGamePlayActions = false;
        }

        private void HandleOpenBuildingPanelPerformed(InputAction.CallbackContext context)
        {
            OpenBuildingPanelRequested?.Invoke();
        }

        private void HandleOpenInventoryPanelPerformed(InputAction.CallbackContext context)
        {
            OpenInventoryPanelRequested?.Invoke();
        }

        private void HandleBackPerformed(InputAction.CallbackContext context)
        {
            BackRequested?.Invoke();
        }

        private static bool IsUiRaycastResult(RaycastResult result)
        {
            return result.module is GraphicRaycaster;
        }

        private static bool IsInteractiveUi(GameObject target)
        {
            if (target == null)
            {
                return false;
            }

            var components = target.GetComponentsInParent<Component>(true);
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (component is Selectable selectable)
                {
                    if (IsVisibleSelectable(selectable))
                    {
                        return true;
                    }

                    continue;
                }

                if (component is IBeginDragHandler
                    || component is IDragHandler
                    || component is IEndDragHandler
                    || component is IScrollHandler
                    || component is IPointerClickHandler
                    || component is IPointerDownHandler
                    || component is IPointerUpHandler)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsVisibleSelectable(Selectable selectable)
        {
            if (selectable == null || !selectable.isActiveAndEnabled || !selectable.IsInteractable())
            {
                return false;
            }

            var targetGraphic = selectable.targetGraphic;
            if (targetGraphic == null)
            {
                return true;
            }

            return targetGraphic.isActiveAndEnabled
                   && targetGraphic.raycastTarget
                   && targetGraphic.color.a > 0.01f
                   && targetGraphic.canvasRenderer.GetAlpha() > 0.01f;
        }

        private void RemoveDestroyedUnityObjectBlockers(HashSet<object> blockers)
        {
            staleBlockers.Clear();

            foreach (var blocker in blockers)
            {
                if (blocker is UnityEngine.Object unityObject && unityObject == null)
                {
                    staleBlockers.Add(blocker);
                }
            }

            for (var i = 0; i < staleBlockers.Count; i++)
            {
                blockers.Remove(staleBlockers[i]);
            }
        }
    }
}
