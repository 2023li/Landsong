using System;
using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    public sealed class GamePanel_TechnologyNodeItem : MonoBehaviour
    {
        [SerializeField] private TechnologyDefinition definition;
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text costLabel;
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private GameObject selectedRoot;

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

        public void Bind(TechnologyService targetTechnology, Action<TechnologyDefinition> onClicked, bool selected)
        {
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
                SetText(nameLabel, gameObject.name);
                SetText(costLabel, string.Empty);
                SetText(statusLabel, "未配置科技");
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

            if (technology.IsCurrentResearch(definition))
            {
                return definition.AllowRepeatResearch && technology.IsUnlocked(definition.TechnologyId)
                    ? $"重复中 {FormatResearchProgress()}"
                    : $"研究中 {FormatResearchProgress()}";
            }

            if (technology.IsUnlocked(definition.TechnologyId) && !definition.AllowRepeatResearch)
            {
                return "已研究";
            }

            if (technology.CanStartResearch(definition, out var reason))
            {
                return definition.AllowRepeatResearch && technology.IsUnlocked(definition.TechnologyId)
                    ? "可重复研究"
                    : "可研究";
            }

            return reason switch
            {
                TechnologyResearchFailureReason.PrerequisitesLocked => "前置未完成",
                TechnologyResearchFailureReason.AlreadyUnlocked => "已研究",
                TechnologyResearchFailureReason.InvalidTechnology => "配置无效",
                _ => "不可研究"
            };
        }

        private string FormatResearchProgress()
        {
            if (definition == null)
            {
                return "0/0";
            }

            var progress = technology == null ? 0 : technology.GetResearchProgress(definition);
            var required = Mathf.Max(0, definition.SciencePointCost);
            return required <= 0 ? "无需科技点" : $"{progress}/{required}";
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
