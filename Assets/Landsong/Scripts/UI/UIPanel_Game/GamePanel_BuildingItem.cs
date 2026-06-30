using System;
using Landsong.BuildingSystem;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanel_BuildingItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image icon;

        private BuildingDefinition definition;
        private Action<BuildingDefinition> clicked;

        private void Reset()
        {
            button = GetComponent<Button>();
            icon = GetComponentInChildren<Image>(true);
        }

        private void OnValidate()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(BuildingDefinition buildingDefinition, Action<BuildingDefinition> onClicked)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }

            definition = buildingDefinition;
            clicked = onClicked;

            if (icon != null)
            {
                icon.sprite = definition == null ? null : definition.Icon;
                icon.enabled = definition != null && definition.Icon != null;
            }

            if (button != null)
            {
                button.interactable = definition != null;
                button.onClick.AddListener(HandleClicked);
            }
        }

        public void Unbind()
        {
            definition = null;
            clicked = null;

            if (icon != null)
            {
                icon.sprite = null;
                icon.enabled = false;
            }

            if (button != null)
            {
                button.interactable = false;
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        private void HandleClicked()
        {
            clicked?.Invoke(definition);
        }
    }
}
