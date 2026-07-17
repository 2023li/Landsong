using System.Collections;
using Landsong.BuildingSystem;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingSelectionView : MonoBehaviour
    {
        private const string DefaultDetailPanelAddressKey = "Popup_GeneralBuildingDetails";

        [SerializeField] private GamePanel_SelectedBuildingOverview selectedOverview;
        [SerializeField] private Popup_BuildingDetails detailPopup;
        [SerializeField] private Transform detailPopupRoot;

        private Landsong.GameSystem gameSystem;
        private BuildingSelectionController selectionController;
        private BuildingSelectionController subscribedSelectionController;
        private bool subscribedToSelection;
        private Coroutine delayedResolveCoroutine;
        private Coroutine detailPopupLoadCoroutine;
        private Popup_BuildingDetails loadedDefaultDetailPopup;
        private AsyncOperationHandle<GameObject> defaultDetailPopupHandle;
        private bool hasDefaultDetailPopupHandle;
        private BuildingBase pendingDetailBuilding;

        private BuildingBase SelectedBuilding => selectionController == null ? null : selectionController.SelectedBuilding;

        private void Reset()
        {
            selectedOverview = GetComponentInChildren<GamePanel_SelectedBuildingOverview>(true);
            detailPopup = GetComponentInChildren<Popup_BuildingDetails>(true);
        }

        private void OnEnable()
        {
            Landsong.Localization.L10n.LanguageChanged += RefreshSelectionViews;
            ResolveReferences();
            SubscribeSelection();
            RefreshSelectionViews();
            QueueDelayedResolveIfNeeded();
        }

        private void OnDisable()
        {
            Landsong.Localization.L10n.LanguageChanged -= RefreshSelectionViews;
            StopDelayedResolve();
            StopDetailPopupLoad();
            UnsubscribeSelection();
            ClearSelectionViews();
        }

        private void OnDestroy()
        {
            StopDetailPopupLoad();

            if (loadedDefaultDetailPopup != null)
            {
                Destroy(loadedDefaultDetailPopup.gameObject);
                loadedDefaultDetailPopup = null;
            }

            if (hasDefaultDetailPopupHandle)
            {
                Addressables.Release(defaultDetailPopupHandle);
                hasDefaultDetailPopupHandle = false;
            }
        }

        public void OpenSelectedBuildingDetail()
        {
            BuildingBase selectedBuilding = SelectedBuilding;
            if (!CanShowBuilding(selectedBuilding))
            {
                ClearSelectionViews();
                return;
            }

            ShowDetailPopup(selectedBuilding);
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            selectionController = gameSystem == null ? null : gameSystem.Services.BuildingSelection;

            if (selectionController == null)
            {
                selectionController = FindFirstObjectByType<BuildingSelectionController>(FindObjectsInactive.Include);
            }

            UIPanel_Game gamePanel = GetComponentInParent<UIPanel_Game>(true);
            if (gamePanel == null)
            {
                return;
            }

            if (selectedOverview == null)
            {
                selectedOverview = gamePanel.SelectedBuildingOverview;
            }

            if (detailPopup == null)
            {
                detailPopup = gamePanel.BuildingDetailPopup;
            }

            if (detailPopupRoot == null)
            {
                detailPopupRoot = gamePanel.transform;
            }
        }

        private void RefreshSelectionViews()
        {
            BuildingBase selectedBuilding = SelectedBuilding;
            if (!CanShowBuilding(selectedBuilding))
            {
                ClearSelectionViews();
                return;
            }

            selectedOverview?.ShowBuilding(selectedBuilding);
            if (detailPopup != null && detailPopup.IsVisible)
            {
                detailPopup.ShowBuilding(selectedBuilding);
            }
            else if (pendingDetailBuilding != null)
            {
                pendingDetailBuilding = selectedBuilding;
            }
        }

        private void ClearSelectionViews()
        {
            selectedOverview?.Hide();
            HideDetailPopup();
        }

        private void ShowDetailPopup(BuildingBase targetBuilding)
        {
            if (!CanShowBuilding(targetBuilding))
            {
                HideDetailPopup();
                return;
            }

            if (detailPopup != null)
            {
                pendingDetailBuilding = null;
                detailPopup.ShowBuilding(targetBuilding);
                return;
            }

            pendingDetailBuilding = targetBuilding;
            if (detailPopupLoadCoroutine == null)
            {
                detailPopupLoadCoroutine = StartCoroutine(LoadDefaultDetailPopupRoutine());
            }
        }

        private void HideDetailPopup()
        {
            pendingDetailBuilding = null;
            detailPopup?.Hide();
        }

        private void SubscribeSelection()
        {
            if (selectionController == null)
            {
                return;
            }

            if (subscribedToSelection && subscribedSelectionController == selectionController)
            {
                return;
            }

            UnsubscribeSelection();
            selectionController.SelectionChanged += HandleSelectionChanged;
            selectionController.SelectedBuildingStateChanged += HandleSelectedBuildingStateChanged;
            selectionController.DetailRequested += HandleBuildingDetailRequested;
            subscribedSelectionController = selectionController;
            subscribedToSelection = true;
        }

        private void UnsubscribeSelection()
        {
            if (!subscribedToSelection || subscribedSelectionController == null)
            {
                subscribedSelectionController = null;
                subscribedToSelection = false;
                return;
            }

            subscribedSelectionController.SelectionChanged -= HandleSelectionChanged;
            subscribedSelectionController.SelectedBuildingStateChanged -= HandleSelectedBuildingStateChanged;
            subscribedSelectionController.DetailRequested -= HandleBuildingDetailRequested;
            subscribedSelectionController = null;
            subscribedToSelection = false;
        }

        private void HandleSelectionChanged(BuildingBase selectedBuilding)
        {
            RefreshSelectionViews();
        }

        private void HandleSelectedBuildingStateChanged(BuildingBase selectedBuilding)
        {
            if (!CanShowBuilding(selectedBuilding))
            {
                ClearSelectionViews();
                return;
            }

            selectedOverview?.ShowBuilding(selectedBuilding);
            if (pendingDetailBuilding != null)
            {
                pendingDetailBuilding = selectedBuilding;
            }
        }

        private void HandleBuildingDetailRequested(BuildingBase building)
        {
            if (!CanShowBuilding(building))
            {
                HideDetailPopup();
                return;
            }

            ShowDetailPopup(building);
        }

        private void QueueDelayedResolveIfNeeded()
        {
            if (selectionController != null && selectedOverview != null)
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
            SubscribeSelection();
            RefreshSelectionViews();
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

        private IEnumerator LoadDefaultDetailPopupRoutine()
        {
            AsyncOperationHandle<GameObject> handle =
                Addressables.LoadAssetAsync<GameObject>(DefaultDetailPanelAddressKey);
            defaultDetailPopupHandle = handle;
            hasDefaultDetailPopupHandle = true;

            yield return handle;
            detailPopupLoadCoroutine = null;

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                Debug.LogWarning(
                    $"无法加载建筑详情栏 Addressables 资源：{DefaultDetailPanelAddressKey}",
                    this);
                Addressables.Release(handle);
                hasDefaultDetailPopupHandle = false;
                yield break;
            }

            Transform parent = ResolveDetailPopupRoot();
            GameObject instance = Instantiate(handle.Result, parent);
            loadedDefaultDetailPopup = instance.GetComponentInChildren<Popup_BuildingDetails>(true);
            detailPopup = loadedDefaultDetailPopup;

            if (detailPopup == null)
            {
                Debug.LogWarning(
                    $"建筑详情栏资源 '{DefaultDetailPanelAddressKey}' 缺少 {nameof(Popup_BuildingDetails)} 组件。",
                    instance);
                Destroy(instance);
                Addressables.Release(handle);
                hasDefaultDetailPopupHandle = false;
                yield break;
            }

            detailPopup.Hide();

            BuildingBase targetBuilding = pendingDetailBuilding;
            pendingDetailBuilding = null;
            if (CanShowBuilding(targetBuilding))
            {
                detailPopup.ShowBuilding(targetBuilding);
            }
        }

        private Transform ResolveDetailPopupRoot()
        {
            if (detailPopupRoot != null)
            {
                return detailPopupRoot;
            }

            UIPanel_Game gamePanel = GetComponentInParent<UIPanel_Game>(true);
            detailPopupRoot = gamePanel == null ? transform : gamePanel.transform;
            return detailPopupRoot;
        }

        private void StopDetailPopupLoad()
        {
            if (detailPopupLoadCoroutine == null)
            {
                return;
            }

            StopCoroutine(detailPopupLoadCoroutine);
            detailPopupLoadCoroutine = null;
            pendingDetailBuilding = null;

            if (hasDefaultDetailPopupHandle && !defaultDetailPopupHandle.IsDone)
            {
                Addressables.Release(defaultDetailPopupHandle);
                hasDefaultDetailPopupHandle = false;
            }
        }

        private static bool CanShowBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }
    }
}
