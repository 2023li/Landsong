using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_BuildingPlacementControls : MonoBehaviour
    {
        [SerializeField, InspectorName("控制条根节点")] private RectTransform controlsRoot;
        [SerializeField, InspectorName("确认按钮")] private Button confirmButton;
        [SerializeField, InspectorName("取消按钮")] private Button cancelButton;
        [SerializeField, InspectorName("放置信息文本（可选）")] private TMP_Text placementInfoText;
        [SerializeField, InspectorName("在相机后方时隐藏")] private bool hideWhenBehindCamera = true;

        private Canvas owningCanvas;
        private BuildingPlacementController boundController;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            Hide();
        }

        private void OnDestroy()
        {
            if (boundController != null)
            {
                RemoveButtonListeners(boundController);
                boundController = null;
            }
        }

        public void Bind(BuildingPlacementController controller)
        {
            if (controller == null)
            {
                return;
            }

            ResolveReferences();

            if (boundController != null && boundController != controller)
            {
                RemoveButtonListeners(boundController);
            }

            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(controller.ConfirmPlacement);
                confirmButton.onClick.AddListener(controller.ConfirmPlacement);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(controller.CancelPlacement);
                cancelButton.onClick.AddListener(controller.CancelPlacement);
            }

            boundController = controller;

            if (confirmButton == null || cancelButton == null)
            {
                Debug.LogWarning(
                    "Building placement controls need a confirm button and a cancel button assigned.",
                    this);
            }
        }

        public void Unbind(BuildingPlacementController controller)
        {
            if (controller == null || boundController != controller)
            {
                return;
            }

            RemoveButtonListeners(controller);
            boundController = null;
        }

        public void SetConfirmInteractable(bool interactable)
        {
            ResolveReferences();

            if (confirmButton != null)
            {
                confirmButton.interactable = interactable;
            }
        }

        public void SetPlacementInfo(string text)
        {
            if (placementInfoText == null)
            {
                return;
            }

            placementInfoText.text = string.IsNullOrWhiteSpace(text) ? string.Empty : text;
            placementInfoText.gameObject.SetActive(!string.IsNullOrWhiteSpace(placementInfoText.text));
        }

        public void SetWorldPosition(Camera worldCamera, Vector3 worldPosition)
        {
            ResolveReferences();

            if (controlsRoot == null || worldCamera == null)
            {
                Hide();
                return;
            }

            var screenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            if (hideWhenBehindCamera && screenPosition.z < 0f)
            {
                Hide();
                return;
            }

            var parentRect = GetPositionParent();
            if (parentRect == null)
            {
                controlsRoot.position = screenPosition;
                Show();
                return;
            }

            var uiCamera = GetCanvasEventCamera();
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    (Vector2)screenPosition,
                    uiCamera,
                    out var localPosition))
            {
                Hide();
                return;
            }

            controlsRoot.anchoredPosition = localPosition;
            Show();
        }

        public void Show()
        {
            ResolveReferences();

            if (controlsRoot != null)
            {
                controlsRoot.gameObject.SetActive(true);
            }
        }

        public void Hide()
        {
            ResolveReferences();

            if (controlsRoot != null)
            {
                controlsRoot.gameObject.SetActive(false);
            }
        }

        private void RemoveButtonListeners(BuildingPlacementController controller)
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(controller.ConfirmPlacement);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(controller.CancelPlacement);
            }
        }

        private void ResolveReferences()
        {
            if (controlsRoot == null)
            {
                controlsRoot = transform as RectTransform;
            }

            if (confirmButton == null)
            {
                confirmButton = GetChildButton(0);
            }

            if (cancelButton == null)
            {
                cancelButton = GetChildButton(1);
            }

            if (owningCanvas == null && controlsRoot != null)
            {
                owningCanvas = controlsRoot.GetComponentInParent<Canvas>(true);
            }
        }

        private Button GetChildButton(int childIndex)
        {
            if (controlsRoot == null || controlsRoot.childCount <= childIndex)
            {
                return null;
            }

            var child = controlsRoot.GetChild(childIndex);
            return child.GetComponent<Button>() ?? child.GetComponentInChildren<Button>(true);
        }

        private RectTransform GetPositionParent()
        {
            if (controlsRoot != null && controlsRoot.parent is RectTransform parentRect)
            {
                return parentRect;
            }

            return owningCanvas == null ? null : owningCanvas.transform as RectTransform;
        }

        private Camera GetCanvasEventCamera()
        {
            if (owningCanvas == null || owningCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return owningCanvas.worldCamera;
        }
    }
}
