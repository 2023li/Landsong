using System.Collections;
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
        private GameObject currentEffect;
        private AsyncOperationHandle<GameObject>? currentAddressableHandle;
        private Coroutine transitionRoutine;
        private int requestVersion;
        private bool interactionLocked;

        private static Sprite placeholderSprite;

        public Transform ViewRoot => viewRoot;
        public BuildingView ViewAdapter => viewAdapter;
        public bool InteractionLocked => interactionLocked;

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnDestroy()
        {
            requestVersion++;
            ReleaseCurrentAddressable();
        }

        public void Bind(BuildingBase building)
        {
            owner = building;
            ResolveReferences();
        }

        public void RefreshImmediate()
        {
            requestVersion++;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
                transitionRoutine = null;
            }

            interactionLocked = false;
            DestroyEffect();
            ResolveAndShowCurrentView(requestVersion);
        }

        public void PlayStageTransition(BuildingLifecycleStage previous)
        {
            var transition = owner?.FamilyDefinition?.Presentation?.ConstructionCompleteTransition;
            PlayTransition(transition);
        }

        public void PlayLevelTransition(int previousLevel)
        {
            var transition = owner?.FamilyDefinition?.Presentation?.DefaultUpgradeTransition;
            PlayTransition(transition);
        }

        public void SkipTransition()
        {
            if (transitionRoutine == null)
            {
                return;
            }

            requestVersion++;
            StopCoroutine(transitionRoutine);
            transitionRoutine = null;
            interactionLocked = false;
            DestroyEffect();
            ResolveAndShowCurrentView(requestVersion);
        }

        private void PlayTransition(BuildingTransitionDefinition transition)
        {
            requestVersion++;
            if (transitionRoutine != null)
            {
                StopCoroutine(transitionRoutine);
            }

            transitionRoutine = StartCoroutine(TransitionRoutine(transition, requestVersion));
        }

        private IEnumerator TransitionRoutine(
            BuildingTransitionDefinition transition,
            int version)
        {
            interactionLocked = true;
            var duration = transition == null ? 0.35f : transition.Duration;
            var swapTime = duration * (transition == null ? 0.5f : transition.ViewSwapNormalizedTime);

            if (transition?.EffectPrefab != null)
            {
                currentEffect = Instantiate(transition.EffectPrefab, viewRoot, false);
            }

            if (transition?.Sound != null && owner != null)
            {
                AudioSource.PlayClipAtPoint(transition.Sound, owner.transform.position);
            }

            var elapsed = 0f;
            var swapped = false;
            while (elapsed < duration)
            {
                if (version != requestVersion)
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                if (!swapped && elapsed >= swapTime)
                {
                    swapped = true;
                    ResolveAndShowCurrentView(version);
                }

                yield return null;
            }

            if (!swapped)
            {
                ResolveAndShowCurrentView(version);
            }

            DestroyEffect();
            interactionLocked = false;
            transitionRoutine = null;
        }

        private void ResolveAndShowCurrentView(int version)
        {
            if (owner == null)
            {
                return;
            }

            var presentation = owner.FamilyDefinition?.Presentation;
            if (presentation == null
                || !presentation.TryResolveView(
                    owner.Stage,
                    owner.CurrentLevel,
                    owner.StyleId,
                    out var reference))
            {
                ShowPlaceholder();
                return;
            }

            if (reference.HasDirectPrefab)
            {
                ShowPrefab(reference.DirectPrefab);
                return;
            }

            if (reference.HasAddressablePrefab)
            {
                StartCoroutine(LoadAddressableView(reference.AddressablePrefab, version));
                return;
            }

            ShowPlaceholder();
        }

        private IEnumerator LoadAddressableView(
            AssetReferenceGameObject reference,
            int version)
        {
            ShowPlaceholder();
            var handle = reference.LoadAssetAsync<GameObject>();
            yield return handle;

            if (version != requestVersion)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                yield break;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                Debug.LogWarning(
                    $"建筑 '{owner?.FamilyId}' 的表现资源加载失败，已使用占位 View。",
                    owner);
                yield break;
            }

            ReleaseCurrentAddressable();
            currentAddressableHandle = handle;
            ShowPrefab(handle.Result, false);
        }

        private void ShowPrefab(GameObject prefab, bool releaseAddressable = true)
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
            if (currentView != null)
            {
                Destroy(currentView);
                currentView = null;
            }
        }

        private void DestroyEffect()
        {
            if (currentEffect != null)
            {
                Destroy(currentEffect);
                currentEffect = null;
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
