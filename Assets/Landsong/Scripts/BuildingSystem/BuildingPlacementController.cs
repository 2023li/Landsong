using System.Collections;
using System.Collections.Generic;
using Landsong.GridSystem;
using Landsong.InputSystem;
using Landsong.InventorySystem;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
        [SerializeField] private Vector2 controlsSize = new Vector2(220f, 72f);
        [SerializeField, Min(0.001f)] private float controlsWorldScale = 0.01f;
        [SerializeField, Min(0f)] private float ghostHitPadding = 0.05f;
        [SerializeField] private bool cancelPlacementOnDisable = true;

        [Header("Grid Highlight")]
        [SerializeField] private Color validCellColor = new Color(0.1f, 0.9f, 0.25f, 1f);
        [SerializeField] private Color invalidCellColor = new Color(1f, 0.1f, 0.1f, 1f);
        [SerializeField, Min(0.001f)] private float highlightLineWidth = 0.035f;
        [SerializeField] private Vector3 highlightWorldOffset = new Vector3(0f, 0f, -0.02f);

        private readonly List<LineRenderer> highlightLines = new List<LineRenderer>();
        private Material highlightMaterial;
        private InputController inputController;
        private BuildingDefinition activeDefinition;
        private GameObject ghostInstance;
        private BuildingView ghostView;
        private GameObject controlsRoot;
        private Button confirmButton;
        private Button cancelButton;
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
            if (isPlacing && isDraggingPlacement)
            {
                SetCameraInputBlocked(true);
            }
        }

        private void OnDisable()
        {
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
            SetCameraInputBlocked(false);

            if (highlightMaterial != null)
            {
                Destroy(highlightMaterial);
                highlightMaterial = null;
            }
        }

        public void BeginPlacement(BuildingDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            ResolveSceneReferences();
            if (gridMap == null || sourceCamera == null)
            {
                Debug.LogWarning("Cannot begin building placement without a grid map and camera.", this);
                return;
            }

            if (!definition.HasBuildingPrefab)
            {
                Debug.LogWarning($"Building '{definition.DisplayName}' has no building prefab.", definition);
                return;
            }

            CancelPlacement();

            activeDefinition = definition;
            isPlacing = true;
            isConfirming = false;
            isDraggingPlacement = false;
            currentCanPlace = false;
            hasCurrentOrigin = false;
            SetCameraInputBlocked(false);

            CreateGhost(definition);
            CreateControls();
            PlaceAtScreenPosition(GetScreenCenter());
        }

        public void ConfirmPlacement()
        {
            if (!isPlacing || isConfirming || activeDefinition == null || !hasCurrentOrigin || !currentCanPlace)
            {
                return;
            }

            StartCoroutine(ConfirmPlacementRoutine(activeDefinition, currentOrigin));
        }

        public void CancelPlacement()
        {
            SetCameraInputBlocked(false);

            activeDefinition = null;
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

            if (controlsRoot != null)
            {
                Destroy(controlsRoot);
                controlsRoot = null;
                confirmButton = null;
                cancelButton = null;
            }

            HideHighlightLines();
        }

        private IEnumerator ConfirmPlacementRoutine(BuildingDefinition definition, GridPosition origin)
        {
            isConfirming = true;

            if (!gridMap.CanOccupy(origin, definition.Size, out var failureReason))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' at {origin}: {failureReason}.", this);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            if (!definition.HasBuildingPrefab)
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because it has no building prefab.", definition);
                isConfirming = false;
                yield break;
            }

            var gameSystem = Landsong.GameSystem.Instance;
            var inventory = gameSystem == null ? null : gameSystem.Inventory;
            if (!CanSpendPlacementCosts(definition, inventory))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because placement costs are missing.", definition);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            var buildingService = gameSystem == null ? null : gameSystem.Buildings;
            if (buildingService == null)
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because BuildingService is missing.", definition);
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            if (!buildingService.TryPlace(definition, gridMap, origin, placedBuildingRoot, out var placedBuilding))
            {
                isConfirming = false;
                RefreshCurrentPlacementState();
                yield break;
            }

            if (!SpendPlacementCosts(definition, inventory))
            {
                Debug.LogWarning($"Cannot place building '{definition.DisplayName}' because placement costs could not be spent.", definition);
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
            if (activeDefinition == null || gridMap == null || sourceCamera == null)
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
            if (activeDefinition == null || gridMap == null || sourceCamera == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            if (!gridMap.TryGetGridPointFromScreenPosition(sourceCamera, screenPosition, out var gridPoint))
            {
                SetNoCurrentPlacement();
                return;
            }

            currentOrigin = GetPlacementOrigin(gridPoint, activeDefinition.Size);
            hasCurrentOrigin = true;
            currentCanPlace = gridMap.CanOccupy(currentOrigin, activeDefinition.Size, out _);

            var placementPosition = gridMap.GetFootprintCenter(currentOrigin, activeDefinition.Size);
            MovePreview(placementPosition);
            UpdateFootprintHighlight(new GridFootprint(currentOrigin, activeDefinition.Size), currentCanPlace);

            if (confirmButton != null)
            {
                confirmButton.interactable = currentCanPlace;
            }
        }

        private void RefreshCurrentPlacementState()
        {
            if (!hasCurrentOrigin || activeDefinition == null || gridMap == null)
            {
                SetNoCurrentPlacement();
                return;
            }

            currentCanPlace = gridMap.CanOccupy(currentOrigin, activeDefinition.Size, out _);
            UpdateFootprintHighlight(new GridFootprint(currentOrigin, activeDefinition.Size), currentCanPlace);

            if (confirmButton != null)
            {
                confirmButton.interactable = currentCanPlace;
            }
        }

        private void SetNoCurrentPlacement()
        {
            hasCurrentOrigin = false;
            currentCanPlace = false;
            HideHighlightLines();

            if (confirmButton != null)
            {
                confirmButton.interactable = false;
            }
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

            if (controlsRoot != null)
            {
                controlsRoot.SetActive(true);
                controlsRoot.transform.position = ghostPosition + controlsWorldOffset;
            }
        }

        private void CreateGhost(BuildingDefinition definition)
        {
            var parent = previewRoot == null ? transform : previewRoot;
            ghostInstance = Instantiate(definition.BuildingPrefab, parent);
            ghostInstance.name = $"{definition.DisplayName}_PlacementGhost";
            ghostInstance.SetActive(false);

            DisablePreviewBuildingRuntime(ghostInstance);
            ghostView = ghostInstance.GetComponentInChildren<BuildingView>(true);
            if (ghostView == null)
            {
                ghostView = ghostInstance.AddComponent<BuildingView>();
            }

            ghostView.SetPlacementPreview(true);
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

        private void CreateControls()
        {
            ResolveInputController();
            inputController?.EnsureEventSystemExists();

            controlsRoot = new GameObject("Building Placement Controls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            controlsRoot.transform.SetParent(transform, false);
            controlsRoot.transform.localScale = Vector3.one * controlsWorldScale;
            controlsRoot.SetActive(false);

            var rect = controlsRoot.GetComponent<RectTransform>();
            rect.sizeDelta = controlsSize;

            var canvas = controlsRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = sourceCamera;
            canvas.sortingOrder = 100;

            confirmButton = CreateControlButton(rect, "确认", new Vector2(0f, 0f), new Vector2(0.48f, 1f));
            cancelButton = CreateControlButton(rect, "取消", new Vector2(0.52f, 0f), new Vector2(1f, 1f));

            confirmButton.onClick.AddListener(ConfirmPlacement);
            cancelButton.onClick.AddListener(CancelPlacement);
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

        private static Button CreateControlButton(RectTransform parent, string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var buttonObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);

            var rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.08f, 0.08f, 0.08f, 0.82f);

            var button = buttonObject.GetComponent<Button>();
            button.targetGraphic = image;

            var textObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(buttonObject.transform, false);

            var textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObject.GetComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 28f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;

            return button;
        }

        private void UpdateFootprintHighlight(GridFootprint footprint, bool canPlace)
        {
            var color = canPlace ? validCellColor : invalidCellColor;
            var index = 0;
            foreach (var position in footprint.Positions())
            {
                var line = GetHighlightLine(index);
                DrawCellLine(line, position, color);
                index++;
            }

            for (var i = index; i < highlightLines.Count; i++)
            {
                highlightLines[i].gameObject.SetActive(false);
            }
        }

        private LineRenderer GetHighlightLine(int index)
        {
            while (highlightLines.Count <= index)
            {
                highlightLines.Add(CreateHighlightLine());
            }

            var line = highlightLines[index];
            line.gameObject.SetActive(true);
            return line;
        }

        private LineRenderer CreateHighlightLine()
        {
            var lineObject = new GameObject("Placement Cell Highlight");
            lineObject.transform.SetParent(transform, false);

            var line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = highlightLineWidth;
            line.endWidth = highlightLineWidth;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.material = GetHighlightMaterial();
            return line;
        }

        private Material GetHighlightMaterial()
        {
            if (highlightMaterial != null)
            {
                return highlightMaterial;
            }

            var shader = Shader.Find("Sprites/Default")
                         ?? Shader.Find("Universal Render Pipeline/Unlit")
                         ?? Shader.Find("Unlit/Color");
            highlightMaterial = new Material(shader);
            return highlightMaterial;
        }

        private void DrawCellLine(LineRenderer line, GridPosition position, Color color)
        {
            var corners = gridMap.Layout.GetCellCorners(position);
            for (var i = 0; i < corners.Length; i++)
            {
                line.SetPosition(i, corners[i] + highlightWorldOffset);
            }

            line.startColor = color;
            line.endColor = color;
        }

        private void HideHighlightLines()
        {
            foreach (var line in highlightLines)
            {
                if (line != null)
                {
                    line.gameObject.SetActive(false);
                }
            }
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
