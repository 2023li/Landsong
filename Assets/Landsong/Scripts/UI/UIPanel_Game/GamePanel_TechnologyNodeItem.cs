using System;
using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_TechnologyNodeItem : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private GameObject selectedRoot;

        private TechnologyDefinition definition;
        private TechnologyService technology;
        private Action<TechnologyDefinition> clicked;

        public TechnologyDefinition Definition => definition;

        private void Awake()
        {
            ResolveReferences();
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
                button.onClick.AddListener(HandleClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClicked);
            }
        }

        public void Bind(
            TechnologyDefinition targetDefinition,
            TechnologyService targetTechnology,
            Action<TechnologyDefinition> onClicked,
            bool selected)
        {
            definition = targetDefinition;
            technology = targetTechnology;
            clicked = onClicked;
            SetSelected(selected);
            Refresh();
        }

        public void SetSelected(bool selected)
        {
            if (selectedRoot != null)
            {
                selectedRoot.SetActive(selected);
            }
        }

        public void Refresh()
        {
            ResolveReferences();

            if (definition == null)
            {
                SetText(nameLabel, string.Empty);
                SetText(costLabel, string.Empty);
                SetText(statusLabel, string.Empty);
                if (button != null)
                {
                    button.interactable = false;
                }

                return;
            }

            SetText(nameLabel, definition.DisplayName);
            SetText(costLabel, $"{definition.SciencePointCost} 科技点");
            SetText(statusLabel, FormatStatus());

            if (iconImage != null)
            {
                iconImage.sprite = definition.Icon;
                iconImage.enabled = definition.HasIcon;
            }

            if (button != null)
            {
                button.interactable = true;
            }
        }

        private string FormatStatus()
        {
            if (technology == null)
            {
                return "未初始化";
            }

            if (technology.IsUnlocked(definition.TechnologyId))
            {
                return "已解锁";
            }

            if (technology.CanUnlock(definition, out var reason))
            {
                return "可解锁";
            }

            return reason switch
            {
                TechnologyUnlockFailureReason.PrerequisitesLocked => "前置未完成",
                TechnologyUnlockFailureReason.InsufficientPoints => "科技点不足",
                TechnologyUnlockFailureReason.AlreadyUnlocked => "已解锁",
                _ => "不可解锁"
            };
        }

        private void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }
        }

        private void HandleClicked()
        {
            clicked?.Invoke(definition);
        }

        private static void SetText(TMP_Text target, string text)
        {
            if (target != null)
            {
                target.text = text ?? string.Empty;
            }
        }
    }
}
