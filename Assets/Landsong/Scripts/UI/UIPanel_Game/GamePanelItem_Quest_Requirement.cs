using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class GamePanelItem_Quest_Requirement : MonoBehaviour
    {
        [SerializeField] private TMP_Text txt_任务要求;
        [SerializeField, LabelText("已完成标记")] private GameObject 已完成标记;

        private Color normalTextColor;
        private bool hasNormalTextColor;

        private void Reset()
        {
            txt_任务要求 = GetComponentInChildren<TMP_Text>(true);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        public void Bind(string requirementText, bool isCompleted)
        {
            EnsureInitialized();
            SetText(txt_任务要求, requirementText);
            SetTextColor(isCompleted
                ? UIThemeConstants.CompletedQuestColor
                : normalTextColor);
            SetActive(已完成标记, isCompleted);
        }

        public void Clear()
        {
            SetText(txt_任务要求, string.Empty);
            if (hasNormalTextColor)
            {
                SetTextColor(normalTextColor);
            }

            SetActive(已完成标记, false);
        }

        public void SetTextAlignment(TextAlignmentOptions alignment)
        {
            if (txt_任务要求 != null)
            {
                txt_任务要求.alignment = alignment;
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            var normalizedValue = value ?? string.Empty;
            if (target != null && target.text != normalizedValue)
            {
                target.text = normalizedValue;
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

        private void EnsureInitialized()
        {
            if (txt_任务要求 == null)
            {
                return;
            }

            ResourceRichTextFormatter.ApplySpriteAsset(txt_任务要求);
            if (hasNormalTextColor)
            {
                return;
            }

            normalTextColor = txt_任务要求.color;
            hasNormalTextColor = true;
        }

        private void SetTextColor(Color color)
        {
            if (txt_任务要求 != null && txt_任务要求.color != color)
            {
                txt_任务要求.color = color;
            }
        }
    }
}
