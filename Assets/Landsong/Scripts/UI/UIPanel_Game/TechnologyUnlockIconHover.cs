using System;
using Landsong.TechnologySystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    /// <summary>
    /// 科技解锁内容项的严格 Prefab 视图。所有显示对象必须由 Prefab 显式引用。
    /// 只监听悬停，不实现点击或拖拽接口，因此事件会继续交给科技节点按钮和外层 ScrollRect。
    /// </summary>
    public sealed class TechnologyUnlockIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text iconBadgeLabel;
        [SerializeField] private TMP_Text textOnlyLabel;
        [SerializeField] private Color missingIconBackgroundColor =
            new Color(0.12f, 0.12f, 0.12f, 0.92f);

        private Action pointerEntered;
        private Action pointerExited;

        public RectTransform RectTransform => transform as RectTransform;

        public bool IsConfigurationValid(out string error)
        {
            if (RectTransform == null)
            {
                error = "根对象必须使用 RectTransform。";
                return false;
            }

            if (iconImage == null)
            {
                error = "未引用图标 Image。";
                return false;
            }

            if (iconBadgeLabel == null)
            {
                error = "未引用有图标时使用的角标 TMP 文本。";
                return false;
            }

            if (textOnlyLabel == null)
            {
                error = "未引用无图标时使用的 TMP 文本。";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public bool Bind(
            TechnologyUnlockContent content,
            string badgeText,
            Action onPointerEntered,
            Action onPointerExited)
        {
            if (!IsConfigurationValid(out var error))
            {
                Debug.LogError($"科技解锁内容项 Prefab 配置错误：{error}", this);
                return false;
            }

            badgeText = string.IsNullOrWhiteSpace(badgeText)
                ? string.Empty
                : badgeText.Trim();
            var hasIcon = content.Icon != null;

            iconImage.sprite = content.Icon;
            iconImage.color = hasIcon ? Color.white : missingIconBackgroundColor;
            iconImage.enabled = true;

            SetLabel(iconBadgeLabel, hasIcon ? badgeText : string.Empty);
            SetLabel(textOnlyLabel, hasIcon ? string.Empty : badgeText);

            pointerEntered = onPointerEntered;
            pointerExited = onPointerExited;

            var amountSuffix = content.Amount > 1 ? $"_x{content.Amount}" : string.Empty;
            gameObject.name = string.IsNullOrWhiteSpace(content.DisplayName)
                ? "解锁内容"
                : $"解锁_{content.DisplayName}{amountSuffix}";
            return true;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerEntered?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerExited?.Invoke();
        }

        private void OnDisable()
        {
            pointerExited?.Invoke();
        }

        private static void SetLabel(TMP_Text label, string text)
        {
            var visible = !string.IsNullOrWhiteSpace(text);
            label.text = visible ? text : string.Empty;
            label.gameObject.SetActive(visible);
        }
    }
}
