using System;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingStatusOverviewItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text indexLabel;
        [SerializeField] private TMP_Text buildingNameLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text valueLabel;
        [SerializeField] private GameObject abnormalRoot;
        [SerializeField] private GameObject normalRoot;

        private BuildingBase building;
        private Action<BuildingBase> clicked;

        private void Reset()
        {
            button = GetComponent<Button>();
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(
            int index,
            BuildingBase targetBuilding,
            BuildingStatusDisplayData data,
            Action<BuildingBase> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            building = targetBuilding;
            clicked = onClicked;

            SetText(indexLabel, index > 0 ? index.ToString() : string.Empty);
            SetText(buildingNameLabel, data.BuildingName);
            SetText(statusLabel, data.StatusText);
            SetText(valueLabel, FormatValue(data));
            SetActive(abnormalRoot, data.HasAbnormalStatus);
            SetActive(normalRoot, !data.HasAbnormalStatus);

            if (button != null)
            {
                button.interactable = building != null;
                button.onClick.AddListener(HandleClicked);
            }
        }

        public void Unbind()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.interactable = false;
            }

            building = null;
            clicked = null;
            SetText(indexLabel, string.Empty);
            SetText(buildingNameLabel, string.Empty);
            SetText(statusLabel, string.Empty);
            SetText(valueLabel, string.Empty);
            SetActive(abnormalRoot, false);
            SetActive(normalRoot, false);
        }

        private void HandleClicked()
        {
            clicked?.Invoke(building);
        }

        private static string FormatValue(BuildingStatusDisplayData data)
        {
            if (string.IsNullOrWhiteSpace(data.ValueText))
            {
                return string.Empty;
            }

            return string.IsNullOrWhiteSpace(data.ValueLabel)
                ? data.ValueText
                : $"{data.ValueLabel} {data.ValueText}";
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value ?? string.Empty;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null)
            {
                target.SetActive(active);
            }
        }
    }
}
