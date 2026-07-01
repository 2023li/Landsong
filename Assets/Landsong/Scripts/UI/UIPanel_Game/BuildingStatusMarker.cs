using System;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class BuildingStatusMarker : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string markerText = "!";

        private BuildingBase building;
        private Action<BuildingBase> clicked;

        private void Reset()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>(true);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(BuildingBase targetBuilding, BuildingStatusDisplayData data, Action<BuildingBase> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            building = targetBuilding;
            clicked = onClicked;

            if (label != null)
            {
                label.text = string.IsNullOrWhiteSpace(markerText) ? "!" : markerText;
            }

            gameObject.name = string.IsNullOrWhiteSpace(data.BuildingName)
                ? "BuildingStatusMarker"
                : $"BuildingStatusMarker_{data.BuildingName}";

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
        }

        private void HandleClicked()
        {
            clicked?.Invoke(building);
        }
    }
}
