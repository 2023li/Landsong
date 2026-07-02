using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    public sealed class GamePanel_SelectedBuildingOverview : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text buildingNameLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text valueLabel;

        private void Reset()
        {
            root = gameObject;
        }

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            Hide();
        }

        public void ShowBuilding(BuildingBase building)
        {
            if (building == null)
            {
                Hide();
                return;
            }

            BuildingStatusDisplayData data = BuildingStatusUIFormatter.CreateDisplayData(building);
            SetActive(root, true);
            SetText(buildingNameLabel, data.BuildingName);
            SetText(statusLabel, data.StatusInfoText);
            SetText(valueLabel, data.BaseInfoText);
        }

        public void Hide()
        {
            SetText(buildingNameLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(valueLabel, string.Empty);
            SetActive(root, false);
        }

        private static void SetText(TMP_Text label, string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }
}
