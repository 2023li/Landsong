using System.Collections.Generic;
using Landsong.BuildingSystem;
using Landsong.CameraSystem;
using Landsong.InventorySystem;
using Landsong.TurnSystem;
using Moyo.Unity;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingResourceFloatTextManager : MonoBehaviour
    {
        [SerializeField, Required] private BuildingResourceFloatText floatTextPrefab;

        [FoldoutGroup("显示")]
        [SerializeField] private Vector3 markerWorldOffset = new Vector3(0f, 1.5f, 0f);

        [FoldoutGroup("显示")]
        [SerializeField] private Vector2 markerScreenOffset;

        [FoldoutGroup("显示")]
        [SerializeField] private bool hideWhenOffscreen = true;

        [FoldoutGroup("显示")]
        [SerializeField, Min(1)] private int maxActiveFloatTexts = 3;

        [FoldoutGroup("显示")]
        [SerializeField, Min(0)] private int maxQueuedFloatTexts = 64;

        [FoldoutGroup("显示")]
        [SerializeField, Min(1)] private int maxSpawnsPerFrame = 1;

        [FoldoutGroup("显示")]
        [SerializeField, Min(0f)] private float spawnIntervalSeconds = 0.05f;

        [FoldoutGroup("显示")]
        [SerializeField, Min(0f)] private float floatDistance = 48f;

        [FoldoutGroup("显示")]
        [SerializeField, Min(0f)] private float sameBuildingStackOffset = 22f;

        [FoldoutGroup("选填")]
        [SerializeField] private CameraController cameraController;

        private readonly Queue<ResourceFloatTextRequest> pendingRequests = new Queue<ResourceFloatTextRequest>();
        private readonly List<ActiveFloatText> activeFloatTexts = new List<ActiveFloatText>();
        private readonly List<BuildingResourceFloatText> floatTextPool = new List<BuildingResourceFloatText>();

        private Landsong.GameSystem gameSystem;
        private TurnService subscribedTurn;
        private Canvas markerCanvas;
        private RectTransform markerRoot;
        private float nextSpawnTime;

        private void OnEnable()
        {
            ResolveReferences();
        }

        private void OnDisable()
        {
            SetTurnService(null);
            pendingRequests.Clear();
            ReleaseAllFloatTexts();
        }

        private void OnValidate()
        {
            maxActiveFloatTexts = Mathf.Max(1, maxActiveFloatTexts);
            maxQueuedFloatTexts = Mathf.Max(0, maxQueuedFloatTexts);
            maxSpawnsPerFrame = Mathf.Max(1, maxSpawnsPerFrame);
            spawnIntervalSeconds = Mathf.Max(0f, spawnIntervalSeconds);
            floatDistance = Mathf.Max(0f, floatDistance);
            sameBuildingStackOffset = Mathf.Max(0f, sameBuildingStackOffset);
        }

        private void Update()
        {
            ResolveReferences();
            UpdateActiveFloatTexts();
            SpawnQueuedFloatTexts();
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            SetTurnService(gameSystem == null ? null : gameSystem.Services.Turn);

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
            var resolvedRoot = gamePanel == null ? null : gamePanel.GameMarkRoot;
            if (markerRoot == resolvedRoot)
            {
                return;
            }

            markerRoot = resolvedRoot;
            markerCanvas = markerRoot == null ? null : markerRoot.GetComponentInParent<Canvas>(true);

            if (markerRoot != null)
            {
                markerRoot.gameObject.SetActive(true);
                ReparentFloatTextsToMarkerRoot();
            }
        }

        private void SetTurnService(TurnService resolvedTurn)
        {
            if (subscribedTurn == resolvedTurn)
            {
                return;
            }

            if (subscribedTurn != null)
            {
                subscribedTurn.BuildingResourceProvided -= HandleBuildingResourceProvided;
                subscribedTurn.BuildingResourceConsumed -= HandleBuildingResourceConsumed;
            }

            subscribedTurn = resolvedTurn;

            if (subscribedTurn != null)
            {
                subscribedTurn.BuildingResourceProvided += HandleBuildingResourceProvided;
                subscribedTurn.BuildingResourceConsumed += HandleBuildingResourceConsumed;
            }
        }

        private void HandleBuildingResourceProvided(TurnService turn, BuildingResourceProvidedEvent resourceEvent)
        {
            if (!resourceEvent.IsValid)
            {
                return;
            }

            EnqueueResourceFloatText(
                resourceEvent.Building,
                resourceEvent.ItemId,
                resourceEvent.Amount);
        }

        private void HandleBuildingResourceConsumed(TurnService turn, BuildingResourceConsumedEvent resourceEvent)
        {
            if (!resourceEvent.IsValid)
            {
                return;
            }

            EnqueueResourceFloatText(
                resourceEvent.Building,
                resourceEvent.ItemId,
                -resourceEvent.Amount);
        }

        private void EnqueueResourceFloatText(BuildingBase building, string itemId, int signedAmount)
        {
            if (floatTextPrefab == null || signedAmount == 0)
            {
                return;
            }

            if (maxQueuedFloatTexts <= 0 || pendingRequests.Count >= maxQueuedFloatTexts)
            {
                return;
            }

            if (!CanShowFloatTextForBuilding(building)
                || !TryGetMarkerAnchoredPosition(building, out var anchoredPosition))
            {
                return;
            }

            pendingRequests.Enqueue(new ResourceFloatTextRequest(
                building.GetInstanceID(),
                itemId,
                signedAmount,
                ResolveItemIcon(itemId),
                anchoredPosition));
        }

        private void SpawnQueuedFloatTexts()
        {
            if (floatTextPrefab == null || markerRoot == null || pendingRequests.Count == 0)
            {
                return;
            }

            var spawnedThisFrame = 0;
            while (activeFloatTexts.Count < maxActiveFloatTexts
                   && pendingRequests.Count > 0
                   && spawnedThisFrame < maxSpawnsPerFrame)
            {
                if (spawnIntervalSeconds > 0f && Time.unscaledTime < nextSpawnTime)
                {
                    break;
                }

                var request = pendingRequests.Dequeue();
                if (!request.IsValid)
                {
                    continue;
                }

                var anchoredPosition = request.InitialAnchoredPosition;
                var view = GetFloatTextFromPool();
                var viewRect = view.RectTransform;
                if (viewRect == null)
                {
                    ReleaseFloatText(view);
                    continue;
                }

                var stackOffset = CountActiveFloatTextsForBuilding(request.BuildingInstanceId)
                                  * sameBuildingStackOffset;
                view.Bind(request.Icon, FormatQuantity(request.Amount));
                viewRect.anchoredPosition = anchoredPosition + Vector2.up * stackOffset;

                activeFloatTexts.Add(new ActiveFloatText(
                    view,
                    request.BuildingInstanceId,
                    anchoredPosition,
                    view.Duration,
                    stackOffset));

                spawnedThisFrame++;
                nextSpawnTime = Time.unscaledTime + spawnIntervalSeconds;
            }
        }

        private void UpdateActiveFloatTexts()
        {
            for (var i = activeFloatTexts.Count - 1; i >= 0; i--)
            {
                var active = activeFloatTexts[i];
                if (active == null || active.View == null)
                {
                    ReleaseActiveFloatTextAt(i);
                    continue;
                }

                var anchoredPosition = active.InitialAnchoredPosition;
                active.Elapsed += Time.unscaledDeltaTime;

                var duration = Mathf.Max(0.05f, active.Duration);
                var progress = Mathf.Clamp01(active.Elapsed / duration);
                var viewRect = active.View.RectTransform;
                if (viewRect == null)
                {
                    ReleaseActiveFloatTextAt(i);
                    continue;
                }

                viewRect.anchoredPosition = anchoredPosition
                                            + Vector2.up * active.StackOffset
                                            + Vector2.up * (floatDistance * progress);
                active.View.SetAlpha(EvaluateAlpha(progress));

                if (active.Elapsed >= duration)
                {
                    ReleaseActiveFloatTextAt(i);
                }
            }
        }

        private static float EvaluateAlpha(float progress)
        {
            return progress < 0.65f ? 1f : 1f - Mathf.InverseLerp(0.65f, 1f, progress);
        }

        private BuildingResourceFloatText GetFloatTextFromPool()
        {
            BuildingResourceFloatText view;
            var lastIndex = floatTextPool.Count - 1;
            if (lastIndex >= 0)
            {
                view = floatTextPool[lastIndex];
                floatTextPool.RemoveAt(lastIndex);
            }
            else
            {
                view = Instantiate(floatTextPrefab);
            }

            view.transform.SetParent(markerRoot, false);
            view.transform.SetAsLastSibling();
            view.gameObject.SetActive(true);
            return view;
        }

        private void ReleaseAllFloatTexts()
        {
            for (var i = activeFloatTexts.Count - 1; i >= 0; i--)
            {
                ReleaseActiveFloatTextAt(i);
            }
        }

        private void ReleaseActiveFloatTextAt(int index)
        {
            if (index < 0 || index >= activeFloatTexts.Count)
            {
                return;
            }

            var active = activeFloatTexts[index];
            activeFloatTexts.RemoveAt(index);

            if (active != null && active.View != null)
            {
                ReleaseFloatText(active.View);
            }
        }

        private void ReleaseFloatText(BuildingResourceFloatText view)
        {
            if (view == null)
            {
                return;
            }

            view.Unbind();
            view.gameObject.SetActive(false);

            if (markerRoot != null)
            {
                view.transform.SetParent(markerRoot, false);
            }

            floatTextPool.Add(view);
        }

        private void ReparentFloatTextsToMarkerRoot()
        {
            for (var i = 0; i < activeFloatTexts.Count; i++)
            {
                var view = activeFloatTexts[i] == null ? null : activeFloatTexts[i].View;
                if (view != null)
                {
                    view.transform.SetParent(markerRoot, false);
                }
            }

            for (var i = 0; i < floatTextPool.Count; i++)
            {
                var view = floatTextPool[i];
                if (view != null)
                {
                    view.transform.SetParent(markerRoot, false);
                }
            }
        }

        private Sprite ResolveItemIcon(string itemId)
        {
            var catalog = gameSystem == null || gameSystem.Services.Inventory == null
                ? null
                : gameSystem.Services.Inventory.ItemCatalog;

            if (catalog != null && catalog.TryGetDefinition(itemId, out var definition))
            {
                return definition == null ? null : definition.Icon;
            }

            return null;
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

        private static bool CanShowFloatTextForBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }

        private int CountActiveFloatTextsForBuilding(int buildingInstanceId)
        {
            var count = 0;
            for (var i = 0; i < activeFloatTexts.Count; i++)
            {
                if (activeFloatTexts[i] != null
                    && activeFloatTexts[i].BuildingInstanceId == buildingInstanceId)
                {
                    count++;
                }
            }

            return count;
        }

        private static string FormatQuantity(int amount)
        {
            return amount > 0 ? $"+{amount}" : amount.ToString();
        }

        private readonly struct ResourceFloatTextRequest
        {
            public ResourceFloatTextRequest(
                int buildingInstanceId,
                string itemId,
                int amount,
                Sprite icon,
                Vector2 initialAnchoredPosition)
            {
                BuildingInstanceId = buildingInstanceId;
                ItemId = string.IsNullOrWhiteSpace(itemId) ? string.Empty : itemId.Trim();
                Amount = amount;
                Icon = icon;
                InitialAnchoredPosition = initialAnchoredPosition;
            }

            public int BuildingInstanceId { get; }
            public string ItemId { get; }
            public int Amount { get; }
            public Sprite Icon { get; }
            public Vector2 InitialAnchoredPosition { get; }
            public bool IsValid => BuildingInstanceId != 0
                                   && !string.IsNullOrWhiteSpace(ItemId)
                                   && Amount != 0;
        }

        private sealed class ActiveFloatText
        {
            public ActiveFloatText(
                BuildingResourceFloatText view,
                int buildingInstanceId,
                Vector2 initialAnchoredPosition,
                float duration,
                float stackOffset)
            {
                View = view;
                BuildingInstanceId = buildingInstanceId;
                InitialAnchoredPosition = initialAnchoredPosition;
                Duration = Mathf.Max(0.05f, duration);
                StackOffset = Mathf.Max(0f, stackOffset);
            }

            public BuildingResourceFloatText View { get; }
            public int BuildingInstanceId { get; }
            public Vector2 InitialAnchoredPosition { get; }
            public float Duration { get; }
            public float StackOffset { get; }
            public float Elapsed { get; set; }
        }
    }
}
