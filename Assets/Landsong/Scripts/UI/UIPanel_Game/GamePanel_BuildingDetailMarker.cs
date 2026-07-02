using System;
using Landsong.BuildingSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_BuildingDetailMarker : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;
        [SerializeField] private string defaultText = "详情";

        private BuildingBase building;
        private Action<BuildingBase> clicked;

        private void Reset()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            SetText(defaultText);
        }

        private void OnEnable()
        {
            if (button != null)
            {
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDisable()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(BuildingBase targetBuilding, Action<BuildingBase> onClicked)
        {
            building = targetBuilding;
            clicked = onClicked;
            SetText(defaultText);
        }

        public void Unbind()
        {
            building = null;
            clicked = null;
        }

        private void HandleClicked()
        {
            if (building == null)
            {
                return;
            }

            clicked?.Invoke(building);
        }

        private void SetText(string text)
        {
            if (label != null)
            {
                label.text = text ?? string.Empty;
            }
        }
    }
}
