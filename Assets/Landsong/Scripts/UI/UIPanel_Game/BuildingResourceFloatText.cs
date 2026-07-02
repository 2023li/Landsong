using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Landsong.UISystem
{
    [DisallowMultipleComponent]
    public sealed class BuildingResourceFloatText : MonoBehaviour
    {
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text quantityLabel;
        [SerializeField, Min(0.05f)] private float duration = 0.9f;

        private RectTransform cachedRectTransform;
        private CanvasGroup canvasGroup;

        public RectTransform RectTransform
        {
            get
            {
                if (cachedRectTransform == null)
                {
                    cachedRectTransform = transform as RectTransform;
                }

                return cachedRectTransform;
            }
        }

        public float Duration => Mathf.Max(0.05f, duration);

        private void Reset()
        {
            ResolveReferences(true);
        }

        private void Awake()
        {
            ResolveReferences(true);
        }

        private void OnValidate()
        {
            duration = Mathf.Max(0.05f, duration);
        }

        public void Bind(Sprite icon, string quantityText)
        {
            ResolveReferences(true);

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (quantityLabel != null)
            {
                quantityLabel.text = string.IsNullOrWhiteSpace(quantityText) ? string.Empty : quantityText;
            }

            SetAlpha(1f);
        }

        public void Unbind()
        {
            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }

            if (quantityLabel != null)
            {
                quantityLabel.text = string.Empty;
            }

            SetAlpha(1f);
        }

        public void SetAlpha(float alpha)
        {
            ResolveReferences(true);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = Mathf.Clamp01(alpha);
            }
        }

        private void ResolveReferences(bool createCanvasGroup)
        {
            if (cachedRectTransform == null)
            {
                cachedRectTransform = transform as RectTransform;
            }

            if (iconImage == null)
            {
                iconImage = GetComponentInChildren<Image>(true);
            }

            if (quantityLabel == null)
            {
                quantityLabel = GetComponentInChildren<TMP_Text>(true);
            }

            if (canvasGroup == null && !TryGetComponent(out canvasGroup) && createCanvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }
}
