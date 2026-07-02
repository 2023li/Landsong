using System.Collections;
using Landsong.BuildingSystem;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingSelectionView : MonoBehaviour
    {
        [SerializeField] private GamePanel_SelectedBuildingOverview selectedOverview;
        [SerializeField] private GamePanel_BuildingDetaiPopup detailPopup;

        private Landsong.GameSystem gameSystem;
        private BuildingSelectionController selectionController;
        private BuildingSelectionController subscribedSelectionController;
        private bool subscribedToSelection;
        private Coroutine delayedResolveCoroutine;

        private BuildingBase SelectedBuilding => selectionController == null ? null : selectionController.SelectedBuilding;

        private void Reset()
        {
            selectedOverview = GetComponentInChildren<GamePanel_SelectedBuildingOverview>(true);
            detailPopup = GetComponentInChildren<GamePanel_BuildingDetaiPopup>(true);
        }

        private void OnEnable()
        {
            ResolveReferences();
            SubscribeSelection();
            RefreshSelectionViews();
            QueueDelayedResolveIfNeeded();
        }

        private void OnDisable()
        {
            StopDelayedResolve();
            UnsubscribeSelection();
            ClearSelectionViews();
        }

        public void OpenSelectedBuildingDetail()
        {
            BuildingBase selectedBuilding = SelectedBuilding;
            if (!CanShowBuilding(selectedBuilding))
            {
                ClearSelectionViews();
                return;
            }

            detailPopup?.ShowBuilding(selectedBuilding);
        }

        private void ResolveReferences()
        {
            gameSystem = Landsong.GameSystem.Instance;
            selectionController = gameSystem == null ? null : gameSystem.BuildingSelection;

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
        }

        private void ClearSelectionViews()
        {
            selectedOverview?.Hide();
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

        private void HandleSelectionChanged(BuildingSelectionController controller, BuildingBase selectedBuilding)
        {
            RefreshSelectionViews();
        }

        private void HandleSelectedBuildingStateChanged(BuildingSelectionController controller, BuildingBase selectedBuilding)
        {
            RefreshSelectionViews();
        }

        private void HandleBuildingDetailRequested(BuildingSelectionController controller, BuildingBase building)
        {
            if (!CanShowBuilding(building))
            {
                detailPopup?.Hide();
                return;
            }

            detailPopup?.ShowBuilding(building);
        }

        private void QueueDelayedResolveIfNeeded()
        {
            if (selectionController != null && selectedOverview != null && detailPopup != null)
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

        private static bool CanShowBuilding(BuildingBase building)
        {
            return building != null && building.isActiveAndEnabled && !building.IsDemolishing;
        }
    }
}
