using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Landsong.BuildingSystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingPresentationController : MonoBehaviour
    {
        [SerializeField] private Transform viewRoot;
        [SerializeField] private BuildingView viewAdapter;

        private BuildingBase owner;
        private GameObject currentView;
        private AsyncOperationHandle<GameObject>? currentAddressableHandle;
        private readonly List<AsyncOperationHandle<GameObject>> pendingAddressableHandles =
            new List<AsyncOperationHandle<GameObject>>();
        private int requestVersion;

        private static Sprite placeholderSprite;

        public Transform ViewRoot => viewRoot;
        public BuildingView ViewAdapter => viewAdapter;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDestroy()
        {
            requestVersion++;
            ReleasePendingAddressables();
            ReleaseCurrentAddressable();
        }

        public void Bind(BuildingBase building)
        {
            owner = building;
            ResolveReferences();
        }

        public void RefreshImmediate()
        {
            RefreshForEntry(BuildingViewEntryReason.Normal);
        }

        public void RefreshForEntry(BuildingViewEntryReason entryReason)
        {
            requestVersion++;
            ResolveAndShowCurrentView(requestVersion, entryReason);
        }

        public void RefreshPlacementPreview()
        {
            requestVersion++;
            if (owner == null)
            {
                return;
            }

            var presentation = owner.FamilyDefinition?.Presentation;
            if (presentation == null
                || !presentation.TryResolvePlacementPreview(
                    owner.CurrentLevel,
                    owner.StyleId,
                    out var reference))
            {
                ShowPlaceholder();
                return;
            }

            ShowResolvedReference(
                reference,
                requestVersion,
                BuildingViewEntryReason.Normal);
        }

        private void ResolveAndShowCurrentView(
            int version,
            BuildingViewEntryReason entryReason)
        {
            if (owner == null)
            {
                return;
            }

            var presentation = owner.FamilyDefinition?.Presentation;
            if (presentation == null)
            {
                ShowPlaceholder();
                return;
            }

            BuildingVisualAssetReference reference;
            var resolved = owner.IsUnderConstruction
                ? presentation.TryResolveConstructionView(
                    owner.CurrentConstructionTurn,
                    owner.StyleId,
                    out reference)
                : presentation.TryResolveView(
                    owner.Stage,
                    owner.CurrentLevel,
                    owner.StyleId,
                    out reference);
            if (!resolved)
            {
                ShowPlaceholder();
                return;
            }

            ShowResolvedReference(reference, version, entryReason);
        }

        private void ShowResolvedReference(
            BuildingVisualAssetReference reference,
            int version,
            BuildingViewEntryReason entryReason)
        {
            if (reference == null)
            {
                ShowPlaceholder();
                return;
            }

            if (reference.HasDirectPrefab)
            {
                ShowPrefab(reference.DirectPrefab, entryReason);
                return;
            }

            if (reference.HasAddressablePrefab)
            {
                StartCoroutine(LoadAddressableView(
                    reference.AddressablePrefab,
                    version,
                    entryReason));
                return;
            }

            ShowPlaceholder();
        }

        private IEnumerator LoadAddressableView(
            AssetReferenceGameObject reference,
            int version,
            BuildingViewEntryReason entryReason)
        {
            ShowPlaceholder();
            // AssetReference belongs to the shared Presentation SO and therefore exposes only one
            // OperationHandle. Multiple building instances must load by key so each controller owns
            // an independently releasable handle without competing for AssetReference.OperationHandle.
            var handle = Addressables.LoadAssetAsync<GameObject>(reference.RuntimeKey);
            pendingAddressableHandles.Add(handle);
            yield return handle;
            pendingAddressableHandles.Remove(handle);

            if (!handle.IsValid())
            {
                yield break;
            }

            if (version != requestVersion)
            {
                Addressables.Release(handle);

                yield break;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Addressables.Release(handle);

                Debug.LogWarning(
                    $"建筑 '{owner?.FamilyId}' 的表现资源加载失败，已使用占位 View。",
                    owner);
                yield break;
            }

            ReleaseCurrentAddressable();
            currentAddressableHandle = handle;
            ShowPrefab(handle.Result, entryReason, false);
        }

        private void ShowPrefab(
            GameObject prefab,
            BuildingViewEntryReason entryReason,
            bool releaseAddressable = true)
        {
            if (prefab == null)
            {
                ShowPlaceholder();
                return;
            }

            if (releaseAddressable)
            {
                ReleaseCurrentAddressable();
            }

            DestroyCurrentView();
            currentView = Instantiate(prefab, viewRoot, false);
            viewAdapter?.PlayEntryAnimation(entryReason);
            if (currentView.GetComponentInChildren<BuildingBase>(true) != null)
            {
                Debug.LogError(
                    $"建筑 View '{prefab.name}' 包含 BuildingBase。View 只能包含纯表现组件。",
                    prefab);
            }
        }

        private void ShowPlaceholder()
        {
            ReleaseCurrentAddressable();
            DestroyCurrentView();

            currentView = new GameObject("MissingBuildingView");
            currentView.transform.SetParent(viewRoot, false);
            var renderer = currentView.AddComponent<SpriteRenderer>();
            renderer.sprite = GetPlaceholderSprite();
            renderer.color = new Color(1f, 0f, 0.8f, 0.65f);

            var size = owner?.Definition?.Size ?? Vector2Int.one;
            currentView.transform.localScale = new Vector3(
                Mathf.Max(1, size.x),
                Mathf.Max(1, size.y),
                1f);
        }

        private void DestroyCurrentView()
        {
            viewAdapter?.InvalidateVisualPlayers();
            if (currentView != null)
            {
                Destroy(currentView);
                currentView = null;
            }
        }

        private void ReleaseCurrentAddressable()
        {
            if (!currentAddressableHandle.HasValue)
            {
                return;
            }

            var handle = currentAddressableHandle.Value;
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }

            currentAddressableHandle = null;
        }

        private void ReleasePendingAddressables()
        {
            for (var i = 0; i < pendingAddressableHandles.Count; i++)
            {
                var handle = pendingAddressableHandles[i];
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }

            pendingAddressableHandles.Clear();
        }

        private void ResolveReferences()
        {
            if (viewRoot == null)
            {
                var child = transform.Find("ViewRoot");
                if (child == null)
                {
                    var root = new GameObject("ViewRoot");
                    root.transform.SetParent(transform, false);
                    child = root.transform;
                }

                viewRoot = child;
            }

            if (viewAdapter == null)
            {
                viewAdapter = viewRoot.GetComponent<BuildingView>();
                if (viewAdapter == null)
                {
                    viewAdapter = viewRoot.gameObject.AddComponent<BuildingView>();
                }
            }
        }

        private static Sprite GetPlaceholderSprite()
        {
            if (placeholderSprite != null)
            {
                return placeholderSprite;
            }

            placeholderSprite = Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                new Vector2(0.5f, 0.5f),
                1f);
            placeholderSprite.name = "MissingBuildingViewSprite";
            return placeholderSprite;
        }
    }
}
